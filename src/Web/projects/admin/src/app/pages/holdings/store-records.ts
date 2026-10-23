import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, ItemBatchResult, LiquidationRow, LostRow, StoreOption, StoreRecordSearch, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { itemStatus } from './items';
import { DateInput } from '../../shared/date-input';

type Kind = 'lost' | 'liquidation';
type Row = (LostRow | LiquidationRow) & { date: string; cardNo?: string | null; decisionNo?: string | null };

/** Chữ và quyền khác nhau giữa hai màn — khoá viết thẳng để công cụ kiểm tra đa ngữ thấy. */
const KINDS = {
  lost: {
    title: 'STORE_RECORD.LOST_TITLE', act: 'STORE_RECORD.LOST_ACT', actN: 'STORE_RECORD.LOST_ACT_N', date: 'STORE_RECORD.LOST_ON',
    undo: 'STORE_RECORD.RESTORE', undone: 'STORE_RECORD.RESTORED', done: 'STORE_RECORD.LOST_DONE', perm: 'LOST_BOOKS', undoStatus: 'L', file: 'sach-mat.xlsx',
  },
  liquidation: {
    title: 'STORE_RECORD.LIQUIDATE_TITLE', act: 'STORE_RECORD.LIQUIDATE_ACT', actN: 'STORE_RECORD.LIQUIDATE_ACT_N', date: 'STORE_RECORD.LIQUIDATED_ON',
    undo: 'STORE_RECORD.CANCEL_LIQUIDATION', undone: 'STORE_RECORD.CANCELLED', done: 'STORE_RECORD.LIQUIDATE_DONE', perm: 'LIQUIDATES', undoStatus: 'S',
    file: 'thanh-ly.xlsx',
  },
} as const;

/**
 * Xử lý sách mất (monolith: /admin/lost-books, quyền LOST_BOOKS) và thanh lý (/admin/liquidates, quyền LIQUIDATES): quét/dán danh sách
 * ĐKCB để báo mất / thanh lý theo lô, danh sách theo bộ lọc + xuất Excel, khôi phục / huỷ thanh lý từng bản.
 */
@Component({
  selector: 'app-store-records',
  imports: [DateInput, FormsModule, LDatePipe, Loading, Paginator, ReasonDialog, TranslatePipe],
  template: `
    @let k = text();
    <div class="mb-5"><h4 class="page-title">{{ k.title | translate }}</h4></div>

    <div class="grid grid-cols-1 xl:grid-cols-4 gap-5">
      @if (session.can(k.perm + ':add')) {
        <div class="panel !mb-0 xl:col-span-1 self-start">
          <label for="sr-codes" class="form-label">{{ 'STORE_RECORD.CODES' | translate }}</label>
          <textarea id="sr-codes" rows="8" class="input font-mono text-[13px]" [(ngModel)]="codes" placeholder="VV000001&#10;VV000002"></textarea>
          <label for="sr-date" class="form-label mt-3">{{ k.date | translate }}</label>
          <app-date-input fieldId="sr-date" [(ngModel)]="actDate"></app-date-input>
          <label for="sr-reason" class="form-label mt-3">{{ 'STORE_RECORD.REASON' | translate }} <span class="text-red-600">*</span></label>
          <input id="sr-reason" class="input" maxlength="500" [(ngModel)]="reason" />
          @if (kind() !== 'lost') {
            <div class="grid grid-cols-2 gap-2 mt-3">
              <div><label for="sr-dno" class="form-label">{{ 'STORE_RECORD.DECISION_NO' | translate }}</label><input id="sr-dno" class="input" maxlength="100" [(ngModel)]="decisionNo" /></div>
              <div><label for="sr-ddate" class="form-label">{{ 'STORE_RECORD.DECISION_DATE' | translate }}</label><app-date-input fieldId="sr-ddate" [(ngModel)]="decisionDate"></app-date-input></div>
            </div>
          }
          <button type="button" class="btn-primary w-full justify-center mt-4" [disabled]="busy() || !codeList().length || !reason.trim()" (click)="act()">
            <span class="material-icons text-[18px]">{{ kind() === 'lost' ? 'report' : 'delete_sweep' }}</span>
            {{ k.actN | translate: { count: codeList().length } }}
          </button>
          @if (result(); as r) {
            <div class="mt-4 text-sm space-y-2">
              <div class="alert-success !block">{{ k.done | translate: { count: r.done } }}</div>
              @if (r.notFound.length) { <div class="alert-error !block">{{ 'STORE_RECORD.NOT_FOUND' | translate }} <span class="font-mono text-xs">{{ r.notFound.join(', ') }}</span></div> }
              @for (s of r.skipped; track s.barcode) { <div class="alert-error !block text-xs"><b class="font-mono">{{ s.barcode }}</b>: {{ s.reason }}</div> }
            </div>
          }
        </div>
      }

      <div [class]="session.can(k.perm + ':add') ? 'xl:col-span-3' : 'xl:col-span-4'">
        <div class="panel grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
          <div class="md:col-span-2">
            <label for="sr-kw" class="field-label">{{ 'STORE_RECORD.SEARCH_LABEL' | translate }}</label>
            <input id="sr-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
          </div>
          <div>
            <label for="sr-store" class="field-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
            <select id="sr-store" class="input" [(ngModel)]="search.storeId" (change)="find()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
          <div>
            <label for="sr-from" class="field-label">{{ 'COMMON.FROM' | translate }}</label>
            <app-date-input fieldId="sr-from" [(ngModel)]="search.from"></app-date-input>
          </div>
          <div>
            <label for="sr-to" class="field-label">{{ 'COMMON.TO' | translate }}</label>
            <app-date-input fieldId="sr-to" [(ngModel)]="search.to"></app-date-input>
          </div>
          <div class="md:col-span-5 flex justify-end gap-2">
            <button class="btn-secondary" [disabled]="loading()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
            <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
          </div>
        </div>

        <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
          @if (loading()) { <app-loading /> }
          <div class="overflow-x-auto w-full">
            <table class="w-full text-left border-collapse min-w-[860px]">
              <thead><tr>
                <th class="th w-32">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
                <th class="th w-36">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-28">{{ k.date | translate }}</th>
                <th class="th w-48">{{ 'STORE_RECORD.REASON' | translate }}</th><th class="th w-28 !text-center">{{ 'STORE_RECORD.CURRENT' | translate }}</th>
                <th class="th w-20 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of rows(); track r.publicId) {
                  <tr class="hover:bg-gray-50/50">
                    <td class="td font-mono text-[13px]">{{ r.barcode }}</td>
                    <td class="td">{{ r.title ?? '—' }}@if (r.mfn) { <span class="text-[11px] text-gray-400"> MFN {{ r.mfn }}</span> }
                      @if (r.cardNo) { <div class="text-[11px] text-gray-500">{{ 'STORE_RECORD.CARD' | translate: { card: r.cardNo } }}</div> }</td>
                    <td class="td">{{ r.storeName ?? '—' }}</td>
                    <td class="td">{{ r.date | ldate: 'dd/MM/yyyy' }}</td>
                    <td class="td text-[13px] text-gray-600">{{ r.reason ?? '' }}@if (r.decisionNo) { <div class="text-[11px] text-gray-500">{{ 'STORE_RECORD.DECISION_SHORT' | translate: { no: r.decisionNo } }}</div> }</td>
                    <td class="td text-center">
                      @if (r.itemStatus) { <span [class]="status(r.itemStatus).badge">{{ status(r.itemStatus).name | translate }}</span> }
                      @else { <span class="text-xs text-gray-400">{{ 'STORE_RECORD.ITEM_DELETED' | translate }}</span> }
                    </td>
                    <td class="td text-center">
                      @if (session.can(k.perm + ':edit') && r.itemStatus === k.undoStatus) {
                        <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [disabled]="busy()" [title]="k.undo | translate" (click)="undoing.set(r)">
                          <span class="material-icons text-[18px]">undo</span></button>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          @if (page(); as p) {
            <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
          }
        </div>
      </div>
    </div>

    @if (undoing(); as u) {
      <app-reason-dialog [title]="kind() === 'lost' ? 'STORE_RECORD.RESTORE_TITLE' : 'STORE_RECORD.CANCEL_TITLE'" [message]="kind() === 'lost' ? 'STORE_RECORD.RESTORE_MSG' : 'STORE_RECORD.CANCEL_MSG'"
                         [params]="{ barcode: u.barcode }" (confirmed)="undo(u, $event)" (cancelled)="undoing.set(null)" />
    }
  `,
})
export class StoreRecords implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  protected readonly kind = signal<Kind>((inject(ActivatedRoute).snapshot.data['kind'] as Kind) ?? 'lost');
  protected readonly text = computed(() => KINDS[this.kind()]);

  protected codes = '';
  protected actDate = '';
  protected reason = '';
  protected decisionNo = '';
  protected decisionDate = '';
  protected readonly undoing = signal<Row | null>(null);
  protected search: StoreRecordSearch = { keyword: '', storeId: null, from: '', to: '', pageIndex: 1, pageSize: 10 };
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly page = signal<CrudPage<LostRow | LiquidationRow> | null>(null);
  protected readonly rows = computed<Row[]>(() => (this.page()?.items ?? []).map((r) =>
    ({ ...r, date: 'lostOn' in r ? r.lostOn : r.liquidatedOn })));
  protected readonly result = signal<ItemBatchResult | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected status(code: string) {
    return itemStatus(code);
  }

  protected codeList(): string[] {
    return [...new Set(this.codes.split(/[\s,;]+/).map((c) => c.trim()).filter((c) => c))];
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

  protected async act(): Promise<void> {
    this.busy.set(true);
    try {
      const body = { barcodes: this.codeList(), reason: this.reason.trim() };
      const o = this.kind() === 'lost'
        ? await this.api.markLost({ ...body, lostOn: this.actDate || null })
        : await this.api.liquidate({ ...body, liquidatedOn: this.actDate || null, decisionNo: this.decisionNo.trim() || null, decisionDate: this.decisionDate || null });
      this.result.set(o.result);
      if (o.pending) this.toastr.info('FINE_TICKET_PAGE.PENDING_SENT');
      else if (o.result && o.result.done > 0) this.toastr.success(this.text().done, { count: o.result.done });
      this.codes = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async undo(r: Row, input: ReasonResult): Promise<void> {
    this.undoing.set(null);
    this.busy.set(true);
    try {
      const pending = this.kind() === 'lost' ? await this.api.restoreLost(r.barcode, input.reason) : await this.api.cancelLiquidation(r.barcode, input.reason);
      if (pending) this.toastr.info('FINE_TICKET_PAGE.PENDING_SENT');
      else this.toastr.success(this.text().undone, { barcode: r.barcode });
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async export(): Promise<void> {
    this.loading.set(true);
    try {
      const body = this.body();
      saveFile(await (this.kind() === 'lost' ? this.api.exportLost(body) : this.api.exportLiquidations(body)), this.text().file);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private body(): StoreRecordSearch {
    const s = this.search;
    return { ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null };
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(this.kind() === 'lost' ? await this.api.searchLost(this.body()) : await this.api.searchLiquidations(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
