import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MetadataSchemaRegistry } from '../../models/ebook/metadata-schema';

export interface SchemaSearchResult {
  data: MetadataSchemaRegistry[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class MetadataSchemaService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook/MetadataSchemaRegistry`;
  }

  search(keyword = '', pageIndex = 1, pageSize = 10, tenantId: string | null = null): Observable<SchemaSearchResult> {
    const payload = { keyword, pageIndex, pageSize, tenantId };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: MetadataSchemaRegistry[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        else if (res?.data?.items) data = res.data.items;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<MetadataSchemaRegistry> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => res.data || res));
  }

  create(item: Partial<MetadataSchemaRegistry>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data || res));
  }

  update(publicId: string, item: Partial<MetadataSchemaRegistry>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data || res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data || res));
  }
}
