import { Component, OnInit, input, signal } from '@angular/core';
import { CrudConfig, CrudPage } from '../../shared/crud-page';
import { TreeCatalogConfig, TreeCatalogPage } from '../../shared/tree-catalog-page';

/**
 * Vỏ nạp muộn cho các danh mục của cổng thông tin: cấu hình (rất nhiều khoá, cột, ô nhập) nằm ở <c>catalogs.ts</c> và chỉ được tải khi mở một
 * màn của portal — không làm phình gói khởi động của trang quản trị. Khoá `catalog` đến từ `data` của route (router bind vào input).
 */
@Component({
  selector: 'app-portal-catalog-route',
  imports: [CrudPage, TreeCatalogPage],
  template: `
    @if (crud(); as c) { <app-crud-page [config]="c" /> }
    @if (tree(); as t) { <app-tree-catalog-page [config]="t" /> }
  `,
})
export class PortalCatalogRoute implements OnInit {
  readonly catalog = input.required<string>();

  protected readonly crud = signal<CrudConfig | null>(null);
  protected readonly tree = signal<TreeCatalogConfig | null>(null);

  async ngOnInit(): Promise<void> {
    const all = await import('./catalogs');
    const configs: Record<string, CrudConfig | TreeCatalogConfig> = {
      categories: all.PORTAL_NEWS_CATEGORIES, events: all.PORTAL_NEWS_EVENTS, comments: all.PORTAL_NEWS_COMMENTS, 'menu-types': all.PORTAL_MENU_TYPES,
      banners: all.PORTAL_BANNERS, ads: all.PORTAL_ADS, 'ads-groups': all.PORTAL_ADS_GROUPS, albums: all.PORTAL_PHOTO_ALBUMS, photos: all.PORTAL_PHOTOS,
      videos: all.PORTAL_VIDEOS, links: all.PORTAL_LINKS, 'link-groups': all.PORTAL_LINK_GROUPS, contacts: all.PORTAL_CONTACTS, 'contact-groups': all.PORTAL_CONTACT_GROUPS,
    };
    const config = configs[this.catalog()];
    if (!config) return;
    if ('extraFields' in config) this.tree.set(config);
    else this.crud.set(config);
  }
}
