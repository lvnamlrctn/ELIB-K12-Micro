import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AbOrder, AbOrderLine } from '../../models/cataloging/order';

export interface AbOrderSearchResult { data: AbOrder[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class AbOrderService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/Order`; }

  search(params: { orderName?: string | null; supplierId?: number | null; fundId?: number | null; status?: number | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<AbOrderSearchResult> {
    const payload = { orderName: params.orderName || '', supplierId: params.supplierId ?? null, fundId: params.fundId ?? null, status: params.status ?? null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: AbOrder[] = [];
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
  getById(id: number): Observable<AbOrder | null> { return this.http.get<any>(`${this.baseUrl}/${id}`).pipe(map(r => r?.data ?? r ?? null), catchError(() => of(null))); }
  /** Lấy đơn đặt theo publicId (dùng cho trang chi tiết). */
  getByPublicId(publicId: string): Observable<AbOrder | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r?.data ?? null), catchError(() => of(null)));
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  add(item: Partial<AbOrder>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<AbOrder>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  saveLine(line: Record<string, unknown>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/SaveDetail`, line).pipe(map(r => r.data ?? r)); }
  /** Xóa 1 dòng đơn đặt. */
  deleteLine(lineId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/DeleteLine/${lineId}`);
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<any> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
  getLines(orderId: number): Observable<AbOrderLine[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/Lines/${orderId}`).pipe(map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])), catchError(() => of([])));
  }

  /** Tra cứu dòng đơn đặt còn hàng chưa nhận trên nhiều đơn cùng lúc — dùng cho modal "Chọn ấn phẩm đã nhận được". */
  lookupReceivableLines(filter: {
    orderCodeFrom?: number | null; orderCodeTo?: number | null;
    mfnFrom?: number | null; mfnTo?: number | null;
    title?: string | null; author?: string | null;
    pageIndex?: number; pageSize?: number;
  }): Observable<{ data: AbOrderLine[]; recordsTotal: number }> {
    const payload = {
      orderCodeFrom: filter.orderCodeFrom ?? null, orderCodeTo: filter.orderCodeTo ?? null,
      mfnFrom: filter.mfnFrom ?? null, mfnTo: filter.mfnTo ?? null,
      title: filter.title || '', author: filter.author || '',
      pageIndex: filter.pageIndex ?? 1, pageSize: filter.pageSize ?? 20,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/LookupReceivableLines`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }
}
