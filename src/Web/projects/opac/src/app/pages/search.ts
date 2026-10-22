import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { SavedSearch, SavedSearches, SearchParams } from '../core/saved-searches';
import { Bib, EbookHits, Facet, Msg, SearchApi, SearchRequest, SearchResult, languageName, searchErrorMessage } from '../core/search';

const PAGE_SIZE = 10;
type FacetKey = 'materialTypes' | 'authors' | 'years' | 'languages' | 'stores';
/** title/label: khoá dịch. */
const FACETS: { key: FacetKey; title: string; param: string }[] = [
  { key: 'materialTypes', title: 'OPAC.SEARCH.F_DOCTYPE', param: 'loai' },
  { key: 'authors', title: 'OPAC.SEARCH.F_AUTHOR', param: 'tacgia' },
  { key: 'years', title: 'OPAC.SEARCH.F_YEAR', param: 'nam' },
  { key: 'languages', title: 'OPAC.SEARCH.F_LANGUAGE', param: 'ngonngu' },
  { key: 'stores', title: 'OPAC.SEARCH.F_STORE', param: 'kho' },
];
const ADVANCED: { key: keyof SearchRequest; label: string; param: string }[] = [
  { key: 'title', label: 'OPAC.SEARCH.F_TITLE', param: 'nhande' },
  { key: 'author', label: 'OPAC.SEARCH.F_AUTHOR', param: 'tg' },
  { key: 'publisher', label: 'OPAC.SEARCH.F_PUBLISHER', param: 'nxb' },
  { key: 'keyword', label: 'OPAC.SEARCH.F_KEYWORD', param: 'tukhoa' },
  { key: 'isbn', label: 'OPAC.SEARCH.F_ISBN', param: 'isbn' },
  { key: 'ddc', label: 'OPAC.SEARCH.F_DDC', param: 'ddc' },
  { key: 'barcode', label: 'OPAC.SEARCH.F_BARCODE', param: 'dkcb' },
];

/**
 * Tra cứu tài liệu (monolith: OPAC /search — phần sách in). Toàn bộ điều kiện nằm trên URL (chia sẻ, quay lại được): q, các ô nâng cao,
 * facet chọn nhiều, chỉ còn bản sẵn sàng, sắp xếp, trang.
 */
@Component({
  selector: 'opac-search',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <section class="bg-white border-b border-slate-200">
      <div class="max-w-6xl mx-auto px-4 py-6">
        <div class="relative">
        <form (ngSubmit)="submit()" class="flex bg-white rounded-xl border border-slate-300 focus-within:border-blue-500 overflow-hidden">
          <span class="material-icons text-slate-400 self-center pl-3">search</span>
          <input name="q" [(ngModel)]="q" (ngModelChange)="typed($event)" (blur)="hideSuggest()" autocomplete="off"
                 class="flex-1 px-3 py-3 text-slate-800 outline-none min-w-0" [placeholder]="'OPAC.SEARCH.PH_KEYWORD' | translate" [attr.aria-label]="'OPAC.SEARCH.KEYWORD_ARIA' | translate" />
          <button type="submit" class="px-5 bg-blue-600 hover:bg-blue-700 text-white font-medium">{{ 'OPAC.SEARCH.SEARCH_BTN' | translate }}</button>
        </form>
        @if (suggestions().length) {
          <ul class="absolute z-20 left-0 right-0 top-full mt-1 bg-white border border-slate-200 rounded-lg shadow-lg">
            @for (s of suggestions(); track s) {
              <li><button type="button" class="w-full text-left px-4 py-2 hover:bg-slate-50 text-sm" (mousedown)="pick(s)">{{ s }}</button></li>
            }
          </ul>
        }
        </div>
        <div class="mt-3 flex items-center gap-4 text-sm">
          <button type="button" class="text-blue-600 hover:underline inline-flex items-center gap-1" (click)="showAdvanced.set(!showAdvanced())">
            <span class="material-icons text-[18px]">tune</span> {{ 'OPAC.SEARCH.ADV_FILTER' | translate }}</button>
          @if (hasCriteria()) {
            <a routerLink="/tim-kiem" class="text-slate-500 hover:underline">{{ 'OPAC.SEARCH.CLEAR_FILTERS' | translate }}</a>
            @if (current(); as s) {
              <button type="button" class="text-green-700 hover:underline inline-flex items-center gap-1" (click)="saved.remove(s.id); current.set(undefined)" [title]="'OPAC.SEARCH.UNSAVE' | translate">
                <span class="material-icons text-[18px]">bookmark</span> {{ 'OPAC.SEARCH.SAVED' | translate }}</button>
            } @else {
              <button type="button" class="text-blue-600 hover:underline inline-flex items-center gap-1" (click)="saveCurrent()">
                <span class="material-icons text-[18px]">bookmark_border</span> {{ 'OPAC.SEARCH.SAVE_SEARCH' | translate }}</button>
            }
          }
          @if (hasZ3950()) {
            <a routerLink="/lien-thu-vien" [queryParams]="{ q: q || null }" class="text-blue-600 hover:underline inline-flex items-center gap-1">
              <span class="material-icons text-[18px]">hub</span> {{ 'OPAC.Z3950.TITLE' | translate }}</a>
          }
          @if (saved.items().length) {
            <button type="button" class="ml-auto text-slate-600 hover:underline inline-flex items-center gap-1" (click)="toggleSaved()"
                    [attr.aria-expanded]="showSaved()">
              <span class="material-icons text-[18px]">bookmarks</span> {{ 'OPAC.SEARCH.SAVED_COUNT' | translate: { count: saved.items().length } }}</button>
          }
        </div>
        @if (showSaved()) {
          <div class="mt-3 bg-slate-50 border border-slate-200 rounded-lg p-3">
            <div class="text-xs text-slate-500 mb-2">{{ 'OPAC.SEARCH.SAVED_HINT' | translate }}</div>
            <ul class="divide-y divide-slate-200">
              @for (s of saved.items(); track s.id) {
                <li class="flex items-center gap-2 py-1.5 text-sm">
                  <a [routerLink]="['/tim-kiem']" [queryParams]="s.params" class="flex-1 min-w-0 truncate text-blue-700 hover:underline">{{ s.name }}</a>
                  @if ((fresh()[s.id] || 0) > 0) {
                    <span class="px-2 py-0.5 rounded-full bg-green-100 text-green-700 text-xs whitespace-nowrap">{{ 'OPAC.SEARCH.NEW_RESULTS' | translate: { count: fresh()[s.id] } }}</span>
                  }
                  <button type="button" class="text-slate-400 hover:text-red-600" (click)="removeSaved(s.id)" [attr.aria-label]="('COMMON.DELETE' | translate) + ' ' + s.name" [title]="'COMMON.DELETE' | translate">
                    <span class="material-icons text-[18px]">close</span></button>
                </li>
              }
            </ul>
          </div>
        }
        @if (showAdvanced()) {
          <form (ngSubmit)="submit()" class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            @for (f of advanced; track f.key) {
              <div>
                <label class="block text-xs text-slate-500 mb-1" [for]="'adv-' + f.param">{{ f.label | translate }}</label>
                <input [id]="'adv-' + f.param" [name]="f.param" [(ngModel)]="adv[f.param]" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
            }
            <div class="flex gap-2">
              <div class="flex-1">
                <label class="block text-xs text-slate-500 mb-1" for="adv-from">{{ 'OPAC.SEARCH.PH_YEAR_FROM' | translate }}</label>
                <input id="adv-from" name="from" type="number" [(ngModel)]="yearFrom" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
              <div class="flex-1">
                <label class="block text-xs text-slate-500 mb-1" for="adv-to">{{ 'OPAC.SEARCH.PH_YEAR_TO' | translate }}</label>
                <input id="adv-to" name="to" type="number" [(ngModel)]="yearTo" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
            </div>
            <div class="sm:col-span-2 lg:col-span-4 flex justify-end">
              <button type="submit" class="px-5 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium">{{ 'OPAC.SEARCH.SEARCH_ADVANCED_BTN' | translate }}</button>
            </div>
          </form>
        }
      </div>
    </section>

    <section class="max-w-6xl mx-auto px-4 py-6 grid gap-6 md:grid-cols-[250px_1fr]">
      <aside class="space-y-5">
        <button type="button" class="md:hidden w-full flex items-center justify-between px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm"
                (click)="showFacets.set(!showFacets())">{{ 'COMMON.SEARCH_FILTERS' | translate }} <span class="material-icons">{{ showFacets() ? 'expand_less' : 'expand_more' }}</span></button>
        <div [class.hidden]="!showFacets()" class="md:block space-y-5">
          @if (result(); as r) {
            <label class="flex items-center gap-2 text-sm bg-white border border-slate-200 rounded-lg px-3 py-2">
              <input type="checkbox" class="rounded border-slate-300" [checked]="availableOnly" (change)="toggleAvailable()" />
              {{ 'OPAC.SEARCH.AVAILABLE_ONLY' | translate }} <span class="ml-auto text-slate-400">{{ r.facets.availableCount }}</span>
            </label>
            @for (f of facets; track f.key) {
              @if (r.facets[f.key].length) {
                <div class="bg-white border border-slate-200 rounded-lg p-3">
                  <div class="font-medium text-sm text-slate-800 mb-2">{{ f.title | translate }}</div>
                  <ul class="space-y-1">
                    @for (v of r.facets[f.key]; track v.key) {
                      <li>
                        <label class="flex items-start gap-2 text-sm text-slate-700 cursor-pointer">
                          <input type="checkbox" class="rounded border-slate-300 mt-0.5" [checked]="isOn(f.param, v.key)" (change)="toggle(f.param, v.key)" />
                          <span class="flex-1">{{ label(f.key, v) | translate }}</span><span class="text-slate-400">{{ v.count }}</span>
                        </label>
                      </li>
                    }
                  </ul>
                </div>
              }
            }
          }
        </div>
      </aside>

      <div>
        <div class="flex items-center gap-3 mb-4 flex-wrap">
          <div class="text-sm text-slate-600">
            @if (loading()) { {{ 'OPAC.SEARCH.SEARCHING' | translate }} }
            @else if (result(); as r) {
              {{ 'OPAC.SEARCH.FOUND_N' | translate: { count: r.total } }}
              @if (r.elapsedMs != null) { <span class="text-slate-400">{{ 'OPAC.SEARCH.ELAPSED' | translate: { ms: r.elapsedMs } }}</span> }
            }
          </div>
          <label class="ml-auto text-sm text-slate-600 flex items-center gap-2">{{ 'OPAC.SEARCH.SORT' | translate }}
            <select class="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" [ngModel]="sort" (ngModelChange)="setSort($event)" name="sort">
              <option value="">{{ 'OPAC.SEARCH.SORT_RELEVANCE' | translate }}</option>
              <option value="newest">{{ 'OPAC.SEARCH.SORT_NEWEST' | translate }}</option>
              <option value="oldest">{{ 'OPAC.SEARCH.SORT_OLDEST' | translate }}</option>
              <option value="title">{{ 'OPAC.SEARCH.SORT_TITLE' | translate }}</option>
            </select>
          </label>
        </div>

        @if (error(); as e) {
          <div class="bg-red-50 text-red-700 rounded-lg px-4 py-3 text-sm">{{ e.key | translate: e.params }}</div>
        }

        @if (ebooks(); as eb) {
          @if (eb.items.length) {
            <section class="mb-4 bg-white border border-slate-200 rounded-xl p-4" [attr.aria-label]="'OPAC.SEARCH.EBOOKS' | translate">
              <div class="flex items-center justify-between gap-3 mb-2">
                <h2 class="font-semibold text-slate-900 flex items-center gap-1.5"><span class="material-icons text-blue-600 text-[20px]">auto_stories</span>
                  {{ 'OPAC.SEARCH.EBOOKS_N' | translate: { count: eb.total } }}</h2>
                <a routerLink="/thu-vien-so" [queryParams]="{ q }" class="text-sm text-blue-600 hover:underline">{{ 'OPAC.SEARCH.EBOOKS_ALL' | translate }}</a>
              </div>
              <ul class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                @for (e of eb.items; track e.publicId) {
                  <li><a [routerLink]="['/thu-vien-so', e.publicId]" class="block rounded-lg border border-slate-100 hover:border-blue-300 px-3 py-2">
                    <div class="font-medium text-slate-900 text-sm line-clamp-1">{{ e.title }}</div>
                    <div class="text-xs text-slate-500 line-clamp-1">{{ e.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}@if (e.publishYear) { · {{ e.publishYear }} }@if (e.free) { · {{ 'OPAC.SEARCH.EBOOK_FREE' | translate }} }</div>
                  </a></li>
                }
              </ul>
            </section>
          }
        }

        <ul class="space-y-3">
          @for (b of result()?.items ?? []; track b.publicId; let i = $index) {
            <li class="bg-white border border-slate-200 rounded-xl p-4 flex gap-4 hover:border-blue-300 transition">
              @if (b.coverUrl && !brokenCovers().has(b.publicId)) {
                <img [src]="b.coverUrl" alt="" loading="lazy" referrerpolicy="no-referrer" (error)="coverFailed(b.publicId)"
                     class="w-14 h-20 rounded object-cover bg-slate-100 shrink-0 border border-slate-200" />
              } @else {
                <div class="w-14 h-20 rounded bg-gradient-to-br from-blue-100 to-sky-50 text-blue-500 flex items-center justify-center shrink-0">
                  <span class="material-icons">menu_book</span>
                </div>
              }
              <div class="min-w-0 flex-1">
                <a [routerLink]="['/tai-lieu', b.publicId]" (click)="opened(b, i)" class="font-semibold text-slate-900 hover:text-blue-700">{{ b.title }}</a>
                <div class="text-sm text-slate-600 mt-0.5">
                  {{ b.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}@if (b.publisher) { · {{ b.publisher }} }@if (b.publishYear) { · {{ b.publishYear }} }
                </div>
                @if (b.summary) { <p class="text-sm text-slate-500 mt-1 line-clamp-2">{{ b.summary }}</p> }
                <div class="mt-2 flex items-center gap-2 flex-wrap text-xs">
                  @if (b.materialType) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">{{ b.materialType }}</span> }
                  @if (b.ddc) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">DDC {{ b.ddc }}</span> }
                  @if (b.copies === 0) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-500">{{ 'OPAC.COMMON.NO_COPIES' | translate }}</span> }
                  @else if (b.available > 0) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">{{ 'OPAC.COMMON.AVAILABLE_OF' | translate: { available: b.available, copies: b.copies } }}</span> }
                  @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">{{ 'OPAC.COMMON.NONE_READY' | translate: { copies: b.copies } }}</span> }
                </div>
              </div>
            </li>
          } @empty {
            @if (!loading() && result()) {
              <li class="bg-white border border-slate-200 rounded-xl p-10 text-center text-slate-500">
                <span class="material-icons text-5xl text-slate-300">search_off</span>
                <p class="mt-2">{{ 'OPAC.SEARCH.NO_RESULT_HINT' | translate }}</p>
                <p class="mt-1 text-xs">{{ 'OPAC.SEARCH.PHRASE_HINT' | translate }}</p>
                @if (hasZ3950() && q) {
                  <a routerLink="/lien-thu-vien" [queryParams]="{ q }" class="mt-3 inline-flex items-center gap-1 text-blue-600 hover:underline">
                    <span class="material-icons text-[18px]">hub</span> {{ 'OPAC.SEARCH.TRY_OTHER_LIBRARIES' | translate: { q } }}</a>
                }
              </li>
            }
          }
        </ul>

        @if (pages() > 1) {
          <nav class="mt-6 flex items-center justify-center gap-1 text-sm" [attr.aria-label]="'OPAC.COMMON.PAGINATION' | translate">
            <button type="button" class="px-3 py-1.5 rounded-lg border border-slate-200 bg-white disabled:opacity-40" [disabled]="page <= 1" (click)="go(page - 1)">{{ 'COMMON.PREV' | translate }}</button>
            <span class="px-3 text-slate-600">{{ 'OPAC.COMMON.PAGE_OF' | translate: { page, pages: pages() } }}</span>
            <button type="button" class="px-3 py-1.5 rounded-lg border border-slate-200 bg-white disabled:opacity-40" [disabled]="page >= pages()" (click)="go(page + 1)">{{ 'COMMON.NEXT' | translate }}</button>
          </nav>
        }
      </div>
    </section>
  `,
})
export class Search implements OnInit {
  private readonly api = inject(SearchApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly saved = inject(SavedSearches);
  private readonly language = inject(Language);

  protected readonly facets = FACETS;
  protected readonly advanced = ADVANCED;
  protected readonly result = signal<SearchResult | null>(null);
  protected readonly ebooks = signal<EbookHits | null>(null);
  private readonly library = inject(Library);
  protected readonly loading = signal(false);
  protected readonly error = signal<Msg | null>(null);
  protected readonly suggestions = signal<string[]>([]);
  protected readonly showAdvanced = signal(false);
  protected readonly showFacets = signal(false);
  protected readonly showSaved = signal(false);
  /** Số kết quả mới của từng tìm kiếm đã lưu (tính khi mở danh sách). */
  protected readonly fresh = signal<Record<string, number>>({});
  protected readonly current = signal<SavedSearch | undefined>(undefined);
  protected readonly brokenCovers = signal(new Set<string>());
  /** Thư viện đã bật máy chủ Z39.50 cho OPAC. */
  protected readonly hasZ3950 = signal(false);
  private queryId: string | null = null;
  protected q = '';
  protected adv: Record<string, string> = {};
  protected yearFrom: number | null = null;
  protected yearTo: number | null = null;
  protected sort = '';
  protected page = 1;
  protected availableOnly = false;
  private params: ParamMap | null = null;
  private suggestTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.api.z3950Servers().then((s) => this.hasZ3950.set(s.length > 0), () => this.hasZ3950.set(false));
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      this.params = p;
      this.current.set(this.saved.find(paramsOf(p)));
      this.q = p.get('q') ?? '';
      this.adv = Object.fromEntries(ADVANCED.map((f) => [f.param, p.get(f.param) ?? '']));
      this.yearFrom = num(p.get('tunam'));
      this.yearTo = num(p.get('dennam'));
      this.sort = p.get('sapxep') ?? '';
      this.page = Math.max(1, num(p.get('trang')) ?? 1);
      this.availableOnly = p.get('consach') === '1';
      if (ADVANCED.some((f) => this.adv[f.param]) || this.yearFrom || this.yearTo) this.showAdvanced.set(true);
      void this.load();
    });
  }

  protected hasCriteria(): boolean {
    return (this.params?.keys.length ?? 0) > 0;
  }

  protected pages(): number {
    const r = this.result();
    return r ? Math.ceil(r.total / r.pageSize) : 0;
  }

  protected label(key: FacetKey, f: Facet): string {
    return key === 'languages' ? languageName(f.key) : f.key;
  }

  protected isOn(param: string, value: string): boolean {
    return this.params?.getAll(param).includes(value) ?? false;
  }

  protected submit(): void {
    this.suggestions.set([]);
    const query: Record<string, string | null> = { q: this.q.trim() || null, tunam: this.yearFrom ? String(this.yearFrom) : null, dennam: this.yearTo ? String(this.yearTo) : null, trang: null };
    for (const f of ADVANCED) query[f.param] = this.adv[f.param]?.trim() || null;
    this.navigate(query);
  }

  protected toggle(param: string, value: string): void {
    const current = this.params?.getAll(param) ?? [];
    const next = current.includes(value) ? current.filter((v) => v !== value) : [...current, value];
    this.navigate({ [param]: next.length ? next : null, trang: null });
  }

  protected toggleAvailable(): void {
    this.navigate({ consach: this.availableOnly ? null : '1', trang: null });
  }

  protected setSort(value: string): void {
    this.navigate({ sapxep: value || null, trang: null });
  }

  protected go(page: number): void {
    this.navigate({ trang: page > 1 ? String(page) : null });
    window.scrollTo({ top: 0 });
  }

  protected typed(value: string): void {
    clearTimeout(this.suggestTimer);
    const text = value.trim();
    if (text.length < 2) {
      this.suggestions.set([]);
      return;
    }
    this.suggestTimer = setTimeout(() => this.api.suggest(text).then((s) => this.suggestions.set(s), () => this.suggestions.set([])), 250);
  }

  protected pick(title: string): void {
    this.q = title;
    this.submit();
  }

  /** Bạn đọc mở kết quả — ghi vị trí trên toàn danh sách cho thống kê chất lượng tìm kiếm. */
  protected opened(b: Bib, index: number): void {
    const r = this.result();
    if (this.queryId && r) this.api.click(this.queryId, b.publicId, (r.page - 1) * r.pageSize + index + 1);
  }

  protected coverFailed(publicId: string): void {
    this.brokenCovers.update((s) => new Set(s).add(publicId));
  }

  protected saveCurrent(): void {
    if (!this.params) return;
    const params = paramsOf(this.params);
    this.current.set(this.saved.save(describe(params, (k, p) => this.language.t(k, p)), params, this.result()?.total ?? 0));
  }

  protected removeSaved(id: string): void {
    this.saved.remove(id);
    if (this.current()?.id === id) this.current.set(undefined);
    if (!this.saved.items().length) this.showSaved.set(false);
  }

  protected toggleSaved(): void {
    const open = !this.showSaved();
    this.showSaved.set(open);
    if (open) void this.countFresh();
  }

  /** Chạy lại từng tìm kiếm đã lưu (1 kết quả/trang, không tính thống kê) để biết có thêm bao nhiêu kết quả so với lần xem trước. */
  private async countFresh(): Promise<void> {
    const counts: Record<string, number> = {};
    for (const s of this.saved.items()) {
      try {
        const r = await this.api.search({ ...buildRequest(convertToParamMap(s.params)), page: 1, pageSize: 1, refine: true });
        counts[s.id] = Math.max(0, r.total - s.total);
      } catch {
        break; // vượt giới hạn tra cứu / mất mạng — bỏ qua phần còn lại
      }
    }
    this.fresh.set(counts);
  }

  protected hideSuggest(): void {
    setTimeout(() => this.suggestions.set([]), 150);
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: params, queryParamsHandling: 'merge' });
  }

  /** Tài liệu số khớp cùng từ khoá (chỉ trang đầu, đơn vị có phân hệ Tài liệu số) — lỗi không làm hỏng kết quả sách in. */
  private async loadEbooks(request: SearchRequest): Promise<void> {
    const q = request.q?.trim();
    if (!q || (request.page ?? 1) > 1 || !this.library.has('DIGITAL')) {
      this.ebooks.set(null);
      return;
    }
    try {
      this.ebooks.set(await this.api.ebooks(q));
    } catch {
      this.ebooks.set(null);
    }
  }

  private async load(): Promise<void> {
    const request = buildRequest(this.params!);
    // Cùng câu tìm với lượt trước trong tab này (đổi bộ lọc, sắp xếp, lật trang, quay lại từ trang chi tiết) → không tính lượt tìm mới.
    const criteria = criteriaKey(request);
    const last = readLast();
    const refine = last?.criteria === criteria;
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.search({ ...request, refine });
      this.result.set(result);
      void this.loadEbooks(request);
      this.queryId = refine ? last!.queryId : result.queryId;
      if (!refine) writeLast({ criteria, queryId: result.queryId });
      const saved = this.current();
      if (saved && result.total !== saved.total) this.saved.seen(saved.id, result.total);
    } catch (e) {
      this.error.set(searchErrorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

/** Điều kiện tìm trên URL → yêu cầu tra cứu (trang hiện tại, và chạy lại tìm kiếm đã lưu). */
function buildRequest(p: ParamMap): SearchRequest {
  const request: SearchRequest = {
    q: p.get('q') || null,
    yearFrom: num(p.get('tunam')),
    yearTo: num(p.get('dennam')),
    materialTypes: p.getAll('loai'),
    authors: p.getAll('tacgia'),
    years: p.getAll('nam').map(Number).filter((y) => !Number.isNaN(y)),
    languages: p.getAll('ngonngu'),
    stores: p.getAll('kho'),
    availableOnly: p.get('consach') === '1',
    sort: p.get('sapxep') || null,
    page: Math.max(1, num(p.get('trang')) ?? 1),
    pageSize: PAGE_SIZE,
  };
  for (const f of ADVANCED) (request as unknown as Record<string, unknown>)[f.key] = p.get(f.param) || null;
  return request;
}

/** Phần "câu tìm" (từ khoá + ô nâng cao + khoảng năm) — khác nhau mới là lượt tìm mới. */
function criteriaKey(r: SearchRequest): string {
  return JSON.stringify([r.q, ...ADVANCED.map((f) => r[f.key] ?? null), r.yearFrom, r.yearTo]);
}

const LAST_KEY = 'opac.lastQuery';

function readLast(): { criteria: string; queryId: string | null } | null {
  try {
    return JSON.parse(sessionStorage.getItem(LAST_KEY) ?? 'null');
  } catch {
    return null;
  }
}

function writeLast(value: { criteria: string; queryId: string | null }): void {
  try {
    sessionStorage.setItem(LAST_KEY, JSON.stringify(value));
  } catch {
    // không lưu được thì lượt quay lại tính như lượt tìm mới
  }
}

/** Tham số URL của tìm kiếm hiện tại (bỏ số trang) — để lưu và so khớp tìm kiếm đã lưu. */
function paramsOf(p: ParamMap): SearchParams {
  const params: SearchParams = {};
  for (const k of p.keys) {
    if (k === 'trang') continue;
    const values = p.getAll(k);
    params[k] = values.length === 1 ? values[0] : values;
  }
  return params;
}

/** Tên tự đặt cho tìm kiếm đã lưu (theo ngôn ngữ lúc lưu): từ khoá, các ô nâng cao, bộ lọc. */
function describe(params: SearchParams, t: (key: string, params?: Record<string, unknown>) => string): string {
  const all = (k: string): string[] => (params[k] === undefined ? [] : Array.isArray(params[k]) ? (params[k] as string[]) : [params[k] as string]);
  const parts: string[] = [];
  if (all('q').length) parts.push(`"${all('q')[0]}"`);
  for (const f of ADVANCED) if (all(f.param).length) parts.push(`${t(f.label)}: ${all(f.param)[0]}`);
  for (const f of FACETS) {
    const values = all(f.param);
    if (values.length) parts.push(`${t(f.title)}: ${values.map((v) => (f.key === 'languages' ? t(languageName(v)) : v)).join(', ')}`);
  }
  if (all('tunam').length || all('dennam').length) parts.push(`${t('OPAC.SEARCH.F_YEAR')} ${all('tunam')[0] ?? '…'}–${all('dennam')[0] ?? '…'}`);
  if (all('consach').length) parts.push(t('OPAC.SEARCH.AVAILABLE_ONLY'));
  return parts.join(' · ') || t('OPAC.COMMON.ALL_DOCS');
}

function num(value: string | null): number | null {
  const n = value ? Number(value) : NaN;
  return Number.isFinite(n) ? n : null;
}
