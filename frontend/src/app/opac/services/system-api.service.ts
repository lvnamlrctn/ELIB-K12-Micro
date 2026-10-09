import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { Hyperlink, VisitStats, Z3950Library, PublicBanner } from './models';

@Injectable({ providedIn: 'root' })
export class SystemApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  
  private backendBase = APP_CONFIG.BackendBase;
  
  private get baseUrl(): string {
    return isPlatformServer(this.platformId) 
      ? `${this.backendBase}/api/public`
      : '/api/public';
  }

  private hyperlinks$?: Observable<Hyperlink[]>;
  private z3950Libraries$?: Observable<Z3950Library[]>;
  private banners$?: Observable<PublicBanner[]>;
  private systemParamsCache = new Map<string, Observable<string>>();

  // Danh sách thư viện liên kết (Z39.50) cho trang Tìm kiếm nâng cao.
  getZ3950Libraries(): Observable<Z3950Library[]> {
    if (!this.z3950Libraries$) {
      const url = `${this.baseUrl}/PublicZ3950Config/Search`;
      this.z3950Libraries$ = this.http.post<any>(url, { tenantId: APP_CONFIG.TenantId, keyword: '', pageIndex: 1, pageSize: 100 }).pipe(
        map(res => {
          const arr = Array.isArray(res?.data?.items) ? res.data.items
                    : Array.isArray(res?.data) ? res.data
                    : Array.isArray(res) ? res : [];
          return arr.map((x: any) => ({ publicId: x.publicId, name: x.name ?? x.databaseName ?? '', id: x.id } as Z3950Library));
        }),
        catchError(() => of([] as Z3950Library[])),
        shareReplay(1)
      );
    }
    return this.z3950Libraries$;
  }

  // Banner cho ảnh nền hero trang chủ — lấy phần tử đầu (đã sắp theo sortOrder ở backend).
  getBanners(): Observable<PublicBanner[]> {
    if (!this.banners$) {
      this.banners$ = this.http.post<any>(`${this.baseUrl}/PublicBanner/SearchAll`, { tenantId: APP_CONFIG.TenantId })
        .pipe(
          map(res => Array.isArray(res?.data) ? res.data as PublicBanner[] : []),
          catchError(() => of([] as PublicBanner[])),
          shareReplay(1)
        );
    }
    return this.banners$;
  }

  getHyperlinks(): Observable<Hyperlink[]> {
    if (!this.hyperlinks$) {
      this.hyperlinks$ = this.http.post<any>(`${this.baseUrl}/PublicHyperLink/SearchAll`, { tenantId: APP_CONFIG.TenantId })
        .pipe(
          map(res => {
             if (res.data && Array.isArray(res.data)) {
                 return res.data as Hyperlink[];
             }
             return [];
          }),
          catchError(err => {
            console.error('Failed to get hyperlinks', err);
            return of([]);
          }),
          shareReplay(1)
        );
    }
    return this.hyperlinks$;
  }

  trackVisit(): Observable<boolean> {
    const url = `${this.baseUrl}/PublicStatistic/TrackVisit`;
    return this.http.post<any>(url, { tenantId: APP_CONFIG.TenantId }).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }

  // Số liệu lượt truy cập để hiển thị widget thống kê (footer). Backend cache ~1 phút.
  getVisitStats(): Observable<VisitStats> {
    const url = `${this.baseUrl}/PublicCounter/Stats?tenantId=${APP_CONFIG.TenantId}`;
    return this.http.get<any>(url).pipe(
      map(res => {
        const d = res?.data ?? res ?? {};
        return {
          total: d.total ?? 0,
          today: d.today ?? 0,
          lastWeek: d.lastWeek ?? 0,
          lastMonth: d.lastMonth ?? 0
        } as VisitStats;
      }),
      catchError(() => of({ total: 0, today: 0, lastWeek: 0, lastMonth: 0 }))
    );
  }

  getSystemPara(code: string): Observable<string> {
    if (!this.systemParamsCache.has(code)) {
      const url = `${this.baseUrl}/PublicSystemParameter/GetSystemPara?Code=${code}&TenantId=${APP_CONFIG.TenantId}`;
      const ob$ = this.http.get<any>(url).pipe(
        map(res => {
          if (res?.success && res.data) {
            return typeof res.data === 'string' ? res.data : (res.data.value || res.data.description || res.data.name || '');
          }
          return '';
        }),
        catchError(() => of('')),
        shareReplay(1)
      );
      this.systemParamsCache.set(code, ob$);
    }
    return this.systemParamsCache.get(code)!;
  }
}

