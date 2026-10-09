import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CheckLog, CheckStatus } from '../../models/receiption/checkin';

export interface CheckLogSearchResult { data: CheckLog[]; recordsTotal: number; }

// ELIB: /api/PrintBook/Receiption/Check — nguồn Bussiness.Common.Reader (CheckIn/Out + SearchReaderCheckIn)
@Injectable({ providedIn: 'root' })
export class CheckInService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Receiption/Check`; }

  // CheckReaderCheckIn(CardNumber) — tra trạng thái thẻ
  getStatus(cardNo: string, circPlaceId?: number | null): Observable<CheckStatus | null> {
    const payload = { cardNo, circPlaceId: circPlaceId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Status`, payload).pipe(
      map(res => {
        const d = res?.data ?? res; if (!d) return { checkedIn: false, found: false } as CheckStatus;
        return { ...d, checkedIn: !!(d.checkedIn ?? d.isCheckedIn), found: d.found ?? true } as CheckStatus;
      }),
      catchError(() => of(null))
    );
  }

  // ReaderCheckIn(...) — quét vào
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  checkIn(payload: { cardNo: string; circPlaceId?: number | null }): Observable<any> { return this.http.post<any>(`${this.baseUrl}/CheckIn`, payload).pipe(map(r => r.data ?? r)); }
  // ReaderCheckOut(...) — quét ra
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  checkOut(payload: { cardNo: string; checkInId?: number; circPlaceId?: number | null }): Observable<any> { return this.http.post<any>(`${this.baseUrl}/CheckOut`, payload).pipe(map(r => r.data ?? r)); }

  // SearchReaderCheckIn(...) — lịch sử vào ra
  searchHistory(params: {
    cardNumber?: string | null; firstName?: string | null; lastName?: string | null;
    checkInFrom?: string | null; checkInTo?: string | null; checkOutFrom?: string | null; checkOutTo?: string | null;
    tenantId?: string | null; readerTypeId?: number | null; classId?: number | null; courseId?: number | null; storeId?: number | null;
    pageIndex?: number; pageSize?: number;
  }): Observable<CheckLogSearchResult> {
    const payload = {
      cardNumber: params.cardNumber || '', firstName: params.firstName || '', lastName: params.lastName || '',
      checkInTimeFrom: params.checkInFrom || '', checkInTimeTo: params.checkInTo || '', checkOutTimeFrom: params.checkOutFrom || '', checkOutTimeTo: params.checkOutTo || '',
      tenantId: params.tenantId ?? null, readerTypeId: params.readerTypeId ?? 0, classId: params.classId ?? 0, courseId: params.courseId ?? 0, circPlaceId: params.storeId || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchHistory`, payload).pipe(
      map(res => {
        let data: CheckLog[] = [];
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
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }

  exportHistory(params: {
    cardNumber?: string | null; firstName?: string | null; checkInFrom?: string | null; checkInTo?: string | null; storeId?: number | null;
  }): Observable<Blob> {
    const payload = {
      cardNumber: params.cardNumber || '', firstName: params.firstName || '',
      checkInTimeFrom: params.checkInFrom || '', checkInTimeTo: params.checkInTo || '',
      circPlaceId: params.storeId || null
    };
    return this.http.post(`${this.baseUrl}/ExportHistory`, payload, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
