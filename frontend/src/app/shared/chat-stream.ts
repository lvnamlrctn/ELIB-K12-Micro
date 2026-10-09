/**
 * Đọc câu trả lời chat dạng luồng (Server-Sent Events) từ backend: `sources` → nhiều `delta` → `done` | `error`.
 *
 * Dùng `fetch` + `ReadableStream` vì HttpClient của Angular không trả dữ liệu từng phần. Vì đi ngoài HttpClient,
 * nơi gọi phải tự gắn header Authorization/Accept-Language (interceptor không chạy ở đây).
 */

/** 1 lượt hội thoại gửi kèm để model hiểu câu hỏi nối tiếp — khớp DTO `ChatTurn` ở backend. */
export interface ChatTurn { role: 'user' | 'assistant'; text: string; }

export interface ChatStreamHandlers {
  onSources?: (sources: any[]) => void;
  onDelta: (text: string) => void;
}

export class ChatStreamError extends Error {
  constructor(message: string, readonly status?: number) { super(message); }
}

/**
 * Gửi `body` tới `url` và gọi handler theo từng sự kiện. Resolve khi nhận `done` (hoặc luồng kết thúc),
 * reject `ChatStreamError` khi gặp `error`/HTTP lỗi, reject `AbortError` khi `signal` bị huỷ (nút Dừng).
 */
export async function streamChat(url: string, body: unknown, handlers: ChatStreamHandlers,
  opts: { headers?: Record<string, string>; signal?: AbortSignal; timeoutMs?: number } = {}): Promise<void> {
  // Không có dữ liệu nào trong khoảng này → coi như máy chủ treo (vẫn cho phép câu trả lời dài chạy tiếp khi đã có chữ).
  const idleMs = opts.timeoutMs ?? 60000;
  const idle = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  const arm = () => { clearTimeout(timer); timer = setTimeout(() => idle.abort(), idleMs); };
  const signal = opts.signal ? anySignal([opts.signal, idle.signal]) : idle.signal;

  arm();
  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream', ...(opts.headers ?? {}) },
      body: JSON.stringify(body),
      signal
    });
    if (!res.ok || !res.body) {
      let message = res.status === 429
        ? 'Bạn đang hỏi quá nhanh. Vui lòng đợi một chút rồi thử lại.'
        : 'Máy chủ tạm thời không trả lời được. Vui lòng thử lại.';
      try { const j = await res.json(); if (res.status !== 429 && j?.message) message = j.message; } catch { /* không phải JSON */ }
      throw new ChatStreamError(message, res.status);
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      arm();
      buffer += decoder.decode(value, { stream: true });
      let sep: number;
      while ((sep = buffer.indexOf('\n\n')) >= 0) {
        const block = buffer.slice(0, sep);
        buffer = buffer.slice(sep + 2);
        if (handleBlock(block, handlers)) return;
      }
    }
    if (buffer.trim()) handleBlock(buffer, handlers);
  } catch (e: any) {
    if (idle.signal.aborted && !opts.signal?.aborted)
      throw new ChatStreamError('Máy chủ phản hồi quá lâu. Vui lòng thử lại.');
    throw e;
  } finally {
    clearTimeout(timer);
  }
}

/** Xử lý 1 sự kiện SSE; trả true khi đã kết thúc (`done`). */
function handleBlock(block: string, handlers: ChatStreamHandlers): boolean {
  let event = 'message';
  const data: string[] = [];
  for (const line of block.split('\n')) {
    if (line.startsWith('event:')) event = line.slice(6).trim();
    else if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
  }
  let payload: any = null;
  try { payload = data.length ? JSON.parse(data.join('\n')) : null; } catch { payload = null; }
  switch (event) {
    case 'sources': handlers.onSources?.(payload?.sources ?? []); return false;
    case 'delta': if (payload?.text) handlers.onDelta(payload.text); return false;
    case 'error': throw new ChatStreamError(payload?.text || 'Đã có lỗi khi tạo câu trả lời.');
    case 'done': return true;
    default: return false;
  }
}

function anySignal(signals: AbortSignal[]): AbortSignal {
  const any = (AbortSignal as any).any;
  if (typeof any === 'function') return any.call(AbortSignal, signals);
  const c = new AbortController();
  for (const s of signals) {
    if (s.aborted) { c.abort(); break; }
    s.addEventListener('abort', () => c.abort(), { once: true });
  }
  return c.signal;
}

/** Lịch sử gửi kèm: vài lượt cuối, bỏ lượt rỗng/lỗi. Backend vẫn tự cắt lần nữa. */
export function toHistory<T>(messages: T[], pick: (m: T) => ChatTurn | null, max = 6): ChatTurn[] {
  return messages.map(pick).filter((t): t is ChatTurn => !!t && !!t.text.trim()).slice(-max);
}
