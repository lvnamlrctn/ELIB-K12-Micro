import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CircPlace, CircPlaceMapping } from '../../models/printbook/circ-place';

export interface CircPlaceSearchResult {
  data:         CircPlace[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class CircPlaceService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/PrintBook/CircPlace`;
  }

  search(params: { keyword?: string | null; pageIndex?: number; pageSize?: number }): Observable<CircPlaceSearchResult> {
    const payload = {
      keyword:   params.keyword   || '',
      pageIndex: params.pageIndex ?? 1,
      pageSize:  params.pageSize  ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => {
        let data: CircPlace[] = [];
        if (Array.isArray(res)) data = res;
        else if (res?.data?.items) data = res.data.items;
        else if (res?.data && Array.isArray(res.data)) data = res.data;
        const recordsTotal = res?.data?.totalCount ?? res?.totalRecords ?? res?.total ?? data.length;
        return { data, recordsTotal };
      }),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<CircPlace> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => res.data ?? res));
  }

  create(item: Partial<CircPlace>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<CircPlace>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }

  searchAll(): Observable<CircPlace[]> {
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

  getMapping(publicId: string): Observable<CircPlaceMapping> {
    return this.http.get<any>(`${this.baseUrl}/GetMapping/${publicId}`).pipe(
      map(res => (res?.data ?? res) as CircPlaceMapping),
      catchError(() => of({ storeIds: [], readerTypeIds: [] }))
    );
  }

  replaceStoreMapping(circPlacePublicId: string, storeIds: number[]): Observable<boolean> {
    return this.http.post<any>(`${this.baseUrl}/ReplaceStoreMapping`, { circPlacePublicId, storeIds }).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }

  replaceReaderTypeMapping(circPlacePublicId: string, readerTypeIds: number[]): Observable<boolean> {
    return this.http.post<any>(`${this.baseUrl}/ReplaceReaderTypeMapping`, { circPlacePublicId, readerTypeIds }).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }
}
