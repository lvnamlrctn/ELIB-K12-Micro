import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Borrow, BorrowReaderSnapshot } from '../../models/circulation/borrow';

@Injectable({ providedIn: 'root' })
export class BorrowService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/Loan`; }

  /** Lấy thông tin độc giả + sách đang mượn theo số thẻ. (SearchBookBorrowByCardNo) */
  getReaderSnapshot(cardNo: string, circPlaceId?: number | null): Observable<BorrowReaderSnapshot | null> {
    const payload = { cardNo, circPlaceId: circPlaceId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ReaderSnapshot`, payload).pipe(
      map(res => {
        const d = res?.data ?? res;
        if (!d) return null;
        return { ...d, currentLoans: d.currentLoans ?? d.loans ?? [] } as BorrowReaderSnapshot;
      }),
      catchError(() => of(null))
    );
  }

  /** Mượn 1 cuốn theo barcode. (BorrowingBook) */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  borrow(readerId: number, barcode: string, circPlaceId?: number | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Checkout`, { readerId, barcode, circPlaceId: circPlaceId ?? null }).pipe(map(r => r?.data ?? r), catchError(e => of({ error: true, message: e?.error?.message })));
  }

  /** Mượn nhiều ĐKCB cùng lúc cho 1 bạn đọc (dải số hoặc nhập tay). (Checkout/Bulk) */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  borrowBulk(readerId: number, barcodes: string[], circPlaceId?: number | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Checkout/Bulk`, { readerId, barcodes, circPlaceId: circPlaceId ?? null }).pipe(map(r => r?.data ?? r), catchError(e => of({ error: true, message: e?.error?.message })));
  }

  /** Trả sách theo mã ĐKCB hoặc borrowId. (ReturnBook) */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return(params: { borrowId?: number; barcode?: string; cardNo?: string; circPlaceId?: number | null }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Return`, params).pipe(map(r => r?.data ?? r), catchError(e => of({ error: true, message: e?.error?.message })));
  }

  /** Gia hạn. (RenewBook) — reason bắt buộc từ Đợt 14, ghi vào UserLog phía backend. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  renew(borrowId: number, circPlaceId: number | null | undefined, reason: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Renew`, { borrowId, circPlaceId: circPlaceId ?? null, reason }).pipe(map(r => r?.data ?? r), catchError(e => of({ error: true, message: e?.error?.message })));
  }

  /** Ghi chú cho lượt mượn. (NoteBorrowBook) — reason bắt buộc từ Đợt 14, ghi vào UserLog phía backend. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  note(borrowId: number, note: string, reason: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Note`, { borrowId, note, reason }).pipe(map(r => r?.data ?? r), catchError(() => of(null)));
  }
}
