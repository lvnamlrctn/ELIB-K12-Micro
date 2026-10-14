import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, ScanOut, StoreOutOptions, StoreOutRow, StoreOutSearch, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Paginator } from '../../shared/ui';

/**
 * Sách ra vào kho (monolith: /admin/book-out-store, quyền BOOK_OUT_STORE): quét ĐKCB xuất kho tạm (photo, triển lãm, cho đơn vị
 * khác mượn…) — bản sách không cho mượn tới khi quét nhập kho. Lịch sử theo bộ lọc + xuất Excel.
 */
@Component({
  selector: 'app-store-outs',
  imports: [FormsModule, LDatePipe, Loading, Paginator, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'STORE_OUT.TITLE' | translate }}</h4></div>

    @if (session.can('BOOK_OUT_STORE:edit')) {
      <div class="panel">
        <div class="flex gap-2 mb-4">
          <button type="button" [class]="mode() === 'out' ? 'btn-primary' : 'btn-secondary'" (click)="mode.set('out')">
            <span class="material-icons text-[18px]">logout</span> {{ 'STORE_OUT.MODE_OUT' | translate }}</button>
          <button type="button" [class]="mode() === 'in' ? 'btn-primary' : 'btn-secondary'" (click)="mode.set('in')">
            <span class="material-icons text-[18px]">login</span> {{ 'STORE_OUT.MODE_IN' | translate }}</button>
        </div>
        <div class="grid grid-cols-1 md:grid-cols-4 gap-3 items-end">
          <div>
            <label for="so-code" class="form-label">{{ 'STORE_OUT.SCAN' | translate }}</label>
            <input id="so-code" class="input font-mono" [(ngModel)]="code" (keydown.enter)="scan()" autocomplete="off" />
          </div>
          @if (mode() === 'out') {
            <div>
              <label for="so-reason" class="form-label">{{ 'STORE_OUT.REASON' | translate }}</label>
              <select id="so-reason" class="input" [(ngModel)]="form.reasonId">
                <option [ngValue]="null">—</option>
                @for (o of options()?.reasons ?? []; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
              </select>
            </div>
            <div>
              <label for="so-unit" class="form-label">{{ 'STORE_OUT.UNIT' | translate }}</label>
              <select id="so-unit" class="input" [(ngModel)]="form.unitId">
                <option [ngValue]="null">—</option>
                @for (o of options()?.units ?? []; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
              </select>
            </div>
            <div>
              <label for="so-loc" class="form-label">{{ 'STORE_OUT.LOCATION' | translate }}</label>
              <select id="so-loc" class="input" [(ngModel)]="form.exhibitionLocationId">
                <option [ngValue]="null">—</option>
                @for (o of options()?.locations ?? []; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
              </select>
            </div>
            <div>
              <label for="so-giver" class="form-label">{{ 'STORE_OUT.DELIVERER' | translate }}</label>
              <input id="so-giver" class="input" maxlength="250" [(ngModel)]="form.delivererName" />
            </div>
            <div>
              <label for="so-taker" class="form-label">{{ 'STORE_OUT.RECEIVER' | translate }}</label>
              <input id="so-taker" class="input" maxlength="250" [(ngModel)]="form.receiverName" />
            </div>
            <label class="flex items-center gap-2 text-sm text-gray-700 pb-2">
              <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="form.returnAtLibrary" /> {{ 'STORE_OUT.RETURN_AT_LIBRARY' | translate }}
            </label>
          }
          <button type="button" class="btn-primary justify-center" [disabled]="busy() || !code.trim()" (click)="scan()">
            <span class="material-icons text-[18px]">qr_code_scanner</span> {{ (mode() === 'out' ? 'STORE_OUT.DO_OUT' : 'STORE_OUT.DO_IN') | translate }}
          </button>
        </div>
        @if (last(); as l) {
          <div class="alert-success !block text-sm mt-3">
            {{ (l.inAt ? 'STORE_OUT.DONE_IN' : 'STORE_OUT.DONE_OUT') | translate: { barcode: l.barcode } }} — {{ l.title ?? '' }}
          </div>
        }
      </div>
    }

    <div class="panel grid grid-cols-1 md:grid-cols-6 gap-3 items-end">
      <div>
        <label for="sf-kw" class="field-label">{{ 'STORE_OUT.NAME_SEARCH' | translate }}</label>
        <input id="sf-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
      </div>
      <div>
        <label for="sf-bc" class="field-label">{{ 'ITEMS_PAGE.BARCODE' | translate }}</label>
        <input id="sf-bc" class="input font-mono" [(ngModel)]="search.barcode" (keydown.enter)="find()" />
      </div>
      <div>
        <label for="sf-open" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
        <select id="sf-open" class="input" [(ngModel)]="search.open" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          <option [ngValue]="true">{{ 'STORE_OUT.OPEN' | translate }}</option>
          <option [ngValue]="false">{{ 'STORE_OUT.CLOSED' | translate }}</option>
        </select>
      </div>
      <div>
        <label for="sf-reason" class="field-label">{{ 'STORE_OUT.REASON' | translate }}</label>
        <select id="sf-reason" class="input" [(ngModel)]="search.reasonId" (change)="find()">
          <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (o of options()?.reasons ?? []; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
        </select>
      </div>
      <div>
        <label for="sf-from" class="field-label">{{ 'STORE_OUT.OUT_FROM' | translate }}</label>
        <input id="sf-from" type="date" class="input" [(ngModel)]="search.outFrom" />
      </div>
      <div>
        <label for="sf-to" class="field-label">{{ 'STORE_OUT.OUT_TO' | translate }}</label>
        <input id="sf-to" type="date" class="input" [(ngModel)]="search.outTo" />
      </div>
      <div class="md:col-span-6 flex justify-end gap-2">
        <button class="btn-secondary" [disabled]="loading()" (click)="export()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-32">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
            <th class="th w-40">{{ 'STORE_OUT.REASON' | translate }}</th><th class="th w-44">{{ 'STORE_OUT.PEOPLE' | translate }}</th>
            <th class="th w-32">{{ 'STORE_OUT.OUT_AT' | translate }}</th><th class="th w-32">{{ 'STORE_OUT.IN_AT' | translate }}</th>
            <th class="th w-16 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (r of page()?.items ?? []; track r.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]">{{ r.barcode }}</td>
                <td class="td">{{ r.title ?? '—' }}<div class="text-[11px] text-gray-400">{{ r.storeName ?? '' }}</div></td>
                <td class="td text-[13px]">{{ r.reasonName ?? '—' }}<div class="text-[11px] text-gray-500">{{ r.unitName ?? '' }} {{ r.exhibitionLocationName ?? '' }}</div></td>
                <td class="td text-[13px]">{{ r.delivererName ?? '' }}@if (r.receiverName) { → {{ r.receiverName }} }</td>
                <td class="td text-[13px]">{{ r.outAt ? (r.outAt | ldate: 'dd/MM/yyyy HH:mm') : '—' }}</td>
                <td class="td text-[13px]">@if (r.inAt) { {{ r.inAt | ldate: 'dd/MM/yyyy HH:mm' }} } @else { <span class="badge-info">{{ 'STORE_OUT.OPEN' | translate }}</span> }</td>
                <td class="td text-center">
                  @if (session.can('BOOK_OUT_STORE:delete')) {
                    <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(r)">
                      <span class="material-icons text-[18px]">delete</span></button>
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
    @if (deleting(); as r) {
      <app-confirm-delete [busy]="busy()" message="STORE_OUT.DELETE_CONFIRM" [messageParams]="{ barcode: r.barcode }" (confirmed)="remove(r)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class StoreOuts implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly mode = signal<'out' | 'in'>('out');
  protected code = '';
  protected form: Omit<ScanOut, 'barcode'> = {
    reasonId: null, unitId: null, exhibitionLocationId: null, delivererName: null, receiverName: null, returnAtLibrary: false,
  };
  protected search: StoreOutSearch = { keyword: '', barcode: '', open: null, reasonId: null, outFrom: '', outTo: '', pageIndex: 1, pageSize: 10 };
  protected readonly options = signal<StoreOutOptions | null>(null);
  protected readonly page = signal<CrudPage<StoreOutRow> | null>(null);
  protected readonly last = signal<StoreOutRow | null>(null);
  protected readonly deleting = signal<StoreOutRow | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOutOptions().then((o) => this.options.set(o), () => undefined);
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

  protected async scan(): Promise<void> {
    const barcode = this.code.trim();
    if (!barcode) return;
    this.busy.set(true);
    try {
      this.last.set(this.mode() === 'out'
        ? await this.api.scanOut({ ...this.form, barcode, delivererName: this.form.delivererName?.trim() || null, receiverName: this.form.receiverName?.trim() || null })
        : await this.api.scanIn(barcode));
      this.code = '';
      await this.load();
    } catch (e) {
      this.last.set(null);
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(r: StoreOutRow): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.deleteStoreOut(r.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
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
      saveFile(await this.api.exportStoreOuts(this.body()), 'sach-ra-vao-kho.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private body(): StoreOutSearch {
    const s = this.search;
    return { ...s, keyword: s.keyword?.trim() || undefined, barcode: s.barcode?.trim() || null, outFrom: s.outFrom || null, outTo: s.outTo || null };
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.api.searchStoreOuts(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
