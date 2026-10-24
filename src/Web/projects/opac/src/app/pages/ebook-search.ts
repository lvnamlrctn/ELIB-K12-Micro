import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Breadcrumb } from '../layout/breadcrumb';
import { DigitalApi, DigitalCatalogNode, DigitalContentHit, DigitalItemSummary, DigitalPage } from '../core/digital';
import { Language } from '../core/i18n';

const PAGE_SIZE = 20;

/**
 * Duyệt/tìm tài liệu số (OPAC, ẩn danh) — tự phục vụ bằng dữ liệu của digital, không qua search service (quyết định #1 của plan).
 * Điều kiện nằm trên URL: từ khoá (q), bộ sưu tập/môn loại/chủ đề (bst/ml/cd), trang (trang) — chia sẻ/quay lại được như /tim-kiem.
 */
@Component({
  selector: 'opac-ebook-search',
  imports: [Breadcrumb, FormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="container mx-auto px-4 pt-8">
      <opac-breadcrumb [current]="'OPAC.NAVBAR.EBOOK' | translate" />
    </div>
    <section class="bg-white border-y border-gray-200">
      <div class="container mx-auto px-4 py-6">
        <form (ngSubmit)="submit()" class="flex bg-white rounded-xl border border-gray-300 focus-within:border-main overflow-hidden">
          <i class="fas fa-magnifying-glass text-gray-400 self-center pl-3"></i>
          <input name="q" [(ngModel)]="q" autocomplete="off" class="flex-1 px-3 py-3 text-gray-800 outline-none min-w-0"
                 [placeholder]="'OPAC.DIGITAL.SEARCH_PH' | translate" />
          <button type="submit" class="px-5 bg-main hover:bg-blue-700 text-white font-medium">{{ 'OPAC.SEARCH.SEARCH_BTN' | translate }}</button>
        </form>
        <label class="mt-3 mr-4 inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input type="checkbox" class="rounded border-gray-300" [ngModel]="inContent" (ngModelChange)="setContent($event)" name="inContent" />
          {{ 'OPAC.DIGITAL.IN_CONTENT' | translate }}
        </label>
        <a routerLink="/nop-tai-lieu" class="mt-3 inline-flex items-center gap-1.5 text-sm text-main hover:underline">
          <i class="fas fa-file-arrow-up text-[18px]"></i>{{ 'OPAC.SUBMIT_DOC.NAV_LINK' | translate }}</a>
      </div>
    </section>

    <section class="container mx-auto px-4 py-6 grid gap-6 md:grid-cols-[250px_1fr]">
      <aside class="space-y-4">
        <div class="bg-white border border-gray-200 rounded-lg p-3">
          <div class="font-medium text-sm text-gray-800 mb-2">{{ 'OPAC.DIGITAL.COLLECTION' | translate }}</div>
          <select class="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm" [ngModel]="collectionId" (ngModelChange)="setFilter('bst', $event)">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (c of collections(); track c.id) { <option [ngValue]="c.id">{{ indent(c.level) }}{{ c.name }}</option> }
          </select>
        </div>
        <div class="bg-white border border-gray-200 rounded-lg p-3">
          <div class="font-medium text-sm text-gray-800 mb-2">{{ 'OPAC.DIGITAL.SUBJECT' | translate }}</div>
          <select class="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm" [ngModel]="subjectId" (ngModelChange)="setFilter('ml', $event)">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of subjects(); track s.id) { <option [ngValue]="s.id">{{ indent(s.level) }}{{ s.name }}</option> }
          </select>
        </div>
        <div class="bg-white border border-gray-200 rounded-lg p-3">
          <div class="font-medium text-sm text-gray-800 mb-2">{{ 'OPAC.DIGITAL.TOPIC' | translate }}</div>
          <select class="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm" [ngModel]="topicId" (ngModelChange)="setFilter('cd', $event)">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (t of topics(); track t.id) { <option [ngValue]="t.id">{{ indent(t.level) }}{{ t.name }}</option> }
          </select>
        </div>
      </aside>

      <div>
        <div class="text-sm text-gray-600 mb-4">
          @if (loading()) { {{ 'OPAC.SEARCH.SEARCHING' | translate }} }
          @else if (result(); as r) { {{ 'OPAC.SEARCH.FOUND_N' | translate: { count: r.totalCount } }} }
        </div>

        @if (error()) { <div class="bg-red-50 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{{ error() }}</div> }

        @if (inContent) {
          <ul class="space-y-3">
            @for (h of hits() ?? []; track h.publicId) {
              <li class="bg-white border border-gray-100 shadow-sm rounded-xl p-4 hover:border-main/40 transition">
                <a [routerLink]="['/thu-vien-so', h.publicId]" class="font-semibold text-gray-900 hover:text-main">{{ h.title }}</a>
                <div class="text-sm text-gray-600 mt-0.5">{{ h.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }} · {{ 'OPAC.DIGITAL.IN_CONTENT_PAGE' | translate: { page: h.page } }}</div>
                @if (h.snippet) { <p class="mt-2 text-sm text-gray-700 bg-gray-50 border-l-4 border-main/40 px-3 py-2 rounded-r">{{ h.snippet }}</p> }
                @else { <p class="mt-2 text-xs text-gray-500">{{ 'OPAC.DIGITAL.IN_CONTENT_LOCKED' | translate }}</p> }
              </li>
            } @empty {
              @if (!loading() && hits()) {
                <li class="bg-white border border-gray-100 shadow-sm rounded-xl p-10 text-center text-gray-500">
                  <i class="fas fa-magnifying-glass text-5xl text-gray-300"></i>
                  <p class="mt-2">{{ (q.trim().length < 2 ? 'OPAC.DIGITAL.IN_CONTENT_MIN' : 'OPAC.SEARCH.NO_RESULT_HINT') | translate }}</p>
                </li>
              }
            }
          </ul>
        } @else {
        <ul class="space-y-3">
          @for (i of result()?.items ?? []; track i.publicId) {
            <li class="bg-white border border-gray-100 shadow-sm rounded-xl p-4 flex gap-4 hover:border-main/40 transition">
              @if (i.coverUrl && !brokenCovers().has(i.publicId)) {
                <img [src]="i.coverUrl" alt="" loading="lazy" referrerpolicy="no-referrer" (error)="coverFailed(i.publicId)"
                     class="w-14 h-20 rounded object-cover bg-gray-100 shrink-0 border border-gray-200" />
              } @else {
                <div class="w-14 h-20 rounded bg-gradient-to-br from-gray-100 to-white text-gray-300 flex items-center justify-center shrink-0">
                  <i class="fas fa-book"></i>
                </div>
              }
              <div class="min-w-0 flex-1">
                <a [routerLink]="['/thu-vien-so', i.publicId]" class="font-semibold text-gray-900 hover:text-main">{{ i.title }}</a>
                <div class="text-sm text-gray-600 mt-0.5">
                  {{ i.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}@if (i.publisher) { · {{ i.publisher }} }@if (i.publishDate) { · {{ i.publishDate }} }
                </div>
                <div class="mt-2 flex items-center gap-2 flex-wrap text-xs">
                  @if (i.collectionName) { <span class="px-2 py-0.5 rounded bg-gray-100 text-gray-600">{{ i.collectionName }}</span> }
                  @if (i.free) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">{{ 'OPAC.DIGITAL.FREE' | translate }}</span> }
                  @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">{{ 'OPAC.DIGITAL.NEEDS_LOGIN' | translate }}</span> }
                </div>
              </div>
            </li>
          } @empty {
            @if (!loading() && result()) {
              <li class="bg-white border border-gray-100 shadow-sm rounded-xl p-10 text-center text-gray-500">
                <i class="fas fa-magnifying-glass text-5xl text-gray-300"></i>
                <p class="mt-2">{{ 'OPAC.SEARCH.NO_RESULT_HINT' | translate }}</p>
              </li>
            }
          }
        </ul>

        }

        @if (!inContent && pages() > 1) {
          <nav class="mt-6 flex items-center justify-center gap-1 text-sm">
            <button type="button" class="px-3 py-1.5 rounded-lg border border-gray-200 bg-white disabled:opacity-40" [disabled]="page <= 1" (click)="go(page - 1)">{{ 'COMMON.PREV' | translate }}</button>
            <span class="px-3 text-gray-600">{{ 'OPAC.COMMON.PAGE_OF' | translate: { page, pages: pages() } }}</span>
            <button type="button" class="px-3 py-1.5 rounded-lg border border-gray-200 bg-white disabled:opacity-40" [disabled]="page >= pages()" (click)="go(page + 1)">{{ 'COMMON.NEXT' | translate }}</button>
          </nav>
        }
      </div>
    </section>
  `,
})
export class EbookSearch implements OnInit {
  private readonly api = inject(DigitalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly language = inject(Language);

  protected readonly result = signal<DigitalPage<DigitalItemSummary> | null>(null);
  protected readonly hits = signal<DigitalContentHit[] | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly brokenCovers = signal(new Set<string>());
  protected readonly collections = signal<DigitalCatalogNode[]>([]);
  protected readonly subjects = signal<DigitalCatalogNode[]>([]);
  protected readonly topics = signal<DigitalCatalogNode[]>([]);

  protected q = '';
  /** Tìm trong nội dung (các tài liệu đã đồng bộ nội dung) thay vì nhan đề/tác giả — tham số nd=1 trên URL. */
  protected inContent = false;
  protected collectionId: number | null = null;
  protected subjectId: number | null = null;
  protected topicId: number | null = null;
  protected page = 1;
  private params: ParamMap | null = null;

  ngOnInit(): void {
    this.api.collections().then((c) => this.collections.set(c), () => this.collections.set([]));
    this.api.subjects().then((s) => this.subjects.set(s), () => this.subjects.set([]));
    this.api.topics().then((t) => this.topics.set(t), () => this.topics.set([]));

    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      this.params = p;
      this.q = p.get('q') ?? '';
      this.inContent = p.get('nd') === '1';
      this.collectionId = num(p.get('bst'));
      this.subjectId = num(p.get('ml'));
      this.topicId = num(p.get('cd'));
      this.page = Math.max(1, num(p.get('trang')) ?? 1);
      void this.load();
    });
  }

  protected indent(level: number): string {
    return '　　'.repeat(Math.max(0, level));
  }

  protected pages(): number {
    const r = this.result();
    return r ? Math.ceil(r.totalCount / r.pageSize) : 0;
  }

  protected submit(): void {
    this.navigate({ q: this.q.trim() || null, trang: null });
  }

  protected setContent(on: boolean): void {
    this.navigate({ nd: on ? '1' : null, trang: null });
  }

  protected setFilter(param: string, value: number | null): void {
    this.navigate({ [param]: value != null ? String(value) : null, trang: null });
  }

  protected go(page: number): void {
    this.navigate({ trang: page > 1 ? String(page) : null });
    window.scrollTo({ top: 0 });
  }

  protected coverFailed(publicId: string): void {
    this.brokenCovers.update((s) => new Set(s).add(publicId));
  }

  private navigate(params: Record<string, string | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: params, queryParamsHandling: 'merge' });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      if (this.inContent) {
        this.hits.set(this.q.trim().length >= 2 ? await this.api.contentSearch(this.q.trim()) : []);
        return;
      }
      const result = await this.api.search({
        keyword: this.q.trim() || null, collectionId: this.collectionId, subjectId: this.subjectId, topicId: this.topicId,
        pageIndex: this.page, pageSize: PAGE_SIZE,
      });
      this.result.set(result);
    } catch (e) {
      this.error.set(this.language.apiError(e));
    } finally {
      this.loading.set(false);
    }
  }
}

function num(value: string | null): number | null {
  const n = value ? Number(value) : NaN;
  return Number.isFinite(n) ? n : null;
}
