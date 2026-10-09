import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapUnshelvedBarcode, MapShelvingSearchParams, FloorWithShelves } from '../../models/printbook/map-shelving';

export interface MapShelvingSearchResult { data: MapUnshelvedBarcode[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class MapShelvingService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/StoreMapShelving`; }

  /** Tìm các số KCB chưa xếp giá, kèm gợi ý vị trí theo DDC (chỉ có khi đã chọn storeId). */
  searchUnshelved(params: MapShelvingSearchParams): Observable<MapShelvingSearchResult> {
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
        let data: MapUnshelvedBarcode[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.data?.recordsTotal ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Sơ đồ Tầng → Giá → Ngăn DDC của 1 Kho, để chọn vị trí xếp giá. */
  getShelvesByStore(storeId: number): Observable<FloorWithShelves[]> {
    return this.http.get<any>(`${this.baseUrl}/ShelvesByStore/${storeId}`).pipe(
      map(res => Array.isArray(res?.data) ? res.data as FloorWithShelves[] : []),
      catchError(() => of([]))
    );
  }

  /** Gán vị trí (giá + ngăn) cho các số KCB đã chọn, đồng thời chuyển trạng thái sẵn sàng. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  place(barcodeIds: number[], mapObjectId: number, mapShelfRowId: number | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Place`, { barcodeIds, mapObjectId, mapShelfRowId }).pipe(
      map(r => r.data ?? r)
    );
  }
}
