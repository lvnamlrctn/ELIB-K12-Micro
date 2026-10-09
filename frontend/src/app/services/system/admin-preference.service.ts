import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Cấu hình 1 bảng danh sách lưu theo tài khoản (Đợt 21 — port từ ELIB-LRC). */
export interface AdminTableSettings {
  version: 1;
  pageSize: number;
  columns: string[];
  filters: Record<string, string | number | null>;
}

export interface AdminTableSettingsResponse { revision: number; settings: AdminTableSettings | null; }

@Injectable({ providedIn: 'root' })
export class AdminPreferenceService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Dbo/AdminPreference`; }

  get(page: string): Observable<AdminTableSettingsResponse> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${this.baseUrl}/${page}`).pipe(map(res => res?.data as AdminTableSettingsResponse));
  }

  save(page: string, revision: number, settings: AdminTableSettings): Observable<{ revision: number }> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.put<any>(`${this.baseUrl}/${page}`, { revision, settings }).pipe(map(res => res?.data as { revision: number }));
  }
}
