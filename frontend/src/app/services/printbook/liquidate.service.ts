import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LiquidateItem } from '../../models/printbook/liquidate';

export interface LiquidateSearchResult { data: LiquidateItem[]; recordsTotal: number; }
export interface LiquidateSearchParams {
  title?: string | null; author?: string | null; barcode?: string | null;
  storeId?: number | null; bibTypeId?: number | null; liquidateFrom?: string | null; liquidateTo?: string | null;
  pageIndex?: number; pageSize?: number; tenantId?: string | null;
}

// ELIB: /api/PrintBook/Store/Liquidate — nguồn Book.LiquidateBook / ReLiquidateBook / SearchLiquidate
@Injectable({ providedIn: 'root' })
export class LiquidateService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/Liquidate`; }

  private buildSearchPayload(params: LiquidateSearchParams) {
    return {
      title: params.title || null, author: params.author || null, barcode: params.barcode || null,
      storeId: params.storeId ?? null, bibTypeId: params.bibTypeId ?? null,
      liquidateFrom: params.liquidateFrom || null, liquidateTo: params.liquidateTo || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null
    };
  }

  // SearchLiquidate(Title, Author, Barcode, StoreId, BibTypeId, LiquidateFrom, LiquidateTo, page)
  search(params: LiquidateSearchParams): Observable<LiquidateSearchResult> {
    const payload = this.buildSearchPayload(params);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: LiquidateItem[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  // LiquidateBook(BarcodeId, Reason, UserId, LiquidateDate) — thanh lý theo mã vạch
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  liquidate(payload: { barcode: string; reason: string; liquidateDate: string }): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Liquidate`, payload).pipe(map(r => r.data ?? r));
  }

  // ReLiquidateBook(BarcodeId) — hủy thanh lý
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  reLiquidate(barcode: string): Observable<any> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ReLiquidate`, { barcode }).pipe(map(r => r.data ?? r));
  }

  // Xuất Excel danh sách thanh lý theo đúng bộ lọc đang tìm kiếm (không phân trang)
  exportExcel(params: LiquidateSearchParams): Observable<Blob> {
    const payload = this.buildSearchPayload(params);
    return this.http.post(`${this.baseUrl}/Export`, payload, { responseType: 'blob' });
  }
}
