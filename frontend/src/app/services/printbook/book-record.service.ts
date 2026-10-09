import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { BookRecord } from '../../models/printbook/book-record';
import { BookTitleRecord } from '../../models/printbook/book-title-record';

export interface BookSearchParams {
  mfnFrom?:    number | null;
  mfnTo?:      number | null;
  title?:      string | null;
  publisher?:  string | null;
  publishYear?: string | null;
  author?:     string | null;
  callNumber?: string | null;
  keyword?:    string | null;
  summary?:    string | null;
  docTypeId?:  number | null;
  status?:     string | null;
  storeId?:    number | null;
  pageIndex?:  number;
  pageSize?:   number;
  tenantId?:   string | null;
}

export interface BookSearchResult {
  data:         BookRecord[];
  recordsTotal: number;
}

export interface BookTitleSearchResult {
  data:         BookTitleRecord[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class BookRecordService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/Book`;
  }

  search(params: BookSearchParams): Observable<BookSearchResult> {
    const payload = {
      mfnFrom:    params.mfnFrom    ?? null,
      mfnTo:      params.mfnTo      ?? null,
      title:      params.title      || null,
      publisher:  params.publisher  || null,
      publishYear: params.publishYear || null,
      author:     params.author     || null,
      callNumber: params.callNumber || null,
      keyword:    params.keyword    || null,
      summary:    params.summary    || null,
      docTypeId:  params.docTypeId  ?? null,
      status:     params.status     ?? null,
      storeId:    params.storeId    ?? null,
      pageIndex:  params.pageIndex  ?? 1,
      pageSize:   params.pageSize   ?? 10,
      tenantId:   params.tenantId   ?? null,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: BookRecord[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.recordsTotal ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  exportDetail(params: BookSearchParams): Observable<Blob> {
    const payload = { ...params };
    return this.http.post(`${this.baseUrl}/ExportDetail`, payload, { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }

  exportSummary(params: BookSearchParams): Observable<Blob> {
    const payload = { ...params };
    return this.http.post(`${this.baseUrl}/ExportSummary`, payload, { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }

  searchTitles(params: BookSearchParams): Observable<BookTitleSearchResult> {
    const payload = {
      mfnFrom:    params.mfnFrom    ?? null,
      mfnTo:      params.mfnTo      ?? null,
      title:      params.title      || null,
      publisher:  params.publisher  || null,
      publishYear: params.publishYear || null,
      author:     params.author     || null,
      callNumber: params.callNumber || null,
      keyword:    params.keyword    || null,
      summary:    params.summary    || null,
      docTypeId:  params.docTypeId  ?? null,
      status:     params.status     ?? null,
      pageIndex:  params.pageIndex  ?? 1,
      pageSize:   params.pageSize   ?? 10,
      tenantId:   params.tenantId   ?? null,
    };
    return this.http.post<any>(`${this.baseUrl}/SearchTitles`, payload).pipe(
      map(res => {
        let data: BookTitleRecord[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.recordsTotal ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  exportTitles(params: BookSearchParams): Observable<Blob> {
    const payload = { ...params };
    return this.http.post(`${this.baseUrl}/ExportSummary`, payload, { responseType: 'blob' }).pipe(
      catchError(() => of(new Blob()))
    );
  }
}
