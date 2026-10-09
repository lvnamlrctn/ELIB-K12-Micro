import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapFloorUtility } from '../../models/map/map-floor-utility';

@Injectable({ providedIn: 'root' })
export class MapFloorUtilityService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/MapFloorUtility`; }

  searchAllByFloor(floorId: number): Observable<MapFloorUtility[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { floorId }).pipe(
      map(res => res?.data ?? []),
      catchError(() => of([]))
    );
  }

  create(item: Partial<MapFloorUtility>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<MapFloorUtility>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
