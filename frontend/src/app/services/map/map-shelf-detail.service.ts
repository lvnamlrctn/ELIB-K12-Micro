import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapShelfDetail } from '../../models/map/map-shelf-detail';

@Injectable({ providedIn: 'root' })
export class MapShelfDetailService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/MapShelfDetail`; }

  getByObjectId(objectId: number): Observable<MapShelfDetail | null> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { objectId }).pipe(
      map(res => (res?.data ?? [])[0] ?? null),
      catchError(() => of(null))
    );
  }

  create(item: Partial<MapShelfDetail>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<MapShelfDetail>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
