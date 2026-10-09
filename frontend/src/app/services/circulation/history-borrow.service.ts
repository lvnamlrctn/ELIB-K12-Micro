import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Borrow } from '../../models/circulation/borrow';

export interface HistorySearchParams {
  cardNo?:    string | null;
  title?:     string | null;
  barcode?:   string | null;
  bibId?:     number | null;
  borrowDateFrom?: string | null;
  borrowDateTo?:   string | null;
  status?:    number | null;   // null=tất cả, 1=đang mượn, 2=đã trả, 3=quá hạn
  circPlaceId?:    number | null;
  readerTypeId?:   number | null;
  orgId?:          number | null;
  classId?:        number | null;
  courseId?:       number | null;
  operatorId?:     number | null;
  isOverdue?:      boolean | null;
  dueDateFrom?:    string | null;
  dueDateTo?:      string | null;
  returnDateFrom?: string | null;
  returnDateTo?:   string | null;
  pageIndex?: number;
  pageSize?:  number;
}
export interface HistorySearchResult { data: Borrow[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class HistoryBorrowService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/History`; }

  private buildSearchPayload(params: HistorySearchParams) {
    return {
      cardNo: params.cardNo || '', title: params.title || '', barcode: params.barcode || '',
      bibId: params.bibId ?? null,
      borrowDateFrom: params.borrowDateFrom || null, borrowDateTo: params.borrowDateTo || null,
      status: params.status ?? null,
      circPlaceId: params.circPlaceId ?? null,
      readerTypeId: params.readerTypeId ?? null,
      orgId: params.orgId ?? null,
      classId: params.classId ?? null,
      courseId: params.courseId ?? null,
      operatorId: params.operatorId ?? null,
      isOverdue: params.isOverdue ?? null,
      dueDateFrom: params.dueDateFrom || null, dueDateTo: params.dueDateTo || null,
      returnDateFrom: params.returnDateFrom || null, returnDateTo: params.returnDateTo || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10,
    };
  }

  search(params: HistorySearchParams): Observable<HistorySearchResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, this.buildSearchPayload(params)).pipe(
      map(res => {
        let data: Borrow[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  exportExcel(params: HistorySearchParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Export`, this.buildSearchPayload(params), { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }

  exportPdf(params: HistorySearchParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ExportPdf`, this.buildSearchPayload(params), { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
