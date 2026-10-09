import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MapEquipment } from '../../models/map/map-equipment';

@Injectable({ providedIn: 'root' })
export class MapEquipmentService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/MapEquipment`; }

  searchAllByObject(objectId: number): Observable<MapEquipment[]> {
    return this.http.post<any>(`${this.baseUrl}/SearchAll`, { objectId }).pipe(
      map(res => res?.data ?? []),
      catchError(() => of([]))
    );
  }

  create(item: Partial<MapEquipment>): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Add`, item).pipe(map(res => res.data ?? res));
  }

  update(publicId: string, item: Partial<MapEquipment>): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Update/${publicId}`, item).pipe(map(res => res.data ?? res));
  }

  delete(publicId: string): Observable<void> {
    return this.http.delete<any>(`${this.baseUrl}/Delete/${publicId}`).pipe(map(res => res.data ?? res));
  }
}
