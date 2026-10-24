import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { DigitalApi } from '../core/digital';
import { LDatePipe, Language } from '../core/i18n';
import { Library } from '../core/library';
import { PortalApi, PortalHome } from '../core/portal';
import { Facet, SearchApi, UnifiedHit } from '../core/search';

interface Service { module: string; title: string; text: string; icon: string; link: string; color: string; }

/** Dịch vụ OPAC theo phân hệ đơn vị đã mua — phân hệ chưa mua không hiện (features của service tenant). */
/** title/text: khoá dịch OPAC.HOME.SERVICE_<MODULE>(_TEXT). */
const SERVICES: Service[] = [
  { module: 'SEARCH', title: 'OPAC.HOME.SERVICE_SEARCH', text: 'OPAC.HOME.SERVICE_SEARCH_TEXT', icon: 'fa-magnifying-glass', link: '/tim-kiem', color: '#3b5b8c' },
  { module: 'DIGITAL', title: 'OPAC.HOME.SERVICE_DIGITAL', text: 'OPAC.HOME.SERVICE_DIGITAL_TEXT', icon: 'fa-book-open-reader', link: '/thu-vien-so', color: '#419b45' },
  { module: 'CIRCULATION', title: 'OPAC.HOME.SERVICE_CIRCULATION', text: 'OPAC.HOME.SERVICE_CIRCULATION_TEXT', icon: 'fa-right-left', link: '/tai-khoan', color: '#e46c0a' },
  { module: 'SPACE', title: 'OPAC.HOME.SERVICE_SPACE', text: 'OPAC.HOME.SERVICE_SPACE_TEXT', icon: 'fa-door-open', link: '/dat-phong', color: '#6b4c9a' },
  { module: 'AI', title: 'OPAC.HOME.SERVICE_AI', text: 'OPAC.HOME.SERVICE_AI_TEXT', icon: 'fa-robot', link: '/tro-ly', color: '#009688' },
  { module: 'PORTAL', title: 'OPAC.HOME.SERVICE_PORTAL', text: 'OPAC.HOME.SERVICE_PORTAL_TEXT', icon: 'fa-bullhorn', link: '/tin-tuc', color: '#b31b1b' },
];
const LINK_COLORS = ['#3b5b8c', '#419b45', '#e46c0a', '#b31b1b', '#6b4c9a', '#009688'];
const LINK_ICONS = ['fa-building-columns', 'fa-microchip', 'fa-database', 'fa-globe', 'fa-book', 'fa-laptop'];
const PER_PAGE = 4;

/**
 * Trang chủ OPAC (giao diện ELIB-TVXA): hero có ô tìm, 3 lối vào nhanh, bộ sưu tập tài liệu số, tài liệu mới, tin tức & thông báo, thẻ giờ mở cửa/hotline/email.
 * Mỗi khối chỉ hiện khi đơn vị có phân hệ tương ứng và có dữ liệu; số liệu tài liệu lấy một lượt tìm gộp (không tính thống kê tìm kiếm).
 */
@Component({
  selector: 'opac-home',
  imports: [FormsModule, RouterLink, LDatePipe, TranslatePipe],
  template: `
    <section class="relative min-h-[400px] flex items-center justify-center text-center text-white bg-cover bg-center bg-main transition-[background-image] duration-700" [style.background-image]="heroBg()">
      <div class="z-10 px-4 py-12 w-full">
        <h2 class="text-3xl md:text-4xl font-bold mb-6">{{ 'OPAC.HOME.HERO_TITLE' | translate }}</h2>
        <form (ngSubmit)="search()" class="flex flex-col md:flex-row max-w-3xl mx-auto bg-white rounded overflow-hidden">
          @if (library.has('SEARCH')) {
            <select name="field" [(ngModel)]="field" [attr.aria-label]="'OPAC.HOME.SEARCH_BY' | translate" class="p-4 text-dark border-b md:border-b-0 md:border-r border-gray-200 outline-none bg-gray-50 font-medium">
              <option value="q">{{ 'OPAC.HOME.FIELD_ALL' | translate }}</option>
              <option value="nhande">{{ 'OPAC.SEARCH.F_TITLE' | translate }}</option>
              <option value="tg">{{ 'OPAC.SEARCH.F_AUTHOR' | translate }}</option>
            </select>
          }
          <input name="q" type="text" [(ngModel)]="q" [placeholder]="'OPAC.HOME.SEARCH_PLACEHOLDER' | translate" [attr.aria-label]="'OPAC.SEARCH.KEYWORD_ARIA' | translate"
                 class="flex-1 p-4 text-dark outline-none min-w-0" />
          <button type="submit" class="bg-main text-white px-8 py-4 font-bold hover:bg-blue-700 transition-colors">
            <i class="fas fa-search mr-2"></i> {{ 'OPAC.SEARCH.BTN_FIND' | translate }}
          </button>
        </form>
        <p class="mt-6 italic text-lg opacity-90">{{ 'OPAC.HOME.HERO_SUBTITLE' | translate }}</p>
      </div>
    </section>

    <div class="container mx-auto px-4 py-12">
      @if (quickLinks().length) {
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-12">
          @for (l of quickLinks(); track l.key) {
            @if (l.external) {
              <a [href]="l.link" target="_blank" rel="noopener noreferrer" [style.background-color]="l.color"
                 class="text-white p-6 rounded-lg flex flex-col items-center justify-center hover:opacity-90 transition-opacity h-[154px] shadow-md">
                <i class="fas {{ l.icon }} text-4xl mb-3"></i>
                <span class="font-bold uppercase tracking-wide text-center">{{ l.title }}</span>
              </a>
            } @else {
              <a [routerLink]="l.link" [style.background-color]="l.color"
                 class="text-white p-6 rounded-lg flex flex-col items-center justify-center hover:opacity-90 transition-opacity h-[154px] shadow-md">
                <i class="fas {{ l.icon }} text-4xl mb-3"></i>
                <span class="font-bold uppercase tracking-wide text-center">{{ l.title }}</span>
              </a>
            }
          }
        </div>
      }

      @if (collections().length) {
        <div class="mb-12">
          <div class="flex justify-between items-center mb-6 border-b border-gray-200 pb-2">
            <h2 class="text-xl font-bold text-[#3b5b8c] uppercase border-l-4 border-[#d93025] pl-3 m-0">{{ 'OPAC.HOME.COLLECTIONS' | translate }}</h2>
            <div class="flex items-center gap-3">
              <a [routerLink]="searchLink()" [queryParams]="digitalQuery()" class="bg-[#d93025] text-white px-4 py-1.5 text-sm font-semibold rounded hover:bg-red-700 transition-colors">{{ 'OPAC.HOME.SEE_MORE' | translate }}</a>
              <div class="flex gap-1">
                <button type="button" (click)="colIndex.set(colIndex() - perPage)" [disabled]="colIndex() === 0" [attr.aria-label]="'COMMON.PREV' | translate"
                        class="border border-gray-300 px-2.5 py-1 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-left text-xs"></i></button>
                <button type="button" (click)="colIndex.set(colIndex() + perPage)" [disabled]="colIndex() + perPage >= collections().length" [attr.aria-label]="'COMMON.NEXT' | translate"
                        class="border border-gray-300 px-2.5 py-1 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-right text-xs"></i></button>
              </div>
            </div>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            @for (c of visibleCollections(); track c.key) {
              <a [routerLink]="searchLink()" [queryParams]="collectionQuery(c.key)"
                 class="border border-gray-200 rounded-lg p-5 flex items-center gap-5 hover:shadow-md transition-shadow bg-white active:scale-95">
                <div class="bg-[#eef2f6] w-16 h-16 rounded-xl flex items-center justify-center shrink-0"><i class="fas fa-folder text-3xl text-[#3b5b8c]"></i></div>
                <div class="min-w-0">
                  <h3 class="font-bold text-gray-800 mb-1.5 text-lg truncate">{{ c.key }}</h3>
                  @if (c.count > 0) {
                    <p class="text-sm text-gray-500 m-0 flex items-center gap-2"><i class="fas fa-file-lines text-[#d93025]"></i> {{ 'OPAC.HOME.DOC_COUNT' | translate: { count: c.count } }}</p>
                  }
                </div>
              </a>
            }
          </div>
        </div>
      }

      @if (latest().length) {
        <div class="flex justify-between items-center mb-8 border-b border-gray-200 pb-2">
          <div class="flex items-center border-l-4 border-main pl-4"><h2 class="text-2xl font-bold text-main m-0">{{ 'OPAC.HOME.NEW_DOCS' | translate }}</h2></div>
          <div class="flex gap-1">
            <button type="button" (click)="bookIndex.set(bookIndex() - perPage)" [disabled]="bookIndex() === 0" [attr.aria-label]="'COMMON.PREV' | translate"
                    class="border border-gray-300 px-3 py-1.5 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-left text-sm"></i></button>
            <button type="button" (click)="bookIndex.set(bookIndex() + perPage)" [disabled]="bookIndex() + perPage >= latest().length" [attr.aria-label]="'COMMON.NEXT' | translate"
                    class="border border-gray-300 px-3 py-1.5 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-right text-sm"></i></button>
          </div>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-6">
          @for (b of visibleBooks(); track b.kind + b.publicId) {
            <div class="bg-white rounded-lg overflow-hidden shadow-sm hover:shadow-md transition-shadow border border-gray-100 flex flex-col">
              <a [routerLink]="docLink(b)" class="block h-64 overflow-hidden bg-gray-50 p-4">
                @if (b.coverUrl) {
                  <img [src]="b.coverUrl" [alt]="b.title" loading="lazy" referrerpolicy="no-referrer" class="w-full h-full object-contain drop-shadow-md transition-transform hover:scale-105" />
                } @else {
                  <div class="w-full h-full flex items-center justify-center text-gray-300"><i class="fas text-6xl" [class.fa-book]="b.kind === 'print'" [class.fa-file-lines]="b.kind === 'digital'"></i></div>
                }
              </a>
              <div class="p-4 flex flex-col flex-1">
                <h4 class="font-semibold text-main mb-2 line-clamp-2"><a [routerLink]="docLink(b)">{{ b.title }}</a></h4>
                <p class="text-sm text-gray-500 mt-auto">{{ b.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}</p>
              </div>
            </div>
          }
        </div>
      }

      @if (news().length) {
        <div class="flex justify-between items-center mb-8 border-b border-gray-200 pb-2 mt-16">
          <div class="flex items-center border-l-4 border-main pl-4"><h2 class="text-2xl font-bold text-main m-0">{{ 'OPAC.HOME.NEWS_TITLE' | translate }}</h2></div>
          <div class="flex items-center gap-3">
            <a routerLink="/tin-tuc" class="text-sm text-main hover:underline">{{ 'OPAC.PORTAL.ALL_NEWS' | translate }}</a>
            <div class="flex gap-1">
              <button type="button" (click)="newsIndex.set(newsIndex() - perPage)" [disabled]="newsIndex() === 0" [attr.aria-label]="'COMMON.PREV' | translate"
                      class="border border-gray-300 px-3 py-1.5 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-left text-sm"></i></button>
              <button type="button" (click)="newsIndex.set(newsIndex() + perPage)" [disabled]="newsIndex() + perPage >= news().length" [attr.aria-label]="'COMMON.NEXT' | translate"
                      class="border border-gray-300 px-3 py-1.5 rounded text-gray-500 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"><i class="fas fa-chevron-right text-sm"></i></button>
            </div>
          </div>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-6">
          @for (n of visibleNews(); track n.publicId) {
            <div class="bg-white rounded-lg overflow-hidden shadow-sm hover:shadow-md transition-shadow border border-gray-100 flex flex-col">
              <a [routerLink]="['/tin-tuc', n.slug]" class="block h-48 overflow-hidden relative">
                <span class="absolute top-3 left-3 bg-main text-white text-xs font-bold px-2 py-1 rounded z-10">{{ n.publishedAt | ldate }}</span>
                @if (n.thumb || n.images; as img) {
                  <img [src]="img" [alt]="n.title" loading="lazy" class="w-full h-full object-cover transition-transform hover:scale-105" />
                } @else {
                  <div class="w-full h-full bg-gradient-to-br from-gray-100 to-white flex items-center justify-center text-main/40"><i class="fas fa-newspaper text-5xl"></i></div>
                }
              </a>
              <div class="p-4 flex flex-col flex-1">
                <h4 class="font-semibold text-main mb-2 line-clamp-2"><a [routerLink]="['/tin-tuc', n.slug]">{{ n.title }}</a></h4>
                @if (n.brief) { <p class="text-sm text-gray-600 line-clamp-3 mt-auto">{{ n.brief }}</p> }
              </div>
            </div>
          }
        </div>
      }

      @if (extraLinkGroups().length) {
        <div class="mt-12">
          @for (g of extraLinkGroups(); track g.publicId) {
            <h2 class="font-semibold text-gray-900 mb-2">{{ g.name }}</h2>
            <div class="flex flex-wrap gap-3 mb-4">
              @for (l of g.links; track l.publicId) {
                <a [href]="l.url" target="_blank" rel="noopener noreferrer" class="inline-flex items-center gap-2 bg-white border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 hover:border-main hover:text-main">
                  @if (l.image) { <img [src]="l.image" alt="" class="h-6 w-auto" /> }{{ l.name }}</a>
              }
            </div>
          }
        </div>
      }

      @if (portal()?.adGroups?.length) {
        <div class="mt-8 flex flex-wrap gap-4 justify-center" [attr.aria-label]="'OPAC.PORTAL.ADS' | translate">
          @for (g of portal()!.adGroups; track g.publicId) {
            @for (a of g.ads; track a.publicId) {
              @if (a.link) { <a [href]="a.link" target="_blank" rel="noopener noreferrer sponsored" (click)="portalApi.adClick(a.publicId)"><img [src]="a.image" [alt]="a.name" [style.max-width.px]="a.width" class="rounded-lg" /></a> }
              @else { <img [src]="a.image" [alt]="a.name" [style.max-width.px]="a.width" class="rounded-lg" /> }
            }
          }
        </div>
      }

      @if (hasContact()) {
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mt-16">
          @if (p()['OPENHOUR']; as v) {
            <div class="bg-white border border-gray-200 rounded-lg p-6 flex items-center gap-5 border-b-4 border-b-[#3b5b8c] shadow-sm hover:shadow-md transition-shadow">
              <div class="bg-[#eef2f6] w-14 h-14 rounded-full flex items-center justify-center shrink-0"><i class="fas fa-clock text-2xl text-[#3b5b8c]"></i></div>
              <div><h3 class="font-bold text-[#3b5b8c] uppercase mb-1.5">{{ 'OPAC.FOOTER.OPEN_HOURS' | translate }}</h3><div class="text-sm text-gray-600 whitespace-pre-line">{{ v }}</div></div>
            </div>
          }
          @if (p()['LIBRARY_TEL']; as v) {
            <div class="bg-white border border-gray-200 rounded-lg p-6 flex items-center gap-5 border-b-4 border-b-[#3b5b8c] shadow-sm hover:shadow-md transition-shadow">
              <div class="bg-[#eef2f6] w-14 h-14 rounded-full flex items-center justify-center shrink-0"><i class="fas fa-phone text-2xl text-[#3b5b8c]"></i></div>
              <div><h3 class="font-bold text-[#3b5b8c] uppercase mb-1.5">{{ 'OPAC.HOME.HOTLINE' | translate }}</h3><p class="text-lg text-gray-600 m-0">{{ v }}</p></div>
            </div>
          }
          @if (p()['LIBRARY_EMAIL']; as v) {
            <div class="bg-white border border-gray-200 rounded-lg p-6 flex items-center gap-5 border-b-4 border-b-[#3b5b8c] shadow-sm hover:shadow-md transition-shadow">
              <div class="bg-[#eef2f6] w-14 h-14 rounded-full flex items-center justify-center shrink-0"><i class="fas fa-envelope text-2xl text-[#3b5b8c]"></i></div>
              <div class="min-w-0"><h3 class="font-bold text-[#3b5b8c] uppercase mb-1.5">{{ 'OPAC.HOME.EMAIL' | translate }}</h3><p class="text-sm text-gray-600 m-0 break-all">{{ v }}</p></div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class Home implements OnInit, OnDestroy {
  protected readonly library = inject(Library);
  protected readonly portalApi = inject(PortalApi);
  private readonly searchApi = inject(SearchApi);
  private readonly digitalApi = inject(DigitalApi);
  private readonly language = inject(Language);
  private readonly router = inject(Router);
  protected readonly p = this.library.parameters;
  protected readonly perPage = PER_PAGE;

  protected q = '';
  protected field = 'q';
  protected readonly portal = signal<PortalHome | null>(null);
  protected readonly latest = signal<UnifiedHit[]>([]);
  protected readonly collections = signal<Facet[]>([]);
  protected readonly slide = signal(0);
  protected readonly bookIndex = signal(0);
  protected readonly newsIndex = signal(0);
  protected readonly colIndex = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;
  /** Chỉ khi không có Tra cứu: mã bộ sưu tập (trang thư viện số lọc theo mã) theo tên. */
  private readonly collectionIds = new Map<string, number>();

  protected readonly heroBg = computed(() => {
    const banners = this.portal()?.banners ?? [];
    const b = banners[this.slide() % Math.max(1, banners.length)];
    return b ? `linear-gradient(rgba(0,0,0,0.6), rgba(0,0,0,0.6)), url('${b.image}')` : 'linear-gradient(135deg, #3b5b8c, #4879bb)';
  });
  protected readonly news = computed(() => this.portal()?.latest ?? []);
  protected readonly visibleBooks = computed(() => this.latest().slice(this.bookIndex(), this.bookIndex() + PER_PAGE));
  protected readonly visibleNews = computed(() => this.news().slice(this.newsIndex(), this.newsIndex() + PER_PAGE));
  protected readonly visibleCollections = computed(() => this.collections().slice(this.colIndex(), this.colIndex() + PER_PAGE));
  protected readonly hasContact = computed(() => ['OPENHOUR', 'LIBRARY_TEL', 'LIBRARY_EMAIL'].some((k) => this.p()[k]));

  /** Ba lối vào nhanh: liên kết đầu tiên của cổng thông tin (nếu có), ngược lại các dịch vụ đơn vị đang dùng. */
  protected readonly quickLinks = computed(() => {
    const links = (this.portal()?.linkGroups ?? []).flatMap((g) => g.links).slice(0, 3);
    if (links.length) {
      return links.map((l, i) => ({ key: l.publicId, title: l.name, link: l.url, external: true, color: LINK_COLORS[i % LINK_COLORS.length], icon: LINK_ICONS[i % LINK_ICONS.length] }));
    }
    return SERVICES.filter((s) => this.library.has(s.module)).slice(0, 6)
      .map((s) => ({ key: s.module, title: s.title, link: s.link, external: false, color: s.color, icon: s.icon }));
  });
  protected readonly extraLinkGroups = computed(() => {
    const first = (this.portal()?.linkGroups ?? []).flatMap((g) => g.links).slice(0, 3).map((l) => l.publicId);
    return (this.portal()?.linkGroups ?? []).map((g) => ({ ...g, links: g.links.filter((l) => !first.includes(l.publicId)) })).filter((g) => g.links.length);
  });

  ngOnInit(): void {
    if (this.library.has('PORTAL')) {
      this.portalApi.home(this.language.lang()).then((h) => {
        this.portal.set(h);
        if (h.banners.length > 1) this.timer = setInterval(() => this.slide.update((i) => (i + 1) % h.banners.length), 6000);
      }, () => this.portal.set(null));
    }
    void this.loadDocuments();
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  /** Có Tra cứu: một lượt tìm gộp lấy cả tài liệu mới lẫn bộ sưu tập kèm số lượng; chỉ có Thư viện số: lấy từ service tài liệu số. */
  private async loadDocuments(): Promise<void> {
    try {
      if (this.library.has('SEARCH')) {
        const r = await this.searchApi.all({ kind: this.library.has('DIGITAL') ? 'all' : 'print', sort: 'newest', page: 1, pageSize: 8, refine: true });
        this.latest.set(r.items);
        this.collections.set(r.collections);
      } else if (this.library.has('DIGITAL')) {
        const [page, nodes] = await Promise.all([
          this.digitalApi.search({ pageIndex: 1, pageSize: 8 }),
          this.digitalApi.collections().catch(() => []),
        ]);
        this.latest.set(page.items.map((i): UnifiedHit => ({
          kind: 'digital', publicId: i.publicId, title: i.title, author: i.author, publisher: i.publisher, publishYear: i.publishDate, coverUrl: i.coverUrl, language: null,
          materialType: null, copies: 0, available: 0, collectionName: i.collectionName, free: i.free, hasFile: true, linkedBibPublicId: null,
        })));
        const roots = nodes.filter((n) => n.level === 0);
        for (const n of roots) this.collectionIds.set(n.name, n.id);
        this.collections.set(roots.map((n) => ({ key: n.name, count: 0 })));
      }
    } catch {
      /* trang chủ vẫn dùng được khi không tải được danh sách tài liệu */
    }
  }

  protected docLink(h: UnifiedHit): string[] {
    return [h.kind === 'print' ? '/tai-lieu' : '/thu-vien-so', h.publicId];
  }

  protected searchLink(): string {
    return this.library.has('SEARCH') ? '/tim-kiem' : '/thu-vien-so';
  }

  protected digitalQuery(): Record<string, string> | null {
    return this.library.has('SEARCH') ? { kieu: 'so' } : null;
  }

  protected collectionQuery(name: string): Record<string, string> {
    return this.library.has('SEARCH') ? { kieu: 'so', bst: name } : { bst: String(this.collectionIds.get(name) ?? '') };
  }

  protected search(): void {
    const text = this.q.trim();
    if (!this.library.has('SEARCH')) {
      void this.router.navigate(['/thu-vien-so'], { queryParams: { q: text || null } });
      return;
    }
    void this.router.navigate(['/tim-kiem'], { queryParams: { [this.field]: text || null } });
  }
}
