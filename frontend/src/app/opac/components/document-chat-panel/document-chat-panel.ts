import { Component, signal, Input, Output, EventEmitter, ElementRef, ViewChild, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FormsModule } from '@angular/forms';
import { MarkdownModule } from 'ngx-markdown';
import { APP_CONFIG } from '../../config';
import { ChatStreamError, streamChat, toHistory } from '../../../shared/chat-stream';

interface DocChatSource { pageNumber: number; excerpt?: string; }
interface DocChatMessage {
  id: string;
  role: 'user' | 'assistant';
  text: string;
  sources?: DocChatSource[];
  streaming?: boolean;
  isError?: boolean;
  stopped?: boolean;
}

/**
 * Panel "Hỏi đáp tài liệu này" — chat theo NỘI DUNG 1 tài liệu số (RAG, `POST /api/public/chat/ask-stream`), dùng trong trang
 * reader; khác opac-chat-widget (toàn cục, tìm tài liệu). Port ELIB-LRC 09-29: chữ hiện dần, nút Dừng, gợi ý câu hỏi, gửi kèm
 * vài lượt trước để hỏi nối tiếp, nguồn theo số trang (bấm để lật tới trang đó). Đổi tài liệu thì bắt đầu lại.
 */
@Component({
  selector: 'app-document-chat-panel',
  imports: [TranslateModule, FormsModule, MarkdownModule],
  templateUrl: './document-chat-panel.html',
  styles: [`
    .chat-markdown ::ng-deep > markdown > * { margin: 0.35em 0; }
    .chat-markdown ::ng-deep > markdown > *:first-child { margin-top: 0; }
    .chat-markdown ::ng-deep > markdown > *:last-child { margin-bottom: 0; }
    .chat-markdown ::ng-deep h1, .chat-markdown ::ng-deep h2, .chat-markdown ::ng-deep h3 { font-size: 0.95em; font-weight: 700; margin: 0.5em 0 0.25em; }
    .chat-markdown ::ng-deep ul, .chat-markdown ::ng-deep ol { padding-left: 1.1em; }
    .chat-markdown ::ng-deep p { line-height: 1.45; }
  `]
})
export class DocumentChatPanelComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) ebookId!: string;
  /** Trình đọc lật được trang → nút "Trang N" ở nguồn trích bấm được. */
  @Input() canJump = false;
  @Output() pageRequested = new EventEmitter<number>();

  @ViewChild('scroll') private scrollRef?: ElementRef<HTMLElement>;

  isOpen = signal(false);
  messages = signal<DocChatMessage[]>([]);
  questionInput = signal('');
  isAsking = signal(false);

  readonly suggestions = [
    'Tóm tắt nội dung chính của tài liệu này',
    'Tài liệu này có những ý chính nào?',
    'Giải thích các thuật ngữ quan trọng trong tài liệu',
  ];

  private abort?: AbortController;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['ebookId'] && !changes['ebookId'].firstChange) this.reset();
  }

  ngOnDestroy(): void { this.abort?.abort(); }

  toggle(): void { this.isOpen.update(v => !v); if (this.isOpen()) this.scrollToEnd(); }
  close(): void { this.isOpen.set(false); }

  reset(): void {
    this.abort?.abort();
    this.messages.set([]);
    this.isAsking.set(false);
    this.questionInput.set('');
  }

  /** Enter gửi, Shift+Enter xuống dòng. */
  onEnter(e: KeyboardEvent): void {
    if (e.shiftKey || e.isComposing) return;
    e.preventDefault();
    this.send();
  }

  stop(): void { this.abort?.abort(); }

  send(preset?: string): void {
    const question = (preset ?? this.questionInput()).trim();
    if (!question || this.isAsking() || !this.ebookId) return;
    if (!preset) this.questionInput.set('');
    void this.ask(question);
  }

  private async ask(question: string): Promise<void> {
    const history = toHistory(this.messages(), m => m.isError || m.stopped ? null : { role: m.role, text: m.text });
    this.messages.update(list => [...list,
      { id: this.newId(), role: 'user', text: question },
      { id: this.newId(), role: 'assistant', text: '', streaming: true }]);
    this.isAsking.set(true);
    this.scrollToEnd();

    const abort = new AbortController();
    this.abort = abort;
    const patchLast = (fn: (m: DocChatMessage) => DocChatMessage) =>
      this.messages.update(list => list.length ? [...list.slice(0, -1), fn(list[list.length - 1])] : list);

    try {
      await streamChat('/api/public/chat/ask-stream', { question, ebookId: this.ebookId, tenantId: APP_CONFIG.TenantId, history }, {
        onSources: sources => patchLast(m => ({ ...m, sources: (sources as DocChatSource[]).filter(s => s.pageNumber > 0) })),
        onDelta: text => { patchLast(m => ({ ...m, text: m.text + text })); this.scrollToEnd(); }
      }, { signal: abort.signal });
      patchLast(m => ({ ...m, streaming: false }));
    } catch (e: unknown) {
      if (abort.signal.aborted) patchLast(m => ({ ...m, streaming: false, stopped: true }));
      else patchLast(m => ({
        ...m, streaming: false, isError: true,
        text: (m.text ? m.text + '\n\n' : '') + (e instanceof ChatStreamError ? e.message : 'Không thể kết nối tới trợ lý AI. Vui lòng thử lại sau.')
      }));
    } finally {
      if (this.abort === abort) { this.abort = undefined; this.isAsking.set(false); }
      this.scrollToEnd();
    }
  }

  jump(page: number): void { if (this.canJump) this.pageRequested.emit(page); }

  private scrollToEnd(): void {
    setTimeout(() => { const el = this.scrollRef?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }

  private newId(): string { return Math.random().toString(36).slice(2); }
}
