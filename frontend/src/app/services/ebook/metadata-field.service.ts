import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MetaDataFieldRegistery } from '../../models/ebook/metadata-field';

export interface FieldSearchResult {
  data: MetaDataFieldRegistery[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class MetadataFieldService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/MetaDataFieldRegistery`;
  }

  search(schemaId: number, keyword = '', pageIndex = 1, pageSize = 10): Observable<FieldSearchResult> {
    const payload = { metaDataSchemaId: schemaId, keyword, pageIndex, pageSize };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: MetaDataFieldRegistery[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items) data = res.data.items;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getAll(keyword = '', pageSize = 999): Observable<MetaDataFieldRegistery[]> {
    return this.http.post<any>(`${this.baseUrl}/Search`, { keyword, pageIndex: 1, pageSize }).pipe(
      map(res => {
        if (Array.isArray(res)) return res;
        if (res?.data?.items) return res.data.items;
        if (res?.data && Array.isArray(res.data)) return res.data;
        return [];
      }),
      catchError(() => of([]))
    );
  }

  getById(id: number): Observable<MetaDataFieldRegistery> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${id}`).pipe(map(res => res.data || res));
  }

  create(item: Partial<MetaDataFieldRegistery>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  update(id: number, item: Partial<MetaDataFieldRegistery>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${id}`, item).pipe(map(res => res.data || res));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${id}`).pipe(map(res => res.data || res));
  }
}
