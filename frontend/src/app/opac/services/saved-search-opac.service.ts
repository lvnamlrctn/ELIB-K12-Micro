import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { UnifiedSearchRequest } from './unified-search.service';

export interface SavedSearchMatch { id: string; title: string; source: string; }

export interface SavedSearch {
  id: string;
  name: string;
  query: UnifiedSearchRequest;
  alertsEnabled: boolean;
  hasUnread: boolean;
  total: number;
  lastCheckedAt: string | null;
  matches: SavedSearchMatch[];
}

/** Tìm kiếm đã lưu + cảnh báo kết quả mới (Đợt 9) — ReaderWorkspaceController, api/public/ReaderWorkspace/Searches.
 * Gọi kèm Bearer token bạn đọc (xem opac/auth.interceptor.ts) — cùng khuôn RoomBookingOpacService. */
@Injectable({ providedIn: 'root' })
export class SavedSearchOpacService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }
  private get baseUrl(): string {
    return `${this.backendRoot}/api/public/ReaderWorkspace/Searches`;
  }

  list(): Observable<SavedSearch[]> {
    return this.http.get<any>(this.baseUrl).pipe(
      map(res => (res?.success && Array.isArray(res.data) ? (res.data as SavedSearch[]) : [])),
      catchError(() => of([]))
    );
  }

  save(name: string, query: UnifiedSearchRequest, alertsEnabled = true): Observable<{ ok: boolean; message?: string; id?: string }> {
    return this.http.post<any>(this.baseUrl, { name, query, alertsEnabled }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message, id: res?.data?.id })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  remove(id: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.delete<any>(`${this.baseUrl}/${id}`).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  toggleAlerts(id: string, enabled: boolean): Observable<{ ok: boolean; message?: string }> {
    return this.http.put<any>(`${this.baseUrl}/${id}/Alerts`, enabled).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  markRead(id: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.put<any>(`${this.baseUrl}/${id}/Read`, {}).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }
}
