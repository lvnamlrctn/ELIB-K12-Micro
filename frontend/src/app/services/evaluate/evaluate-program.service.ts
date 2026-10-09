import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';

export interface EvaluateProgram {
  id?: string;
  publicId?: string;
  name?: string;
  description?: string;
  donViId?: number | null;
  portalId?: string;
  language?: string;
  tenantName?: string;
}

@Injectable({ providedIn: 'root' })
export class EvaluateProgramService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Evaluate/EvaluateProgram`;
  }

  getAll(params: DataTableParams): Observable<DataTableResponse<EvaluateProgram>> {
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
        let data: EvaluateProgram[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  searchAll(): Observable<EvaluateProgram[]> {
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

  getById(id: string): Observable<EvaluateProgram> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data ?? res));
  }

  create(item: Partial<EvaluateProgram>): Observable<EvaluateProgram> {
    const payload = { name: item.name, description: item.description, donViId: item.donViId ?? null, portalId: item.portalId || '', language: item.language || '' };
    return this.http.post<any>(`${this.baseUrl}/Add`, payload).pipe(map(res => res.data ?? res));
  }

  update(id: string, item: Partial<EvaluateProgram>): Observable<EvaluateProgram> {
    const payload = { name: item.name, description: item.description, donViId: item.donViId ?? null, portalId: item.portalId || '', language: item.language || '' };
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, payload).pipe(map(res => res.data ?? res));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data ?? res));
  }
}
