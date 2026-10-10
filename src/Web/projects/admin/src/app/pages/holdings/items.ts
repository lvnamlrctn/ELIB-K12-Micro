import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, CrudClient, CrudPage, ITEM_STATUSES, Item, ItemSearch, Store, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';

/** Nhãn + kiểu badge của mã trạng thái bản sách. */
export function itemStatus(code: string): { name: string; badge: string } {
  return ITEM_STATUSES.find((s) => s.code === code) ?? { name: code, badge: 'badge-off' };
}

/** Tìm kiếm tài liệu theo ĐKCB (monolith: /admin/books, quyền DOC_SEARCH) — sửa kho/ghi chú, xoá theo quyền CATALOG_BIBS. */
@Component({
  selector: 'app-items',
  imports: [FormsModule, RouterLink, Loading, Paginator, Modal, ConfirmDelete],
  template: `
    <div class="mb-5"><h4 class="page-title">Tìm kiếm tài liệu</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="it-kw" class="field-label">Số ĐKCB / nhan đề</label>
          <input id="it-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" placeholder="Đầu số ĐKCB hoặc một phần nhan đề" />
        </div>
        <div>
          <label for="it-store" class="field-label">Kho</label>
          <select id="it-store" class="input" [(ngModel)]="search.storeId" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
          </select>
        </div>
        <div>
          <label for="it-status" class="field-label">Trạng thái</label>
          <select id="it-status" class="input" [(ngModel)]="search.itemStatus" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name }}</option> }
          </select>
        </div>
        <div>
          <label for="it-from" class="field-label">ĐKCB từ</label>
          <input id="it-from" class="input font-mono" [(ngModel)]="search.barcodeFrom" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="it-to" class="field-label">đến</label>
          <input id="it-to" class="input font-mono" [(ngModel)]="search.barcodeTo" (keydown.enter)="find()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
      </div>
    </div>

    <div class="panel flex justify-end"><span class="text-sm text-gray-500">{{ page()?.totalCount ?? 0 }} bản sách</span></div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-36">Số ĐKCB</th><th class="th">Nhan đề</th><th class="th w-44">Tác giả</th><th class="th w-40">Kho</th>
            <th class="th w-32 !text-center">Trạng thái</th><th class="th w-48">Ghi chú</th><th class="th w-28 !text-center">Thao tác</th>
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
                <td class="td">{{ it.storeCode ? it.storeCode + ' — ' + it.storeName : '—' }}</td>
                <td class="td text-center"><span [class]="statusOf(it.status).badge">{{ statusOf(it.status).name }}</span></td>
                <td class="td text-[13px] text-gray-600">{{ it.note ?? '' }}</td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (session.can('CATALOG_BIBS:edit')) {
                      <button (click)="edit(it)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Sửa"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (session.can('CATALOG_BIBS:delete')) {
                      <button (click)="deleting.set(it)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" title="Xoá"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không có bản sách nào' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (editing(); as it) {
      <app-modal [title]="'Sửa ĐKCB ' + it.barcode" widthClass="max-w-md" (closed)="editing.set(null)">
        <div class="space-y-4">
          <div>
            <label for="ie-store" class="form-label">Kho</label>
            <select id="ie-store" class="input" [(ngModel)]="form.storeId">
              <option [ngValue]="null">—</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }
            </select>
          </div>
          <div>
            <label for="ie-note" class="form-label">Ghi chú</label>
            <input id="ie-note" class="input" maxlength="500" [(ngModel)]="form.note" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="editing.set(null)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="saving()" (click)="save(it)">Lưu</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as it) {
      <app-confirm-delete [busy]="saving()" [message]="'Xoá ĐKCB ' + it.barcode + ' — ' + (it.title ?? '') + '?'" (confirmed)="remove(it)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Items implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly client: CrudClient<Item> = this.api.crud<Item>('items', 'holdings');
  protected readonly session = inject(Session);

  protected readonly statuses = ITEM_STATUSES;
  protected search: ItemSearch = { keyword: '', storeId: null, itemStatus: null, barcodeFrom: '', barcodeTo: '', pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Item> | null>(null);
  protected readonly stores = signal<Store[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly editing = signal<Item | null>(null);
  protected readonly deleting = signal<Item | null>(null);
  protected form: { storeId: number | null; note: string } = { storeId: null, note: '' };

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.stores.set(await this.api.crud<Store>('stores', 'holdings').searchAll());
    } catch {
      /* thiếu quyền xem kho — chỉ mất bộ lọc kho */
    }
  }

  protected statusOf(code: string) {
    return itemStatus(code);
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
      this.toastr.success('Đã cập nhật ĐKCB.');
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
      this.toastr.success('Đã xoá ĐKCB.');
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
      this.page.set(await this.api.lookupItems({
        ...s, keyword: s.keyword?.trim() || undefined, barcodeFrom: s.barcodeFrom?.trim() || null, barcodeTo: s.barcodeTo?.trim() || null,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
