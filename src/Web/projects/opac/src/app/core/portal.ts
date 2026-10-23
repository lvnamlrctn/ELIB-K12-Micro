import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Language } from './i18n';
import { Library } from './library';
import { ReaderAuth } from './reader-auth';

export interface PortalNewsItem {
  publicId: string; slug: string; title: string; brief: string | null; thumb: string | null; images: string | null; publishedAt: string; categoryId: number | null;
  categoryName: string | null; author: string | null; featured: boolean; totalView: number;
}
export interface PortalNewsPage { total: number; page: number; pageSize: number; items: PortalNewsItem[]; }
export interface PortalAttachment { publicId: string; name: string; url: string; fileSize: number | null; }
export interface PortalNewsDetail {
  publicId: string; slug: string; title: string; brief: string | null; content: string | null; images: string | null; publishedAt: string; categoryId: number | null;
  categoryName: string | null; author: string | null; source: string | null; keyword: string | null; allowComment: boolean; totalView: number; metaTitle: string | null;
  metaKeyword: string | null; metaDescription: string | null; attachments: PortalAttachment[]; related: PortalNewsItem[];
}
export interface PortalCategory { id: number; name: string; parentId: number | null; level: number; description: string | null; pageTitle: string | null; metaDescription: string | null; }
export interface PortalComment { publicId: string; name: string; title: string | null; content: string; createdAt: string; }
export interface PortalPageContent { slug: string; title: string; content: string | null; metaDescription: string | null; }
export interface PortalBanner { publicId: string; name: string; image: string; link: string | null; width: number | null; height: number | null; position: string; }
export interface PortalAd { publicId: string; name: string; image: string; link: string | null; width: number | null; height: number | null; }
export interface PortalAdGroup { publicId: string; name: string; type: string | null; width: number | null; height: number | null; ads: PortalAd[]; }
export interface PortalLink { publicId: string; name: string; url: string; description: string | null; image: string | null; }
export interface PortalLinkGroup { publicId: string; name: string; links: PortalLink[]; }
export interface PortalPhoto { publicId: string; name: string; brief: string | null; image: string; link: string | null; }
export interface PortalAlbum { publicId: string; name: string; description: string | null; image: string | null; photoCount: number; }
export interface PortalVideo { publicId: string; name: string; description: string | null; embedUrl: string; provider: string; image: string | null; }
export interface PortalMenuItem { id: number; name: string; href: string; newTab: boolean; requiresLogin: boolean; icon: string | null; children: PortalMenuItem[]; }
export interface PortalMenu { code: string; name: string; items: PortalMenuItem[]; }
export interface PortalContactGroup { id: number; name: string; description: string | null; }
export interface PortalHome {
  banners: PortalBanner[]; featured: PortalNewsItem[]; latest: PortalNewsItem[]; albums: PortalAlbum[]; videos: PortalVideo[]; linkGroups: PortalLinkGroup[]; adGroups: PortalAdGroup[];
}
export interface PortalCounter { total: number; today: number; last7Days: number; }

/** Nội dung cổng thông tin (service portal) — /api/opac/portal/**, ẩn danh theo host đơn vị, chỉ nội dung đã đăng. */
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);
  private readonly readerAuth = inject(ReaderAuth);
  private readonly pub = '/api/opac/portal';

  /** Bạn đọc đã đăng nhập đi đường /member (gateway chỉ nhận token bạn đọc) để xem thêm tin của chuyên mục yêu cầu đăng nhập; còn lại đi đường ẩn danh. */
  /**
   * Gọi một API tin tức: bạn đọc đã đăng nhập thử đường /member trước; token bị từ chối (hết phiên) thì quay về đường ẩn danh như khách,
   * để trang tin không hỏng chỉ vì phiên cũ.
   */
  private async news$<T>(call: (base: string) => Promise<T>): Promise<T> {
    if (!this.readerAuth.accessToken()) return call(this.pub);
    try {
      return await call(`${this.pub}/member`);
    } catch (e) {
      if (e instanceof HttpErrorResponse && (e.status === 401 || e.status === 403)) return call(this.pub);
      throw e;
    }
  }

  home(lang: string) { return firstValueFrom(this.http.get<PortalHome>(`${this.pub}/home`, { params: { lang } })); }
  menus(lang: string) { return firstValueFrom(this.http.get<PortalMenu[]>(`${this.pub}/menus`, { params: { lang } })); }
  categories(lang: string) { return this.news$((base) => firstValueFrom(this.http.get<PortalCategory[]>(`${base}/categories`, { params: { lang } }))); }
  searchNews(body: { q?: string | null; categoryId?: number | null; language: string; page: number; pageSize: number }) {
    return this.news$((base) => firstValueFrom(this.http.post<PortalNewsPage>(`${base}/news/Search`, body)));
  }
  news(slug: string, lang: string) { return this.news$((base) => firstValueFrom(this.http.get<PortalNewsDetail>(`${base}/news/${encodeURIComponent(slug)}`, { params: { lang } }))); }
  viewed(slug: string) { return this.news$((base) => firstValueFrom(this.http.post<void>(`${base}/news/${encodeURIComponent(slug)}/view`, {}))); }
  comments(slug: string) { return this.news$((base) => firstValueFrom(this.http.get<PortalComment[]>(`${base}/news/${encodeURIComponent(slug)}/comments`))); }
  comment(slug: string, body: { name: string; email: string | null; title: string | null; content: string; website: string | null }) {
    return this.news$((base) => firstValueFrom(this.http.post<void>(`${base}/news/${encodeURIComponent(slug)}/comments`, body)));
  }
  page(slug: string, lang: string) { return firstValueFrom(this.http.get<PortalPageContent>(`${this.pub}/pages/${encodeURIComponent(slug)}`, { params: { lang } })); }
  albums(lang: string) { return firstValueFrom(this.http.get<PortalAlbum[]>(`${this.pub}/albums`, { params: { lang } })); }
  photos(albumId: string) { return firstValueFrom(this.http.get<PortalPhoto[]>(`${this.pub}/albums/${albumId}/photos`)); }
  videos(lang: string) { return firstValueFrom(this.http.get<PortalVideo[]>(`${this.pub}/videos`, { params: { lang } })); }
  contactGroups(lang: string) { return firstValueFrom(this.http.get<PortalContactGroup[]>(`${this.pub}/contact-groups`, { params: { lang } })); }
  contact(body: { contactGroupId: number | null; name: string; email: string; mobile: string | null; address: string | null; title: string | null; content: string; website: string | null }) {
    return firstValueFrom(this.http.post<void>(`${this.pub}/contacts`, body));
  }
  /** Đếm lượt nhấp quảng cáo: sendBeacon không chặn việc mở liên kết; lỗi bỏ qua. */
  adClick(publicId: string): void {
    try { navigator.sendBeacon(`${this.pub}/ads/${publicId}/click`); } catch { /* chỉ là số liệu phụ */ }
  }
  hit() { return firstValueFrom(this.http.post<PortalCounter>(`${this.pub}/counter/hit`, {})); }
  counter() { return firstValueFrom(this.http.get<PortalCounter>(`${this.pub}/counter`)); }
}

/**
 * Menu động của khung OPAC (đầu/chân trang) — chỉ tải khi đơn vị có phân hệ Cổng thông tin, tải lại khi đổi ngôn ngữ. Lỗi/chưa cấu hình → rỗng
 * (khung OPAC dùng các liên kết mặc định).
 */
@Injectable({ providedIn: 'root' })
export class PortalMenus {
  private readonly api = inject(PortalApi);
  private readonly library = inject(Library);
  private readonly language = inject(Language);

  readonly menus = signal<PortalMenu[]>([]);
  readonly counter = signal<PortalCounter | null>(null);

  constructor() {
    effect(() => {
      const lang = this.language.lang();
      if (this.library.loaded() && this.library.has('PORTAL')) void this.load(lang);
    });
  }

  items(code: string): PortalMenuItem[] {
    return this.menus().find((m) => m.code === code)?.items ?? [];
  }

  /** Mỗi phiên trình duyệt chỉ tính một lượt truy cập (sessionStorage) rồi chỉ đọc số. */
  async countVisit(): Promise<void> {
    try {
      const key = 'opac.visit.counted';
      const counted = sessionStorage.getItem(key) === '1';
      this.counter.set(counted ? await this.api.counter() : await this.api.hit());
      if (!counted) sessionStorage.setItem(key, '1');
    } catch {
      /* bộ đếm chỉ là phụ trợ */
    }
  }

  private async load(lang: string): Promise<void> {
    try {
      this.menus.set(await this.api.menus(lang));
    } catch {
      this.menus.set([]);
    }
  }
}
