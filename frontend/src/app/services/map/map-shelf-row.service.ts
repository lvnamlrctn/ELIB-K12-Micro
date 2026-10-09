import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapShelfRow } from '../../models/map/map-shelf-row';

@Injectable({ providedIn: 'root' })
export class MapShelfRowService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/MapShelfRow`; }

  searchAllByShelfDetail(shelfDetailId: number): Observable<MapShelfRow[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { shelfDetailId }).pipe(
      map(res => res?.data ?? []),
      catchError(() => of([]))
    );
  }

  create(item: Partial<MapShelfRow>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<MapShelfRow>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
