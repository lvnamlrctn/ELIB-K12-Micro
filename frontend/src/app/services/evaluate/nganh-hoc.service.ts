import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';

export interface NganhHoc {
  id?: string;
  publicId?: string;
  majorsCode?: string;
  majorsName?: string;
  parentId?: number | null;
  programId?: number | null;
  amountStudent?: number | null;
  sortOrder?: number | null;
  status?: number | null;
  portalId?: string;
  language?: string;
  tenantName?: string;
}

@Injectable({ providedIn: 'root' })
export class NganhHocService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/NganhHoc`;
  }

  getAll(params: DataTableParams): Observable<DataTableResponse<NganhHoc>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      tenantId: params['tenantId'] ?? null,
      pageIndex,
      pageSize: params.length || 10
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: NganhHoc[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  searchAll(): Observable<NganhHoc[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, {}).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }

  getById(id: string): Observable<NganhHoc> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data ?? res));
  }

  private toPayload(item: Partial<NganhHoc>) {
    return {
      majorsCode: item.majorsCode,
      majorsName: item.majorsName,
      parentId: item.parentId ?? null,
      programId: item.programId ?? null,
      amountStudent: item.amountStudent ?? null,
      sortOrder: item.sortOrder ?? null,
      status: item.status ?? 2,
      portalId: item.portalId || '',
      language: item.language || ''
    };
  }

  create(item: Partial<NganhHoc>): Observable<NganhHoc> {
    return this.http.post<any>(`${this.baseUrl}/Add`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  update(id: string, item: Partial<NganhHoc>): Observable<NganhHoc> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, this.toPayload(item)).pipe(map(res => res.data ?? res));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data ?? res));
  }
}
