import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export type SerialReportType = 'RECEIVED_SUMMARY' | 'RECEIVED_DETAIL' | 'MISSING_CLAIM_LIST';

const REPORT_ACTIONS: Record<SerialReportType, string> = {
  RECEIVED_SUMMARY:   'ReceivedSummary',
  RECEIVED_DETAIL:    'ReceivedDetail',
  MISSING_CLAIM_LIST: 'MissingClaimList',
};

// Một dòng báo cáo báo tạp chí — các trường tuỳ loại báo cáo, xem docs/BACKEND-PRINT-DOCUMENT-MGMT.md mục Phase 5
export interface SerialReportRow {
  [key: string]: unknown;
  subscriptionTitle?: string;
  issn?:              string;
  serialSeq?:         string;
  publishedDate?:     string;
  status?:            number;
  claimCount?:        number;
  plannedDate?:       string;
  quantity?:          number;
  receivedIssues?:    number;   // tổng hợp: số kỳ nhận (quantity = số bản)
}

export interface SerialReportParams {
  subscriptionCode?: string | null;
  dateFrom?:         string | null;
  dateTo?:           string | null;
  pageIndex?:        number;
  pageSize?:         number;
}

@Injectable({ providedIn: 'root' })
export class SerialReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Magazine/Report`; }

  report(type: SerialReportType, params: SerialReportParams): Observable<{ data: SerialReportRow[]; recordsTotal: number }> {
    const payload = {
      subscriptionCode: params.subscriptionCode || null, dateFrom: params.dateFrom || null, dateTo: params.dateTo || null,
      pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 20,
    };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/${REPORT_ACTIONS[type]}`, payload).pipe(
      map(res => {
        let data: SerialReportRow[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  export(type: SerialReportType, params: SerialReportParams): Observable<Blob> {
    const payload = { subscriptionCode: params.subscriptionCode || null, dateFrom: params.dateFrom || null, dateTo: params.dateTo || null };
    return this.http.post(`${this.baseUrl}/${REPORT_ACTIONS[type]}Export`, payload, { responseType: 'blob' }).pipe(catchError(() => of(new Blob())));
  }
}
