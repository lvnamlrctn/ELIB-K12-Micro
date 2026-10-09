import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// Một dòng báo cáo lượt vào theo bạn đọc
export interface ReceiptionReportRow {
  readerId?:    number;
  cardNo?:      string;
  fullName?:    string;
  readerType?:  string;
  className?:   string;
  departmentName?: string;
  checkInCount: number;   // số lượt vào
}

// ELIB: /api/PrintBook/Receiption/Report — nguồn ReportReaderCheckInCount / ReportReaderCheckInByDate
@Injectable({ providedIn: 'root' })
export class ReceiptionReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Receiption/Report`; }

  // ReportReaderCheckInCount(ClassId, CourseId, TenantId, ReaderTypeId, FromDate, ToDate, CircPlaceId, Top)
  report(params: {
    classId?: number | null; courseId?: number | null; tenantId?: string | null; readerTypeId?: number | null;
    fromDate?: string | null; toDate?: string | null; circPlaceId?: number | null; top?: number; pageIndex?: number; pageSize?: number;
  }): Observable<{ data: ReceiptionReportRow[]; recordsTotal: number }> {
    const payload = {
      classId: params.classId ?? 0, courseId: params.courseId ?? 0, tenantId: params.tenantId ?? null, readerTypeId: params.readerTypeId ?? 0,
      fromDate: params.fromDate || '', toDate: params.toDate || '', circPlaceId: params.circPlaceId ?? 0, top: params.top ?? 0,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 20
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/ReaderCount`, payload).pipe(
      map(res => {
        let data: ReceiptionReportRow[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  export(params: Record<string, unknown>): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/ReaderCountExport`, params, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
