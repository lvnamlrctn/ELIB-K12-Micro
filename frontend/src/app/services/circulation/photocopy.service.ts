import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  PhotoCopyRow, PhotoCopyReaderLookup, PhotoCopyDocLookup, PhotoCopyTotals, PhotoCopySearchParams
} from '../../models/circulation/photocopy';

export interface PhotoCopySearchResult { data: PhotoCopyRow[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class PhotoCopyService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/CPhoto`; }

  private buildPayload(p: PhotoCopySearchParams): Record<string, unknown> {
    return {
      cardNo:        p.cardNo        || null,
      lastName:      p.lastName      || null,
      firstName:     p.firstName     || null,
      bibTitle:      p.bibTitle      || null,
      photoDateFrom: p.photoDateFrom || null,
      photoDateTo:   p.photoDateTo   || null,
      isPaid:        p.isPaid        ?? null,
      tenantId:      p.tenantId      ?? null,
      pageIndex:     p.pageIndex     ?? 1,
      pageSize:      p.pageSize      ?? 10,
    };
  }

  search(params: PhotoCopySearchParams): Observable<PhotoCopySearchResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, this.buildPayload(params)).pipe(
      map(res => {
        let data: PhotoCopyRow[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  /** Tổng thành tiền trên toàn bộ kết quả khớp filter (không chỉ trang hiện tại). */
  totals(params: PhotoCopySearchParams): Observable<PhotoCopyTotals> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Totals`, this.buildPayload(params)).pipe(
      map(res => (res?.data ?? res) as PhotoCopyTotals),
      catchError(() => of({ totalAmountSum: 0, totalPaid: 0, totalUnpaid: 0, count: 0 }))
    );
  }

  /** Tra bạn đọc theo số thẻ. Trả null nếu không tìm thấy. */
  lookupCard(cardNo: string): Observable<PhotoCopyReaderLookup | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/LookupCard/${encodeURIComponent(cardNo)}`).pipe(
      map(r => (r?.data ?? r) as PhotoCopyReaderLookup),
      catchError(() => of(null))
    );
  }

  /** Tra tài liệu theo Số ĐKCB. Trả null nếu không tìm thấy. */
  lookupBarcode(barcode: string): Observable<PhotoCopyDocLookup | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/LookupBarcode/${encodeURIComponent(barcode)}`).pipe(
      map(r => (r?.data ?? r) as PhotoCopyDocLookup),
      catchError(() => of(null))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Record<string, unknown>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Record<string, unknown>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
}
