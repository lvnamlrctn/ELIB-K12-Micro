import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AcqLookupRow, AcqReportRequest, AcquisitionOptions, Api, CrudPage, StoreOption, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { AcqReportActions, AcqReportKind } from './acq-report-actions';
import { DateInput } from '../../shared/date-input';

const EMPTY_OPTIONS: AcquisitionOptions = { sources: [], suppliers: [], budgets: [], funds: [] };

/** "Loại báo cáo" của nút "Báo cáo" (ELIB-LRC: ab-receipt-lookup) — mỗi loại in/xuất theo đúng bộ lọc đang nhập. */
type ReportType = 'labels' | 'labelsQty' | 'allocation' | 'list' | 'newbooks' | 'register';

const REPORT_TYPES: { code: ReportType; label: string }[] = [
  { code: 'labels', label: 'ACQ_LOOKUP.RT_LABELS' },
  { code: 'labelsQty', label: 'ACQ_LOOKUP.RT_LABELS_QTY' },
  { code: 'allocation', label: 'ACQ_LOOKUP.RT_ALLOCATION' },
  { code: 'list', label: 'ACQ_LOOKUP.RT_LIST' },
  { code: 'newbooks', label: 'ACQ_LOOKUP.RT_NEWBOOKS' },
  { code: 'register', label: 'ACQ_LOOKUP.RT_REGISTER' },
];

const emptyRequest = (): AcqReportRequest => ({
  from: '', to: '', storeId: null, supplierId: null, sourceId: null, fundId: null, receiptCodeFrom: null, receiptCodeTo: null, receiptName: '',
  completed: null, createdFrom: '', createdTo: '', createdBy: null, mfnFrom: null, mfnTo: null, title: '', author: '', publisher: '', publishYear: '',
  pageIndex: 1, pageSize: 20,
});

/**
 * Tra cứu đơn nhận (ELIB-LRC: /admin/ab-receipts/lookup, quyền AB_RECEIPTS xem): mỗi dòng là một tài liệu của một đơn nhận. Ô chữ/số
 * Enter để tìm, đổi ô chọn (trừ Loại báo cáo) thì tìm ngay. "Mã đơn" mở chi tiết đơn. "Báo cáo" in/xuất theo Loại báo cáo với cùng bộ lọc.
 */
@Component({
  selector: 'app-acq-receipt-lookup',
  imports: [DateInput, FormsModule, RouterLink, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-4 flex items-center justify-between gap-3 flex-wrap">
      <h4 class="page-title !mb-0">{{ 'ACQ_LOOKUP.TITLE' | translate }}</h4>
    </div>

    <div class="panel">
      <button type="button" class="w-full flex items-center justify-between text-left" (click)="collapsed.set(!collapsed())" [attr.aria-expanded]="!collapsed()">
        <span class="font-semibold text-gray-800">{{ 'ACQ_LOOKUP.FILTERS' | translate }}</span>
        <span class="material-icons text-gray-400">{{ collapsed() ? 'expand_more' : 'expand_less' }}</span>
      </button>
      @if (!collapsed()) {
        <div class="flex flex-col lg:flex-row gap-4 mt-3">
          <div class="flex-1 grid grid-cols-2 md:grid-cols-4 gap-3">
            <div><label for="lk-cf" class="field-label">{{ 'ACQ_LOOKUP.CODE_FROM' | translate }}</label><input id="lk-cf" type="number" min="1" class="input" [(ngModel)]="r.receiptCodeFrom" (keydown.enter)="find()" /></div>
            <div><label for="lk-ct" class="field-label">{{ 'ACQ_LOOKUP.TO' | translate }}</label><input id="lk-ct" type="number" min="1" class="input" [(ngModel)]="r.receiptCodeTo" (keydown.enter)="find()" /></div>
            <div><label for="lk-name" class="field-label">{{ 'ACQ_RECEIPT.NAME' | translate }}</label><input id="lk-name" class="input" [(ngModel)]="r.receiptName" (keydown.enter)="find()" /></div>
            <div><label for="lk-ti" class="field-label">{{ 'ACQ_DUP.TITLE' | translate }}</label><input id="lk-ti" class="input" [placeholder]="'ACQ_LOOKUP.TITLE_PH' | translate" [(ngModel)]="r.title" (keydown.enter)="find()" /></div>
            <div><label for="lk-mf" class="field-label">{{ 'ACQ_LOOKUP.MFN_FROM' | translate }}</label><input id="lk-mf" type="number" min="1" class="input" [(ngModel)]="r.mfnFrom" (keydown.enter)="find()" /></div>
            <div><label for="lk-mt" class="field-label">{{ 'ACQ_LOOKUP.TO' | translate }}</label><input id="lk-mt" type="number" min="1" class="input" [(ngModel)]="r.mfnTo" (keydown.enter)="find()" /></div>
            <div><label for="lk-st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
              <select id="lk-st" class="input" [(ngModel)]="r.completed" (ngModelChange)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                <option [ngValue]="false">{{ 'ACQ_RECEIPT.DRAFT' | translate }}</option><option [ngValue]="true">{{ 'ACQ_RECEIPT.COMPLETED' | translate }}</option></select></div>
            <div><label for="lk-au" class="field-label">{{ 'ACQ_DUP.AUTHOR' | translate }}</label><input id="lk-au" class="input" [placeholder]="'ACQ_LOOKUP.AUTHOR_PH' | translate" [(ngModel)]="r.author" (keydown.enter)="find()" /></div>
            <div><label for="lk-rf" class="field-label">{{ 'ACQ_RECEIPT.RECEIVED_FROM' | translate }}</label><app-date-input fieldId="lk-rf" [(ngModel)]="r.from" (keydown.enter)="find()"></app-date-input></div>
            <div><label for="lk-rt" class="field-label">{{ 'ACQ_LOOKUP.RECEIVED_TO' | translate }}</label><app-date-input fieldId="lk-rt" [(ngModel)]="r.to" (keydown.enter)="find()"></app-date-input></div>
            <div><label for="lk-src" class="field-label">{{ 'ACQ.SOURCE' | translate }}</label>
              <select id="lk-src" class="input" [(ngModel)]="r.sourceId" (ngModelChange)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (o of options().sources; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
            <div><label for="lk-yr" class="field-label">{{ 'ACQ_DUP.YEAR_SHORT' | translate }}</label><input id="lk-yr" class="input" maxlength="4" [placeholder]="'ACQ_LOOKUP.YEAR_PH' | translate" [(ngModel)]="r.publishYear" (keydown.enter)="find()" /></div>
            <div><label for="lk-cr" class="field-label">{{ 'ACQ_RECEIPT.CREATED_FROM' | translate }}</label><app-date-input fieldId="lk-cr" [(ngModel)]="r.createdFrom" (keydown.enter)="find()"></app-date-input></div>
            <div><label for="lk-crt" class="field-label">{{ 'ACQ_RECEIPT.CREATED_TO' | translate }}</label><app-date-input fieldId="lk-crt" [(ngModel)]="r.createdTo" (keydown.enter)="find()"></app-date-input></div>
            <div><label for="lk-fund" class="field-label">{{ 'ACQ.FUND' | translate }}</label>
              <select id="lk-fund" class="input" [(ngModel)]="r.fundId" (ngModelChange)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (o of options().funds; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
            <div><label for="lk-pub" class="field-label">{{ 'ACQ_DUP.PUBLISHER' | translate }}</label><input id="lk-pub" class="input" [placeholder]="'ACQ_LOOKUP.PUBLISHER_PH' | translate" [(ngModel)]="r.publisher" (keydown.enter)="find()" /></div>
            <div><label for="lk-sup" class="field-label">{{ 'ACQ.SUPPLIER' | translate }}</label>
              <select id="lk-sup" class="input" [(ngModel)]="r.supplierId" (ngModelChange)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (o of options().suppliers; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }</select></div>
            <div><label for="lk-by" class="field-label">{{ 'ACQ_LOOKUP.CREATED_BY' | translate }}</label>
              <select id="lk-by" class="input" [(ngModel)]="r.createdBy" (ngModelChange)="find()" [disabled]="!users().length"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (u of users(); track u.id) { <option [ngValue]="u.id">{{ u.name }}</option> }</select></div>
            <div><label for="lk-store" class="field-label">{{ 'ACQ_LOOKUP.STORE' | translate }}</label>
              <select id="lk-store" class="input" [(ngModel)]="r.storeId" (ngModelChange)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
            <div><label for="lk-rt2" class="field-label">{{ 'ACQ_LOOKUP.REPORT_TYPE' | translate }}</label>
              <select id="lk-rt2" class="input" [(ngModel)]="reportType">@for (t of reportTypes; track t.code) { <option [ngValue]="t.code">{{ t.label | translate }}</option> }</select></div>
          </div>
          <div class="flex lg:flex-col gap-2 lg:w-40 flex-wrap">
            <button class="btn-primary justify-center" [disabled]="busy()" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
            <button class="btn-secondary justify-center" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
            <button class="btn-secondary justify-center" [disabled]="busy()" (click)="report()"><span class="material-icons text-[18px]">print</span> {{ 'ACQ_LOOKUP.REPORT' | translate }}</button>
            <a routerLink="/ab-receipts" class="btn-secondary justify-center"><span class="material-icons text-[18px]">logout</span> {{ 'ACQ_LOOKUP.EXIT' | translate }}</a>
          </div>
        </div>
      }
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
        <span class="font-semibold text-gray-800">{{ 'ACQ_LOOKUP.RESULTS' | translate }}</span>
        @if (page(); as p) { <span class="badge-info">{{ 'ACQ_LOOKUP.N_RECORDS' | translate: { count: p.totalCount } }}</span> }
      </div>
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-14 !text-center">{{ 'ACQ_LOOKUP.NO' | translate }}</th><th class="th w-24">MFN</th><th class="th w-28">{{ 'ACQ_RECEIPT.CODE' | translate }}</th>
            <th class="th">{{ 'ACQ_LOOKUP.ISBD' | translate }}</th><th class="th w-28">{{ 'ACQ_LOOKUP.CREATED_ON' | translate }}</th>
            <th class="th w-36">{{ 'ACQ.SOURCE' | translate }}</th><th class="th w-40">{{ 'ACQ_LOOKUP.STORE' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (x of page()?.items ?? []; track $index) {
              <tr class="hover:bg-gray-50/50">
                <td class="td text-center text-gray-500">{{ (r.pageIndex - 1) * r.pageSize + $index + 1 }}</td>
                <td class="td font-mono text-[13px]">{{ x.mfn }}</td>
                <td class="td"><a [routerLink]="['/ab-receipts', x.receiptPublicId]" class="font-mono font-medium text-blue-700 hover:underline">#{{ x.receiptId }}</a></td>
                <td class="td">{{ x.isbd }}</td>
                <td class="td">{{ x.createdAt | ldate: 'dd/MM/yyyy' }}</td>
                <td class="td">{{ x.sourceName ?? '' }}</td>
                <td class="td">{{ storeName(x) }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ busy() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="r.pageIndex" [pageSize]="r.pageSize" [sizes]="[10, 20, 50, 100]" (pageChange)="onPage($event)" /> }
    </div>
  `,
})
export class ReceiptLookup implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly actions = inject(AcqReportActions);
  protected readonly session = inject(Session);

  protected readonly reportTypes = REPORT_TYPES;
  protected r: AcqReportRequest = emptyRequest();
  protected reportType: ReportType = 'labels';
  protected readonly collapsed = signal(false);
  protected readonly options = signal<AcquisitionOptions>(EMPTY_OPTIONS);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly users = signal<{ id: number; name: string }[]>([]);
  protected readonly page = signal<CrudPage<AcqLookupRow> | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.acquisitionOptions().then((o) => this.options.set(o), () => undefined);
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
    void this.loadUsers();
  }

  protected find(): void {
    this.r.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.r.pageIndex = e.pageIndex;
    this.r.pageSize = e.pageSize;
    void this.load();
  }

  protected storeName(x: AcqLookupRow): string {
    if (x.storeName) return x.storeName;
    const s = x.storeId === null ? null : this.stores().find((st) => st.id === x.storeId);
    return s ? `${s.code} — ${s.name}` : '';
  }

  protected async export(): Promise<void> {
    await this.act(() => this.actions.export('lookup', this.body()));
  }

  /** "Báo cáo" theo Loại báo cáo: in nhãn môn loại (ĐKCB theo bộ lọc), bản in phân bổ kho / danh mục / sổ ĐKCB, Excel thư mục sách mới. */
  protected async report(): Promise<void> {
    const body = this.body();
    const kinds: Record<Exclude<ReportType, 'labels' | 'labelsQty' | 'newbooks'>, AcqReportKind> = { allocation: 'allocation', list: 'list', register: 'register' };
    await this.act(() => {
      switch (this.reportType) {
        case 'labels': return this.actions.printLabels(body);
        case 'labelsQty': return this.actions.printClassLabels(body);
        case 'newbooks': return this.actions.export('newbooks', body);
        default: return this.actions.print(kinds[this.reportType], body, (x) => this.storeName(x));
      }
    });
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.page.set(await this.api.acqReport<CrudPage<AcqLookupRow>>('LookupLines', this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Người tạo: cán bộ của đơn vị (cần quyền xem người dùng — thiếu quyền thì ô này để trống, các ô khác vẫn lọc được). */
  private async loadUsers(): Promise<void> {
    try {
      const all: { id: number; name: string }[] = [];
      for (let p = 1; p <= 50; p++) {
        const page = await this.api.users(undefined, p);
        all.push(...page.items.map((u) => ({ id: u.id, name: u.fullName || u.userName })));
        if (!page.items.length || all.length >= page.total) break;
      }
      this.users.set(all.sort((a, b) => a.name.localeCompare(b.name, 'vi')));
    } catch {
      this.users.set([]);
    }
  }

  private async act(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private body(): AcqReportRequest {
    const text = (v: string | null) => v?.trim() || null;
    const num = (v: number | null | undefined) => (v === null || v === undefined || (v as unknown) === '' ? null : Number(v));
    const r = this.r;
    return {
      ...r, from: r.from || null, to: r.to || null, createdFrom: r.createdFrom || null, createdTo: r.createdTo || null, receiptName: text(r.receiptName),
      title: text(r.title), author: text(r.author), publisher: text(r.publisher), publishYear: text(r.publishYear),
      receiptCodeFrom: num(r.receiptCodeFrom), receiptCodeTo: num(r.receiptCodeTo), mfnFrom: num(r.mfnFrom), mfnTo: num(r.mfnTo),
    };
  }
}
