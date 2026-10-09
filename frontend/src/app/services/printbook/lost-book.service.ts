import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LostBook, LostBookLookup } from '../../models/printbook/lost-book';

export interface LostBookSearchResult { data: LostBook[]; recordsTotal: number; }
export interface LostBookSearchParams {
  storeId?: number | null; dateFrom?: string | null; dateTo?: string | null;
  barcode?: string | null; bibTypeId?: number | null; mfnFrom?: number | null; mfnTo?: number | null;
  pageIndex?: number; pageSize?: number; tenantId?: string | null;
}

// ELIB: /api/PrintBook/Store/LostBook — nguồn Book.ProcessLossBook / SearchLostBook
@Injectable({ providedIn: 'root' })
export class LostBookService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/LostBook`; }

  private buildSearchPayload(params: LostBookSearchParams) {
    return {
      storeId: params.storeId ?? null,
      createdDateFrom: params.dateFrom || null,
      createdDateTo: params.dateTo || null,
      barcode: params.barcode || '',
      bibTypeId: params.bibTypeId ?? null,
      mfnFrom: params.mfnFrom ?? null,
      mfnTo: params.mfnTo ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize: params.pageSize ?? 10,
      tenantId: params.tenantId ?? null
    };
  }

  // SearchLostBook(StoreId, CreatedDateFrom, CreatedDateTo, BibTypeId, Barcode, MfnFrom, MfnTo)
  search(params: LostBookSearchParams): Observable<LostBookSearchResult> {
    const payload = this.buildSearchPayload(params);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: LostBook[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // Tra cứu theo ĐKCB để tự động điền MFN/Kho/Trạng thái/ISBD
  lookupBarcode(barcode: string): Observable<LostBookLookup | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/LookupBarcode/${encodeURIComponent(barcode)}`).pipe(
      map(r => (r.data ?? r) as LostBookLookup),
      catchError(() => of(null))
    );
  }

  // MarkLost(Barcode, LossDate, Reason) — Báo mất: Barcode.Status = 'L'
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  markLost(payload: { barcode: string; lossDate: string; reason?: string }): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/MarkLost`, payload).pipe(map(r => r.data ?? r));
  }

  // RestoreLost(Barcode) — Khôi phục mất: Barcode.Status = 'R' + xoá bản ghi tại bảng sách mất
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  restoreLost(barcode: string): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/RestoreLost`, { barcode }).pipe(map(r => r.data ?? r));
  }

  // Xuất Excel danh sách sách mất theo đúng bộ lọc đang tìm kiếm (không phân trang)
  exportExcel(params: LostBookSearchParams): Observable<Blob> {
    const payload = this.buildSearchPayload(params);
    return this.http.post(`${this.baseUrl}/Export`, payload, { responseType: 'blob' });
  }
}
