import { NgTemplateOutlet } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../core/i18n';
import { Library } from '../core/library';
import { SavedSearch, SavedSearches, SearchParams } from '../core/saved-searches';
import { Facet, Facets, Msg, SearchApi, SearchRequest, UnifiedHit, UnifiedRequest, languageName, searchErrorMessage } from '../core/search';

const PAGE_SIZE = 12;
type FacetKey = 'materialTypes' | 'authors' | 'years' | 'languages' | 'stores';
/** title: khoá dịch. */
const FACETS: { key: FacetKey; title: string; param: string }[] = [
  { key: 'materialTypes', title: 'OPAC.SEARCH.F_DOCTYPE', param: 'loai' },
  { key: 'authors', title: 'OPAC.SEARCH.F_AUTHOR', param: 'tacgia' },
  { key: 'years', title: 'OPAC.SEARCH.F_YEAR', param: 'nam' },
  { key: 'languages', title: 'OPAC.SEARCH.F_LANGUAGE', param: 'ngonngu' },
  { key: 'stores', title: 'OPAC.SEARCH.F_STORE', param: 'kho' },
];
/** Ô tìm theo trường dùng cho cả hai loại (sách in và tài liệu số). */
const FIELDS: { key: 'title' | 'author' | 'publisher' | 'keyword'; label: string; placeholder: string; param: string }[] = [
  { key: 'title', label: 'OPAC.SEARCH.F_TITLE', placeholder: 'OPAC.SEARCH.PH_TITLE', param: 'nhande' },
  { key: 'author', label: 'OPAC.SEARCH.F_AUTHOR', placeholder: 'OPAC.SEARCH.PH_AUTHOR', param: 'tg' },
  { key: 'publisher', label: 'OPAC.SEARCH.F_PUBLISHER', placeholder: 'OPAC.SEARCH.PH_PUBLISHER', param: 'nxb' },
  { key: 'keyword', label: 'OPAC.SEARCH.F_KEYWORD', placeholder: 'OPAC.SEARCH.PH_KEYWORD_FIELD', param: 'tukhoa' },
];
/** Ô chỉ có ở sách in — có giá trị thì tìm được sách in mà thôi. */
const PRINT_ONLY: { key: 'isbn' | 'ddc' | 'barcode'; label: string; param: string }[] = [
  { key: 'isbn', label: 'OPAC.SEARCH.F_ISBN', param: 'isbn' },
  { key: 'ddc', label: 'OPAC.SEARCH.F_DDC', param: 'ddc' },
  { key: 'barcode', label: 'OPAC.SEARCH.F_BARCODE', param: 'dkcb' },
];
const PRINT_PARAMS = [...FACETS.map((f) => f.param), ...PRINT_ONLY.map((f) => f.param), 'consach'];

type Kind = 'all' | 'print' | 'digital';
const KIND_PARAM: Record<Kind, string | null> = { all: null, print: 'in', digital: 'so' };

/**
 * Tìm kiếm tài liệu (giao diện ELIB-TVXA): một danh sách gộp sách in + tài liệu số, chia theo loại (Tất cả / Tài liệu in / Tài liệu số).
 * Toàn bộ điều kiện nằm trên URL (chia sẻ, quay lại được). Loại "in" dùng tra cứu sách in đầy đủ (facet, ISBN, DDC, ĐKCB), các loại còn lại dùng tìm gộp.
 */
@Component({
  selector: 'opac-search',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, TranslatePipe],
  template: `
    <div class="container mx-auto px-4 py-8">
      <nav class="flex items-center text-sm text-gray-500 mb-8 bg-white p-3 rounded shadow-sm border-l-4 border-main">
        <a routerLink="/" class="hover:text-main flex items-center"><i class="fas fa-home mr-2"></i> {{ 'OPAC.NAVBAR.HOME' | translate }}</a>
        <i class="fas fa-chevron-right mx-3 text-xs text-gray-300"></i>
        <span class="text-main font-semibold">{{ 'OPAC.SEARCH.TITLE' | translate }}</span>
      </nav>

      <div class="flex flex-col md:flex-row gap-8">
        <aside class="w-full md:w-1/3 lg:w-1/4">
          <div class="bg-white p-6 rounded-xl shadow-sm border border-gray-100">
            <h3 class="text-lg font-bold text-main mb-6 pb-2 border-b-2 border-gray-100 uppercase">{{ 'OPAC.SEARCH.ADV_TITLE' | translate }}</h3>
            <form (ngSubmit)="submit()" class="space-y-4">
              <div class="relative">
                <label for="s-q" class="block text-sm font-semibold text-gray-700 mb-1">{{ 'OPAC.SEARCH.QUICK' | translate }}</label>
                <input id="s-q" name="q" [(ngModel)]="q" (ngModelChange)="typed($event)" (blur)="hideSuggest()" autocomplete="off"
                       [placeholder]="'OPAC.SEARCH.PH_KEYWORD' | translate" class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm" />
                @if (suggestions().length) {
                  <ul class="absolute z-20 left-0 right-0 top-full mt-1 bg-white border border-gray-200 rounded shadow-lg list-none p-0 m-0">
                    @for (s of suggestions(); track s) {
                      <li><button type="button" class="w-full text-left px-3 py-2 hover:bg-gray-50 text-sm" (mousedown)="pick(s)">{{ s }}</button></li>
                    }
                  </ul>
                }
              </div>
              @for (f of fields; track f.key) {
                <div>
                  <label [for]="'s-' + f.param" class="block text-sm font-semibold text-gray-700 mb-1">{{ f.label | translate }}</label>
                  <input [id]="'s-' + f.param" [name]="f.param" [(ngModel)]="fieldValues[f.param]" [placeholder]="f.placeholder | translate"
                         class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm" />
                </div>
              }
              <div>
                <label class="block text-sm font-semibold text-gray-700 mb-1" for="s-from">{{ 'OPAC.SEARCH.F_YEAR' | translate }}</label>
                <div class="flex items-center gap-2">
                  <input id="s-from" name="from" type="number" [(ngModel)]="yearFrom" [placeholder]="'OPAC.SEARCH.PH_YEAR_FROM' | translate"
                         class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm" />
                  <input id="s-to" name="to" type="number" [(ngModel)]="yearTo" [placeholder]="'OPAC.SEARCH.PH_YEAR_TO' | translate" [attr.aria-label]="'OPAC.SEARCH.PH_YEAR_TO' | translate"
                         class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm" />
                </div>
              </div>
              @if (hasDigital() && kind() !== 'print') {
                <div>
                  <label for="s-bst" class="block text-sm font-semibold text-gray-700 mb-1">{{ 'OPAC.SEARCH.COLLECTION' | translate }}</label>
                  <select id="s-bst" name="bst" [(ngModel)]="collection" class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main text-sm bg-white">
                    <option value="">{{ 'OPAC.SEARCH.ALL_COLLECTIONS' | translate }}</option>
                    @for (c of collections(); track c.key) { <option [value]="c.key">{{ c.key }} ({{ c.count }})</option> }
                  </select>
                </div>
              }
              @if (kind() === 'print') {
                @for (f of printOnly; track f.key) {
                  <div>
                    <label [for]="'s-' + f.param" class="block text-sm font-semibold text-gray-700 mb-1">{{ f.label | translate }}</label>
                    <input [id]="'s-' + f.param" [name]="f.param" [(ngModel)]="fieldValues[f.param]"
                           class="w-full p-2.5 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm" />
                  </div>
                }
              }
              <div class="flex flex-col gap-2 mt-4">
                <button type="submit" class="w-full bg-orange text-white font-bold py-3 rounded hover:bg-orange-600 transition-colors shadow-md uppercase">
                  <i class="fas fa-search mr-2"></i> {{ 'OPAC.SEARCH.BTN_FIND' | translate }}
                </button>
                <a routerLink="/tim-kiem" class="w-full text-center bg-gray-50 text-gray-600 font-semibold py-2.5 rounded hover:bg-gray-100 transition-colors border border-gray-200 uppercase">
                  <i class="fas fa-sync-alt mr-2"></i> {{ 'OPAC.SEARCH.BTN_CLEAR' | translate }}
                </a>
              </div>
            </form>

            <div class="mt-5 pt-4 border-t border-gray-100 flex flex-col gap-2 text-sm">
              @if (hasCriteria()) {
                @if (current(); as s) {
                  <button type="button" class="text-left text-green-700 hover:underline" (click)="saved.remove(s.id); current.set(undefined)" [title]="'OPAC.SEARCH.UNSAVE' | translate">
                    <i class="fas fa-bookmark mr-2"></i>{{ 'OPAC.SEARCH.SAVED' | translate }}</button>
                } @else {
                  <button type="button" class="text-left text-main hover:underline" (click)="saveCurrent()">
                    <i class="far fa-bookmark mr-2"></i>{{ 'OPAC.SEARCH.SAVE_SEARCH' | translate }}</button>
                }
              }
              @if (hasZ3950()) {
                <a routerLink="/lien-thu-vien" [queryParams]="{ q: q || null }" class="text-main hover:underline"><i class="fas fa-circle-nodes mr-2"></i>{{ 'OPAC.Z3950.TITLE' | translate }}</a>
              }
              @if (saved.items().length) {
                <button type="button" class="text-left text-gray-600 hover:underline" (click)="toggleSaved()" [attr.aria-expanded]="showSaved()">
                  <i class="fas fa-bookmark mr-2"></i>{{ 'OPAC.SEARCH.SAVED_COUNT' | translate: { count: saved.items().length } }}</button>
              }
            </div>
            @if (showSaved()) {
              <div class="mt-3 bg-gray-50 border border-gray-200 rounded p-3">
                <div class="text-xs text-gray-500 mb-2">{{ 'OPAC.SEARCH.SAVED_HINT' | translate }}</div>
                <ul class="divide-y divide-gray-200 list-none p-0 m-0">
                  @for (s of saved.items(); track s.id) {
                    <li class="flex items-center gap-2 py-1.5 text-sm">
                      <a [routerLink]="['/tim-kiem']" [queryParams]="s.params" class="flex-1 min-w-0 truncate text-main hover:underline">{{ s.name }}</a>
                      @if ((fresh()[s.id] || 0) > 0) {
                        <span class="px-2 py-0.5 rounded-full bg-green-100 text-green-700 text-xs whitespace-nowrap">{{ 'OPAC.SEARCH.NEW_RESULTS' | translate: { count: fresh()[s.id] } }}</span>
                      }
                      <button type="button" class="text-gray-400 hover:text-red-600" (click)="removeSaved(s.id)" [attr.aria-label]="('COMMON.DELETE' | translate) + ' ' + s.name" [title]="'COMMON.DELETE' | translate">
                        <i class="fas fa-xmark"></i></button>
                    </li>
                  }
                </ul>
              </div>
            }
          </div>

          @if (kind() === 'print' && facets(); as fc) {
            <div class="bg-white p-6 rounded-xl shadow-sm border border-gray-100 mt-6">
              <h3 class="text-lg font-bold text-main mb-4 pb-2 border-b-2 border-gray-100 uppercase">{{ 'OPAC.SEARCH.PRINT_FILTERS' | translate }}</h3>
              <label class="flex items-center gap-2 text-sm mb-4 cursor-pointer">
                <input type="checkbox" class="rounded border-gray-300" [checked]="availableOnly" (change)="toggleAvailable()" />
                {{ 'OPAC.SEARCH.AVAILABLE_ONLY' | translate }} <span class="ml-auto text-gray-400">{{ fc.availableCount }}</span>
              </label>
              @for (f of facetDefs; track f.key) {
                @if (fc[f.key].length) {
                  <div class="mb-4">
                    <div class="font-semibold text-sm text-gray-700 mb-2">{{ f.title | translate }}</div>
                    <ul class="space-y-1 list-none p-0 m-0">
                      @for (v of fc[f.key]; track v.key) {
                        <li>
                          <label class="flex items-start gap-2 text-sm text-gray-700 cursor-pointer">
                            <input type="checkbox" class="rounded border-gray-300 mt-0.5" [checked]="isOn(f.param, v.key)" (change)="toggle(f.param, v.key)" />
                            <span class="flex-1">{{ label(f.key, v) | translate }}</span><span class="text-gray-400">{{ v.count }}</span>
                          </label>
                        </li>
                      }
                    </ul>
                  </div>
                }
              }
            </div>
          }
        </aside>

        <main class="w-full md:w-2/3 lg:w-3/4">
          <div class="bg-white p-6 rounded-xl shadow-sm border border-gray-100 min-h-[500px]">
            <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center mb-6 gap-4">
              <h2 class="text-xl font-bold text-orange m-0">
                @if (loading()) { {{ 'OPAC.SEARCH.SEARCHING' | translate }} }
                @else { {{ 'OPAC.SEARCH.RESULT_N' | translate: { count: total() } }}
                  @if (elapsedMs() != null) { <span class="text-sm font-normal text-gray-400 ml-1">{{ 'OPAC.SEARCH.ELAPSED' | translate: { ms: elapsedMs() } }}</span> } }
              </h2>
              <div class="flex items-center gap-2">
                <label for="s-sort" class="text-sm font-semibold text-gray-600 whitespace-nowrap">{{ 'OPAC.SEARCH.SORT' | translate }}:</label>
                <select id="s-sort" name="sort" class="p-2 border border-gray-300 rounded outline-none focus:border-main transition-colors text-sm bg-white" [ngModel]="sort" (ngModelChange)="setSort($event)">
                  <option value="">{{ 'OPAC.SEARCH.SORT_RELEVANCE' | translate }}</option>
                  <option value="newest">{{ 'OPAC.SEARCH.SORT_NEWEST' | translate }}</option>
                  <option value="oldest">{{ 'OPAC.SEARCH.SORT_OLDEST' | translate }}</option>
                  <option value="title">{{ 'OPAC.SEARCH.SORT_TITLE' | translate }}</option>
                </select>
              </div>
            </div>

            @if (hasDigital()) {
              <div class="flex gap-2 mb-6 border-b border-gray-200 flex-wrap" role="tablist">
                @for (t of tabs(); track t.kind) {
                  <button type="button" role="tab" [attr.aria-selected]="kind() === t.kind" (click)="setKind(t.kind)"
                          class="px-5 py-3 font-semibold border-b-2 transition-colors"
                          [class]="kind() === t.kind ? 'text-main border-orange bg-orange/10' : 'text-gray-500 border-transparent hover:text-main'">
                    {{ t.label | translate }} <span class="text-sm font-normal">({{ t.count }})</span>
                  </button>
                }
              </div>
            }

            @if (error(); as e) {
              <div class="bg-red-50 text-red-700 rounded px-4 py-3 text-sm mb-4">{{ e.key | translate: e.params }}</div>
            }

            <ng-template #pager>
              @if (pageCount() > 1) {
                <nav class="flex justify-end gap-2 flex-wrap" [attr.aria-label]="'OPAC.COMMON.PAGINATION' | translate">
                  <button type="button" (click)="go(page - 1)" [disabled]="page <= 1" [attr.aria-label]="'COMMON.PREV' | translate"
                          class="w-10 h-10 flex items-center justify-center border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                    <i class="fas fa-chevron-left text-sm"></i>
                  </button>
                  @for (n of pageNumbers(); track n) {
                    <button type="button" (click)="go(n)" [attr.aria-current]="page === n ? 'page' : null"
                            class="w-10 h-10 flex items-center justify-center border rounded hover:bg-gray-50 transition-colors font-medium"
                            [class]="page === n ? 'bg-main text-white border-main hover:bg-main' : 'border-gray-300'">{{ n }}</button>
                  }
                  <button type="button" (click)="go(page + 1)" [disabled]="page >= pageCount()" [attr.aria-label]="'COMMON.NEXT' | translate"
                          class="w-10 h-10 flex items-center justify-center border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                    <i class="fas fa-chevron-right text-sm"></i>
                  </button>
                </nav>
              }
            </ng-template>

            <div class="mb-6"><ng-container *ngTemplateOutlet="pager" /></div>

            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
              @for (h of items(); track h.kind + h.publicId; let i = $index) {
                <div class="relative bg-white border border-gray-100 p-4 rounded-lg text-center hover:shadow-lg hover:-translate-y-1 transition-all flex flex-col">
                  <span class="absolute top-2 left-2 z-10 text-xs font-bold px-2 py-0.5 rounded text-white shadow-sm" [class]="h.kind === 'print' ? 'bg-main' : 'bg-orange'">
                    <i class="fas mr-1" [class.fa-book]="h.kind === 'print'" [class.fa-tablet-screen-button]="h.kind === 'digital'"></i>{{ (h.kind === 'print' ? 'OPAC.SEARCH.BADGE_PRINT' : 'OPAC.SEARCH.BADGE_DIGITAL') | translate }}
                  </span>
                  <div class="bg-gray-50 p-4 inline-block mb-4 rounded">
                    <a [routerLink]="link(h)" (click)="opened(h, i)" class="block">
                      @if (h.coverUrl && !brokenCovers().has(h.publicId)) {
                        <img [src]="h.coverUrl" [alt]="h.title" loading="lazy" referrerpolicy="no-referrer" (error)="coverFailed(h.publicId)" class="w-32 h-48 object-cover shadow-md mx-auto bg-white" />
                      } @else {
                        <div class="w-32 h-48 mx-auto shadow-md bg-gradient-to-br from-gray-100 to-white text-gray-300 flex items-center justify-center">
                          <i class="fas text-5xl" [class.fa-book]="h.kind === 'print'" [class.fa-file-lines]="h.kind === 'digital'"></i>
                        </div>
                      }
                    </a>
                  </div>
                  <div class="flex-1 flex flex-col">
                    <h4 class="font-semibold text-sm text-gray-800 line-clamp-2 mb-2">
                      <a [routerLink]="link(h)" (click)="opened(h, i)" class="hover:text-main">{{ h.title }}</a>
                    </h4>
                    <div class="mt-auto">
                      <p class="text-xs text-gray-500 m-0"><strong>{{ 'OPAC.SEARCH.F_AUTHOR' | translate }}:</strong> {{ h.author ?? ('OPAC.COMMON.UNKNOWN_AUTHOR' | translate) }}</p>
                      @if (h.publishYear) { <p class="text-xs text-gray-400 m-0 mt-1"><strong>{{ 'OPAC.SEARCH.YEAR_SHORT' | translate }}:</strong> {{ h.publishYear }}</p> }
                      <p class="text-xs m-0 mt-2">
                        @if (h.kind === 'print') {
                          @if (h.copies === 0) { <span class="px-2 py-0.5 rounded bg-gray-100 text-gray-500">{{ 'OPAC.COMMON.NO_COPIES' | translate }}</span> }
                          @else if (h.available > 0) { <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">{{ 'OPAC.COMMON.AVAILABLE_OF' | translate: { available: h.available, copies: h.copies } }}</span> }
                          @else { <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">{{ 'OPAC.COMMON.NONE_READY' | translate: { copies: h.copies } }}</span> }
                        } @else if (h.free) {
                          <span class="px-2 py-0.5 rounded bg-green-50 text-green-700">{{ 'OPAC.SEARCH.FREE' | translate }}</span>
                        } @else {
                          <span class="px-2 py-0.5 rounded bg-amber-50 text-amber-700">{{ 'OPAC.SEARCH.LOGIN_TO_READ' | translate }}</span>
                        }
                      </p>
                    </div>
                  </div>
                </div>
              }
            </div>

            @if (!loading() && loaded() && !items().length) {
              <div class="py-16 text-center text-gray-500">
                <i class="fas fa-magnifying-glass text-5xl text-gray-300"></i>
                <p class="mt-3">{{ 'OPAC.SEARCH.NO_RESULT_HINT' | translate }}</p>
                <p class="mt-1 text-xs">{{ 'OPAC.SEARCH.PHRASE_HINT' | translate }}</p>
                @if (hasZ3950() && q) {
                  <a routerLink="/lien-thu-vien" [queryParams]="{ q }" class="mt-3 inline-block text-main hover:underline">
                    <i class="fas fa-circle-nodes mr-1"></i> {{ 'OPAC.SEARCH.TRY_OTHER_LIBRARIES' | translate: { q } }}</a>
                }
              </div>
            }

            <div class="mt-10"><ng-container *ngTemplateOutlet="pager" /></div>
          </div>
        </main>
      </div>
    </div>
  `,
})
export class Search implements OnInit {
  private readonly api = inject(SearchApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly saved = inject(SavedSearches);
  private readonly language = inject(Language);
  private readonly library = inject(Library);

  protected readonly facetDefs = FACETS;
  protected readonly fields = FIELDS;
  protected readonly printOnly = PRINT_ONLY;
  protected readonly items = signal<UnifiedHit[]>([]);
  protected readonly total = signal(0);
  protected readonly printTotal = signal(0);
  protected readonly digitalTotal = signal(0);
  protected readonly elapsedMs = signal<number | null>(null);
  protected readonly facets = signal<Facets | null>(null);
  protected readonly collections = signal<Facet[]>([]);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly error = signal<Msg | null>(null);
  protected readonly suggestions = signal<string[]>([]);
  protected readonly showSaved = signal(false);
  /** Số kết quả mới của từng tìm kiếm đã lưu (tính khi mở danh sách). */
  protected readonly fresh = signal<Record<string, number>>({});
  protected readonly current = signal<SavedSearch | undefined>(undefined);
  protected readonly brokenCovers = signal(new Set<string>());
  /** Thư viện đã bật máy chủ Z39.50 cho OPAC. */
  protected readonly hasZ3950 = signal(false);
  protected readonly kind = signal<Kind>('all');
  /** Đơn vị có phân hệ Tài liệu số — không có thì chỉ còn sách in (không hiện tab). */
  protected readonly hasDigital = computed(() => this.library.has('DIGITAL'));
  protected readonly pageSize = signal(PAGE_SIZE);
  protected readonly tabs = computed(() => [
    { kind: 'all' as Kind, label: 'OPAC.SEARCH.TAB_ALL', count: this.printTotal() + this.digitalTotal() },
    { kind: 'print' as Kind, label: 'OPAC.SEARCH.TAB_PRINT', count: this.printTotal() },
    { kind: 'digital' as Kind, label: 'OPAC.SEARCH.TAB_DIGITAL', count: this.digitalTotal() },
  ]);
  private queryId: string | null = null;
  protected q = '';
  protected fieldValues: Record<string, string> = {};
  protected yearFrom: number | null = null;
  protected yearTo: number | null = null;
  protected collection = '';
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
      this.fieldValues = Object.fromEntries([...FIELDS, ...PRINT_ONLY].map((f) => [f.param, p.get(f.param) ?? '']));
      this.yearFrom = num(p.get('tunam'));
      this.yearTo = num(p.get('dennam'));
      this.collection = p.get('bst') ?? '';
      this.sort = p.get('sapxep') ?? '';
      this.page = Math.max(1, num(p.get('trang')) ?? 1);
      this.availableOnly = p.get('consach') === '1';
      this.kind.set(this.kindOf(p));
      void this.load();
    });
  }

  /** Loại đang xem từ URL: không có Tài liệu số thì chỉ còn "in". */
  private kindOf(p: ParamMap): Kind {
    if (!this.hasDigital()) return 'print';
    const k = p.get('kieu');
    // Liên kết/tìm kiếm đã lưu cũ mang bộ lọc riêng của sách in mà không có 'kieu' → coi là tìm sách in.
    if (k !== 'so' && PRINT_PARAMS.some((x) => p.has(x))) return 'print';
    return k === 'in' ? 'print' : k === 'so' ? 'digital' : 'all';
  }

  protected hasCriteria(): boolean {
    return (this.params?.keys.length ?? 0) > 0;
  }

  protected pageCount(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize()));
  }

  protected pageNumbers(): number[] {
    const total = this.pageCount();
    let start = Math.max(1, this.page - 2);
    const end = Math.min(total, start + 4);
    if (end - start < 4) start = Math.max(1, end - 4);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }

  protected link(h: UnifiedHit): string[] {
    return [h.kind === 'print' ? '/tai-lieu' : '/thu-vien-so', h.publicId];
  }

  protected label(key: FacetKey, f: Facet): string {
    return key === 'languages' ? languageName(f.key) : f.key;
  }

  protected isOn(param: string, value: string): boolean {
    return this.params?.getAll(param).includes(value) ?? false;
  }

  protected submit(): void {
    this.suggestions.set([]);
    const query: Record<string, string | null> = {
      q: this.q.trim() || null, tunam: this.yearFrom ? String(this.yearFrom) : null, dennam: this.yearTo ? String(this.yearTo) : null,
      bst: this.collection || null, trang: null,
    };
    for (const f of [...FIELDS, ...PRINT_ONLY]) query[f.param] = this.fieldValues[f.param]?.trim() || null;
    // Ô chỉ có ở sách in có giá trị → chỉ tìm sách in; chọn bộ sưu tập → chỉ tìm tài liệu số.
    if (PRINT_ONLY.some((f) => query[f.param])) query['kieu'] = 'in';
    else if (query['bst'] && this.kind() !== 'digital') query['kieu'] = 'so';
    this.navigate(query);
  }

  protected setKind(kind: Kind): void {
    const query: Record<string, string | string[] | null> = { kieu: KIND_PARAM[kind], trang: null };
    if (kind !== 'print') for (const k of PRINT_PARAMS) query[k] = null;
    if (kind === 'print') query['bst'] = null;
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
    if (page < 1 || page > this.pageCount() || page === this.page) return;
    this.navigate({ trang: page > 1 ? String(page) : null });
    window.scrollTo({ top: 0, behavior: 'smooth' });
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

  /** Bạn đọc mở kết quả — chỉ sách in có thống kê lượt mở (vị trí tính theo thứ tự trong danh sách đang xem). */
  protected opened(h: UnifiedHit, index: number): void {
    if (h.kind === 'print' && this.queryId) this.api.click(this.queryId, h.publicId, (this.page - 1) * this.pageSize() + index + 1);
  }

  protected coverFailed(publicId: string): void {
    this.brokenCovers.update((s) => new Set(s).add(publicId));
  }

  protected saveCurrent(): void {
    if (!this.params) return;
    const params = paramsOf(this.params);
    this.current.set(this.saved.save(describe(params, (k, p) => this.language.t(k, p)), params, this.total()));
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
        const map = convertToParamMap(s.params);
        const kind = this.kindOf(map);
        const total = kind === 'print'
          ? (await this.api.search({ ...buildPrintRequest(map), page: 1, pageSize: 1, refine: true })).total
          : (await this.api.all({ ...buildUnifiedRequest(map, kind), page: 1, pageSize: 1, refine: true })).total;
        counts[s.id] = Math.max(0, total - s.total);
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

  private async load(): Promise<void> {
    const p = this.params!;
    const kind = this.kind();
    const request = buildPrintRequest(p);
    // Cùng câu tìm với lượt trước trong tab này (đổi loại, bộ lọc, sắp xếp, lật trang, quay lại từ trang chi tiết) → không tính lượt tìm mới.
    const criteria = criteriaKey(request, p.get('bst'));
    const last = readLast();
    const refine = last?.criteria === criteria;
    this.loading.set(true);
    this.error.set(null);
    try {
      if (kind === 'print') {
        // Sách in: tra cứu đầy đủ (facet). Số lượng của loại số lấy thêm bằng một lượt tìm gộp nhẹ (không tính thống kê).
        const [bibs, counts] = await Promise.all([
          this.api.search({ ...request, refine }),
          this.hasDigital() ? this.api.all({ ...buildUnifiedRequest(p, 'all'), page: 1, pageSize: 1, refine: true }).catch(() => null) : Promise.resolve(null),
        ]);
        this.items.set(bibs.items.map((b): UnifiedHit => ({
          kind: 'print', publicId: b.publicId, title: b.title, author: b.author, publisher: b.publisher, publishYear: b.publishYear, coverUrl: b.coverUrl,
          language: b.language, materialType: b.materialType, copies: b.copies, available: b.available, collectionName: null, free: false, hasFile: false,
          linkedBibPublicId: null,
        })));
        this.total.set(bibs.total);
        this.printTotal.set(bibs.total);
        this.digitalTotal.set(counts?.digitalTotal ?? 0);
        this.elapsedMs.set(bibs.elapsedMs ?? null);
        this.facets.set(bibs.facets);
        if (counts) this.collections.set(counts.collections);
        this.pageSize.set(bibs.pageSize);
        this.queryId = refine ? last!.queryId : bibs.queryId;
        if (!refine) writeLast({ criteria, queryId: bibs.queryId });
      } else {
        const r = await this.api.all({ ...buildUnifiedRequest(p, kind), refine });
        this.items.set(r.items);
        this.total.set(r.total);
        this.printTotal.set(r.printTotal);
        this.digitalTotal.set(r.digitalTotal);
        this.elapsedMs.set(r.elapsedMs);
        this.facets.set(null);
        this.collections.set(r.collections);
        this.pageSize.set(r.pageSize);
        this.queryId = refine ? last!.queryId : r.queryId;
        if (!refine) writeLast({ criteria, queryId: r.queryId });
      }
      const saved = this.current();
      if (saved && this.total() !== saved.total) this.saved.seen(saved.id, this.total());
    } catch (e) {
      this.error.set(searchErrorMessage(e));
    } finally {
      this.loading.set(false);
      this.loaded.set(true);
    }
  }
}

/** Điều kiện tìm trên URL → yêu cầu tra cứu sách in (trang hiện tại, và chạy lại tìm kiếm đã lưu). */
function buildPrintRequest(p: ParamMap): SearchRequest {
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
  for (const f of [...FIELDS, ...PRINT_ONLY]) (request as unknown as Record<string, unknown>)[f.key] = p.get(f.param) || null;
  return request;
}

/** Điều kiện tìm trên URL → yêu cầu tìm gộp. */
function buildUnifiedRequest(p: ParamMap, kind: Kind): UnifiedRequest {
  return {
    q: p.get('q') || null,
    title: p.get('nhande') || null,
    author: p.get('tg') || null,
    publisher: p.get('nxb') || null,
    keyword: p.get('tukhoa') || null,
    yearFrom: num(p.get('tunam')),
    yearTo: num(p.get('dennam')),
    collection: p.get('bst') || null,
    kind,
    sort: p.get('sapxep') || null,
    page: Math.max(1, num(p.get('trang')) ?? 1),
    pageSize: PAGE_SIZE,
  };
}

/** Phần "câu tìm" (từ khoá + ô theo trường + khoảng năm + bộ sưu tập) — khác nhau mới là lượt tìm mới. */
function criteriaKey(r: SearchRequest, collection: string | null): string {
  return JSON.stringify([r.q, ...FIELDS.map((f) => r[f.key] ?? null), ...PRINT_ONLY.map((f) => r[f.key] ?? null), r.yearFrom, r.yearTo, collection]);
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

/** Tên tự đặt cho tìm kiếm đã lưu (theo ngôn ngữ lúc lưu): từ khoá, các ô theo trường, bộ lọc. */
function describe(params: SearchParams, t: (key: string, params?: Record<string, unknown>) => string): string {
  const all = (k: string): string[] => (params[k] === undefined ? [] : Array.isArray(params[k]) ? (params[k] as string[]) : [params[k] as string]);
  const parts: string[] = [];
  if (all('q').length) parts.push(`"${all('q')[0]}"`);
  for (const f of [...FIELDS, ...PRINT_ONLY]) if (all(f.param).length) parts.push(`${t(f.label)}: ${all(f.param)[0]}`);
  if (all('bst').length) parts.push(`${t('OPAC.SEARCH.COLLECTION')}: ${all('bst')[0]}`);
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
