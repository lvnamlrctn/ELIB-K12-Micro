import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, Delivery, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Nhận phân bổ sách (ELIB-LRC: /admin/ab-deliverers-receive): hàng đợi các đơn phân bổ đã "Nhận đủ" mà chưa ký — thủ kho xem đơn và
 * ký nhận (các bản chưa xếp giá sang sẵn sàng phục vụ ở kho nhận, đơn khoá). Quyền AB_DELIVERERS (ký cần quyền sửa).
 */
@Component({
  selector: 'app-delivery-receive',
  imports: [FormsModule, RouterLink, LDatePipe, Loading, Paginator, Modal, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'TRANSFER.RECEIVE_TITLE' | translate }}</h4>
      <p class="text-sm text-gray-500">{{ 'TRANSFER.RECEIVE_HINT' | translate }}</p></div>
    <div class="panel grid grid-cols-2 lg:grid-cols-6 gap-3 items-end">
      <div><label for="dr-cf" class="field-label">{{ 'TRANSFER.CODE_FROM' | translate }}</label><input id="dr-cf" type="number" min="1" class="input" [(ngModel)]="filter.codeFrom" (keydown.enter)="find()" /></div>
      <div><label for="dr-ct" class="field-label">{{ 'TRANSFER.CODE_TO' | translate }}</label><input id="dr-ct" type="number" min="1" class="input" [(ngModel)]="filter.codeTo" (keydown.enter)="find()" /></div>
      <div><label for="dr-dn" class="field-label">{{ 'TRANSFER.DELIVERER' | translate }}</label><input id="dr-dn" class="input" [(ngModel)]="filter.delivererName" (keydown.enter)="find()" /></div>
      <div><label for="dr-rn" class="field-label">{{ 'TRANSFER.RECEIVER' | translate }}</label><input id="dr-rn" class="input" [(ngModel)]="filter.receiverName" (keydown.enter)="find()" /></div>
      <div><label for="dr-df" class="field-label">{{ 'TRANSFER.DELIVERED_FROM' | translate }}</label><input id="dr-df" type="date" class="input" [(ngModel)]="filter.deliveredFrom" /></div>
      <div><label for="dr-dt" class="field-label">{{ 'TRANSFER.DELIVERED_TO' | translate }}</label><input id="dr-dt" type="date" class="input" [(ngModel)]="filter.deliveredTo" /></div>
      <div class="col-span-2 lg:col-span-6 flex justify-end gap-2">
        <button class="btn-secondary" type="button" (click)="reset()">{{ 'TRANSFER.RESET' | translate }}</button>
        <button class="btn-primary" type="button" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[760px]">
          <thead><tr>
            <th class="th w-24">{{ 'TRANSFER.NO' | translate }}</th><th class="th">{{ 'TRANSFER.PEOPLE' | translate }}</th>
            <th class="th w-48">{{ 'TRANSFER.TO_STORE' | translate }}</th><th class="th w-28">{{ 'TRANSFER.DELIVERED_ON' | translate }}</th>
            <th class="th w-20 !text-right">{{ 'TRANSFER.COPIES' | translate }}</th><th class="th w-32 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (d of page()?.items ?? []; track d.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td"><a [routerLink]="['/ab-deliverers', d.publicId]" class="font-mono font-medium text-blue-700 hover:underline">#{{ d.id }}</a></td>
                <td class="td text-[13px]">{{ d.delivererName ?? '' }}@if (d.receiverName) { → {{ d.receiverName }} }
                  @if (d.receiptCode) { <div class="text-[11px] text-gray-400">{{ 'TRANSFER.RECEIPT_NO' | translate: { no: d.receiptCode } }}</div> }</td>
                <td class="td">{{ d.storeName ?? '—' }}</td>
                <td class="td">{{ d.deliveredOn ? (d.deliveredOn | ldate: 'dd/MM/yyyy') : '' }}</td>
                <td class="td text-right">{{ d.lineCount }}</td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    <a class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'TRANSFER.VIEW' | translate" [routerLink]="['/ab-deliverers', d.publicId]"><span class="material-icons text-[18px]">visibility</span></a>
                    @if (session.can('AB_DELIVERERS:edit')) {
                      <button class="icon-btn bg-green-50 text-green-700 hover:bg-green-100" [title]="'TRANSFER.SIGN' | translate" (click)="signing.set(d)"><span class="material-icons text-[18px]">how_to_reg</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('TRANSFER.RECEIVE_EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
    </div>

    @if (signing(); as d) {
      <app-modal [title]="'TRANSFER.SIGN' | translate" widthClass="max-w-md" (closed)="signing.set(null)">
        <p class="text-sm text-gray-700">{{ 'TRANSFER.SIGN_CONFIRM' | translate: { no: d.id } }}</p>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="signing.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="sign(d)"><span class="material-icons text-[18px]">how_to_reg</span> {{ 'TRANSFER.SIGN' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class DeliveryReceive implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  private readonly client = this.api.crud<Delivery>('deliveries', 'holdings');

  protected filter = emptyFilter();
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<Delivery> | null>(null);
  protected readonly signing = signal<Delivery | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  protected find(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected reset(): void {
    this.filter = emptyFilter();
    this.find();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected async sign(d: Delivery): Promise<void> {
    this.busy.set(true);
    try {
      const r = await this.api.signDelivery(d.publicId);
      this.toastr.success('TRANSFER.SIGNED_N', { count: r.done });
      this.signing.set(null);
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
      const f = this.filter;
      // Hàng đợi: đã "Nhận đủ" (status 2) và chưa ký.
      this.page.set(await this.client.search({
        pageIndex: this.pageIndex, pageSize: this.pageSize, deliveryStatus: 2, confirmed: false,
        codeFrom: f.codeFrom || null, codeTo: f.codeTo || null, delivererName: f.delivererName.trim() || null, receiverName: f.receiverName.trim() || null,
        deliveredFrom: f.deliveredFrom || null, deliveredTo: f.deliveredTo || null,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}

function emptyFilter() {
  return { codeFrom: null as number | null, codeTo: null as number | null, delivererName: '', receiverName: '', deliveredFrom: '', deliveredTo: '' };
}
