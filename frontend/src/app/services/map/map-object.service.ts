import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapObject } from '../../models/map/map-object';

export interface MapObjectSearchResult {
  data:         MapObject[];
  recordsTotal: number;
}

@Injectable({ providedIn: 'root' })
export class MapObjectService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/MapObject`; }

  search(params: { keyword?: string | null; floorId?: number | null; objectType?: string | null; tenantId?: string | null; pageIndex?: number; pageSize?: number }): Observable<MapObjectSearchResult> {
    const payload = {
      keyword:    params.keyword    || '',
      floorId:    params.floorId    ?? null,
      objectType: params.objectType || null,
      tenantId:   params.tenantId   ?? null,
      pageIndex:  params.pageIndex  ?? 1,
      pageSize:   params.pageSize   ?? 10,
    };
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  getById(publicId: string): Observable<MapObject> {
    return this.http.get<any>(`${this.baseUrl}/GetById/${publicId}`).pipe(map(res => res.data ?? res));
  }

  create(item: Partial<MapObject>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<MapObject>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }

  searchAllByFloor(floorId: number): Observable<MapObject[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { floorId }).pipe(
      map(res => res?.data ?? []),
      catchError(() => of([]))
    );
  }
}
