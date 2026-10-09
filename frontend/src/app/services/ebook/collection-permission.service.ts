import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface ReaderType {
  id: number;
  publicId?: string;
  name: string;
  parentId?: number | null;
  children?: ReaderType[];
}

export interface CollectionPermissionItem {
  readerTypeId: number;
  collectionId?: number;
  read: number;
  download: number;
  maxdocument: number;
  offlineDays?: number | null;
}

@Injectable({ providedIn: 'root' })
export class CollectionPermissionService {
  private http = inject(HttpClient);

  private get baseUrl(): string {
    return `${environment.baseApiUrl}/api/Ebook`;
  }

  getReaderTypes(): Observable<ReaderType[]> {
    return this.http.post<any>(`${environment.baseApiUrl}/api/Dbo/ReaderType/Search`, { keyword: '', pageIndex: 1, pageSize: 1000 }).pipe(
      map(res => {
        const data = res?.data?.items ?? res?.data ?? res ?? [];
        return Array.isArray(data) ? data : [];
      }),
      catchError(err => {
        console.error('[getReaderTypes] error:', err);
        return of([]);
      })
    );
  }

  getPermissions(collectionPublicId: string): Observable<CollectionPermissionItem[]> {
    return this.http.get<any>(`${this.baseUrl}/EBookCollection/GetPermissions/${collectionPublicId}`).pipe(
      map(res => {
        const data = res?.data ?? res ?? [];
        const arr: any[] = Array.isArray(data) ? data : [];
        return arr.map(item => ({
          ...item,
          readerTypeId: item.readerTypeId ?? item.readerTypeid
        } as CollectionPermissionItem));
      }),
      catchError(err => {
        console.error('[getPermissions] error:', err);
        return of([]);
      })
    );
  }

  savePermissions(collectionPublicId: string, permissions: CollectionPermissionItem[]): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/EBookCollection/SavePermissions`, {
      collectionPublicId,
      permissions
    }).pipe(map(res => res?.data ?? res));
  }
}
