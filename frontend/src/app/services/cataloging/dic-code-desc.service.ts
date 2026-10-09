import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { environment } from '../../../environments/environment';

export abstract class DicCodeDescService {
  protected http = inject(HttpClient);
  protected abstract get baseUrl(): string;

  protected get apiBase(): string {
    return environment.baseApiUrl + this.baseUrl;
  }

  getAll(params: DataTableParams, language = ''): Observable<DataTableResponse<any>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language,
      tenantId: params['tenantId'] ?? null, // lọc theo đơn vị (super-admin)
      pageIndex: pageIndex,
      pageSize: params.length || 10
    };

    return this.http.post<any>(`${this.apiBase}/Search`, payload).pipe(
      map(res => {
        let data: any[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items && Array.isArray(res.data.items)) data = res.data.items;
        else if (res && Array.isArray(res.items)) data = res.items;

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { draw: params.draw, data: data, recordsTotal: total, recordsFiltered: total };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  getById(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiBase}/GetById/${id}`).pipe(map(res => res.data || res));
  }

  create(item: any): Observable<any> {
    return this.http.post<any>(`${this.apiBase}/Add`, item).pipe(map(res => res.data || res));
  }

  update(id: string, item: any): Observable<any> {
    return this.http.put<any>(`${this.apiBase}/Update/${id}`, item).pipe(map(res => res.data || res));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<any>(`${this.apiBase}/Delete/${id}`).pipe(map(res => res.data || res));
  }

  import(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<any>(`${this.apiBase}/Import`, formData).pipe(
      map(res => res.data || res)
    );
  }
}
