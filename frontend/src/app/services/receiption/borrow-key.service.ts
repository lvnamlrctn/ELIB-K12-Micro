import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { BorrowKey, KeyHolderSnapshot } from '../../models/receiption/borrow-key';

export interface BorrowKeySearchResult { data: BorrowKey[]; recordsTotal: number; }

// ELIB: /api/PrintBook/Receiption/BorrowKey — nguồn Bussiness.PrintBook.Circulation.Loan.BorrowKey
@Injectable({ providedIn: 'root' })
export class BorrowKeyService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Receiption/BorrowKey`; }

  // SearchKeyBorrowByCardNo(CardNo, CircPlace) — bạn đọc + khóa đang mượn
  getSnapshot(cardNo: string, circPlaceId?: number | null): Observable<KeyHolderSnapshot | null> {
    const payload = { cardNo, circPlaceId: circPlaceId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Snapshot`, payload).pipe(
      map(res => { const d = res?.data ?? res; if (!d) return null; return { ...d, currentKeys: d.currentKeys ?? d.keys ?? [], currentLoans: d.currentLoans ?? [] } as KeyHolderSnapshot; }),
      catchError(() => of(null))
    );
  }

  // Borrow(ReaderId, CompartmentCode, CircPlaceId)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  borrowKey(payload: { readerId: number; compartmentCode: string; circPlaceId?: number | null }): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Borrow`, payload).pipe(map(r => r.data ?? r)); }
  // Return
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  returnKey(payload: { keyOutId?: number; compartmentCode?: string; cardNo?: string; circPlaceId?: number | null }): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Return`, payload).pipe(map(r => r.data ?? r)); }

  // SearchBorrowKey / SearchReturnKey — tra cứu lịch sử mượn/trả khóa
  search(params: {
    cardNumber?: string | null; firstName?: string | null; lastName?: string | null;
    borrowDateFrom?: string | null; borrowDateTo?: string | null; returnDateFrom?: string | null; returnDateTo?: string | null;
    tenantId?: string | null; readerTypeId?: number | null; classId?: number | null; courseId?: number | null; storeId?: number | null;
    onlyReturned?: boolean; pageIndex?: number; pageSize?: number;
  }): Observable<BorrowKeySearchResult> {
    const payload = {
      cardNumber: params.cardNumber || '', firstName: params.firstName || '', lastName: params.lastName || '',
      borrowDateFrom: params.borrowDateFrom || '', borrowDateTo: params.borrowDateTo || '', returnDateFrom: params.returnDateFrom || '', returnDateTo: params.returnDateTo || '',
      tenantId: params.tenantId ?? null, readerTypeId: params.readerTypeId ?? 0, classId: params.classId ?? 0, courseId: params.courseId ?? 0, storeId: params.storeId ?? 0,
      onlyReturned: params.onlyReturned ?? false, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: BorrowKey[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }
}
