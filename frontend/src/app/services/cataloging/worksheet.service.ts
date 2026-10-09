import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { WorkSheet } from '../../models/cataloging/worksheet';

export interface WorkSheetSearchResult { data: WorkSheet[]; recordsTotal: number; }

@Injectable({ providedIn: 'root' })
export class WorkSheetService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/PrintBook/Catalogue/WorkSheet`; }

  search(params: { keyword?: string | null; bibTypeId?: number | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<WorkSheetSearchResult> {
    const payload = { keyword: params.keyword || '', bibTypeId: params.bibTypeId ?? null, pageIndex: params.pageIndex ?? 1, pageSize: params.pageSize ?? 10, tenantId: params.tenantId ?? null };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: WorkSheet[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getByBibType(bibTypeId: number): Observable<WorkSheet | null> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/GetByBibType/${bibTypeId}`).pipe(
      map(r => r?.data ?? r ?? null), catchError(() => of(null))
    );
  }

  searchAll(): Observable<WorkSheet[]> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => Array.isArray(res) ? res : (res?.data?.items ?? res?.data ?? [])),
      catchError(() => of([]))
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getById(publicId: string): Observable<WorkSheet> { return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  create(item: Partial<WorkSheet>): Observable<any> { return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  update(publicId: string, item: Partial<WorkSheet>): Observable<any> { return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(r => r.data ?? r)); }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  delete(publicId: string): Observable<void> { return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(r => r.data ?? r)); }
}
