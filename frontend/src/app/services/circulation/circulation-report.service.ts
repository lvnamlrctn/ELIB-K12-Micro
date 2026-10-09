import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface CirculationReportParams {
  reportType:    number | null;
  dateFrom?:     string | null;
  dateTo?:       string | null;
  classId?:      number | null;
  courseId?:     number | null;
  orgId?:        number | null;
  readerTypeId?: number | null;
  circPlaceId?:  number | null;
  pageIndex?:    number;
  pageSize?:     number;
}

export interface CirculationReportResult {
  headers:       string[];
  rows:          string[][];
  totalCount:    number;
  /** Dòng "Tổng cộng" tính trên toàn bộ dữ liệu (null = loại báo cáo không có tổng). */
  totalRow:      string[] | null;
  parentLibrary: string;
  libraryName:   string;
}

@Injectable({ providedIn: 'root' })
export class CirculationReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Circulation/Report`; }

  private buildPayload(params: CirculationReportParams) {
    return {
      reportType:   params.reportType ?? 0,
      dateFrom:     params.dateFrom     || null,
      dateTo:       params.dateTo       || null,
      classId:      params.classId      ?? null,
      courseId:     params.courseId     ?? null,
      orgId:        params.orgId        ?? null,
      readerTypeId: params.readerTypeId ?? null,
      circPlaceId:  params.circPlaceId  ?? null,
      pageIndex:    params.pageIndex ?? 1,
      pageSize:     params.pageSize  ?? 20,
    };
  }

  search(params: CirculationReportParams): Observable<CirculationReportResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, this.buildPayload(params)).pipe(
      map(res => ({
        headers:       res?.data?.headers ?? [],
        rows:          res?.data?.rows ?? [],
        totalCount:    res?.data?.totalCount ?? 0,
        totalRow:      res?.data?.totalRow ?? null,
        parentLibrary: res?.data?.parentLibrary ?? '',
        libraryName:   res?.data?.libraryName ?? '',
      })),
      catchError(() => of({ headers: [], rows: [], totalCount: 0, totalRow: null, parentLibrary: '', libraryName: '' }))
    );
  }

  exportExcel(params: CirculationReportParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Export`, this.buildPayload(params), { responseType: 'blob' })
      .pipe(catchError(() => of(new Blob())));
  }
}
