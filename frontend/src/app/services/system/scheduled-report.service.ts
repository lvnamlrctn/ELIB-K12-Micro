import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { ScheduledReport } from '../../models/system/scheduled-report';

export interface ScheduledReportSearchResult { data: ScheduledReport[]; recordsTotal: number; }

// Service riêng (không qua BaseEntityService) vì payload Add/Update cần đủ các field đặc thù
// (reportType/frequencyType/dayOfWeek/dayOfMonth/timeOfDay/recipientEmails) mà
// BaseEntityService.create/update chỉ gửi cố định name/portalId/language.
@Injectable({ providedIn: 'root' })
export class ScheduledReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/ScheduledReport`; }

  search(keyword: string, pageIndex: number, pageSize: number, tenantId?: string | null): Observable<ScheduledReportSearchResult> {
    const payload = { keyword: keyword || '', tenantId: tenantId ?? null, pageIndex, pageSize };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: ScheduledReport[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<ScheduledReport | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(
      map(r => r?.data ?? r ?? null), catchError(() => of(null))
    );
  }

  create(item: ScheduledReport): Observable<ScheduledReport> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r));
  }

  update(publicId: string, item: ScheduledReport): Observable<ScheduledReport> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r));
  }

  delete(publicId: string): Observable<void> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r));
  }
}
