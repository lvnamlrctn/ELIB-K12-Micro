import { Component, signal, inject, ElementRef, ViewChild, AfterViewChecked } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MarkdownModule } from 'ngx-markdown';
import { ChatApiService } from '../../services/chat-api.service';
import { ChatMessage, FoundDocument } from '../../services/chat.models';
import { toHistory } from '../../../shared/chat-stream';

/**
 * Nút chat nổi của OPAC — trợ lý TÌM TÀI LIỆU (`chat/find-documents`, port ELIB-LRC 09-29): hỏi bằng lời thường, nhận câu dẫn ngắn
 * kèm thẻ tài liệu bấm được. Chỉ tra mục lục, không đọc nội dung sách — hỏi nội dung 1 tài liệu dùng panel "Hỏi đáp tài liệu này"
 * ở trang đọc. Gửi kèm vài lượt trước để hiểu câu nối tiếp ("chỉ bản từ 2020", "còn sách in không").
 */
@Component({
  selector: 'opac-chat-widget',
  imports: [TranslateModule, FormsModule, MarkdownModule],
  templateUrl: './chat-widget.html',
  styles: [`
    .chat-markdown ::ng-deep > markdown > * { margin: 0.35em 0; }
    .chat-markdown ::ng-deep > markdown > *:first-child { margin-top: 0; }
    .chat-markdown ::ng-deep > markdown > *:last-child { margin-bottom: 0; }
    .chat-markdown ::ng-deep h1, .chat-markdown ::ng-deep h2, .chat-markdown ::ng-deep h3 { font-size: 0.95em; font-weight: 700; margin: 0.5em 0 0.25em; }
    .chat-markdown ::ng-deep ul, .chat-markdown ::ng-deep ol { padding-left: 1.1em; }
    .chat-markdown ::ng-deep p { line-height: 1.45; }
  `]
})
export class ChatWidgetComponent implements AfterViewChecked {
  private chatApi = inject(ChatApiService);
  private router = inject(Router);

  @ViewChild('messagesEnd') private messagesEnd?: ElementRef<HTMLElement>;
  private shouldScroll = false;

  isOpen = signal(false);
  messages = signal<ChatMessage[]>([]);
  questionInput = signal('');
  isAsking = signal(false);

  readonly suggestions = ['Giáo trình kinh tế vĩ mô', 'Sách tin học mới nhất', 'Gợi ý tài liệu cho tôi'];

  toggle(): void { this.isOpen.update(v => !v); if (this.isOpen()) this.shouldScroll = true; }
  close(): void { this.isOpen.set(false); }
  reset(): void { this.messages.set([]); }

  ngAfterViewChecked(): void {
    if (this.shouldScroll && this.messagesEnd) {
      this.messagesEnd.nativeElement.scrollIntoView({ behavior: 'smooth' });
      this.shouldScroll = false;
    }
  }

  /** Enter gửi, Shift+Enter xuống dòng. */
  onEnter(e: KeyboardEvent): void {
    if (e.shiftKey || e.isComposing) return;
    e.preventDefault();
    this.send();
  }

  send(preset?: string): void {
    const question = (preset ?? this.questionInput()).trim();
    if (!question || this.isAsking()) return;
    const history = toHistory(this.messages(), m => m.isError ? null : { role: m.role, text: m.text }, 4);
    this.messages.update(list => [...list, { id: this.newId(), role: 'user', text: question, timestamp: Date.now() }]);
    if (!preset) this.questionInput.set('');
    this.isAsking.set(true);
    this.shouldScroll = true;
    this.chatApi.findDocuments(question, history).subscribe(res => {
      this.isAsking.set(false);
      this.messages.update(list => [...list, {
        id: this.newId(), role: 'assistant', timestamp: Date.now(),
        text: res.ok ? res.answer : (res.errorMessage || ''),
        documents: res.ok ? res.documents : undefined,
        total: res.ok ? res.total : undefined,
        isError: !res.ok,
      }]);
      this.shouldScroll = true;
    });
  }

  /** Tài liệu in → trang chi tiết biểu ghi; tài liệu số → trang chi tiết tài liệu số (cùng quy ước trang Tìm kiếm). */
  openDocument(d: FoundDocument): void {
    if (!d.publicId) return;
    this.close();
    this.router.navigate([d.docType === 'print' ? '/tai-lieu-in' : '/book', d.publicId]);
  }

  /** "Xem tất cả" → trang tìm kiếm với câu hỏi gần nhất. */
  searchAll(question: string): void {
    this.close();
    this.router.navigate(['/tra-cuu'], { queryParams: { q: question } });
  }

  lastQuestionBefore(index: number): string {
    const list = this.messages();
    for (let i = index - 1; i >= 0; i--) if (list[i].role === 'user') return list[i].text;
    return '';
  }

  private newId(): string { return Math.random().toString(36).slice(2); }
}
