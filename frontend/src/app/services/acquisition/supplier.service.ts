import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Supplier } from '../../models/acquisition/supplier';

export interface SupplierSearchResult {
  data:         Supplier[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class SupplierService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/Supplier`;
  }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number; tenantId?: string | null }): Observable<SupplierSearchResult> {
    const payload = {
      keyword:   params.keyword   || '',
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
      tenantId:  params.tenantId  ?? null,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: Supplier[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(id: number): Observable<Supplier> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data ?? res));
  }

  create(item: Partial<Supplier>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(id: number, item: Partial<Supplier>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item).pipe(map(res => res.data ?? res));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data ?? res));
  }
}
