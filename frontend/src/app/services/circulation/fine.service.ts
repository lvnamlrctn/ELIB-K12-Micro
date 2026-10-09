import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Fine } from '../../models/circulation/fine';

export interface FineSearchResult { data: Fine[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class FineService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/Fine`; }

  search(params: { cardNo?: string | null; status?: number | null; fineDateFrom?: string | null; fineDateTo?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<FineSearchResult> {
    const payload = { cardNo: params.cardNo || '', status: params.status ?? null, fineDateFrom: params.fineDateFrom || null, fineDateTo: params.fineDateTo || null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Fine[] = [];
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
  save(fine: Partial<Fine>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Save`, fine).pipe(map(r => r?.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  pay(id: number): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Pay`, { id }).pipe(map(r => r?.data ?? r), catchError(() => of(null))); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  waive(id: number): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Waive`, { id }).pipe(map(r => r?.data ?? r), catchError(() => of(null))); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(id: number): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(r => r?.data ?? r)); }
}
