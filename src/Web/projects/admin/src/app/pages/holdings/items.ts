import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { RouterLink } from '@angular/router';
import { Api, CrudClient, CrudPage, ITEM_STATUSES, Item, ItemMovement, ItemSearch, Store, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { clampPage, rememberSearch, rememberedSearch } from '../../core/list-state';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';

/** Nhãn (khoá dịch) + kiểu badge của mã trạng thái bản sách; bản đang có người mượn (theo lưu thông) hiện "Đang mượn". */
export function itemStatus(code: string, onLoan = false): { name: string; badge: string } {
  if (onLoan) return { name: 'ITEM_STATUS.B', badge: 'badge-info' };
  return ITEM_STATUSES.find((s) => s.code === code) ?? { name: code, badge: 'badge-off' };
}

/** Tìm kiếm tài liệu theo ĐKCB (monolith: /admin/books, quyền DOC_SEARCH) — sửa kho/ghi chú, xoá theo quyền CATALOG_BIBS. */
@Component({
  selector: 'app-items',
  imports: [FormsModule, LDatePipe, RouterLink, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'ITEMS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="it-kw" class="field-label">{{ 'ITEMS_PAGE.SEARCH_LABEL' | translate }}</label>
          <input id="it-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" [placeholder]="'ITEMS_PAGE.SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="it-store" class="field-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
          <select id="it-store" class="input" [(ngModel)]="search.storeId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
          </select>
        </div>
        <div>
          <label for="it-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="it-status" class="input" [(ngModel)]="search.itemStatus" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
          </select>
        </div>
        <div>
          <label for="it-from" class="field-label">{{ 'ITEMS_PAGE.BARCODE_FROM' | translate }}</label>
          <input id="it-from" class="input font-mono" [(ngModel)]="search.barcodeFrom" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="it-to" class="field-label">{{ 'ITEMS_PAGE.BARCODE_TO' | translate }}</label>
          <input id="it-to" class="input font-mono" [(ngModel)]="search.barcodeTo" (keydown.enter)="find()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="panel flex justify-end"><span class="text-sm text-gray-500">{{ 'ITEMS_PAGE.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span></div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-36">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th><th class="th w-44">{{ 'ITEMS_PAGE.AUTHOR' | translate }}</th><th class="th w-40">{{ 'ITEMS_PAGE.STORE' | translate }}</th>
            <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-48">{{ 'COMMON.NOTE' | translate }}</th><th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (it of page()?.items ?? []; track it.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px] font-medium text-gray-800">{{ it.barcode }}</td>
                <td class="td">
                  @if (session.can('CATALOG_BIBS:view')) {
                    <a [routerLink]="['/catalog-bibs', it.mfn]" class="hover:text-blue-600">{{ it.title ?? '—' }}</a>
                  } @else { {{ it.title ?? '—' }} }
                  <div class="text-[11px] text-gray-400">MFN {{ it.mfn }}@if (it.ddc) { · DDC {{ it.ddc }} }@if (it.publishYear) { · {{ it.publishYear }} }</div>
                </td>
                <td class="td">{{ it.author ?? '—' }}</td>
                <td class="td">{{ it.storeCode ? it.storeCode + ' — ' + it.storeName : '—' }}
                  @if (it.shelfName) { <div class="text-[11px] text-gray-500">{{ it.shelfName }}@if (it.shelfRowIndex) { — {{ 'SPACE_FLOOR_MAP_PAGE.SHELF_ROW_N' | translate: { n: it.shelfRowIndex } }} }</div> }</td>
                <td class="td text-center"><span [class]="statusOf(it.status, it.onLoan).badge">{{ statusOf(it.status, it.onLoan).name | translate }}</span>
                  @if (it.onLoan) { <div class="text-[11px] text-gray-500">{{ 'ITEMS_PAGE.LOAN_INFO' | translate: { card: it.loanCardNo, due: (it.loanDueAt | ldate: 'dd/MM/yyyy') } }}</div> }</td>
                <td class="td text-[13px] text-gray-600">{{ it.note ?? '' }}</td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    <button (click)="showHistory(it)" class="icon-btn bg-gray-50 text-gray-600 hover:bg-gray-100" [title]="'ITEMS_PAGE.MOVEMENTS' | translate"><span class="material-icons text-[18px]">history</span></button>
                    @if (session.can('CATALOG_BIBS:edit')) {
                      <button (click)="edit(it)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (session.can('CATALOG_BIBS:delete')) {
                      <button (click)="deleting.set(it)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('ITEMS_PAGE.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (editing(); as it) {
      <app-modal [title]="'ITEMS_PAGE.EDIT_TITLE' | translate: { barcode: it.barcode }" widthClass="max-w-md" (closed)="editing.set(null)">
        <div class="space-y-4">
          <div>
            <label for="ie-store" class="form-label">{{ 'ITEMS_PAGE.STORE' | translate }}</label>
            <select id="ie-store" class="input" [(ngModel)]="form.storeId">
              <option [ngValue]="null">—</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
          <div>
            <label for="ie-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label>
            <input id="ie-note" class="input" maxlength="500" [(ngModel)]="form.note" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="editing.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="save(it)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (history(); as h) {
      <!-- Lịch sử di chuyển của bản sách: xếp giá, xếp giá lại, chuyển kho, đăng ký lại mã vạch. -->
      <app-modal [title]="'ITEMS_PAGE.MOVEMENTS_TITLE' | translate: { barcode: h.item.barcode }" widthClass="max-w-3xl" (closed)="history.set(null)">
        @if (h.rows === null) { <p class="text-sm text-gray-500">{{ 'COMMON.LOADING' | translate }}</p> }
        @else if (!h.rows.length) { <p class="text-sm text-gray-500">{{ 'ITEMS_PAGE.NO_MOVEMENTS' | translate }}</p> }
        @else {
          <ol class="space-y-2">
            @for (m of h.rows; track m.id) {
              <li class="border border-gray-200 rounded-lg p-3 text-sm">
                <div class="flex flex-wrap items-center gap-2"><b>{{ m.kindName }}</b>
                  <span class="text-gray-500">{{ m.at | ldate: 'dd/MM/yyyy HH:mm' }}</span>
                  @if (m.actorName) { <span class="text-gray-500">· {{ m.actorName }}</span> }
                  @if (m.fromStatus !== m.toStatus) { <span class="text-[12px]">{{ m.fromStatus }} → {{ m.toStatus }}</span> }</div>
                @if (m.fromStore !== m.toStore) { <div class="text-[13px]">{{ 'ITEMS_PAGE.STORE' | translate }}: {{ m.fromStore ?? '—' }} → {{ m.toStore ?? '—' }}</div> }
                @if (m.fromShelf !== m.toShelf || m.fromRow !== m.toRow) {
                  <div class="text-[13px]">{{ 'ITEMS_PAGE.SHELF' | translate }}: {{ m.fromShelf ?? '—' }}@if (m.fromRow) { / {{ m.fromRow }} } → {{ m.toShelf ?? '—' }}@if (m.toRow) { / {{ m.toRow }} }</div>
                }
                @if (m.reason) { <div class="text-[12px] text-gray-500 mt-1">{{ m.reason }}</div> }
              </li>
            }
          </ol>
        }
        <ng-container footer><button type="button" class="btn-secondary" (click)="history.set(null)">{{ 'COMMON.CLOSE' | translate }}</button></ng-container>
      </app-modal>
    }
    @if (deleting(); as it) {
      <app-confirm-delete [busy]="saving()" message="ITEMS_PAGE.DELETE_CONFIRM" [messageParams]="{ barcode: it.barcode, title: it.title ?? '' }" (confirmed)="remove(it)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Items implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Item> = this.api.crud<Item>('items', 'holdings');
  protected readonly session = inject(Session);

  protected readonly statuses = [...ITEM_STATUSES, { code: 'B', name: 'ITEM_STATUS.B', badge: 'badge-info' }];
  protected search: ItemSearch = rememberedSearch<ItemSearch>('items', { keyword: '', storeId: null, itemStatus: null, barcodeFrom: '', barcodeTo: '', pageIndex: 1, pageSize: 10 });
  protected readonly page = signal<CrudPage<Item> | null>(null);
  protected readonly stores = signal<Store[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly editing = signal<Item | null>(null);
  protected readonly deleting = signal<Item | null>(null);
  protected readonly history = signal<{ item: Item; rows: ItemMovement[] | null } | null>(null);
  protected form: { storeId: number | null; note: string } = { storeId: null, note: '' };

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.stores.set(await this.api.crud<Store>('stores', 'holdings').searchAll());
    } catch {
      /* thiếu quyền xem kho — chỉ mất bộ lọc kho */
    }
  }

  protected async showHistory(item: Item): Promise<void> {
    this.history.set({ item, rows: null });
    try {
      this.history.set({ item, rows: await this.api.itemMovements(item.publicId) });
    } catch (e) {
      this.history.set(null);
      this.toastr.error(errorMessage(e));
    }
  }

  protected statusOf(code: string, onLoan = false) {
    return itemStatus(code, onLoan);
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

  protected edit(it: Item): void {
    this.form = { storeId: it.storeId, note: it.note ?? '' };
    this.editing.set(it);
  }

  protected async save(it: Item): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.update(it.publicId, { storeId: this.form.storeId, note: this.form.note });
      this.toastr.success('ITEMS_PAGE.UPDATED');
      this.editing.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(it: Item): Promise<void> {
    this.saving.set(true);
    try {
      await this.client.delete(it.publicId);
      this.toastr.success('ITEMS_PAGE.DELETED');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = this.search;
      const page = await this.api.lookupItems({
        ...s, keyword: s.keyword?.trim() || undefined, barcodeFrom: s.barcodeFrom?.trim() || null, barcodeTo: s.barcodeTo?.trim() || null,
      });
      if (clampPage(s, page)) return void (await this.load());
      this.page.set(page);
      rememberSearch('items', s);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
