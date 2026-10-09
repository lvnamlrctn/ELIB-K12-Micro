import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PatternMagazine } from '../../models/serial/pattern-magazine';

export interface PatternSearchResult { data: PatternMagazine[]; recordsTotal: number; }

// ELIB: /api/PrintBook/Magazine/Pattern — nguồn Bussiness.PrintBook.Magazine.PatternMagazine (+ PartemMagazineDetail)
@Injectable({ providedIn: 'root' })
export class PatternMagazineService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Magazine/Pattern`; }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<PatternSearchResult> {
    const payload = { keyword: params.keyword || '', pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: PatternMagazine[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  searchAll(): Observable<PatternMagazine[]> { return this.search({ pageSize: 1000 }).pipe(map(r => r.data)); }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(publicId: string): Observable<PatternMagazine> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r.data ?? r)); }
  // tra cứu theo id số (khác getById dùng publicId) — dùng cho modal Ghép số ở issue-receipt
  getByNumericId(id: number): Observable<PatternMagazine | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetByNumericId/${id}`).pipe(map(r => r?.data ?? r ?? null), catchError(() => of(null)));
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<PatternMagazine>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<PatternMagazine>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
}
