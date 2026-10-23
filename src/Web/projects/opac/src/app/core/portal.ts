import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Language } from './i18n';
import { Library } from './library';

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
  private readonly base = '/api/opac/portal';

  home(lang: string) { return firstValueFrom(this.http.get<PortalHome>(`${this.base}/home`, { params: { lang } })); }
  menus(lang: string) { return firstValueFrom(this.http.get<PortalMenu[]>(`${this.base}/menus`, { params: { lang } })); }
  categories(lang: string) { return firstValueFrom(this.http.get<PortalCategory[]>(`${this.base}/categories`, { params: { lang } })); }
  searchNews(body: { q?: string | null; categoryId?: number | null; language: string; page: number; pageSize: number }) {
    return firstValueFrom(this.http.post<PortalNewsPage>(`${this.base}/news/Search`, body));
  }
  news(slug: string, lang: string) { return firstValueFrom(this.http.get<PortalNewsDetail>(`${this.base}/news/${encodeURIComponent(slug)}`, { params: { lang } })); }
  viewed(slug: string) { return firstValueFrom(this.http.post<void>(`${this.base}/news/${encodeURIComponent(slug)}/view`, {})); }
  comments(slug: string) { return firstValueFrom(this.http.get<PortalComment[]>(`${this.base}/news/${encodeURIComponent(slug)}/comments`)); }
  comment(slug: string, body: { name: string; email: string | null; title: string | null; content: string; website: string | null }) {
    return firstValueFrom(this.http.post<void>(`${this.base}/news/${encodeURIComponent(slug)}/comments`, body));
  }
  page(slug: string, lang: string) { return firstValueFrom(this.http.get<PortalPageContent>(`${this.base}/pages/${encodeURIComponent(slug)}`, { params: { lang } })); }
  albums(lang: string) { return firstValueFrom(this.http.get<PortalAlbum[]>(`${this.base}/albums`, { params: { lang } })); }
  photos(albumId: string) { return firstValueFrom(this.http.get<PortalPhoto[]>(`${this.base}/albums/${albumId}/photos`)); }
  videos(lang: string) { return firstValueFrom(this.http.get<PortalVideo[]>(`${this.base}/videos`, { params: { lang } })); }
  contactGroups(lang: string) { return firstValueFrom(this.http.get<PortalContactGroup[]>(`${this.base}/contact-groups`, { params: { lang } })); }
  contact(body: { contactGroupId: number | null; name: string; email: string; mobile: string | null; address: string | null; title: string | null; content: string; website: string | null }) {
    return firstValueFrom(this.http.post<void>(`${this.base}/contacts`, body));
  }
  hit() { return firstValueFrom(this.http.post<PortalCounter>(`${this.base}/counter/hit`, {})); }
  counter() { return firstValueFrom(this.http.get<PortalCounter>(`${this.base}/counter`)); }
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
