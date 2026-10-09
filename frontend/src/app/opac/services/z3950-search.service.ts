import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, catchError, map, of } from 'rxjs';

export interface Z3950Field { field: string; value: string; }

export interface Z3950BriefResult {
  libraryId: string;
  libraryName: string;
  systax: string;
  connected: boolean;
  count: number;
  error: string | null;
}

export interface Z3950Record {
  publicId: string | null;
  title: string | null;
  author: string | null;
  publisher: string | null;
  publishDate: string | null;
  keyword: string | null;
  otherTitle: string | null;
}

export interface Z3950DetailResult {
  libraryId: string;
  libraryName: string;
  systax: string;
  connected: boolean;
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  records: Z3950Record[];
  error: string | null;
}

@Injectable({ providedIn: 'root' })
export class Z3950SearchService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get baseUrl(): string {
    return isPlatformServer(this.platformId)
      ? `${APP_CONFIG.BackendBase}/api/public/PrintBook/SearchZ3950`
      : '/api/public/PrintBook/SearchZ3950';
  }

  // Kiểm tra nhanh kết nối + đếm số biểu ghi của từng thư viện.
  searchBrief(fields: Z3950Field[], operator: 'AND' | 'OR', libraryIds: string[]): Observable<Z3950BriefResult[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchBrief`, { fields, operator, libraryIds }).pipe(
      map(res => Array.isArray(res?.data) ? res.data as Z3950BriefResult[] : []),
      catchError(() => of([] as Z3950BriefResult[]))
    );
  }

  // Lấy biểu ghi chi tiết có phân trang (riêng theo từng thư viện).
  searchDetail(
    fields: Z3950Field[],
    operator: 'AND' | 'OR',
    libraryIds: string[],
    pageIndex = 1,
    pageSize = 10
  ): Observable<Z3950DetailResult[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchDetail`, { fields, operator, libraryIds, pageIndex, pageSize }).pipe(
      map(res => Array.isArray(res?.data) ? res.data as Z3950DetailResult[] : []),
      catchError(() => of([] as Z3950DetailResult[]))
    );
  }
}
