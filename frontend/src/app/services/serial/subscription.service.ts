import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Subscription } from '../../models/serial/subscription';

export interface SubscriptionSearchResult { data: Subscription[]; recordsTotal: number; }

// ELIB: /api/PrintBook/Magazine/Serial — nguồn DataAccess.PrintBook.Magazine.Serial (SaveSerial/SearchSerialSubscription/UpdateSerial/DeleteSerial)
@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Magazine/Serial`; }

  // SearchSerialSubscription(MfnFrom, MfnTo, Title, Publisher, Author, Issn, startTime..., endTime..., page)
  search(params: {
    title?: string | null; author?: string | null; publisher?: string | null; issn?: string | null;
    startTimeFrom?: string | null; startTimeTo?: string | null; endTimeFrom?: string | null; endTimeTo?: string | null;
    pageIndex?: number; pageSize?: number; tenantId?: string | null;
  }): Observable<SubscriptionSearchResult> {
    const payload = {
      title: params.title || '', author: params.author || '', publisher: params.publisher || '', issn: params.issn || '',
      startTimeFrom: params.startTimeFrom || '', startTimeTo: params.startTimeTo || '',
      endTimeFrom: params.endTimeFrom || '', endTimeTo: params.endTimeTo || '',
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Subscription[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(publicId: string): Observable<Subscription> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r.data ?? r)); }

  // SaveSerial — Add/Update đăng ký
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<Subscription>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<Subscription>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  // Đổi trạng thái vòng đời đăng ký (đang hiệu lực / hết hạn / đã hủy)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  changeStatus(publicId: string, status: number): Observable<any> { return this.http.put<any>(`${this.baseUrl}/ChangeStatus`, { publicId, status }).pipe(map(r => r.data ?? r), catchError(() => of(null))); }

  // Duyệt đơn đặt — 1 chiều, sau khi duyệt backend phải tự chặn Update/Delete
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  approve(publicId: string): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Approve`, { publicId }).pipe(map(r => r.data ?? r), catchError(() => of(null))); }
}
