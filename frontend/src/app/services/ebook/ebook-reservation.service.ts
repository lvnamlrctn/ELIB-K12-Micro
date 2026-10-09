import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { EbookReservation } from '../../models/ebook/ebook-reservation';

export interface ReservationSearchParams {
  keyword?:           string | null;
  reservationStatus?: number | null;
  pageIndex?:         number;
  pageSize?:          number;
}

export interface ReservationSearchResult {
  data:         EbookReservation[];
  recordsTotal: number;
}

/**
 * Chỉ đọc — không có endpoint huỷ/thăng hạng qua admin (những hành động đó chỉ chạy nội bộ, kích hoạt
 * từ tự phục vụ bạn đọc qua MyLibraryController hoặc job EbookLoanExpiryJob).
 */
@Injectable({ providedIn: 'root' })
export class EbookReservationService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Ebook/EbookItemReservation`; }

  search(params: ReservationSearchParams): Observable<ReservationSearchResult> {
    return this.http.post<any>(`${this.baseUrl}/Search`, {
      keyword:           params.keyword || '',
      reservationStatus: params.reservationStatus ?? null,
      pageIndex:         params.pageIndex ?? 1,
      pageSize:          params.pageSize  ?? 10,
    }).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }
}
