import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, CompleteResult, CrudPage, Delivery, DeliveryReceiptOption, Item, ItemBatchResult, StoreMove, StoreOption, TransferKind, TransferLine, errorMessage,
} from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { itemStatus } from './items';

type Transfer = StoreMove | Delivery;

/** Chữ/quyền theo loại phiếu — khoá viết thẳng để công cụ kiểm tra đa ngữ thấy. */
const KINDS = {
  'store-moves': { title: 'TRANSFER.MOVE_TITLE', add: 'TRANSFER.MOVE_ADD', perm: 'AB_MOVES', link: '/ab-moves', finish: 'TRANSFER.COMPLETE', finished: 'TRANSFER.COMPLETED' },
  deliveries: { title: 'TRANSFER.DELIVERY_TITLE', add: 'TRANSFER.DELIVERY_ADD', perm: 'AB_DELIVERERS', link: '/ab-deliverers', finish: 'TRANSFER.SIGN', finished: 'TRANSFER.SIGNED' },
} as const;

interface TransferForm {
  publicId: string | null; fromStoreId: number | null; toStoreId: number | null; storeId: number | null; status: number;
  delivererName: string; delivererAddress: string; receiverName: string; receiverAddress: string;
  movedOn: string; deliveredOn: string; receivedOn: string; note: string; receiptPublicId: string | null;
}

function kindOf(route: ActivatedRoute): TransferKind {
  return (route.snapshot.data['kind'] as TransferKind) ?? 'store-moves';
}

function isLocked(t: Transfer): boolean {
  return 'completedAt' in t ? !!t.completedAt : !!(t as Delivery).signedAt;
}

/**
 * Phiếu điều chuyển kho (monolith: /admin/ab-moves, quyền AB_MOVES) và phiếu giao nhận (/admin/ab-deliverers, quyền AB_DELIVERERS):
 * danh sách, thêm/sửa/xoá phiếu chưa khoá; mở phiếu để chọn bản sách và hoàn thành / ký nhận.
 */
@Component({
  selector: 'app-transfers',
  imports: [FormsModule, RouterLink, LDatePipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    @let k = text();
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ k.title | translate }}</h4>
      @if (session.can(k.perm + ':add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ k.add | translate }}</button>
      }
    </div>
    <div class="panel flex gap-3 items-end">
      <div class="flex-1">
        <label for="tr-kw" class="field-label">{{ 'TRANSFER.SEARCH_LABEL' | translate }}</label>
        <input id="tr-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" />
      </div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[860px]">
          <thead><tr>
            <th class="th w-24">{{ 'TRANSFER.NO' | translate }}</th>
            <th class="th">{{ (kind === 'store-moves' ? 'TRANSFER.STORES' : 'TRANSFER.PEOPLE') | translate }}</th>
            <th class="th w-48">{{ (kind === 'store-moves' ? 'TRANSFER.PEOPLE' : 'TRANSFER.TO_STORE') | translate }}</th>
            <th class="th w-28">{{ 'TRANSFER.DATE' | translate }}</th><th class="th w-20 !text-right">{{ 'TRANSFER.COPIES' | translate }}</th>
            <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (t of page()?.items ?? []; track t.publicId) {
              @let m = asMove(t); @let d = asDelivery(t);
              <tr class="hover:bg-gray-50/50">
                <td class="td"><a [routerLink]="[k.link, t.publicId]" class="font-mono font-medium text-blue-700 hover:underline">#{{ t.id }}</a></td>
                @if (m) {
                  <td class="td">{{ m.fromStoreName }} → {{ m.toStoreName }}</td>
                  <td class="td text-[13px]">{{ m.delivererName ?? '' }}@if (m.receiverName) { → {{ m.receiverName }} }</td>
                  <td class="td">{{ m.movedOn ? (m.movedOn | ldate: 'dd/MM/yyyy') : '' }}</td>
                } @else if (d) {
                  <td class="td text-[13px]">{{ d.delivererName ?? '' }}@if (d.receiverName) { → {{ d.receiverName }} }
                    @if (d.receiptCode) { <div class="text-[11px] text-gray-400">{{ 'TRANSFER.RECEIPT_NO' | translate: { no: d.receiptCode } }}</div> }</td>
                  <td class="td">{{ d.storeName ?? '—' }}</td>
                  <td class="td">{{ d.deliveredOn ? (d.deliveredOn | ldate: 'dd/MM/yyyy') : '' }}</td>
                }
                <td class="td text-right">{{ t.lineCount }}</td>
                <td class="td text-center"><span [class]="statusOf(t).badge">{{ statusOf(t).label | translate }}</span></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (!locked(t) && session.can(k.perm + ':edit')) {
                      <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(t)"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (!locked(t) && session.can(k.perm + ':delete')) {
                      <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(t)"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (form(); as f) {
      <app-modal [title]="(f.publicId ? 'COMMON.MODAL_EDIT_TITLE' : k.add) | translate" widthClass="max-w-xl" (closed)="form.set(null)">
        <div class="grid grid-cols-2 gap-4">
          @if (kind === 'store-moves') {
            <div><label for="tf-from" class="form-label">{{ 'TRANSFER.FROM_STORE' | translate }} *</label>
              <select id="tf-from" class="input" [(ngModel)]="f.fromStoreId">@for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
            <div><label for="tf-to" class="form-label">{{ 'TRANSFER.TO_STORE' | translate }} *</label>
              <select id="tf-to" class="input" [(ngModel)]="f.toStoreId">@for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
            <div><label for="tf-dn" class="form-label">{{ 'TRANSFER.DELIVERER' | translate }}</label><input id="tf-dn" class="input" maxlength="250" [(ngModel)]="f.delivererName" /></div>
            <div><label for="tf-rn" class="form-label">{{ 'TRANSFER.RECEIVER' | translate }}</label><input id="tf-rn" class="input" maxlength="250" [(ngModel)]="f.receiverName" /></div>
            <div><label for="tf-mo" class="form-label">{{ 'TRANSFER.DATE' | translate }}</label><input id="tf-mo" type="date" class="input" [(ngModel)]="f.movedOn" /></div>
          } @else {
            <div class="col-span-2"><label for="tf-rc" class="form-label">{{ 'TRANSFER.RECEIPT' | translate }}</label>
              <select id="tf-rc" class="input" [(ngModel)]="f.receiptPublicId" (ngModelChange)="onReceipt(f)">
                <option [ngValue]="null">{{ 'TRANSFER.NO_RECEIPT' | translate }}</option>
                @for (r of receipts(); track r.publicId) { <option [ngValue]="r.publicId">{{ receiptLabel(r) | translate: receiptParams(r) }}</option> }
              </select>
              <p class="text-[11px] text-gray-400 mt-1">{{ 'TRANSFER.RECEIPT_HINT' | translate }}</p></div>
            <div><label for="tf-dn" class="form-label">{{ 'TRANSFER.DELIVERER' | translate }}</label><input id="tf-dn" class="input" maxlength="250" [(ngModel)]="f.delivererName" /></div>
            <div><label for="tf-da" class="form-label">{{ 'TRANSFER.DELIVERER_ADDRESS' | translate }}</label><input id="tf-da" class="input" maxlength="500" [(ngModel)]="f.delivererAddress" /></div>
            <div><label for="tf-rn" class="form-label">{{ 'TRANSFER.RECEIVER' | translate }}</label><input id="tf-rn" class="input" maxlength="250" [(ngModel)]="f.receiverName" /></div>
            <div><label for="tf-ra" class="form-label">{{ 'TRANSFER.RECEIVER_ADDRESS' | translate }}</label><input id="tf-ra" class="input" maxlength="500" [(ngModel)]="f.receiverAddress" /></div>
            <div><label for="tf-dd" class="form-label">{{ 'TRANSFER.DELIVERED_ON' | translate }}</label><input id="tf-dd" type="date" class="input" [(ngModel)]="f.deliveredOn" /></div>
            <div><label for="tf-rd" class="form-label">{{ 'TRANSFER.RECEIVED_ON' | translate }}</label><input id="tf-rd" type="date" class="input" [(ngModel)]="f.receivedOn" /></div>
            <div><label for="tf-st" class="form-label">{{ 'TRANSFER.TO_STORE' | translate }}</label>
              <select id="tf-st" class="input" [(ngModel)]="f.storeId"><option [ngValue]="null">—</option>@for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
            <div><label for="tf-ss" class="form-label">{{ 'COMMON.STATUS' | translate }}</label>
              <select id="tf-ss" class="input" [(ngModel)]="f.status">
                <option [ngValue]="1">{{ 'TRANSFER.DELIVERING' | translate }}</option><option [ngValue]="2">{{ 'TRANSFER.RECEIVED' | translate }}</option>
              </select></div>
          }
          <div class="col-span-2"><label for="tf-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="tf-note" class="input" maxlength="500" [(ngModel)]="f.note" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as t) {
      <app-confirm-delete [busy]="busy()" message="TRANSFER.DELETE_CONFIRM" [messageParams]="{ no: t.id }" (confirmed)="remove(t)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Transfers implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  protected readonly kind = kindOf(inject(ActivatedRoute));
  protected readonly text = computed(() => KINDS[this.kind]);
  private readonly client = this.api.crud<Transfer>(this.kind, 'holdings');

  protected keyword = '';
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<Transfer> | null>(null);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly receipts = signal<DeliveryReceiptOption[]>([]);
  protected readonly form = signal<TransferForm | null>(null);
  protected readonly deleting = signal<Transfer | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOptions().then((s) => this.stores.set(s), () => undefined);
  }

  protected asMove(t: Transfer): StoreMove | null {
    return 'fromStoreId' in t ? t : null;
  }

  protected asDelivery(t: Transfer): Delivery | null {
    return 'fromStoreId' in t ? null : t;
  }

  protected locked(t: Transfer): boolean {
    return isLocked(t);
  }

  protected statusOf(t: Transfer): { label: string; badge: string } {
    return transferStatus(t);
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

  protected edit(t: Transfer | null): void {
    const m = t && 'fromStoreId' in t ? t : null;
    const d = t && !('fromStoreId' in t) ? t : null;
    this.form.set({
      publicId: t?.publicId ?? null, fromStoreId: m?.fromStoreId ?? this.stores()[0]?.id ?? null, toStoreId: m?.toStoreId ?? this.stores()[1]?.id ?? null,
      storeId: d?.storeId ?? null, status: d?.status ?? 1, delivererName: t?.delivererName ?? '', delivererAddress: d?.delivererAddress ?? '',
      receiverName: t?.receiverName ?? '', receiverAddress: d?.receiverAddress ?? '', movedOn: m?.movedOn ?? '', deliveredOn: d?.deliveredOn ?? '',
      receivedOn: d?.receivedOn ?? '', note: t?.note ?? '', receiptPublicId: d?.receiptPublicId ?? null,
    });
    if (this.kind === 'deliveries') this.api.deliveryReceipts(d?.receiptPublicId).then((r) => this.receipts.set(r), () => this.receipts.set([]));
  }

  protected receiptLabel(r: DeliveryReceiptOption): string {
    return receiptOptionLabel(r);
  }

  protected receiptParams(r: DeliveryReceiptOption) {
    return receiptOptionParams(r);
  }

  /** Chọn đơn nhận: kho nhận mặc định theo kho của đơn (nếu phiếu chưa chọn kho). */
  protected onReceipt(f: TransferForm): void {
    const r = this.receipts().find((x) => x.publicId === f.receiptPublicId);
    if (r?.storeId && f.storeId === null) f.storeId = r.storeId;
  }

  protected async save(f: TransferForm): Promise<void> {
    const text = (v: string) => v.trim() || null;
    const body = this.kind === 'store-moves'
      ? { fromStoreId: f.fromStoreId, toStoreId: f.toStoreId, delivererName: text(f.delivererName), receiverName: text(f.receiverName), movedOn: f.movedOn || null, note: text(f.note) }
      : {
        delivererName: text(f.delivererName), delivererAddress: text(f.delivererAddress), receiverName: text(f.receiverName),
        receiverAddress: text(f.receiverAddress), deliveredOn: f.deliveredOn || null, receivedOn: f.receivedOn || null, storeId: f.storeId,
        status: f.status, note: text(f.note), receiptPublicId: f.receiptPublicId,
      };
    this.busy.set(true);
    try {
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(t: Transfer): Promise<void> {
    this.busy.set(true);
    try {
      await this.client.delete(t.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ keyword: this.keyword.trim() || undefined, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

/** "Đơn nhận #12 — Mua đợt 1 (05/09/2026) · còn 8 bản" — khoá dịch + tham số. */
function receiptOptionLabel(r: DeliveryReceiptOption): string {
  return r.name ? 'TRANSFER.RECEIPT_OPTION_NAMED' : 'TRANSFER.RECEIPT_OPTION';
}

function receiptOptionParams(r: DeliveryReceiptOption) {
  const on = r.receivedOn ? r.receivedOn.split('-').reverse().join('/') : '';
  return { no: r.code, name: r.name ?? '', on, pending: r.pending };
}

function transferStatus(t: Transfer): { label: string; badge: string } {
  if ('fromStoreId' in t) return t.completedAt ? { label: 'TRANSFER.COMPLETED', badge: 'badge-on' } : { label: 'TRANSFER.DRAFT', badge: 'badge-info' };
  if (t.signedAt) return { label: 'TRANSFER.SIGNED', badge: 'badge-on' };
  return t.status === 2 ? { label: 'TRANSFER.RECEIVED', badge: 'badge-info' } : { label: 'TRANSFER.DELIVERING', badge: 'badge-off' };
}

/**
 * Một phiếu điều chuyển / giao nhận: dòng bản sách, thêm bằng mã quét/dán hoặc chọn trong danh sách bản phù hợp (bản ở kho nguồn /
 * bản chưa xếp giá chưa thuộc phiếu nào), bỏ dòng, rồi hoàn thành điều chuyển / ký nhận.
 */
@Component({
  selector: 'app-transfer-detail',
  imports: [FormsModule, RouterLink, Loading, Paginator, TranslatePipe],
  template: `
    @let k = text();
    <div class="mb-5 flex flex-wrap gap-3 justify-between items-center">
      <div>
        <a [routerLink]="k.link" class="text-sm text-blue-600 hover:underline">← {{ k.title | translate }}</a>
        @if (transfer(); as t) {
          <h4 class="page-title">#{{ t.id }} <span [class]="statusOf(t).badge + ' align-middle'">{{ statusOf(t).label | translate }}</span></h4>
          <div class="text-sm text-gray-500">
            @if (asMove(t); as m) { {{ m.fromStoreName }} → {{ m.toStoreName }} }
            @else if (asDelivery(t); as d) {
              {{ d.delivererName ?? '' }} → {{ d.receiverName ?? '' }} · {{ 'TRANSFER.TO_STORE' | translate }}: {{ d.storeName ?? '—' }}
              @if (d.receiptCode) { · {{ 'TRANSFER.RECEIPT_NO' | translate: { no: d.receiptCode } }} }
            }
            @if (t.note) { · {{ t.note }} }
          </div>
        }
      </div>
      @if (transfer(); as t) {
        @if (!locked(t) && session.can(k.perm + ':edit')) {
          <button class="btn-primary" [disabled]="busy() || !lines().length" (click)="finish()">
            <span class="material-icons text-[18px]">task_alt</span> {{ k.finish | translate }}</button>
        }
      }
    </div>

    @if (result(); as r) {
      <div class="panel text-sm space-y-1">
        <div class="alert-success !block">{{ 'TRANSFER.DONE_N' | translate: { count: r.done } }}</div>
        @if (asBatch(r)?.notFound?.length) { <div class="alert-error !block">{{ 'STORE_RECORD.NOT_FOUND' | translate }} <span class="font-mono text-xs">{{ asBatch(r)!.notFound.join(', ') }}</span></div> }
        @for (s of r.skipped; track $index) { <div class="alert-error !block text-xs"><b class="font-mono">{{ s.barcode }}</b>: {{ s.reason }}</div> }
      </div>
    }

    @if (editable()) {
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-5 mb-5">
        <div class="panel !mb-0">
          <label for="td-codes" class="form-label">{{ 'STORE_RECORD.CODES' | translate }}</label>
          <textarea id="td-codes" rows="6" class="input font-mono text-[13px]" [(ngModel)]="codes"></textarea>
          <button type="button" class="btn-primary w-full justify-center mt-3" [disabled]="busy() || !codeList().length" (click)="add({ barcodes: codeList() })">
            <span class="material-icons text-[18px]">playlist_add</span> {{ 'TRANSFER.ADD_N' | translate: { count: codeList().length } }}</button>
        </div>
        <div class="xl:col-span-2 bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
          <div class="p-3 flex flex-wrap gap-2 items-end border-b border-gray-100">
            @if (kind === 'deliveries') {
              <div class="w-56"><label for="td-rc" class="field-label">{{ 'TRANSFER.RECEIPT' | translate }}</label>
                <select id="td-rc" class="input" [(ngModel)]="pick.receiptPublicId" (ngModelChange)="findCandidates()">
                  <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
                  @for (r of receipts(); track r.publicId) { <option [ngValue]="r.publicId">{{ receiptLabel(r) | translate: receiptParams(r) }}</option> }
                </select></div>
            }
            <div class="flex-1 min-w-[160px]"><label for="td-kw" class="field-label">{{ 'TRANSFER.PICK_SEARCH' | translate }}</label>
              <input id="td-kw" class="input" [(ngModel)]="pick.keyword" (keydown.enter)="findCandidates()" /></div>
            <div class="w-32"><label for="td-bf" class="field-label">{{ 'ITEMS_PAGE.BARCODE_FROM' | translate }}</label><input id="td-bf" class="input font-mono" [(ngModel)]="pick.barcodeFrom" /></div>
            <div class="w-32"><label for="td-bt" class="field-label">{{ 'ITEMS_PAGE.BARCODE_TO' | translate }}</label><input id="td-bt" class="input font-mono" [(ngModel)]="pick.barcodeTo" /></div>
            <button class="btn-secondary" (click)="findCandidates()"><span class="material-icons text-[18px]">search</span></button>
            <button class="btn-primary" [disabled]="busy() || !picked().size" (click)="add({ itemIds: [...picked()] })">{{ 'TRANSFER.ADD_N' | translate: { count: picked().size } }}</button>
          </div>
          <div class="max-h-80 overflow-y-auto">
            <table class="w-full text-left border-collapse">
              <tbody>
                @for (it of candidates()?.items ?? []; track it.publicId) {
                  <tr class="hover:bg-gray-50/50 cursor-pointer" (click)="toggle(it)">
                    <td class="td w-10 text-center"><input type="checkbox" class="rounded border-gray-300" [attr.aria-label]="it.barcode" [checked]="picked().has(it.publicId)" (click)="$event.stopPropagation()" (change)="toggle(it)" /></td>
                    <td class="td font-mono text-[13px] w-32">{{ it.barcode }}</td><td class="td">{{ it.title ?? '—' }}</td><td class="td w-32">{{ it.storeCode ?? '' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="py-6 text-center text-gray-500">{{ 'TRANSFER.NO_CANDIDATES' | translate }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          @if (candidates(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pick.pageIndex" [pageSize]="pick.pageSize" (pageChange)="onPickPage($event)" /> }
        </div>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'TRANSFER.LINES' | translate: { count: lines().length } }}</div>
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[760px]">
          <thead><tr>
            <th class="th w-36">{{ 'ITEMS_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'ITEMS_PAGE.TITLE_COL' | translate }}</th>
            <th class="th w-40">{{ 'ITEMS_PAGE.STORE' | translate }}</th><th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
            <th class="th w-16"></th>
          </tr></thead>
          <tbody>
            @for (l of lines(); track l.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                <td class="td">{{ l.title ?? '—' }}<span class="text-[11px] text-gray-400"> MFN {{ l.mfn }}</span>
                  @if (l.skipReason) { <div class="text-[11px] text-red-600">{{ l.skipReason }}</div> }</td>
                <td class="td">{{ l.storeName ?? '—' }}</td>
                <td class="td text-center"><span [class]="status(l.status, l.onLoan).badge">{{ status(l.status, l.onLoan).name | translate }}</span></td>
                <td class="td text-center">
                  @if (editable()) {
                    <button class="icon-btn text-gray-400 hover:text-red-600" [title]="'COMMON.DELETE' | translate" (click)="removeLine(l)"><span class="material-icons text-[18px]">close</span></button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-8 text-center text-gray-500">{{ 'TRANSFER.NO_LINES' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class TransferDetail implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  protected readonly kind = kindOf(inject(ActivatedRoute));
  protected readonly text = computed(() => KINDS[this.kind]);

  protected codes = '';
  protected pick = { keyword: '', barcodeFrom: '', barcodeTo: '', receiptPublicId: null as string | null, pageIndex: 1, pageSize: 25 };
  protected readonly receipts = signal<DeliveryReceiptOption[]>([]);
  protected readonly transfer = signal<Transfer | null>(null);
  protected readonly lines = signal<TransferLine[]>([]);
  protected readonly candidates = signal<CrudPage<Item> | null>(null);
  protected readonly picked = signal<Set<string>>(new Set());
  protected readonly result = signal<ItemBatchResult | CompleteResult | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly editable = computed(() => {
    const t = this.transfer();
    return !!t && !isLocked(t) && this.session.can(this.text().perm + ':edit');
  });

  async ngOnInit(): Promise<void> {
    await this.load();
    const d = this.transfer();
    if (d && !('fromStoreId' in d)) {
      this.pick.receiptPublicId = d.receiptPublicId;
      if (this.editable()) this.api.deliveryReceipts(d.receiptPublicId).then((r) => this.receipts.set(r), () => undefined);
    }
    if (this.editable()) void this.findCandidates();
  }

  protected receiptLabel(r: DeliveryReceiptOption): string {
    return receiptOptionLabel(r);
  }

  protected receiptParams(r: DeliveryReceiptOption) {
    return receiptOptionParams(r);
  }

  protected asMove(t: Transfer): StoreMove | null {
    return 'fromStoreId' in t ? t : null;
  }

  protected asDelivery(t: Transfer): Delivery | null {
    return 'fromStoreId' in t ? null : t;
  }

  protected asBatch(r: ItemBatchResult | CompleteResult): ItemBatchResult | null {
    return 'notFound' in r ? r : null;
  }

  protected locked(t: Transfer): boolean {
    return isLocked(t);
  }

  protected statusOf(t: Transfer) {
    return transferStatus(t);
  }

  protected status(code: string, onLoan: boolean) {
    return itemStatus(code, onLoan);
  }

  protected codeList(): string[] {
    return [...new Set(this.codes.split(/[\s,;]+/).map((c) => c.trim()).filter((c) => c))];
  }

  protected toggle(it: Item): void {
    const next = new Set(this.picked());
    if (next.has(it.publicId)) next.delete(it.publicId);
    else next.add(it.publicId);
    this.picked.set(next);
  }

  protected onPickPage(e: { pageIndex: number; pageSize: number }): void {
    this.pick.pageIndex = e.pageIndex;
    this.pick.pageSize = e.pageSize;
    void this.loadCandidates();
  }

  protected findCandidates(): void {
    this.pick.pageIndex = 1;
    void this.loadCandidates();
  }

  protected async add(body: { itemIds?: string[]; barcodes?: string[] }): Promise<void> {
    this.busy.set(true);
    try {
      this.result.set(await this.api.addTransferLines(this.kind, this.id(), body));
      this.codes = '';
      this.picked.set(new Set());
      await this.load();
      await this.loadCandidates();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async removeLine(l: TransferLine): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.removeTransferLine(this.kind, l.publicId);
      await this.load();
      await this.loadCandidates();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async finish(): Promise<void> {
    this.busy.set(true);
    try {
      this.result.set(this.kind === 'store-moves' ? await this.api.completeMove(this.id()) : await this.api.signDelivery(this.id()));
      this.toastr.success(this.text().finished);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [transfer, lines] = await Promise.all([
        this.api.crud<Transfer>(this.kind, 'holdings').get(this.id()), this.api.transferLines(this.kind, this.id()),
      ]);
      this.transfer.set(transfer);
      this.lines.set(lines);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadCandidates(): Promise<void> {
    if (!this.editable()) return;
    try {
      const p = this.pick;
      this.candidates.set(await this.api.transferCandidates(this.kind, {
        transferId: this.id(), keyword: p.keyword.trim() || undefined, barcodeFrom: p.barcodeFrom.trim() || null, barcodeTo: p.barcodeTo.trim() || null,
        receiptPublicId: this.kind === 'deliveries' ? p.receiptPublicId : null, pageIndex: p.pageIndex, pageSize: p.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }
}
