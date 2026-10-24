import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Breadcrumb } from '../layout/breadcrumb';
import { LDatePipe, Language } from '../core/i18n';
import { PortalApi, PortalCategory, PortalNewsPage } from '../core/portal';

const PAGE_SIZE = 9;

/** Danh sách tin của cổng thông tin: lọc theo chuyên mục (cả nhánh con), tìm theo từ khoá không dấu, phân trang. Tham số trên URL để chia sẻ/quay lại. */
@Component({
  selector: 'opac-news-list',
  imports: [Breadcrumb, FormsModule, RouterLink, LDatePipe, TranslatePipe],
  template: `
    <div class="container mx-auto px-4 py-8">
      <opac-breadcrumb [current]="'OPAC.PORTAL.NEWS_TITLE' | translate" />
      <h1 class="text-2xl font-bold text-main border-l-4 border-main pl-4">{{ 'OPAC.PORTAL.NEWS_TITLE' | translate }}</h1>
      <div class="mt-5 grid gap-6 lg:grid-cols-[240px_1fr]">
        <aside>
          <form (ngSubmit)="search()" class="flex bg-white rounded-lg border border-gray-200 overflow-hidden">
            <input name="q" [(ngModel)]="q" class="flex-1 px-3 py-2 outline-none min-w-0 text-sm" [placeholder]="'OPAC.PORTAL.SEARCH_NEWS' | translate" [attr.aria-label]="'OPAC.PORTAL.SEARCH_NEWS' | translate" />
            <button type="submit" class="px-3 text-gray-500 hover:text-main" [attr.aria-label]="'OPAC.HOME.SEARCH_BTN' | translate"><i class="fas fa-magnifying-glass"></i></button>
          </form>
          <nav class="mt-4 bg-white rounded-lg border border-gray-200 py-2 text-sm" [attr.aria-label]="'OPAC.PORTAL.CATEGORIES' | translate">
            <a [routerLink]="[]" [queryParams]="{ chuyenmuc: null, trang: null }" queryParamsHandling="merge" class="block px-4 py-1.5 hover:bg-gray-50" [class.font-semibold]="!categoryId()">{{ 'OPAC.PORTAL.ALL_CATEGORIES' | translate }}</a>
            @for (c of categories(); track c.id) {
              <a [routerLink]="[]" [queryParams]="{ chuyenmuc: c.id, trang: null }" queryParamsHandling="merge" class="block py-1.5 hover:bg-gray-50 pr-3"
                 [style.padding-left.px]="16 + (c.level - 1) * 14" [class.font-semibold]="categoryId() === c.id" [class.text-main]="categoryId() === c.id">{{ c.name }}</a>
            }
          </nav>
        </aside>

        <section>
          @if (loading()) { <div class="flex justify-center py-16"><i class="fas fa-circle-notch animate-spin text-main text-3xl"></i></div> }
          @else if (result(); as r) {
            @if (r.items.length) {
              <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                @for (n of r.items; track n.publicId) {
                  <a [routerLink]="['/tin-tuc', n.slug]" class="group bg-white rounded-xl border border-gray-200 overflow-hidden hover:border-main/40 hover:shadow-md transition flex flex-col">
                    @if (n.thumb || n.images; as img) { <img [src]="img" alt="" loading="lazy" class="h-40 w-full object-cover bg-gray-100" /> }
                    @else { <div class="h-40 bg-gradient-to-br from-gray-100 to-white flex items-center justify-center text-gray-300"><i class="fas fa-newspaper text-5xl"></i></div> }
                    <div class="p-4 flex-1 flex flex-col">
                      <div class="text-xs text-gray-500">{{ n.publishedAt | ldate }} @if (n.categoryName) { · {{ n.categoryName }} }</div>
                      <h2 class="mt-1 font-semibold text-gray-900 group-hover:text-main line-clamp-2">{{ n.title }}</h2>
                      @if (n.brief) { <p class="mt-1 text-sm text-gray-600 line-clamp-3">{{ n.brief }}</p> }
                    </div>
                  </a>
                }
              </div>
              @if (pages() > 1) {
                <div class="mt-6 flex items-center justify-center gap-2 text-sm">
                  <a [routerLink]="[]" [queryParams]="{ trang: page() - 1 }" queryParamsHandling="merge" class="px-3 py-1.5 rounded-lg border border-gray-200 bg-white" [class.pointer-events-none]="page() <= 1" [class.opacity-40]="page() <= 1">‹</a>
                  <span class="text-gray-600">{{ page() }} / {{ pages() }}</span>
                  <a [routerLink]="[]" [queryParams]="{ trang: page() + 1 }" queryParamsHandling="merge" class="px-3 py-1.5 rounded-lg border border-gray-200 bg-white" [class.pointer-events-none]="page() >= pages()" [class.opacity-40]="page() >= pages()">›</a>
                </div>
              }
            } @else {
              <p class="py-16 text-center text-gray-500">{{ 'OPAC.PORTAL.NO_NEWS' | translate }}</p>
            }
          }
          @if (error()) { <p class="text-red-600 text-sm" role="alert">{{ error() }}</p> }
        </section>
      </div>
    </div>
  `,
})
export class NewsList implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly language = inject(Language);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly categories = signal<PortalCategory[]>([]);
  protected readonly result = signal<PortalNewsPage | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly categoryId = signal<number | null>(null);
  protected readonly page = signal(1);
  protected readonly pages = computed(() => Math.max(1, Math.ceil((this.result()?.total ?? 0) / PAGE_SIZE)));
  protected q = '';

  ngOnInit(): void {
    this.api.categories(this.language.lang()).then((c) => this.categories.set(this.flatten(c)), () => this.categories.set([]));
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      this.q = p.get('q') ?? '';
      this.categoryId.set(Number(p.get('chuyenmuc')) || null);
      this.page.set(Math.max(1, Number(p.get('trang')) || 1));
      void this.load();
    });
  }

  protected search(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { q: this.q.trim() || null, trang: null }, queryParamsHandling: 'merge' });
  }

  private flatten(rows: PortalCategory[]): PortalCategory[] {
    const byParent = new Map<number | null, PortalCategory[]>();
    for (const r of rows) (byParent.get(r.parentId) ?? byParent.set(r.parentId, []).get(r.parentId)!).push(r);
    const out: PortalCategory[] = [];
    const walk = (parent: number | null): void => (byParent.get(parent) ?? []).forEach((r) => { out.push(r); walk(r.id); });
    walk(null);
    return out;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.result.set(await this.api.searchNews({ q: this.q.trim() || null, categoryId: this.categoryId(), language: this.language.lang(), page: this.page(), pageSize: PAGE_SIZE }));
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.loading.set(false);
    }
  }
}
