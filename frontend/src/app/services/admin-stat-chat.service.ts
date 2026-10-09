import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../environments/environment';
import { Auth } from './auth';
import { ChatTurn, streamChat } from '../shared/chat-stream';

export interface AdminStatChatResponse {
  answer: string;
  permittedModules: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AdminStatChatService {
  private http = inject(HttpClient);
  private auth = inject(Auth);
  private translate = inject(TranslateService);

  askStats(question: string, history: ChatTurn[] = []): Observable<{ success: boolean; data?: AdminStatChatResponse; message?: string }> {
    return this.http.post<any>(`${environment.baseApiUrl}/api/Cms/AdminStatChat/ask`, { question, history });
  }

  /** Bản trả dần (SSE). Đi bằng fetch nên tự gắn token admin + ngôn ngữ (interceptor không chạy). */
  askStatsStream(question: string, history: ChatTurn[], onDelta: (text: string) => void, signal: AbortSignal): Promise<void> {
    const token = this.auth.getToken();
    return streamChat(`${environment.baseApiUrl}/api/Cms/AdminStatChat/ask-stream`, { question, history }, { onDelta }, {
      signal,
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        'Accept-Language': this.translate.currentLang || 'vi'
      }
    });
  }
}
