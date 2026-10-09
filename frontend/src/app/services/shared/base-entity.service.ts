import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';
import { BaseEntity } from '../../models/shared/base-entity';
import { environment } from '../../../environments/environment';

export abstract class BaseEntityService<T extends BaseEntity> {
  protected http = inject(HttpClient);
  protected abstract get baseUrl(): string;

  protected get apiBase(): string {
    return environment.baseApiUrl + this.baseUrl;
  }

  getAll(params: DataTableParams): Observable<DataTableResponse<T>> {
    const pageIndex = params.length ? Math.floor(params.start / params.length) + 1 : 1;
    const payload = {
      keyword: params.search?.value || '',
      portalId: '',
      language: '',
      tenantId: params['tenantId'] ?? null, // lọc theo đơn vị (super-admin)
      pageIndex: pageIndex,
      pageSize: params.length || 10
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Search`, payload).pipe(
      map(res => {
        let data: T[] = [];
        if (Array.isArray(res)) {
          data = res;
        } else if (res?.data && Array.isArray(res.data)) {
          data = res.data;
        } else if (res?.data?.items && Array.isArray(res.data.items)) {
          data = res.data.items;
        } else if (res && Array.isArray(res.items)) {
           data = res.items;
        }

        const total = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;

        return {
          draw: params.draw,
          data: data,
          recordsTotal: total,
          recordsFiltered: total
        };
      }),
      catchError(() => of({ draw: params.draw, data: [], recordsTotal: 0, recordsFiltered: 0 }))
    );
  }

  getById(id: string): Observable<T> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.apiBase}/GetById/${id}`).pipe(
      map(res => res.data || res)
    );
  }

  create(item: Partial<T>): Observable<T> {
    const itemRecord = item as Record<string, unknown>;
    const payload = {
      name: item.name,
      portalId: itemRecord['portalId'] || '',
      language: itemRecord['language'] || ''
    };

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Add`, payload).pipe(
      map(res => res.data || res)
    );
  }

  update(item: T): Observable<T> {
    const itemRecord = item as Record<string, unknown>;
    const id =itemRecord['publicId'] || item.id || itemRecord['Id'];
    
    const payload = {
      name: item.name,
      portalId: itemRecord['portalId'] || '',
      language: itemRecord['language'] || ''
    };
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.apiBase}/Update/${id}`, payload).pipe(
      map(res => res.data || res)
    );
  }

  delete(id: string): Observable<void> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.delete<any>(`${this.apiBase}/Delete/${id}`).pipe(
      map(res => res.data || res)
    );
  }

  changeStatus(id: string, status: number | boolean): Observable<T> {
    const payloadStatus = typeof status === 'boolean' ? (status ? 2 : 1) : status;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.apiBase}/ChangeStatus`, { publicId: id, status: payloadStatus }).pipe(
      map(res => res.data || res)
    );
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  import(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.post<any>(`${this.apiBase}/Import`, formData).pipe(
      map(res => res.data || res)
    );
  }
}

