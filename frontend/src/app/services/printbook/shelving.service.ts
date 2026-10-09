import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { UnshelvedBarcode, ShelvingSearchParams } from '../../models/printbook/shelving';

export interface ShelvingSearchResult { data: UnshelvedBarcode[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class ShelvingService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store`; }

  /** Tìm các số KCB chưa xếp giá (trạng thái "chưa sẵn sàng"). */
  searchUnshelved(params: ShelvingSearchParams): Observable<ShelvingSearchResult> {
    const payload = {
      receiptCode: params.receiptCode || '',
      barcodeFrom: params.barcodeFrom || null,
      barcodeTo:   params.barcodeTo   || null,
      storeId:     params.storeId     ?? null,
      pageIndex:   params.pageIndex   ?? 1,
      pageSize:    params.pageSize    ?? 10,
      tenantId:    params.tenantId    ?? null,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchUnshelved`, payload).pipe(
      map(res => {
        let data: UnshelvedBarcode[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Đánh dấu các số KCB đã chọn sang trạng thái "sẵn sàng cho mượn". */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  /** storeId: gán kho cho các ĐKCB cùng lúc xếp giá; bỏ trống = giữ nguyên kho hiện tại. */
  shelve(barcodeIds: number[], storeId?: number | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Shelve`, { barcodeIds, storeId: storeId ?? null }).pipe(map(r => r.data ?? r));
  }
}
