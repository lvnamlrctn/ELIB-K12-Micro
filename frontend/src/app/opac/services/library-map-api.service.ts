import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

/** Sơ đồ 2D thư viện + tra vị trí bản sách, OPAC công khai. */

export interface MapBuilding {
  id: number;
  name?: string;
  code?: string;
}

export interface MapFloorSummary {
  id: number;
  buildingId: number;
  buildingName?: string;
  floorNumber?: number;
  name?: string;
}

export interface MapShelfRowInfo {
  id: number;
  rowIndex?: number;
  ddcStart?: string;
  ddcEnd?: string;
  description?: string;
}

export interface MapShelfDetail {
  categoryRange?: string;
  subjectName?: string;
  capacity?: number;
  occupiedCount: number;
  rows: MapShelfRowInfo[];
}

export interface MapObjectInfo {
  id: number;
  name?: string;
  code?: string;
  objectType?: string;
  positionX?: number;
  positionY?: number;
  width?: number;
  height?: number;
  colorHex?: string;
  iconName?: string;
  shelfDetail?: MapShelfDetail;
}

export interface MapFloor {
  id: number;
  name?: string;
  floorNumber?: number;
  width?: number;
  height?: number;
  buildingId: number;
  buildingName?: string;
  objects: MapObjectInfo[];
}

export interface BookLocation {
  found: boolean;
  source: 'assigned' | 'ddc-range' | 'none';
  buildingId?: number;
  buildingName?: string;
  floorId?: number;
  floorNumber?: number;
  floorName?: string;
  floorWidth?: number;
  floorHeight?: number;
  shelfObjectId?: number;
  shelfCode?: string;
  shelfName?: string;
  positionX?: number;
  positionY?: number;
  shelfRowId?: number;
  rowIndex?: number;
  ddcStart?: string;
  ddcEnd?: string;
  categoryRange?: string;
  subjectName?: string;
  entranceObjectId?: number;
  entrancePositionX?: number;
  entrancePositionY?: number;
}

@Injectable({ providedIn: 'root' })
export class LibraryMapApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }

  getBuildings(): Observable<MapBuilding[]> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicLibraryMap/Buildings`).pipe(
      map(res => (res?.success && res.data ? (res.data as MapBuilding[]) : [])),
      catchError(() => of([]))
    );
  }

  getFloors(buildingId: number): Observable<MapFloorSummary[]> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicLibraryMap/Floors/${buildingId}`).pipe(
      map(res => (res?.success && res.data ? (res.data as MapFloorSummary[]) : [])),
      catchError(() => of([]))
    );
  }

  getFloor(floorId: number): Observable<MapFloor | null> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicLibraryMap/Floor/${floorId}`).pipe(
      map(res => (res?.success && res.data ? (res.data as MapFloor) : null)),
      catchError(() => of(null))
    );
  }

  locateBarcode(barcodeId: number): Observable<BookLocation | null> {
    return this.http.get<any>(`${this.backendRoot}/api/public/PublicLibraryMap/LocateBarcode/${barcodeId}`).pipe(
      map(res => (res?.success && res.data ? (res.data as BookLocation) : null)),
      catchError(() => of(null))
    );
  }
}
