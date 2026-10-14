import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CircPlace, CrudPage, Loan, LoanSearch, Store, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { clampPage, rememberSearch, rememberedSearch } from '../../core/list-state';
import { columnPrefs } from '../../core/preferences';
import { ColumnPicker } from '../../shared/column-picker';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

type LoanColumn = 'store' | 'loaned' | 'loanedBy' | 'due' | 'returned' | 'returnedBy' | 'renewals';
const COLUMNS: { key: LoanColumn; label: string; shown: boolean }[] = [
  { key: 'store', label: 'LOAN_HISTORY_PAGE.STORE', shown: false },
  { key: 'loaned', label: 'LOAN_HISTORY_PAGE.LOANED', shown: true },
  { key: 'loanedBy', label: 'LOAN_HISTORY_PAGE.LOANED_BY', shown: true },
  { key: 'due', label: 'LOAN_HISTORY_PAGE.DUE', shown: true },
  { key: 'returned', label: 'LOAN_HISTORY_PAGE.RETURNED_AT', shown: true },
  { key: 'returnedBy', label: 'LOAN_HISTORY_PAGE.RETURNED_BY', shown: true },
  { key: 'renewals', label: 'LOAN_HISTORY_PAGE.RENEWALS', shown: true },
];

/**
 * Lịch sử lưu thông (monolith: /admin/loan-history, quyền LOAN_HISTORY) như ELIB-LRC: 4 trạng thái (đang mượn / quá hạn / trả đúng hạn /
 * trả quá hạn), lọc theo điểm lưu thông và kho, người cho mượn / nhận trả; cột hiện/ẩn nhớ theo tài khoản.
 */
@Component({
  selector: 'app-loan-history',
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe, ColumnPicker],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'LOAN_HISTORY_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-7 gap-4">
        <div class="md:col-span-2">
          <label for="lh-kw" class="field-label">{{ 'LOAN_HISTORY_PAGE.SEARCH_LABEL' | translate }}</label>
          <input id="lh-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="lh-state" class="field-label">{{ 'LOAN_HISTORY_PAGE.STATE' | translate }}</label>
          <select id="lh-state" class="input" [(ngModel)]="search.state" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option value="borrowing">{{ 'LOAN_HISTORY_PAGE.BORROWING' | translate }}</option>
            <option value="overdue">{{ 'LOAN_HISTORY_PAGE.OVERDUE' | translate }}</option>
            <option value="returned_ontime">{{ 'LOAN_HISTORY_PAGE.RETURNED_ONTIME' | translate }}</option>
            <option value="returned_late">{{ 'LOAN_HISTORY_PAGE.RETURNED_LATE' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="lh-place" class="field-label">{{ 'LOAN_HISTORY_PAGE.PLACE' | translate }}</label>
          <select id="lh-place" class="input" [(ngModel)]="search.circPlaceId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div>
          <label for="lh-store" class="field-label">{{ 'LOAN_HISTORY_PAGE.STORE' | translate }}</label>
          <select id="lh-store" class="input" [(ngModel)]="search.storeId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (st of stores(); track st.id) { <option [ngValue]="st.id">{{ st.code }} — {{ st.name }}</option> }
          </select>
        </div>
        <div>
          <label for="lh-from" class="field-label">{{ 'LOAN_HISTORY_PAGE.FROM' | translate }}</label>
          <input id="lh-from" type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
        <div>
          <label for="lh-to" class="field-label">{{ 'LOAN_HISTORY_PAGE.TO' | translate }}</label>
          <input id="lh-to" type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
      <div class="flex justify-end gap-2 mt-4">
        <button (click)="export()" class="btn-secondary" [disabled]="exporting()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="panel flex justify-end items-center gap-2">
      <span class="text-sm text-gray-500">{{ 'LOAN_HISTORY_PAGE.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span>
      <app-column-picker [prefs]="cols" />
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-44">{{ 'LOAN_HISTORY_PAGE.READER' | translate }}</th><th class="th w-32">{{ 'LOAN_HISTORY_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'LOAN_HISTORY_PAGE.TITLE_COL' | translate }}</th>
            @if (cols.has('store')) { <th class="th w-32">{{ 'LOAN_HISTORY_PAGE.STORE' | translate }}</th> }
            @if (cols.has('loaned')) { <th class="th w-32">{{ 'LOAN_HISTORY_PAGE.LOANED' | translate }}</th> }
            @if (cols.has('loanedBy')) { <th class="th w-40">{{ 'LOAN_HISTORY_PAGE.LOANED_BY' | translate }}</th> }
            @if (cols.has('due')) { <th class="th w-28">{{ 'LOAN_HISTORY_PAGE.DUE' | translate }}</th> }
            @if (cols.has('returned')) { <th class="th w-36">{{ 'LOAN_HISTORY_PAGE.RETURNED_AT' | translate }}</th> }
            @if (cols.has('returnedBy')) { <th class="th w-40">{{ 'LOAN_HISTORY_PAGE.RETURNED_BY' | translate }}</th> }
            @if (cols.has('renewals')) { <th class="th w-20 !text-center">{{ 'LOAN_HISTORY_PAGE.RENEWALS' | translate }}</th> }
          </tr></thead>
          <tbody>
            @for (l of page()?.items ?? []; track l.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ l.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ l.cardNo }}</div></td>
                <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                <td class="td">{{ l.title ?? '—' }}@if (l.note) { <div class="text-[11px] text-amber-700">{{ l.note }}</div> }</td>
                @if (cols.has('store')) { <td class="td text-[13px]">{{ l.storeName ?? '—' }}</td> }
                @if (cols.has('loaned')) { <td class="td text-[13px]">{{ l.loanedAt | ldate: 'dd/MM/yyyy HH:mm' }}</td> }
                @if (cols.has('loanedBy')) { <td class="td text-[13px]">{{ l.loanedByName ?? '—' }}</td> }
                @if (cols.has('due')) { <td class="td text-[13px]" [class.text-red-600]="overdue(l)" [class.font-semibold]="overdue(l)">{{ l.dueAt | ldate: 'dd/MM/yyyy' }}</td> }
                @if (cols.has('returned')) {
                  <td class="td text-[13px]">
                    @if (l.returnedAt) { {{ l.returnedAt | ldate: 'dd/MM/yyyy HH:mm' }} }
                    <div><span [class]="badge(l)">{{ stateLabel(l) | translate }}</span></div>
                  </td>
                }
                @if (cols.has('returnedBy')) { <td class="td text-[13px]">{{ l.returnedByName ?? '—' }}</td> }
                @if (cols.has('renewals')) { <td class="td text-center">{{ l.renewCount }}</td> }
              </tr>
            } @empty {
              <tr><td [attr.colspan]="cols.visible().length + 3" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('LOAN_HISTORY_PAGE.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class LoanHistory implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected search: LoanSearch = rememberedSearch<LoanSearch>('loan-history',
    { keyword: '', state: null, circPlaceId: null, storeId: null, from: null, to: null, pageIndex: 1, pageSize: 10 });
  protected readonly cols = columnPrefs<LoanColumn>('loan-history', COLUMNS);
  protected readonly stores = signal<Store[]>([]);
  protected readonly page = signal<CrudPage<Loan> | null>(null);
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly loading = signal(false);
  protected readonly exporting = signal(false);

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.places.set(await this.api.crud<CircPlace>('circ-places', 'circulation').searchAll());
    } catch {
      /* thiếu quyền xem điểm lưu thông — chỉ mất bộ lọc điểm */
    }
    try {
      this.stores.set(await this.api.crud<Store>('stores', 'holdings').searchAll());
    } catch {
      /* không có phân hệ kho hoặc thiếu quyền — chỉ mất bộ lọc kho */
    }
  }

  protected overdue(l: Loan): boolean {
    return !l.returnedAt && new Date(l.dueAt).getTime() < Date.now();
  }

  /** 4 trạng thái như ELIB-LRC. */
  protected stateLabel(l: Loan): string {
    if (l.returnedAt) return l.returnedLate ? 'LOAN_HISTORY_PAGE.RETURNED_LATE' : 'LOAN_HISTORY_PAGE.RETURNED_ONTIME';
    return this.overdue(l) ? 'LOAN_HISTORY_PAGE.OVERDUE' : 'LOAN_HISTORY_PAGE.BORROWING';
  }

  protected badge(l: Loan): string {
    if (l.returnedAt) return l.returnedLate ? 'badge-warn' : 'badge-on';
    return this.overdue(l) ? 'badge-off !bg-red-50 !text-red-700' : 'badge-info';
  }

  protected find(): void {
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected async export(): Promise<void> {
    this.exporting.set(true);
    try {
      const s = this.search;
      saveFile(await this.api.exportLoans({ ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null }), 'lich-su-luu-thong.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = this.search;
      const page = await this.api.searchLoans({ ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null });
      if (clampPage(s, page)) return void (await this.load());
      this.page.set(page);
      rememberSearch('loan-history', s);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
