import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { APP_CONFIG } from '../config';

/**
 * Đa đơn vị (multi-tenant) theo subdomain: <madonvi>.thuvientn.vn.
 * Lúc khởi động (APP_INITIALIZER) gọi backend resolve host → tenantId + tên/logo đơn vị,
 * gán vào APP_CONFIG.TenantId để mọi API public dùng đúng tenant.
 */
@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  private document = inject(DOCUMENT);

  // Tên đơn vị + chữ trên logo, dùng cho header OPAC.
  name = signal<string>('');
  logoText = signal<string>('TV');

  async resolve(): Promise<void> {
    // Chỉ resolve ở browser; SSR/dev giữ tenant fallback trong APP_CONFIG.
    if (!isPlatformBrowser(this.platformId)) return;

    const host = window.location.hostname;
    // localhost / IP / không có subdomain → giữ fallback (môi trường dev).
    const isDevHost = !host || host === 'localhost' || /^\d{1,3}(\.\d{1,3}){3}$/.test(host) || host.split('.').length < 3;

    if (!isDevHost) {
      const res = await firstValueFrom(
        this.http.get<any>(`/api/public/PublicTenant/ResolveByHost?host=${encodeURIComponent(host)}`)
          .pipe(catchError(() => of(null)))
      );

      const data = res?.data ?? res;
      if (data?.tenantId) {
        APP_CONFIG.TenantId = data.tenantId;
        if (data.name) this.name.set(data.name);
        this.logoText.set(data.logoText || this.initials(data.name));
      }
    }

    // Đợt 22.6 — PWA: <link rel="manifest"> động theo tenant hiện tại (dev host thì dùng fallback
    // APP_CONFIG.TenantId), gắn cả khi isDevHost để cài đặt/kiểm thử PWA được ngay trên localhost.
    this.setManifestLink();
  }

  private setManifestLink(): void {
    const href = `/api/public/PublicTenant/Manifest.json?tenantId=${encodeURIComponent(APP_CONFIG.TenantId)}`;
    let link = this.document.querySelector<HTMLLinkElement>('link[rel="manifest"]');
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'manifest';
      this.document.head.appendChild(link);
    }
    link.href = href;
  }

  private initials(name?: string): string {
    if (!name) return 'TV';
    const words = name.trim().split(/\s+/).filter(Boolean);
    const letters = words.slice(-3).map(w => w[0]?.toUpperCase() ?? '').join('');
    return letters || 'TV';
  }
}
