import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { Book } from './models';

/**
 * Tài liệu yêu thích (lưu DB theo bạn đọc). API công khai, scope theo readerId + tenantId.
 * Xem docs/BACKEND-FAVORITE.md cho hợp đồng endpoint.
 */
@Injectable({ providedIn: 'root' })
export class FavoriteApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get baseUrl(): string {
    return isPlatformServer(this.platformId)
      ? `${APP_CONFIG.BackendBase}/api/public`
      : '/api/public';
  }

  // tenantId lấy theo bạn đọc đăng nhập (từ response login); fallback host-resolved cho phiên cũ.
  private resolveTenant(tenantId?: string): string {
    return tenantId || APP_CONFIG.TenantId;
  }

  // Danh sách tài liệu yêu thích của bạn đọc (kèm thông tin hiển thị).
  getFavorites(readerId: string, tenantId?: string): Observable<Book[]> {
    const body = { readerId, tenantId: this.resolveTenant(tenantId) };
    return this.http.post<any>(`${this.baseUrl}/Ebook/EbookFavorite/Search`, body).pipe(
      map(res => {
        const data = res?.data ?? res ?? {};
        const items = Array.isArray(data.items) ? data.items : (Array.isArray(data) ? data : []);
        return items.map((x: any) => this.mapBook(x));
      }),
      catchError(() => of([] as Book[]))
    );
  }

  addFavorite(readerId: string, itemId: string, tenantId?: string): Observable<boolean> {
    const body = { readerId, itemId, tenantId: this.resolveTenant(tenantId) };
    return this.http.post<any>(`${this.baseUrl}/Ebook/EbookFavorite/Add`, body).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }

  removeFavorite(readerId: string, itemId: string, tenantId?: string): Observable<boolean> {
    const body = { readerId, itemId, tenantId: this.resolveTenant(tenantId) };
    return this.http.post<any>(`${this.baseUrl}/Ebook/EbookFavorite/Remove`, body).pipe(
      map(res => res?.success === true),
      catchError(() => of(false))
    );
  }

  // Media base cho ảnh bìa (giống backenData của book-api): browser '/data' (proxy → 8089), SSR BackendDataBase.
  private get mediaBase(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendDataBase : '/data';
  }

  private buildImg(p?: string): string {
    if (!p) return '';
    if (/^https?:\/\//i.test(p)) return p;       // đã là URL tuyệt đối
    return `${this.mediaBase}/${p.replace(/^\/+/, '')}`;
  }

  private mapBook(x: any): Book {
    return {
      id: x.itemId || x.publicId || x.id,        // GUID tài liệu → link /book/:id (không dùng id dòng favorite)
      title: x.itemTitle ?? x.title ?? '',
      author: x.author ?? '',
      publisher: x.publisher ?? '',
      year: x.publishDate || '',
      imageUrl: this.buildImg(x.images ?? x.imageUrl ?? ''),
      description: x.brief || x.description || '',
      views: x.totalView ?? 0,
      downloads: x.totalDownload ?? 0,
      files: []
    } as Book;
  }
}
