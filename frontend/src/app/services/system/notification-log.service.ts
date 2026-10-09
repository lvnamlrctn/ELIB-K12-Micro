import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { NotificationLog } from '../../models/system/notification-log';

export interface NotificationLogSearchParams {
  channel?: string | null;
  eventCode?: string | null;
  success?: boolean | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  tenantId?: string | null;
  pageIndex?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationLogService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/NotificationLog`; }

  search(params: NotificationLogSearchParams): Observable<{ data: NotificationLog[]; total: number }> {
    const payload = {
      channel: params.channel || undefined,
      eventCode: params.eventCode || undefined,
      success: params.success ?? undefined,
      dateFrom: params.dateFrom || undefined,
      dateTo: params.dateTo || undefined,
      tenantId: params.tenantId ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize: params.pageSize ?? 10,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({
        data: res?.data?.items ?? [],
        total: res?.data?.totalCount ?? 0,
      })),
      catchError(() => of({ data: [], total: 0 }))
    );
  }
}
