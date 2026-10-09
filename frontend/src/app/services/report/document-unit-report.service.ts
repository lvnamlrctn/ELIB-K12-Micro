import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DocumentUnitReportRow } from '../../models/report/document-unit-report';

export type DocumentReportType = 'summary' | 'print' | 'digital';

export interface DocumentUnitReportParams {
  tenantId?:  number | null;
  fromDate?:  string | null;
  toDate?:    string | null;
  reportType: DocumentReportType;
}

// ELIB: /api/PrintBook/Report/DocumentUnit — báo cáo tài liệu (Bib/Barcode/EbookItem) theo đơn vị (Tenant)
@Injectable({ providedIn: 'root' })
export class DocumentUnitReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Report/DocumentUnit`; }

  private buildPayload(params: DocumentUnitReportParams) {
    return {
      tenantId: params.tenantId ?? null,
      fromDate: params.fromDate || null,
      toDate: params.toDate || null,
      reportType: params.reportType
    };
  }

  summary(params: DocumentUnitReportParams): Observable<DocumentUnitReportRow[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Summary`, this.buildPayload(params)).pipe(
      map(res => {
        const d = res?.data ?? res;
        return (d?.items ?? []) as DocumentUnitReportRow[];
      }),
      catchError(() => of([]))
    );
  }

  export(params: DocumentUnitReportParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/SummaryExport`, this.buildPayload(params), { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }
}
