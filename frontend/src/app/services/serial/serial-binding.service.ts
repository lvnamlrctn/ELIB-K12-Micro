import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { SerialBinding } from '../../models/serial/serial-binding';

export interface SerialBindingSearchResult { data: SerialBinding[]; recordsTotal: number; }

// Module backend mới hoàn toàn — chưa tồn tại trong ELIB, xem docs/BACKEND-PRINT-DOCUMENT-MGMT.md mục Phase 5
@Injectable({ providedIn: 'root' })
export class SerialBindingService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Magazine/Binding`; }

  search(params: { accessionNo?: string | null; volumeTitle?: string | null; storeId?: number | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<SerialBindingSearchResult> {
    const payload = {
      accessionNo: params.accessionNo || '', volumeTitle: params.volumeTitle || '', storeId: params.storeId ?? null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: SerialBinding[] = [];
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
  getById(publicId: string): Observable<SerialBinding> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<SerialBinding>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<SerialBinding>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
}
