import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, CrudClient, CrudPage, Inventory, InventoryImportResult, InventoryScanRow, InventorySummary, MissingRow, StoreOption, errorMessage, saveFile,
} from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { itemStatus } from './items';

/** Đợt kiểm kê (monolith: /admin/inventory, quyền INVENTORY): thêm/sửa/kết thúc/xoá đợt, mở đợt để quét. */
@Component({
  selector: 'app-inventories',
  imports: [FormsModule, RouterLink, LDatePipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ 'INVENTORY_PAGE.TITLE' | translate }}</h4>
      @if (session.can('INVENTORY:add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'INVENTORY_PAGE.ADD' | translate }}</button>
      }
    </div>
    <div class="panel flex gap-3 items-end">
      <div class="flex-1">
        <label for="iv-kw" class="field-label">{{ 'INVENTORY_PAGE.NAME' | translate }}</label>
        <input id="iv-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" />
      </div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th">{{ 'INVENTORY_PAGE.NAME' | translate }}</th><th class="th w-32">{{ 'INVENTORY_PAGE.STARTED_ON' | translate }}</th>
          <th class="th w-44">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-24 !text-right">{{ 'INVENTORY_PAGE.SCANNED' | translate }}</th>
          <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-36 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
        </tr></thead>
        <tbody>
          @for (it of page()?.items ?? []; track it.publicId) {
            <tr class="hover:bg-gray-50/50">
              <td class="td"><a [routerLink]="['/inventory', it.publicId]" class="font-medium text-blue-700 hover:underline">{{ it.name }}</a>
                @if (it.note) { <div class="text-[11px] text-gray-500">{{ it.note }}</div> }</td>
              <td class="td">{{ it.startedOn | ldate: 'dd/MM/yyyy' }}</td>
              <td class="td">{{ it.storeName ?? ('INVENTORY_PAGE.SCANNED_STORES' | translate) }}</td>
              <td class="td text-right">{{ it.scanCount }}</td>
              <td class="td text-center">
                <span [class]="it.closedAt ? 'badge-off' : 'badge-on'">{{ (it.closedAt ? 'INVENTORY_PAGE.CLOSED' : 'INVENTORY_PAGE.OPEN') | translate }}</span>
              </td>
              <td class="td">
                <div class="flex items-center justify-center gap-1.5">
                  @if (session.can('INVENTORY:edit')) {
                    <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(it)"><span class="material-icons text-[18px]">edit</span></button>
                    <button class="icon-btn bg-amber-50 text-amber-700 hover:bg-amber-100" [disabled]="busy()"
                            [title]="(it.closedAt ? 'INVENTORY_PAGE.REOPEN' : 'INVENTORY_PAGE.CLOSE') | translate" (click)="toggle(it)">
                      <span class="material-icons text-[18px]">{{ it.closedAt ? 'lock_open' : 'lock' }}</span></button>
                  }
                  @if (session.can('INVENTORY:delete')) {
                    <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(it)"><span class="material-icons text-[18px]">delete</span></button>
                  }
                </div>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
          }
        </tbody>
      </table>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (editing(); as e) {
      <app-modal [title]="(e.publicId ? 'INVENTORY_PAGE.EDIT' : 'INVENTORY_PAGE.ADD') | translate" (closed)="editing.set(null)">
        <div class="space-y-4">
          <div><label for="ie-name" class="form-label">{{ 'INVENTORY_PAGE.NAME' | translate }} *</label><input id="ie-name" class="input" maxlength="250" [(ngModel)]="e.name" /></div>
          <div><label for="ie-date" class="form-label">{{ 'INVENTORY_PAGE.STARTED_ON' | translate }}</label><input id="ie-date" type="date" class="input" [(ngModel)]="e.startedOn" /></div>
          <div>
            <label for="ie-store" class="form-label">{{ 'INVENTORY_PAGE.STORE' | translate }}</label>
            <select id="ie-store" class="input" [(ngModel)]="e.storeId">
              <option [ngValue]="null">{{ 'INVENTORY_PAGE.SCANNED_STORES' | translate }}</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
            <p class="text-[11px] text-gray-500 mt-1">{{ 'INVENTORY_PAGE.STORE_HINT' | translate }}</p>
          </div>
          <div><label for="ie-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="ie-note" class="input" maxlength="500" [(ngModel)]="e.note" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="editing.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !e.name.trim()" (click)="save(e)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as it) {
      <app-confirm-delete [busy]="busy()" message="INVENTORY_PAGE.DELETE_CONFIRM" [messageParams]="{ name: it.name }" (confirmed)="remove(it)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Inventories implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Inventory> = this.api.crud<Inventory>('inventories', 'holdings');
  protected readonly session = inject(Session);

  protected keyword = '';
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<Inventory> | null>(null);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly editing = signal<{ publicId: string | null; name: string; startedOn: string; storeId: number | null; note: string } | null>(null);
  protected readonly deleting = signal<Inventory | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected find(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected edit(it: Inventory | null): void {
    this.editing.set(it
      ? { publicId: it.publicId, name: it.name, startedOn: it.startedOn, storeId: it.storeId, note: it.note ?? '' }
      : { publicId: null, name: '', startedOn: '', storeId: null, note: '' });
  }

  protected async save(e: { publicId: string | null; name: string; startedOn: string; storeId: number | null; note: string }): Promise<void> {
    this.busy.set(true);
    try {
      const body = { name: e.name.trim(), startedOn: e.startedOn || null, storeId: e.storeId, note: e.note.trim() || null };
      if (e.publicId) await this.client.update(e.publicId, body);
      else await this.client.add(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.editing.set(null);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async toggle(it: Inventory): Promise<void> {
    this.busy.set(true);
    try {
      if (it.closedAt) await this.api.reopenInventory(it.publicId);
      else await this.api.closeInventory(it.publicId);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(it: Inventory): Promise<void> {
    this.busy.set(true);
    try {
      await this.client.delete(it.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ keyword: this.keyword.trim() || undefined, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}

const FILTERS: { value: string | null; label: string }[] = [
  { value: null, label: 'INVENTORY_PAGE.F_ALL' },
  { value: 'wrong-store', label: 'INVENTORY_PAGE.F_WRONG_STORE' },
  { value: 'on-loan', label: 'INVENTORY_PAGE.F_ON_LOAN' },
  { value: 'unregistered', label: 'INVENTORY_PAGE.F_UNREGISTERED' },
  { value: 'abnormal', label: 'INVENTORY_PAGE.F_ABNORMAL' },
];

/**
 * Một đợt kiểm kê: quét từng mã (máy quét đọc như bàn phím + Enter) hoặc nhập Excel, tổng hợp đối chiếu, danh sách đã quét theo bộ
 * lọc, bản thiếu (nghi mất) — báo mất hàng loạt nếu có quyền LOST_BOOKS — và báo cáo Excel.
 */
@Component({
  selector: 'app-inventory-detail',
  imports: [FormsModule, RouterLink, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5 flex flex-wrap gap-3 justify-between items-center">
      <div>
        <a routerLink="/inventory" class="text-sm text-blue-600 hover:underline">← {{ 'INVENTORY_PAGE.TITLE' | translate }}</a>
        <h4 class="page-title">{{ inventory()?.name ?? '' }}</h4>
        @if (inventory(); as iv) {
          <div class="text-sm text-gray-500">{{ iv.startedOn | ldate: 'dd/MM/yyyy' }} · {{ iv.storeName ?? ('INVENTORY_PAGE.SCANNED_STORES' | translate) }}
            · <span [class]="iv.closedAt ? 'badge-off' : 'badge-on'">{{ (iv.closedAt ? 'INVENTORY_PAGE.CLOSED' : 'INVENTORY_PAGE.OPEN') | translate }}</span></div>
        }
      </div>
      <button class="btn-secondary" [disabled]="busy()" (click)="report()"><span class="material-icons text-[18px]">download</span> {{ 'INVENTORY_PAGE.REPORT' | translate }}</button>
    </div>

    @if (summary(); as s) {
      <div class="grid grid-cols-2 md:grid-cols-6 gap-3 mb-5">
        @for (c of cards(s); track c.label) {
          <button type="button" class="panel !mb-0 text-left hover:ring-2 hover:ring-blue-200" [class.ring-2]="c.filter !== undefined && filter() === c.filter" (click)="c.filter !== undefined ? setFilter(c.filter) : showMissing()">
            <div class="text-2xl font-semibold" [class]="c.tone">{{ c.value }}</div>
            <div class="text-xs text-gray-500">{{ c.label | translate }}</div>
          </button>
        }
      </div>
    }

    @if (session.can('INVENTORY:add') && inventory() && !inventory()!.closedAt) {
      <div class="panel grid grid-cols-1 md:grid-cols-4 gap-3 items-end">
        <div class="md:col-span-2">
          <label for="iv-scan" class="form-label">{{ 'INVENTORY_PAGE.SCAN' | translate }}</label>
          <input id="iv-scan" class="input font-mono" [(ngModel)]="code" (keydown.enter)="scan()" autocomplete="off" />
        </div>
        <div>
          <label for="iv-store" class="form-label">{{ 'INVENTORY_PAGE.SCAN_STORE' | translate }}</label>
          <select id="iv-store" class="input" [(ngModel)]="scanStoreId">
            <option [ngValue]="null">{{ 'INVENTORY_PAGE.SCAN_STORE_DEFAULT' | translate }}</option>
            @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
          </select>
        </div>
        <div class="flex gap-2">
          <label class="btn-secondary cursor-pointer justify-center flex-1">
            <span class="material-icons text-[18px]">upload_file</span> {{ 'INVENTORY_PAGE.IMPORT' | translate }}
            <input type="file" accept=".xlsx" class="hidden" (change)="importFile($event)" />
          </label>
          <button type="button" class="btn-secondary" [title]="'IMPORT.TEMPLATE_FILE' | translate" (click)="template()"><span class="material-icons text-[18px]">description</span></button>
        </div>
        @if (lastScan(); as l) {
          <div class="md:col-span-4 text-sm" [class]="l.scan.registered && !l.scan.wrongStore && !l.scan.abnormal && !l.scan.onLoan ? 'alert-success !block' : 'alert-error !block'">
            <b class="font-mono">{{ l.scan.barcode }}</b> {{ l.scan.title ?? ('INVENTORY_PAGE.UNREGISTERED' | translate) }}
            @if (l.alreadyScanned) { — {{ 'INVENTORY_PAGE.ALREADY' | translate }} }
            @if (l.scan.wrongStore) { — {{ 'INVENTORY_PAGE.WRONG_STORE_AT' | translate: { store: l.scan.storeName ?? '' } }} }
            @if (l.scan.onLoan) { — {{ 'INVENTORY_PAGE.F_ON_LOAN' | translate }} }
            @if (l.scan.abnormal && l.scan.itemStatus) { — {{ status(l.scan.itemStatus).name | translate }} }
          </div>
        }
        @if (imported(); as r) {
          <div class="md:col-span-4 text-sm" [class]="r.errors.length ? 'alert-error !block' : 'alert-success !block'">
            @if (r.errors.length) {
              @for (e of r.errors; track e.row) { <div>{{ 'HOLDINGS_COMMON.ROW_ERROR' | translate: { row: e.row, message: e.message } }}</div> }
            } @else { {{ 'INVENTORY_PAGE.IMPORTED' | translate: { scanned: r.scanned, already: r.alreadyScanned, unregistered: r.unregistered } }} }
          </div>
        }
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 flex flex-wrap gap-2 items-center">
        @for (f of filters; track f.label) {
          <button type="button" [class]="!missingMode() && filter() === f.value ? 'btn-primary !py-1' : 'btn-secondary !py-1'" (click)="setFilter(f.value)">{{ f.label | translate }}</button>
        }
        <button type="button" [class]="missingMode() ? 'btn-primary !py-1' : 'btn-secondary !py-1'" (click)="showMissing()">{{ 'INVENTORY_PAGE.MISSING' | translate }}</button>
        @if (missingMode() && session.can('LOST_BOOKS:add') && (missing()?.items?.length ?? 0) > 0) {
          <button type="button" class="btn-danger !py-1 ml-auto" [disabled]="busy()" (click)="markMissingLost()">
            <span class="material-icons text-[18px]">report</span> {{ 'INVENTORY_PAGE.MARK_LOST' | translate: { count: missing()!.items.length } }}</button>
        }
      </div>
      <div class="overflow-x-auto w-full">
        @if (!missingMode()) {
          <table class="w-full text-left border-collapse min-w-[800px]">
            <thead><tr>
              <th class="th w-36">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-40">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-64">{{ 'INVENTORY_PAGE.CHECK' | translate }}</th>
              <th class="th w-32">{{ 'INVENTORY_PAGE.SCANNED_AT' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (r of scans()?.items ?? []; track r.publicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td font-mono text-[13px]">{{ r.barcode }}</td>
                  <td class="td">{{ r.registered ? (r.title ?? '—') : ('INVENTORY_PAGE.UNREGISTERED' | translate) }}</td>
                  <td class="td">{{ r.storeName ?? '—' }}</td>
                  <td class="td text-xs space-x-1">
                    @if (r.wrongStore) { <span class="badge-off">{{ 'INVENTORY_PAGE.F_WRONG_STORE' | translate }}</span> }
                    @if (r.onLoan) { <span class="badge-info">{{ 'INVENTORY_PAGE.F_ON_LOAN' | translate }}</span> }
                    @if (r.abnormal && r.itemStatus) { <span class="badge-off">{{ status(r.itemStatus).name | translate }}</span> }
                    @if (r.registered && !r.wrongStore && !r.onLoan && !r.abnormal) { <span class="badge-on">{{ 'INVENTORY_PAGE.OK' | translate }}</span> }
                  </td>
                  <td class="td text-[13px]">{{ r.createdAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
              }
            </tbody>
          </table>
          @if (scans(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
        } @else {
          <table class="w-full text-left border-collapse min-w-[800px]">
            <thead><tr>
              <th class="th w-36">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-44">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</th><th class="th w-40">{{ 'ITEMS_PAGE.STORE' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (r of missing()?.items ?? []; track r.itemPublicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td font-mono text-[13px]">{{ r.barcode }}</td><td class="td">{{ r.title ?? '—' }}</td>
                  <td class="td">{{ r.author ?? '' }}</td><td class="td">{{ r.storeName ?? '—' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('INVENTORY_PAGE.NO_MISSING' | translate) }}</td></tr>
              }
            </tbody>
          </table>
          @if (missing(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" [sizes]="[50, 200, 1000]" (pageChange)="onPage($event)" /> }
        }
      </div>
    </div>
  `,
})
export class InventoryDetail implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly filters = FILTERS;
  protected code = '';
  protected scanStoreId: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 50;
  protected readonly inventory = signal<Inventory | null>(null);
  protected readonly summary = signal<InventorySummary | null>(null);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly filter = signal<string | null>(null);
  protected readonly missingMode = signal(false);
  protected readonly scans = signal<CrudPage<InventoryScanRow> | null>(null);
  protected readonly missing = signal<CrudPage<MissingRow> | null>(null);
  protected readonly lastScan = signal<{ scan: InventoryScanRow; alreadyScanned: boolean } | null>(null);
  protected readonly imported = signal<InventoryImportResult | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.refresh();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected status(code: string) {
    return itemStatus(code);
  }

  protected cards(s: InventorySummary): { label: string; value: number; tone: string; filter?: string | null }[] {
    return [
      { label: 'INVENTORY_PAGE.SCANNED', value: s.scanned, tone: 'text-gray-800', filter: null },
      { label: 'INVENTORY_PAGE.F_WRONG_STORE', value: s.wrongStore, tone: 'text-amber-600', filter: 'wrong-store' },
      { label: 'INVENTORY_PAGE.F_UNREGISTERED', value: s.unregistered, tone: 'text-amber-600', filter: 'unregistered' },
      { label: 'INVENTORY_PAGE.F_ON_LOAN', value: s.onLoan, tone: 'text-blue-600', filter: 'on-loan' },
      { label: 'INVENTORY_PAGE.F_ABNORMAL', value: s.abnormal, tone: 'text-amber-600', filter: 'abnormal' },
      { label: 'INVENTORY_PAGE.MISSING', value: s.missing, tone: 'text-red-600' },
    ];
  }

  protected setFilter(value: string | null): void {
    this.missingMode.set(false);
    this.filter.set(value);
    this.pageIndex = 1;
    this.pageSize = 50;
    void this.loadList();
  }

  protected showMissing(): void {
    this.missingMode.set(true);
    this.pageIndex = 1;
    this.pageSize = 200;
    void this.loadList();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.loadList();
  }

  protected async scan(): Promise<void> {
    const barcode = this.code.trim();
    if (!barcode) return;
    this.code = '';
    try {
      this.lastScan.set(await this.api.scanInventory(this.id(), barcode, this.scanStoreId));
      void this.refresh();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async importFile(event: Event): Promise<void> {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    target.value = '';
    if (!file) return;
    this.busy.set(true);
    try {
      this.imported.set(await this.api.importInventoryScans(this.id(), file));
      await this.refresh();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async template(): Promise<void> {
    try {
      saveFile(await this.api.inventoryScanTemplate(), 'mau-nhap-kiem-ke.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async report(): Promise<void> {
    this.busy.set(true);
    try {
      saveFile(await this.api.inventoryReport(this.id()), 'bao-cao-kiem-ke.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Báo mất các bản thiếu đang hiện (một trang, tối đa 2.000) — đi qua đúng nghiệp vụ báo mất (bỏ qua bản đang mượn…). */
  protected async markMissingLost(): Promise<void> {
    const barcodes = (this.missing()?.items ?? []).map((m) => m.barcode);
    this.busy.set(true);
    try {
      const r = await this.api.markLost({ barcodes, lostOn: null, reason: this.inventory()?.name ?? null });
      this.toastr.success('STORE_RECORD.LOST_DONE', { count: r.done });
      await this.refresh();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async refresh(): Promise<void> {
    try {
      const [inventory, summary] = await Promise.all([this.api.crud<Inventory>('inventories', 'holdings').get(this.id()), this.api.inventorySummary(this.id())]);
      this.inventory.set(inventory);
      this.summary.set(summary);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
    await this.loadList();
  }

  private async loadList(): Promise<void> {
    this.loading.set(true);
    try {
      if (this.missingMode()) this.missing.set(await this.api.inventoryMissing(this.id(), this.pageIndex, this.pageSize));
      else this.scans.set(await this.api.inventoryScans({ inventoryId: this.id(), filter: this.filter(), pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
