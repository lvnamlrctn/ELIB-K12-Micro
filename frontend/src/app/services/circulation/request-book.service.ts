import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { RequestBook } from '../../models/circulation/request-book';

export interface RequestBookSearchResult { data: RequestBook[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class RequestBookService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/Request`; }

  search(params: { cardNo?: string | null; status?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<RequestBookSearchResult> {
    const payload = { cardNo: params.cardNo || '', status: params.status || '', pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: RequestBook[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Duyệt cho mượn theo yêu cầu. (BorrowByRequest) */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  approve(requestId: number): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Approve`, { requestId }).pipe(map(r => r?.data ?? r), catchError(() => of(null))); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  reject(requestId: number): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Reject`, { requestId }).pipe(map(r => r?.data ?? r), catchError(() => of(null))); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(id: number): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(r => r?.data ?? r)); }
}
