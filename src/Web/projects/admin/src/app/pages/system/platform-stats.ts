import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PlatformStatsRequest, TenantSummary, errorMessage, saveFile } from '../../core/api';
import { LNumberPipe, Language } from '../../core/i18n';
import { DateInput } from '../../shared/date-input';

type Tab = 'readers' | 'loans' | 'fines' | 'items';
/** now: số liệu hiện tại (không theo kỳ lọc); money: tiền (đồng). */
interface Col { key: string; label: string; now?: boolean; money?: boolean; }
type Values = Partial<Record<string, number>>;
interface Row { tenant: TenantSummary; values: Values; }

const COLUMNS: Record<Tab, Col[]> = {
  readers: [
    { key: 'total', label: 'PLATFORM_STATS.READERS_TOTAL', now: true },
    { key: 'added', label: 'PLATFORM_STATS.READERS_ADDED' },
    { key: 'locked', label: 'PLATFORM_STATS.READERS_LOCKED', now: true },
    { key: 'expired', label: 'PLATFORM_STATS.READERS_EXPIRED', now: true },
  ],
  loans: [
    { key: 'loans', label: 'PLATFORM_STATS.LOANS' },
    { key: 'returns', label: 'PLATFORM_STATS.RETURNS' },
    { key: 'readersServed', label: 'PLATFORM_STATS.READERS_SERVED' },
    { key: 'lost', label: 'PLATFORM_STATS.LOANS_LOST' },
    { key: 'onLoan', label: 'PLATFORM_STATS.ON_LOAN', now: true },
    { key: 'overdue', label: 'PLATFORM_STATS.OVERDUE', now: true },
  ],
  fines: [
    { key: 'tickets', label: 'PLATFORM_STATS.FINE_TICKETS' },
    { key: 'total', label: 'PLATFORM_STATS.FINE_TOTAL', money: true },
    { key: 'discount', label: 'PLATFORM_STATS.FINE_DISCOUNT', money: true },
    { key: 'paid', label: 'PLATFORM_STATS.FINE_PAID', money: true },
    { key: 'remaining', label: 'PLATFORM_STATS.FINE_REMAINING', money: true },
  ],
  items: [
    { key: 'titles', label: 'PLATFORM_STATS.TITLES', now: true },
    { key: 'copies', label: 'PLATFORM_STATS.COPIES', now: true },
    { key: 'added', label: 'PLATFORM_STATS.ITEMS_ADDED' },
    { key: 'available', label: 'PLATFORM_STATS.AVAILABLE', now: true },
    { key: 'unshelved', label: 'PLATFORM_STATS.UNSHELVED', now: true },
    { key: 'onLoan', label: 'PLATFORM_STATS.ITEMS_ON_LOAN', now: true },
    { key: 'outOfStore', label: 'PLATFORM_STATS.OUT_OF_STORE', now: true },
    { key: 'lost', label: 'PLATFORM_STATS.ITEMS_LOST', now: true },
    { key: 'liquidated', label: 'PLATFORM_STATS.LIQUIDATED', now: true },
  ],
};

const TABS: { key: Tab; label: string; icon: string; hint?: string }[] = [
  { key: 'readers', label: 'PLATFORM_STATS.TAB_READERS', icon: 'people' },
  { key: 'loans', label: 'PLATFORM_STATS.TAB_LOANS', icon: 'swap_horiz' },
  { key: 'fines', label: 'PLATFORM_STATS.TAB_FINES', icon: 'gavel', hint: 'PLATFORM_STATS.FINES_HINT' },
  { key: 'items', label: 'PLATFORM_STATS.TAB_ITEMS', icon: 'menu_book', hint: 'PLATFORM_STATS.ITEMS_HINT' },
];

/**
 * Thống kê toàn hệ thống của quản trị nền tảng: bạn đọc (patron), mượn trả và phạt (circulation), tài liệu in (holdings) của từng đơn vị,
 * lọc theo đơn vị và khoảng ngày. Ba service gọi độc lập — một service lỗi chỉ báo lỗi ở tab của nó. Đơn vị chưa có số liệu hiện 0.
 */
@Component({
  selector: 'app-platform-stats',
  imports: [DateInput, FormsModule, LNumberPipe, TranslatePipe],
  template: `
    <div class="mb-4 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'PLATFORM_STATS.TITLE' | translate }}</h4>
    </div>

    <div class="panel !mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="relative">
          <span class="form-label">{{ 'PLATFORM_STATS.TENANTS' | translate }}</span>
          <button type="button" class="input !w-64 text-left flex items-center justify-between" (click)="pickerOpen.set(!pickerOpen())"
                  [attr.aria-expanded]="pickerOpen()" aria-haspopup="listbox">
            <span class="truncate">{{ selected().size ? ('PLATFORM_STATS.N_TENANTS' | translate: { count: selected().size }) : ('PLATFORM_STATS.ALL_TENANTS' | translate) }}</span>
            <span class="material-icons text-[18px] text-gray-400">expand_more</span>
          </button>
          @if (pickerOpen()) {
            <div class="fixed inset-0 z-10" (click)="pickerOpen.set(false)" aria-hidden="true"></div>
            <div class="absolute z-20 mt-1 w-80 max-w-[90vw] bg-white rounded-lg shadow-lg border border-gray-200 p-2">
              <input type="search" class="input !py-1 mb-2" [placeholder]="'PLATFORM_STATS.FIND_TENANT' | translate" [ngModel]="tenantQuery()" (ngModelChange)="tenantQuery.set($event)"
                     [attr.aria-label]="'PLATFORM_STATS.FIND_TENANT' | translate" />
              <div class="flex justify-between text-xs mb-1 px-1">
                <button type="button" class="text-blue-600 hover:underline" (click)="selectShown()">{{ 'PLATFORM_STATS.SELECT_SHOWN' | translate }}</button>
                <button type="button" class="text-gray-500 hover:underline" (click)="clearSelection()">{{ 'PLATFORM_STATS.CLEAR' | translate }}</button>
              </div>
              <div class="max-h-64 overflow-y-auto divide-y divide-gray-50" role="listbox" aria-multiselectable="true">
                @for (t of shownTenants(); track t.id) {
                  <label class="flex items-center gap-2 px-1 py-1.5 text-sm hover:bg-gray-50 cursor-pointer">
                    <input type="checkbox" class="rounded border-gray-300" [checked]="selected().has(t.id)" (change)="toggle(t.id)" />
                    <span class="font-mono text-xs text-gray-500">{{ t.code }}</span><span class="truncate">{{ t.name }}</span>
                  </label>
                } @empty { <p class="text-xs text-gray-400 text-center py-3">{{ 'COMMON.NO_DATA' | translate }}</p> }
              </div>
            </div>
          }
        </div>
        <div><label for="ps-from" class="form-label">{{ 'PLATFORM_STATS.FROM' | translate }}</label>
          <app-date-input fieldId="ps-from" [(ngModel)]="from"></app-date-input></div>
        <div><label for="ps-to" class="form-label">{{ 'PLATFORM_STATS.TO' | translate }}</label>
          <app-date-input fieldId="ps-to" [(ngModel)]="to"></app-date-input></div>
        <div class="flex gap-1 flex-wrap pb-0.5">
          @for (p of presets; track p.key) {
            <button type="button" class="btn-secondary !py-1.5 !px-2 !text-xs" (click)="preset(p.key)">{{ p.label | translate }}</button>
          }
        </div>
        <button type="button" class="btn-primary ml-auto" [disabled]="loading()" (click)="load()">
          <span class="material-icons text-[18px]" [class.animate-spin]="loading()">{{ loading() ? 'autorenew' : 'query_stats' }}</span> {{ 'PLATFORM_STATS.VIEW' | translate }}</button>
      </div>
      <p class="text-xs text-gray-500 mt-2">{{ 'PLATFORM_STATS.PERIOD_HINT' | translate }}</p>
    </div>

    @if (errors()['tenants']; as err) { <div class="alert-error mb-4">{{ 'PLATFORM_STATS.TENANTS_FAILED' | translate }}: {{ err }}</div> }

    <div class="flex gap-1 border-b border-gray-200 mb-4 overflow-x-auto" role="tablist">
      @for (t of tabs; track t.key) {
        <button type="button" role="tab" [attr.aria-selected]="tab() === t.key" class="px-4 py-2 text-sm flex items-center gap-1.5 border-b-2 -mb-px whitespace-nowrap"
                [class]="tab() === t.key ? 'border-blue-600 text-blue-700 font-medium' : 'border-transparent text-gray-500 hover:text-gray-700'" (click)="tab.set(t.key)">
          <span class="material-icons text-[18px]">{{ t.icon }}</span>{{ t.label | translate }}
          @if (errors()[source(t.key)]) { <span class="material-icons text-[16px] text-red-500">error</span> }
        </button>
      }
    </div>

    @if (errors()[source(tab())]; as err) {
      <div class="alert-error mb-4">{{ 'PLATFORM_STATS.LOAD_FAILED' | translate }}: {{ err }}</div>
    }
    @if (tabHint(); as hint) { <p class="text-xs text-gray-500 mb-3">{{ hint | translate }}</p> }

    <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-5 gap-3 mb-4">
      @for (c of columns(); track c.key) {
        <div class="bg-white rounded-xl shadow-sm border border-gray-100 p-4">
          <div class="text-xs text-gray-500">{{ c.label | translate }}@if (c.now) { <span class="text-gray-400"> · {{ 'PLATFORM_STATS.NOW' | translate }}</span> }</div>
          <div class="text-2xl font-bold text-gray-800 mt-1">{{ totals()[c.key] | lnumber }}</div>
        </div>
      }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
      <div class="px-4 py-3 border-b border-gray-100 flex items-center gap-3 flex-wrap">
        <span class="text-sm text-gray-600">{{ 'PLATFORM_STATS.ROWS' | translate: { count: rows().length } }}</span>
        <label class="flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
          <input type="checkbox" class="rounded border-gray-300" [ngModel]="hideEmpty()" (ngModelChange)="hideEmpty.set($event)" /> {{ 'PLATFORM_STATS.HIDE_EMPTY' | translate }}</label>
        <button type="button" class="btn-secondary !py-1.5 ml-auto" [disabled]="!rows().length" (click)="exportCsv()">
          <span class="material-icons text-[18px]">download</span> {{ 'PLATFORM_STATS.EXPORT' | translate }}</button>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[720px]">
          <thead><tr>
            <th class="th">
              <button type="button" class="flex items-center gap-1" (click)="sortBy('')">{{ 'PLATFORM_STATS.TENANT' | translate }}
                @if (sort() === '') { <span class="material-icons text-[14px]">arrow_upward</span> }</button></th>
            @for (c of columns(); track c.key) {
              <th class="th !text-right">
                <button type="button" class="inline-flex items-center gap-1" (click)="sortBy(c.key)">
                  @if (sort() === c.key) { <span class="material-icons text-[14px]">arrow_downward</span> }{{ c.label | translate }}</button>
                @if (c.now) { <div class="text-[10px] font-normal text-gray-400 normal-case">{{ 'PLATFORM_STATS.NOW' | translate }}</div> }
              </th>
            }
          </tr></thead>
          <tbody>
            @for (r of rows(); track r.tenant.id) {
              <tr class="hover:bg-gray-50/60 border-b border-gray-50">
                <td class="td"><span class="font-mono text-xs text-gray-500 mr-2">{{ r.tenant.code }}</span>{{ r.tenant.name }}</td>
                @for (c of columns(); track c.key) {
                  <td class="td text-right tabular-nums" [class.text-gray-300]="!r.values[c.key]">{{ (r.values[c.key] ?? 0) | lnumber }}</td>
                }
              </tr>
            } @empty {
              <tr><td [attr.colspan]="columns().length + 1" class="py-10 text-center text-sm text-gray-500">
                {{ (loading() ? 'COMMON.LOADING' : 'COMMON.NO_DATA') | translate }}</td></tr>
            }
          </tbody>
          @if (rows().length) {
            <tfoot><tr class="bg-gray-50 font-semibold">
              <td class="td">{{ 'PLATFORM_STATS.TOTAL' | translate }}</td>
              @for (c of columns(); track c.key) { <td class="td text-right tabular-nums">{{ totals()[c.key] | lnumber }}</td> }
            </tr></tfoot>
          }
        </table>
      </div>
    </div>
  `,
})
export class PlatformStats implements OnInit {
  private readonly api = inject(Api);
  private readonly language = inject(Language);

  protected readonly tabs = TABS;
  protected readonly presets = [
    { key: 'month', label: 'PLATFORM_STATS.THIS_MONTH' },
    { key: 'lastMonth', label: 'PLATFORM_STATS.LAST_MONTH' },
    { key: 'year', label: 'PLATFORM_STATS.THIS_YEAR' },
    { key: 'days30', label: 'PLATFORM_STATS.LAST_30_DAYS' },
  ] as const;

  protected readonly tenants = signal<TenantSummary[]>([]);
  protected readonly selected = signal<Set<number>>(new Set());
  protected readonly tenantQuery = signal('');
  protected readonly pickerOpen = signal(false);
  protected readonly tab = signal<Tab>('readers');
  protected readonly loading = signal(false);
  protected readonly hideEmpty = signal(false);
  protected readonly sort = signal<string>('');
  /** Số liệu theo nguồn: readers | loans | fines | items → đơn vị → giá trị. */
  private readonly data = signal<Record<Tab, Map<number, Values>>>({ readers: new Map(), loans: new Map(), fines: new Map(), items: new Map() });
  protected readonly errors = signal<Record<string, string | null>>({});
  protected from = '';
  protected to = '';

  protected readonly columns = computed(() => COLUMNS[this.tab()]);
  protected readonly tabHint = computed(() => TABS.find((t) => t.key === this.tab())?.hint ?? null);
  protected readonly shownTenants = computed(() => {
    const q = this.tenantQuery().trim().toLowerCase();
    return q ? this.tenants().filter((t) => t.code.toLowerCase().includes(q) || t.name.toLowerCase().includes(q)) : this.tenants();
  });

  /** Đơn vị đã chọn (hoặc mọi đơn vị) — mỗi đơn vị một dòng, thiếu số liệu = 0. */
  protected readonly rows = computed<Row[]>(() => {
    const picked = this.selected();
    const values = this.data()[this.tab()];
    const cols = this.columns();
    let rows = this.tenants().filter((t) => picked.size === 0 || picked.has(t.id)).map((tenant) => ({ tenant, values: values.get(tenant.id) ?? {} }));
    if (this.hideEmpty()) rows = rows.filter((r) => cols.some((c) => r.values[c.key]));
    const key = this.sort();
    return key ? [...rows].sort((a, b) => (b.values[key] ?? 0) - (a.values[key] ?? 0)) : rows;
  });

  protected readonly totals = computed<Values>(() => {
    const out: Values = {};
    for (const c of this.columns()) out[c.key] = this.rows().reduce((sum, r) => sum + (r.values[c.key] ?? 0), 0);
    return out;
  });

  async ngOnInit(): Promise<void> {
    this.preset('month');
    try {
      this.tenants.set(await this.api.allTenants());
    } catch (e) {
      this.errors.set({ tenants: errorMessage(e) });
    }
    await this.load();
  }

  /** Tab → service nguồn (mượn trả và phạt cùng một lời gọi circulation). */
  protected source(tab: Tab): string {
    return tab === 'fines' ? 'loans' : tab;
  }

  protected toggle(id: number): void {
    const next = new Set(this.selected());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selected.set(next);
  }

  protected clearSelection(): void {
    this.selected.set(new Set());
  }

  protected selectShown(): void {
    this.selected.set(new Set([...this.selected(), ...this.shownTenants().map((t) => t.id)]));
  }

  protected sortBy(key: string): void {
    this.sort.set(key);
  }

  protected preset(key: (typeof this.presets)[number]['key']): void {
    const now = vietnamToday();
    const [y, m, d] = [now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()];
    const range: Record<typeof key, [Date, Date]> = {
      month: [utc(y, m, 1), now],
      lastMonth: [utc(y, m - 1, 1), utc(y, m, 0)],
      year: [utc(y, 0, 1), now],
      days30: [utc(y, m, d - 29), now],
    };
    [this.from, this.to] = range[key].map(isoDate) as [string, string];
  }

  protected async load(): Promise<void> {
    const body: PlatformStatsRequest = { tenantIds: [...this.selected()], from: this.from || null, to: this.to || null };
    this.loading.set(true);
    this.errors.update((e) => ({ tenants: e['tenants'] ?? null }));
    const fail = (source: string) => (e: unknown) => this.errors.update((x) => ({ ...x, [source]: errorMessage(e) }));
    await Promise.all([
      this.api.platformReaderStats(body).then((r) => this.put('readers', r), fail('readers')),
      this.api.platformCirculationStats(body).then((r) => {
        this.put('loans', r.loans);
        this.put('fines', r.fines);
      }, fail('loans')),
      this.api.platformItemStats(body).then((r) => this.put('items', r), fail('items')),
    ]);
    this.loading.set(false);
  }

  private put(tab: Tab, rows: ({ tenantId: number } & object)[]): void {
    const map = new Map<number, Values>(rows.map((r) => [r.tenantId, r as unknown as Values]));
    this.data.update((d) => ({ ...d, [tab]: map }));
  }

  /** CSV (UTF-8 có BOM để Excel đọc đúng tiếng Việt) của tab đang xem, kèm dòng tổng. */
  protected exportCsv(): void {
    const t = (k: string) => this.language.t(k);
    const cols = this.columns();
    const cell = (v: string | number | undefined) => `"${String(v ?? 0).replace(/"/g, '""')}"`;
    const lines = [
      [t('COMMON.CODE'), t('PLATFORM_STATS.TENANT'), ...cols.map((c) => t(c.label))],
      ...this.rows().map((r) => [r.tenant.code, r.tenant.name, ...cols.map((c) => r.values[c.key] ?? 0)]),
      ['', t('PLATFORM_STATS.TOTAL'), ...cols.map((c) => this.totals()[c.key])],
    ].map((l) => l.map(cell).join(','));
    const period = [this.from, this.to].filter(Boolean).join('_');
    saveFile(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }), `thong-ke-${this.tab()}${period ? '-' + period : ''}.csv`);
  }
}

/** Hôm nay theo giờ Việt Nam, biểu diễn bằng Date UTC 0 giờ (chỉ dùng phần ngày). */
function vietnamToday(): Date {
  const vn = new Date(Date.now() + 7 * 3600_000);
  return utc(vn.getUTCFullYear(), vn.getUTCMonth(), vn.getUTCDate());
}

function utc(y: number, m: number, d: number): Date {
  return new Date(Date.UTC(y, m, d));
}

function isoDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}
