import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface StoreBookReportParams {
  dateFrom?:  string | null;
  dateTo?:    string | null;
  storeId?:   number | null;
  pageIndex?: number;
  pageSize?:  number;
}

export interface StoreStatEntry {
  storeName:  string;
  titleCount: number;
  copyCount:  number;
}

export interface StoreBookReportResult {
  headers:       string[];
  rows:          string[][];
  totalCount:    number;
  titleCount:    number;
  parentLibrary: string;
  libraryName:   string;
  storeName:     string;
  storeStats:    StoreStatEntry[];
}

@Injectable({ providedIn: 'root' })
export class StoreBookReportService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Store/BookReport`; }

  private buildPayload(params: StoreBookReportParams) {
    return {
      dateFrom:  params.dateFrom || null,
      dateTo:    params.dateTo   || null,
      storeId:   params.storeId  ?? null,
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 20,
    };
  }

  search(params: StoreBookReportParams): Observable<StoreBookReportResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, this.buildPayload(params)).pipe(
      map(res => ({
        headers:       res?.data?.headers ?? [],
        rows:          res?.data?.rows ?? [],
        totalCount:    res?.data?.totalCount ?? 0,
        titleCount:    res?.data?.titleCount ?? 0,
        parentLibrary: res?.data?.parentLibrary ?? '',
        libraryName:   res?.data?.libraryName ?? '',
        storeName:     res?.data?.storeName ?? '',
        storeStats:    res?.data?.storeStats ?? [],
      })),
      catchError(() => of({ headers: [], rows: [], totalCount: 0, titleCount: 0, parentLibrary: '', libraryName: '', storeName: '', storeStats: [] }))
    );
  }

  exportExcel(params: StoreBookReportParams): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Export`, this.buildPayload(params), { responseType: 'blob' })
      .pipe(catchError(() => of(new Blob())));
  }
}
