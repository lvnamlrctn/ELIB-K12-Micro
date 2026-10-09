import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of, map, catchError, timeout } from 'rxjs';
import { APP_CONFIG, CHATBOT_API_BASE } from '../config';
import { ChatSource, FoundDocument } from './chat.models';
import { ChatTurn } from '../../shared/chat-stream';

export interface ChatAskResult {
  answer:        string;
  sources:       ChatSource[];
  ok:            boolean;
  errorMessage?: string;
}

export interface FindDocumentsResult {
  ok:            boolean;
  answer:        string;
  total:         number;
  documents:     FoundDocument[];
  errorMessage?: string;
}

// Chatbot AI công khai — RAG nội dung (chat/ask) và trợ lý TÌM TÀI LIỆU (chat/find-documents). Trình duyệt gọi đường dẫn tương đối.
@Injectable({ providedIn: 'root' })
export class ChatApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  private get base(): string { return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : CHATBOT_API_BASE; }

  ask(question: string, scope?: { ebookId?: string }): Observable<ChatAskResult> {
    const body = {
      question,
      tenantId: APP_CONFIG.TenantId,
      language: 'vi',
      ...(scope?.ebookId ? { ebookId: scope.ebookId } : {})
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.base}/api/public/chat/ask`, body).pipe(
      map(res => {
        if (res?.success && res.data) {
          return { answer: res.data.answer || '', sources: res.data.sources || [], ok: true };
        }
        return { answer: '', sources: [], ok: false, errorMessage: res?.message || 'Không nhận được phản hồi từ trợ lý AI.' };
      }),
      catchError(() => of({ answer: '', sources: [], ok: false, errorMessage: 'Không thể kết nối tới trợ lý AI. Vui lòng thử lại sau.' }))
    );
  }

  /** Trợ lý TÌM TÀI LIỆU (port ELIB-LRC 09-29): câu nói thường → câu dẫn ngắn + danh sách tài liệu. Gửi kèm vài lượt trước để hiểu
   * câu nối tiếp ("chỉ bản từ 2020"); token bạn đọc (nếu đăng nhập) do opac/auth.interceptor.ts gắn — để gợi ý theo hồ sơ. */
  findDocuments(question: string, history: ChatTurn[]): Observable<FindDocumentsResult> {
    const body = { question, tenantId: APP_CONFIG.TenantId, history, pageSize: 6 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.base}/api/public/chat/find-documents`, body).pipe(
      timeout(30000),
      map(res => res?.success && res.data
        ? { ok: true, answer: res.data.answer || '', total: res.data.total ?? 0, documents: (res.data.documents || []) as FoundDocument[] }
        : { ok: false, answer: '', total: 0, documents: [], errorMessage: res?.message || 'Không nhận được phản hồi từ trợ lý AI.' }),
      catchError(e => of({
        ok: false, answer: '', total: 0, documents: [],
        errorMessage: e?.status === 429
          ? 'Bạn hỏi hơi nhanh, vui lòng đợi một lát rồi thử lại.'
          : 'Không thể kết nối tới trợ lý AI. Vui lòng thử lại sau.'
      }))
    );
  }
}
