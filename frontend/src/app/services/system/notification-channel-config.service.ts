import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { NotificationChannelConfig } from '../../models/system/notification-channel-config';

@Injectable({ providedIn: 'root' })
export class NotificationChannelConfigService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/NotificationChannelConfig`; }

  // 1 dòng/tenant — tìm dòng đầu tiên (nếu có) khớp tenant hiện tại qua ApplyTenantFilter phía server.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getMine(): Observable<NotificationChannelConfig | null> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { pageIndex: 1, pageSize: 1 }).pipe(
      map(res => {
        const items: NotificationChannelConfig[] = Array.isArray(res) ? res : (res?.data ?? []);
        return items.length > 0 ? items[0] : null;
      }),
      catchError(() => of(null))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<NotificationChannelConfig>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r));
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<NotificationChannelConfig>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r));
  }
}
