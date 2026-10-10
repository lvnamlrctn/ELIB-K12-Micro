import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Facet, SearchApi, SearchRequest, SearchResult, languageName } from '../core/search';

const PAGE_SIZE = 10;
type FacetKey = 'materialTypes' | 'authors' | 'years' | 'languages' | 'stores';
const FACETS: { key: FacetKey; title: string; param: string }[] = [
  { key: 'materialTypes', title: 'Loại tài liệu', param: 'loai' },
  { key: 'authors', title: 'Tác giả', param: 'tacgia' },
  { key: 'years', title: 'Năm xuất bản', param: 'nam' },
  { key: 'languages', title: 'Ngôn ngữ', param: 'ngonngu' },
  { key: 'stores', title: 'Kho', param: 'kho' },
];
const ADVANCED: { key: keyof SearchRequest; label: string; param: string }[] = [
  { key: 'title', label: 'Nhan đề', param: 'nhande' },
  { key: 'author', label: 'Tác giả', param: 'tg' },
  { key: 'publisher', label: 'Nhà xuất bản', param: 'nxb' },
  { key: 'keyword', label: 'Từ khoá, chủ đề', param: 'tukhoa' },
  { key: 'isbn', label: 'ISBN', param: 'isbn' },
  { key: 'ddc', label: 'Phân loại DDC', param: 'ddc' },
  { key: 'barcode', label: 'Số ĐKCB', param: 'dkcb' },
];

/**
 * Tra cứu tài liệu (monolith: OPAC /search — phần sách in). Toàn bộ điều kiện nằm trên URL (chia sẻ, quay lại được): q, các ô nâng cao,
 * facet chọn nhiều, chỉ còn bản sẵn sàng, sắp xếp, trang.
 */
@Component({
  selector: 'opac-search',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="bg-white border-b border-slate-200">
      <div class="max-w-6xl mx-auto px-4 py-6">
        <div class="relative">
        <form (ngSubmit)="submit()" class="flex bg-white rounded-xl border border-slate-300 focus-within:border-blue-500 overflow-hidden">
          <span class="material-icons text-slate-400 self-center pl-3">search</span>
          <input name="q" [(ngModel)]="q" (ngModelChange)="typed($event)" (blur)="hideSuggest()" autocomplete="off"
                 class="flex-1 px-3 py-3 text-slate-800 outline-none min-w-0" placeholder="Nhan đề, tác giả, chủ đề, ISBN..." aria-label="Từ khoá tra cứu" />
          <button type="submit" class="px-5 bg-blue-600 hover:bg-blue-700 text-white font-medium">Tìm</button>
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
            <span class="material-icons text-[18px]">tune</span> Tìm nâng cao</button>
          @if (hasCriteria()) { <a routerLink="/tim-kiem" class="text-slate-500 hover:underline">Xoá điều kiện</a> }
        </div>
        @if (showAdvanced()) {
          <form (ngSubmit)="submit()" class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            @for (f of advanced; track f.key) {
              <div>
                <label class="block text-xs text-slate-500 mb-1" [for]="'adv-' + f.param">{{ f.label }}</label>
                <input [id]="'adv-' + f.param" [name]="f.param" [(ngModel)]="adv[f.param]" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
            }
            <div class="flex gap-2">
              <div class="flex-1">
                <label class="block text-xs text-slate-500 mb-1" for="adv-from">Năm từ</label>
                <input id="adv-from" name="from" type="number" [(ngModel)]="yearFrom" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
              <div class="flex-1">
                <label class="block text-xs text-slate-500 mb-1" for="adv-to">đến</label>
                <input id="adv-to" name="to" type="number" [(ngModel)]="yearTo" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
              </div>
            </div>
            <div class="sm:col-span-2 lg:col-span-4 flex justify-end">
              <button type="submit" class="px-5 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium">Tìm theo điều kiện</button>
            </div>
          </form>
        }
      </div>
    </section>

    <section class="max-w-6xl mx-auto px-4 py-6 grid gap-6 md:grid-cols-[250px_1fr]">
      <aside class="space-y-5">
        <button type="button" class="md:hidden w-full flex items-center justify-between px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm"
                (click)="showFacets.set(!showFacets())">Bộ lọc <span class="material-icons">{{ showFacets() ? 'expand_less' : 'expand_more' }}</span></button>
        <div [class.hidden]="!showFacets()" class="md:block space-y-5">
          @if (result(); as r) {
            <label class="flex items-center gap-2 text-sm bg-white border border-slate-200 rounded-lg px-3 py-2">
              <input type="checkbox" class="rounded border-slate-300" [checked]="availableOnly" (change)="toggleAvailable()" />
              Còn sách để mượn <span class="ml-auto text-slate-400">{{ r.facets.availableCount }}</span>
            </label>
            @for (f of facets; track f.key) {
              @if (r.facets[f.key].length) {
                <div class="bg-white border border-slate-200 rounded-lg p-3">
                  <div class="font-medium text-sm text-slate-800 mb-2">{{ f.title }}</div>
                  <ul class="space-y-1">
                    @for (v of r.facets[f.key]; track v.key) {
                      <li>
                        <label class="flex items-start gap-2 text-sm text-slate-700 cursor-pointer">
                          <input type="checkbox" class="rounded border-slate-300 mt-0.5" [checked]="isOn(f.param, v.key)" (change)="toggle(f.param, v.key)" />
                          <span class="flex-1">{{ label(f.key, v) }}</span><span class="text-slate-400">{{ v.count }}</span>
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
            @if (loading()) { Đang tìm... }
            @else if (result(); as r) { Tìm thấy <b>{{ r.total }}</b> tài liệu }
          </div>
          <label class="ml-auto text-sm text-slate-600 flex items-center gap-2">Sắp xếp
            <select class="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" [ngModel]="sort" (ngModelChange)="setSort($event)" name="sort">
              <option value="">Liên quan nhất</option>
              <option value="newest">Mới xuất bản</option>
              <option value="oldest">Cũ nhất</option>
              <option value="title">Nhan đề A–Z</option>
            </select>
          </label>
        </div>

        @if (error()) {
          <div class="bg-red-50 text-red-700 rounded-lg px-4 py-3 text-sm">{{ error() }}</div>
        }

        <ul class="space-y-3">
          @for (b of result()?.items ?? []; track b.publicId) {
            <li class="bg-white border border-slate-200 rounded-xl p-4 flex gap-4 hover:border-blue-300 transition">
              <div class="w-14 h-20 rounded bg-gradient-to-br from-blue-100 to-sky-50 text-blue-500 flex items-center justify-center shrink-0">
                <span class="material-icons">menu_book</span>
              </div>
              <div class="min-w-0 flex-1">
                <a [routerLink]="['/tai-lieu', b.publicId]" class="font-semibold text-slate-900 hover:text-blue-700">{{ b.title }}</a>
                <div class="text-sm text-slate-600 mt-0.5">
                  {{ b.author ?? 'Không rõ tác giả' }}@if (b.publisher) { · {{ b.publisher }} }@if (b.publishYear) { · {{ b.publishYear }} }
                </div>
                @if (b.summary) { <p class="text-sm text-slate-500 mt-1 line-clamp-2">{{ b.summary }}</p> }
                <div class="mt-2 flex items-center gap-2 flex-wrap text-xs">
                  @if (b.materialType) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">{{ b.materialType }}</span> }
                  @if (b.ddc) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-600">DDC {{ b.ddc }}</span> }
                  @if (b.copies === 0) { <span class="px-2 py-0.5 rounded bg-slate-100 text-slate-500">Chưa có bản phục vụ</span> }
                  @else if (b.available > 0) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">Còn {{ b.available }}/{{ b.copies }} bản</span> }
                  @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">Đã mượn hết ({{ b.copies }} bản)</span> }
                </div>
              </div>
            </li>
          } @empty {
            @if (!loading() && result()) {
              <li class="bg-white border border-slate-200 rounded-xl p-10 text-center text-slate-500">
                <span class="material-icons text-5xl text-slate-300">search_off</span>
                <p class="mt-2">Không tìm thấy tài liệu phù hợp. Thử bỏ dấu, dùng ít từ hơn hoặc bỏ bớt bộ lọc.</p>
              </li>
            }
          }
        </ul>

        @if (pages() > 1) {
          <nav class="mt-6 flex items-center justify-center gap-1 text-sm" aria-label="Phân trang">
            <button type="button" class="px-3 py-1.5 rounded-lg border border-slate-200 bg-white disabled:opacity-40" [disabled]="page <= 1" (click)="go(page - 1)">Trước</button>
            <span class="px-3 text-slate-600">Trang {{ page }} / {{ pages() }}</span>
            <button type="button" class="px-3 py-1.5 rounded-lg border border-slate-200 bg-white disabled:opacity-40" [disabled]="page >= pages()" (click)="go(page + 1)">Sau</button>
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

  protected readonly facets = FACETS;
  protected readonly advanced = ADVANCED;
  protected readonly result = signal<SearchResult | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly suggestions = signal<string[]>([]);
  protected readonly showAdvanced = signal(false);
  protected readonly showFacets = signal(false);
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
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      this.params = p;
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

  protected hideSuggest(): void {
    setTimeout(() => this.suggestions.set([]), 150);
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: params, queryParamsHandling: 'merge' });
  }

  private async load(): Promise<void> {
    const p = this.params!;
    const request: SearchRequest = {
      q: this.q || null,
      yearFrom: this.yearFrom,
      yearTo: this.yearTo,
      materialTypes: p.getAll('loai'),
      authors: p.getAll('tacgia'),
      years: p.getAll('nam').map(Number).filter((y) => !Number.isNaN(y)),
      languages: p.getAll('ngonngu'),
      stores: p.getAll('kho'),
      availableOnly: this.availableOnly,
      sort: this.sort || null,
      page: this.page,
      pageSize: PAGE_SIZE,
    };
    for (const f of ADVANCED) (request as unknown as Record<string, unknown>)[f.key] = this.adv[f.param] || null;
    this.loading.set(true);
    this.error.set(null);
    try {
      this.result.set(await this.api.search(request));
    } catch {
      this.error.set('Không tra cứu được lúc này, vui lòng thử lại.');
    } finally {
      this.loading.set(false);
    }
  }
}

function num(value: string | null): number | null {
  const n = value ? Number(value) : NaN;
  return Number.isFinite(n) ? n : null;
}
