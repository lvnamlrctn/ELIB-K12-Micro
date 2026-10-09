import { Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MarkdownComponent } from 'ngx-markdown';
import { AdminStatChatService } from '../../services/admin-stat-chat.service';
import { PermissionService } from '../../services/system/permission.service';
import { ChatStreamError, toHistory } from '../../shared/chat-stream';

export interface ChatMessage {
  sender: 'user' | 'bot';
  text: string;
  time: Date;
  greeting?: boolean;
  streaming?: boolean;
  error?: boolean;
  stopped?: boolean;
}

@Component({
  selector: 'app-admin-stat-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, MarkdownComponent],
  template: `
    @if (hasAnyPermission()) {
      <div class="fixed bottom-6 right-6 z-50 flex flex-col items-end">
        
        <!-- Chat Window Modal -->
        @if (isOpen()) {
          <div class="w-[calc(100vw-3rem)] sm:w-[30rem] lg:w-[42rem] h-[min(600px,calc(100vh-7rem))] bg-white rounded-2xl shadow-2xl border border-slate-200 flex flex-col overflow-hidden animate-fade-in mb-3">
            
            <!-- Header -->
            <div class="bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 text-white p-4 flex items-center justify-between shadow-sm">
              <div class="flex items-center gap-2.5">
                <div class="w-8 h-8 rounded-xl bg-amber-400 text-slate-950 flex items-center justify-center font-bold text-sm shadow-sm">
                  <span class="material-icons text-lg">insights</span>
                </div>
                <div>
                  <h3 class="text-xs font-bold tracking-wide uppercase text-amber-300">Trợ Lý Thống Kê AI</h3>
                  <p class="text-[10px] text-slate-300">Hỏi đáp số liệu tài liệu & mượn trả</p>
                </div>
              </div>
              <button (click)="toggleChat()" class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-white/10 transition">
                <span class="material-icons text-lg">close</span>
              </button>
            </div>

            <!-- Permitted Modules Summary Badge -->
            @if (permittedModules().length > 0) {
              <div class="bg-slate-50 border-b border-slate-200 px-3 py-1.5 flex items-center gap-1.5 overflow-x-auto text-[10px] text-slate-600">
                <span class="font-bold text-slate-500 uppercase tracking-wider shrink-0">Phân hệ:</span>
                @for (mod of permittedModules(); track mod) {
                  <span class="px-2 py-0.5 rounded-md bg-indigo-50 text-indigo-700 font-semibold border border-indigo-150 shrink-0">{{ mod }}</span>
                }
              </div>
            }

            <!-- Messages Container -->
            <div #scroll class="flex-1 p-3 overflow-y-auto space-y-3 bg-slate-50/50 text-xs custom-scrollbar">
              @for (msg of messages(); track $index) {
                <div [class]="msg.sender === 'user' ? 'flex justify-end' : 'flex justify-start'">
                  <div [class]="msg.sender === 'user' 
                       ? 'bg-indigo-600 text-white rounded-2xl rounded-tr-none px-3.5 py-2.5 max-w-[85%] shadow-sm leading-relaxed' 
                       : 'bg-white border border-slate-200 text-slate-800 rounded-2xl rounded-tl-none px-3.5 py-2.5 max-w-[94%] shadow-xs leading-relaxed'">
                    @if (msg.sender === 'user') {
                      <div class="whitespace-pre-wrap">{{ msg.text }}</div>
                    } @else if (!msg.text && msg.streaming) {
                      <span class="flex items-center gap-2 text-slate-500">
                        <span class="inline-block w-3.5 h-3.5 border-2 border-indigo-600 border-t-transparent rounded-full animate-spin"></span>
                        <span class="text-[11px] font-medium">Đang truy vấn số liệu...</span>
                      </span>
                    } @else {
                      <markdown class="chat-md" [class.text-red-700]="msg.error" [data]="msg.text"></markdown>
                      @if (msg.streaming) { <span class="inline-block w-1.5 h-3.5 bg-indigo-500 align-middle animate-pulse"></span> }
                      @if (msg.stopped) { <div class="text-[10px] text-slate-400 mt-1">Đã dừng — câu trả lời có thể chưa đầy đủ.</div> }
                    }
                    <div [class]="msg.sender === 'user' ? 'text-[9px] text-indigo-200 text-right mt-1' : 'text-[9px] text-slate-400 text-left mt-1'">
                      {{ msg.time | date:'HH:mm' }}
                    </div>
                  </div>
                </div>
              }

            </div>

            <!-- Quick Question Suggestions -->
            <div class="bg-white border-t border-slate-100 p-2 flex items-center gap-1.5 overflow-x-auto text-[10px] scrollbar-none">
              <button (click)="useSuggestion('Thống kê tổng số tài liệu số và lượt mượn đọc?')" 
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                📊 Thống kê tài liệu số
              </button>
              <button (click)="useSuggestion('Hôm nay có bao nhiêu lượt mượn, lượt trả sách in và lượt vào thư viện?')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                📚 Mượn trả hôm nay
              </button>
              <button (click)="useSuggestion('Hiện có bao nhiêu sách đang được mượn và bao nhiêu lượt quá hạn?')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                ⏰ Đang mượn / quá hạn
              </button>
              <button (click)="useSuggestion('Liệt kê các tài liệu đang mượn quá hạn')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                📋 DS sách quá hạn
              </button>
              <button (click)="useSuggestion('Danh sách bạn đọc đang giữ sách quá hạn')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                👤 Bạn đọc quá hạn
              </button>
              <button (click)="useSuggestion('Top 10 sách in được mượn nhiều nhất')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                🔥 Sách mượn nhiều
              </button>
              <button (click)="useSuggestion('Top 10 tài liệu số được đọc nhiều nhất')"
                      class="px-2.5 py-1 rounded-full bg-slate-100 hover:bg-indigo-50 hover:text-indigo-700 text-slate-600 font-medium whitespace-nowrap transition cursor-pointer border border-slate-200/60">
                💻 Tài liệu số đọc nhiều
              </button>
            </div>

            <!-- Input Box -->
            <div class="p-2.5 bg-white border-t border-slate-200 flex items-center gap-2">
              <input type="text" 
                     [(ngModel)]="questionText"
                     (keydown.enter)="sendQuestion()"
                     [disabled]="loading()"
                     placeholder="Hỏi thống kê tài liệu, lượt mượn trả..." 
                     class="flex-1 bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition disabled:bg-slate-100" />
              @if (loading()) {
                <button (click)="stop()" title="Dừng"
                        class="w-9 h-9 bg-slate-800 hover:bg-slate-900 text-white rounded-xl flex items-center justify-center shadow-sm transition shrink-0 cursor-pointer">
                  <span class="material-icons text-base">stop</span>
                </button>
              } @else {
                <button (click)="sendQuestion()"
                        [disabled]="!questionText.trim()"
                        class="w-9 h-9 bg-indigo-600 hover:bg-indigo-700 disabled:bg-slate-300 text-white rounded-xl flex items-center justify-center shadow-sm transition shrink-0 cursor-pointer">
                  <span class="material-icons text-base">send</span>
                </button>
              }
            </div>

          </div>
        }

        <!-- Floating Trigger Button -->
        <button (click)="toggleChat()" 
                class="bg-gradient-to-r from-slate-900 to-indigo-950 hover:from-indigo-900 hover:to-slate-900 text-white p-3.5 rounded-full shadow-xl transition-all duration-300 hover:scale-105 flex items-center gap-2 cursor-pointer border border-indigo-500/30 group">
          <div class="w-7 h-7 rounded-lg bg-amber-400 text-slate-950 flex items-center justify-center font-bold shadow-sm group-hover:rotate-12 transition-transform">
            <span class="material-icons text-base">insights</span>
          </div>
          <span class="text-xs font-bold pr-1 hidden sm:inline text-amber-300">Trợ Lý Thống Kê</span>
        </button>

      </div>
    }
  `
})
export class AdminStatChatComponent implements OnDestroy {
  private statChatService = inject(AdminStatChatService);
  private permService = inject(PermissionService);

  @ViewChild('scroll') scrollRef?: ElementRef<HTMLElement>;

  isOpen = signal<boolean>(false);
  loading = signal<boolean>(false);
  questionText = '';
  permittedModules = signal<string[]>([]);

  private abort?: AbortController;

  messages = signal<ChatMessage[]>([
    {
      sender: 'bot',
      greeting: true,
      text: 'Xin chào! Tôi là Trợ Lý Thống Kê AI. Bạn có thể hỏi tôi các số liệu thống kê về tài liệu số, mượn trả sách in, bạn đọc tương ứng với các phân hệ bạn được cấp quyền — kể cả theo thời gian (hôm nay, tháng này, quý trước…) và hỏi tiếp dựa trên câu trả lời trước.',
      time: new Date()
    }
  ]);

  /** Backend chỉ trả lời người có quyền STAT_CHAT (xem) — người khác bấm vào sẽ chỉ nhận lỗi 403, nên ẩn nút. */
  hasAnyPermission(): boolean {
    return this.permService.canViewLink('/admin/stat-chat');
  }

  toggleChat() {
    this.isOpen.set(!this.isOpen());
    this.scrollToEnd();
  }

  useSuggestion(text: string) {
    if (this.loading()) return;
    this.questionText = text;
    this.sendQuestion();
  }

  stop() { this.abort?.abort(); }

  ngOnDestroy() { this.abort?.abort(); }

  async sendQuestion() {
    const q = this.questionText.trim();
    if (!q || this.loading()) return;

    // Lịch sử (trước câu hỏi mới) để hỏi nối tiếp "còn hôm qua?" — bỏ lời chào và lượt lỗi.
    const history = toHistory(this.messages(), m => m.greeting || m.error || m.stopped ? null
      : { role: m.sender === 'user' ? 'user' : 'assistant', text: m.text });
    this.messages.update(msgs => [...msgs, { sender: 'user', text: q, time: new Date() }, { sender: 'bot', text: '', time: new Date(), streaming: true }]);
    this.questionText = '';
    this.loading.set(true);
    this.scrollToEnd();

    const abort = new AbortController();
    this.abort = abort;
    const patchLast = (fn: (m: ChatMessage) => ChatMessage) =>
      this.messages.update(list => [...list.slice(0, -1), fn(list[list.length - 1])]);

    try {
      await this.statChatService.askStatsStream(q, history, text => {
        patchLast(m => ({ ...m, text: m.text + text }));
        this.scrollToEnd();
      }, abort.signal);
      patchLast(m => ({ ...m, streaming: false, text: m.text || 'Không thể tra cứu thống kê lúc này.' }));
    } catch (e: any) {
      if (abort.signal.aborted) patchLast(m => ({ ...m, streaming: false, stopped: true }));
      else patchLast(m => ({
        ...m, streaming: false, error: true,
        text: (m.text ? m.text + '\n\n' : '') + (e instanceof ChatStreamError ? e.message : 'Lỗi kết nối máy chủ khi tra cứu thống kê.')
      }));
    } finally {
      if (this.abort === abort) { this.abort = undefined; this.loading.set(false); }
      this.scrollToEnd();
    }
  }

  private scrollToEnd() {
    setTimeout(() => { const el = this.scrollRef?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }
}
