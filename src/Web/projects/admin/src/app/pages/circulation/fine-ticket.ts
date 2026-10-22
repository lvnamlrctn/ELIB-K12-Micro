import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Api, FineLine, FineReason, FineTicketDetail, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { EntityHistoryDialog } from '../../shared/entity-history';
import { PaymentDialog } from '../payment/payment-dialog';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { Loading } from '../../shared/ui';

interface EditLine { id: number | null; reasonCode: string; amount: number; barcode: string | null; title: string | null; overdueDays: number; dueAt: string | null; loanOpen: boolean; }

/**
 * Chi tiết phiếu phạt (monolith: /admin/fine-ticket/:publicId, quyền FINES). Sửa lý do/số tiền từng dòng, thêm dòng theo ĐKCB hoặc khoản
 * tự do, giảm trừ, tiền nộp, hoàn thành phiếu. Dòng lý do "Mất tài liệu" khi lưu đóng lượt mượn và báo kho mất sách.
 */
@Component({
  selector: 'app-fine-ticket',
  imports: [FormsModule, LDatePipe, LNumberPipe, RouterLink, Loading, ReasonDialog, EntityHistoryDialog, PaymentDialog, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/fines" class="icon-btn text-gray-500 hover:bg-gray-100" [title]="'FINE_TICKET_PAGE.BACK' | translate"><span class="material-icons">arrow_back</span></a>
      <h4 class="page-title">{{ 'FINE_TICKET_PAGE.TITLE' | translate: { code: detail()?.ticket?.code ?? '' } }}</h4>
      @if (detail(); as d) {
        <span [class]="d.ticket.status === 2 ? 'badge-on' : 'badge-info'">{{ (d.ticket.status === 2 ? 'FINES_PAGE.DONE' : 'FINES_PAGE.OPEN') | translate }}</span>
        @if (canPay() && d.ticket.remaining > 0) {
          <button type="button" class="btn-primary !py-1 ml-auto" (click)="paying.set(true)"><span class="material-icons text-[18px]">qr_code_2</span> {{ 'PAYMENT_DIALOG.COLLECT' | translate }}</button>
        }
        <button type="button" class="btn-secondary !py-1" [class.ml-auto]="!(canPay() && d.ticket.remaining > 0)" (click)="historyOpen.set(true)"><span class="material-icons text-[18px]">history</span> {{ 'FINE_TICKET_PAGE.HISTORY' | translate }}</button>
      }
    </div>

    @if (detail(); as d) {
      @for (r of d.pendingRequests ?? []; track r.publicId) {
        <div class="alert-info mb-4" role="status">
          <div class="font-medium">{{ 'FINE_TICKET_PAGE.PENDING_TITLE' | translate: { kind: r.kindName } }}</div>
          <div class="text-sm">{{ r.summary }} — {{ 'FINE_TICKET_PAGE.PENDING_BY' | translate: { name: r.requestedByName ?? '—', date: (r.requestedAt | ldate: 'dd/MM/yyyy HH:mm') } }}</div>
          <div class="text-xs text-gray-600">{{ 'REASON_DIALOG.REASON' | translate }}: {{ r.reason }}</div>
        </div>
      }
      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 text-sm">
          <div><div class="text-gray-500 text-xs">{{ 'FINE_TICKET_PAGE.READER' | translate }}</div><div class="font-medium">{{ d.ticket.readerName ?? '—' }}</div><div class="font-mono text-xs text-gray-500">{{ d.ticket.cardNo }}</div></div>
          <div><div class="text-gray-500 text-xs">{{ 'FINE_TICKET_PAGE.DATE' | translate }}</div><div>{{ d.ticket.fineDate | ldate: 'dd/MM/yyyy HH:mm' }}</div></div>
          <div><div class="text-gray-500 text-xs">{{ 'FINE_TICKET_PAGE.ROUND' | translate }}</div><div>{{ d.ticket.round }}</div></div>
          <div><div class="text-gray-500 text-xs">{{ 'FINE_TICKET_PAGE.FINE_PER_DAY' | translate }}</div><div>{{ d.finePerDay | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mb-5">
        @if (busy()) { <app-loading /> }
        <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'FINE_TICKET_PAGE.LINES' | translate: { count: lines().length } }}</div>
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>
              <th class="th w-32">{{ 'FINE_TICKET_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'FINE_TICKET_PAGE.TITLE_COL' | translate }}</th><th class="th w-28">{{ 'FINE_TICKET_PAGE.DUE' | translate }}</th><th class="th w-24 !text-center">{{ 'FINE_TICKET_PAGE.OVERDUE_DAYS' | translate }}</th>
              <th class="th w-56">{{ 'FINE_TICKET_PAGE.REASON' | translate }}</th><th class="th w-40 !text-right">{{ 'FINE_TICKET_PAGE.AMOUNT' | translate }}</th><th class="th w-14"></th>
            </tr></thead>
            <tbody>
              @for (l of lines(); track $index) {
                <tr>
                  <td class="td font-mono text-[13px]">
                    @if (l.id === null && editable()) {
                      <input class="input !py-1 font-mono" [(ngModel)]="l.barcode" [placeholder]="'FINE_TICKET_PAGE.OPTIONAL' | translate" [attr.aria-label]="'FINE_TICKET_PAGE.BARCODE_N' | translate: { n: $index + 1 }" />
                    } @else { {{ l.barcode ?? '—' }} }
                  </td>
                  <td class="td">{{ l.title ?? (l.id === null ? ('FINE_TICKET_PAGE.NEW_LINE' | translate) : '—') }}
                    @if (l.barcode && l.id !== null) { <div class="text-[11px]" [class.text-amber-700]="l.loanOpen" [class.text-gray-400]="!l.loanOpen">{{ (l.loanOpen ? 'FINE_TICKET_PAGE.LOAN_OPEN' : 'FINE_TICKET_PAGE.LOAN_CLOSED') | translate }}</div> }
                  </td>
                  <td class="td text-[13px]">{{ l.dueAt ? (l.dueAt | ldate: 'dd/MM/yyyy') : '—' }}</td>
                  <td class="td text-center">{{ l.overdueDays || '' }}</td>
                  <td class="td">
                    <select class="input !py-1" [(ngModel)]="l.reasonCode" (ngModelChange)="reasonChanged(l)" [disabled]="!editable()" [attr.aria-label]="'FINE_TICKET_PAGE.REASON_N' | translate: { n: $index + 1 }">
                      @for (r of reasons(); track r.code) { <option [value]="r.code">{{ r.name }}</option> }
                    </select>
                  </td>
                  <td class="td"><input type="number" min="0" class="input !py-1 text-right" [(ngModel)]="l.amount" (ngModelChange)="version.update(bump)" [disabled]="!editable()" [attr.aria-label]="'FINE_TICKET_PAGE.AMOUNT_N' | translate: { n: $index + 1 }" /></td>
                  <td class="td text-center">
                    @if (editable()) {
                      <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'FINE_TICKET_PAGE.REMOVE_LINE' | translate" (click)="removeLine($index)"><span class="material-icons text-[18px]">close</span></button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="py-6 text-center text-gray-500">{{ 'FINE_TICKET_PAGE.NO_LINES' | translate }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (editable()) {
          <div class="px-5 py-3 border-t border-gray-100">
            <button class="btn-secondary !py-1.5" (click)="addLine()"><span class="material-icons text-[18px]">add</span> {{ 'FINE_TICKET_PAGE.ADD_LINE' | translate }}</button>
          </div>
        }
      </div>

      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <label for="ft-total" class="field-label">{{ 'FINE_TICKET_PAGE.TOTAL' | translate }}</label>
            @if (lines().length === 0) {
              <input id="ft-total" type="number" min="0" class="input" [(ngModel)]="manualAmount" [disabled]="!can('edit')" />
            } @else {
              <div id="ft-total" class="input bg-gray-50">{{ total() | lnumber: '1.0-0' }}</div>
            }
          </div>
          <div>
            <label for="ft-discount" class="field-label">{{ 'FINE_TICKET_PAGE.DISCOUNT' | translate }}</label>
            <input id="ft-discount" type="number" min="0" class="input" [(ngModel)]="discount" [disabled]="!can('edit')" />
          </div>
          <div>
            <label for="ft-paid" class="field-label">{{ 'FINE_TICKET_PAGE.PAID' | translate }}</label>
            <div class="flex gap-2">
              <input id="ft-paid" type="number" [min]="d.ticket.paid" class="input" [(ngModel)]="paid" [disabled]="!can('edit')" [title]="'FINE_TICKET_PAGE.PAID_TIP' | translate" />
              @if (can('edit')) { <button class="btn-secondary !px-2 whitespace-nowrap" [title]="'FINE_TICKET_PAGE.PAY_ALL_TIP' | translate" (click)="payAll()">{{ 'FINE_TICKET_PAGE.PAY_ALL' | translate }}</button> }
            </div>
          </div>
          <div>
            <div class="field-label">{{ 'FINE_TICKET_PAGE.REMAINING' | translate }}</div>
            <div class="text-xl font-semibold pt-1" [class.text-red-600]="remaining() > 0">{{ remaining() | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div>
          </div>
          <div class="md:col-span-4">
            <label for="ft-note" class="field-label">{{ 'COMMON.NOTE' | translate }}</label>
            <input id="ft-note" class="input" maxlength="1000" [(ngModel)]="note" [disabled]="!can('edit')" />
          </div>
          @if (d.payments?.length) {
            <div class="md:col-span-4">
              <div class="flex items-center justify-between mb-1">
                <div class="field-label !mb-0">{{ 'FINE_TICKET_PAGE.PAYMENTS' | translate }}</div>
                @if (can('edit') && d.ticket.paid > 0) {
                  <button type="button" class="btn-secondary !py-1 text-amber-700" (click)="adjusting.set(true)"><span class="material-icons text-[16px]">undo</span> {{ 'FINE_TICKET_PAGE.ADJUST' | translate }}</button>
                }
              </div>
              <table class="w-full text-xs border border-gray-100 rounded-lg overflow-hidden">
                <thead class="bg-gray-50 text-gray-500"><tr>
                  <th class="px-2 py-1 text-left">{{ 'FINE_TICKET_PAGE.PAY_DATE' | translate }}</th><th class="px-2 py-1 text-left">{{ 'FINE_TICKET_PAGE.PAY_KIND' | translate }}</th>
                  <th class="px-2 py-1 text-right">{{ 'FINE_TICKET_PAGE.AMOUNT' | translate }}</th><th class="px-2 py-1 text-left">{{ 'FINE_TICKET_PAGE.PAY_BY' | translate }}</th><th class="px-2 py-1 text-left">{{ 'REASON_DIALOG.REASON' | translate }}</th>
                </tr></thead>
                <tbody>
                  @for (p of d.payments; track p.id) {
                    <tr class="border-t border-gray-100">
                      <td class="px-2 py-1 whitespace-nowrap">{{ p.paidAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                      <td class="px-2 py-1">{{ methodKey(p.method) | translate }}</td>
                      <td class="px-2 py-1 text-right font-medium" [class.text-red-600]="p.amount < 0">{{ p.amount | lnumber: '1.0-0' }}</td>
                      <td class="px-2 py-1">{{ p.byName ?? '—' }}</td>
                      <td class="px-2 py-1 text-gray-500">{{ p.reason ?? p.note ?? '' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
        @if (can('edit')) {
          <div class="flex justify-end gap-2 mt-4">
            @if (d.ticket.status === 2) {
              <button class="btn-secondary" [disabled]="busy()" (click)="save(1)">{{ 'FINE_TICKET_PAGE.REOPEN' | translate }}</button>
            } @else {
              <button class="btn-secondary" [disabled]="busy()" (click)="save(1)"><span class="material-icons text-[18px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
              <button class="btn-primary" [disabled]="busy()" (click)="save(2)"><span class="material-icons text-[18px]">task_alt</span> {{ 'FINE_TICKET_PAGE.SAVE_DONE' | translate }}</button>
            }
          </div>
        }
      </div>
    } @else if (!busy()) {
      <div class="panel text-gray-500">{{ 'FINE_TICKET_PAGE.NOT_FOUND' | translate }}</div>
    }

    @if (reasonFor(); as r) {
      <app-reason-dialog title="FINE_TICKET_PAGE.REASON_TITLE" message="FINE_TICKET_PAGE.REASON_MSG" [preview]="r.preview"
                         (confirmed)="reasonGiven($event)" (cancelled)="reasonFor.set(null)" />
    }
    @if (adjusting()) {
      <app-reason-dialog title="FINE_TICKET_PAGE.ADJUST_TITLE" message="FINE_TICKET_PAGE.ADJUST_MSG" [showAmount]="true" amountLabel="FINE_TICKET_PAGE.ADJUST_AMOUNT"
                         confirmText="FINE_TICKET_PAGE.ADJUST" (confirmed)="adjust($event)" (cancelled)="adjusting.set(false)" />
    }
    @if (paying() && detail(); as p) {
      <app-payment-dialog sourceService="circulation" [sourceId]="p.ticket.publicId" (closed)="paying.set(false)" (paid)="reload()" />
    }
    @if (historyOpen() && detail(); as h) {
      <app-entity-history entityType="FineTicket" [publicId]="h.ticket.publicId" title="FINE_TICKET_PAGE.HISTORY" [labels]="historyLabels" (closed)="historyOpen.set(false)" />
    }
  `,
})
export class FineTicketPage implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  readonly publicId = input.required<string>();

  protected readonly detail = signal<FineTicketDetail | null>(null);
  protected readonly reasons = signal<FineReason[]>([]);
  protected readonly lines = signal<EditLine[]>([]);
  protected readonly busy = signal(false);
  protected readonly version = signal(0);
  protected readonly reasonFor = signal<{ status: number; preview: { label: string; from: string; to: string }[] } | null>(null);
  protected readonly adjusting = signal(false);
  protected readonly paying = signal(false);
  protected readonly canPay = computed(() => this.session.can('PAYMENT_TRANSACTIONS:add') && (this.session.features()?.modules ?? []).includes('PAYMENT'));
  protected readonly historyOpen = signal(false);
  protected readonly historyLabels: Record<string, string> = {
    status: 'FINE_TICKET_PAGE.H_STATUS', total: 'FINE_TICKET_PAGE.TOTAL', discount: 'FINE_TICKET_PAGE.DISCOUNT', paid: 'FINE_TICKET_PAGE.PAID',
    remaining: 'FINE_TICKET_PAGE.REMAINING', note: 'COMMON.NOTE', manualAmount: 'FINE_TICKET_PAGE.TOTAL', lines: 'FINE_TICKET_PAGE.H_LINES',
  };
  protected readonly editable = computed(() => this.can('edit') && this.detail()?.ticket.status === 1);
  protected readonly total = computed(() => {
    this.version();
    const lines = this.lines();
    return lines.length ? lines.reduce((s, l) => s + (Number(l.amount) || 0), 0) : Number(this.manualAmount) || 0;
  });
  protected readonly remaining = computed(() => {
    this.version();
    return this.total() - (Number(this.discount) || 0) - (Number(this.paid) || 0);
  });
  protected readonly bump = (x: number) => x + 1;
  private deleted: number[] = [];
  private manualAmountValue = 0;
  private discountValue = 0;
  private paidValue = 0;
  protected note = '';

  // Ô số gắn ngModel: setter đẩy version để tổng/còn lại tính lại.
  protected get manualAmount(): number { return this.manualAmountValue; }
  protected set manualAmount(v: number) { this.manualAmountValue = v; this.version.update((x) => x + 1); }
  protected get discount(): number { return this.discountValue; }
  protected set discount(v: number) { this.discountValue = v; this.version.update((x) => x + 1); }
  protected get paid(): number { return this.paidValue; }
  protected set paid(v: number) { this.paidValue = v; this.version.update((x) => x + 1); }

  async ngOnInit(): Promise<void> {
    this.busy.set(true);
    try {
      const [detail, reasons] = await Promise.all([
        this.api.fineTicket(this.publicId()),
        this.api.crud<FineReason>('fine-reasons', 'circulation').searchAll(),
      ]);
      this.reasons.set(reasons);
      this.apply(detail);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected can(action: string): boolean {
    return this.session.can(`FINES:${action}`);
  }

  protected methodKey(method: string): string {
    switch (method) {
      case 'QR': return 'FINE_TICKET_PAGE.METHOD_QR';
      case 'ADJUST': return 'FINE_TICKET_PAGE.METHOD_ADJUST';
      case 'CARRY': return 'FINE_TICKET_PAGE.METHOD_CARRY';
      default: return 'FINE_TICKET_PAGE.METHOD_CASH';
    }
  }

  protected reasonChanged(l: EditLine): void {
    const reason = this.reasons().find((r) => r.code === l.reasonCode);
    const d = this.detail();
    if (reason?.code === 'QUAHAN' && d) l.amount = l.overdueDays * d.finePerDay;
    else if (reason && reason.amount > 0) l.amount = reason.amount;
    this.version.update((x) => x + 1);
  }

  protected addLine(): void {
    const reason = this.reasons().find((r) => r.code !== 'QUAHAN') ?? this.reasons()[0];
    this.lines.update((all) => [...all, { id: null, reasonCode: reason?.code ?? '', amount: reason?.amount ?? 0, barcode: null, title: null, overdueDays: 0, dueAt: null, loanOpen: false }]);
  }

  protected removeLine(index: number): void {
    const line = this.lines()[index];
    if (line.id !== null) this.deleted.push(line.id);
    this.lines.update((all) => all.filter((_, i) => i !== index));
  }

  protected payAll(): void {
    this.paid = Math.max(0, this.total() - (Number(this.discount) || 0));
  }

  /** Thao tác quan trọng (đổi giảm trừ, sửa/xoá dòng phạt, mở lại phiếu) — hỏi lý do trước khi gửi; server vẫn kiểm lại. */
  private sensitiveChanges(status: number): { label: string; from: string; to: string }[] {
    const d = this.detail();
    if (!d) return [];
    const out: { label: string; from: string; to: string }[] = [];
    if ((Number(this.discount) || 0) !== d.ticket.discount) out.push({ label: 'FINE_TICKET_PAGE.DISCOUNT', from: String(d.ticket.discount), to: String(Number(this.discount) || 0) });
    if (d.ticket.status === 2 && status === 1) out.push({ label: 'FINE_TICKET_PAGE.H_STATUS', from: '2', to: '1' });
    if (this.lines().length === 0 && (Number(this.manualAmount) || 0) !== (d.ticket.manualAmount ?? d.ticket.total)) out.push({ label: 'FINE_TICKET_PAGE.TOTAL', from: String(d.ticket.manualAmount ?? d.ticket.total), to: String(Number(this.manualAmount) || 0) });
    if (this.editable()) {
      if (this.deleted.length) out.push({ label: 'FINE_TICKET_PAGE.H_DELETED', from: String(this.deleted.length), to: '0' });
      for (const l of this.lines()) {
        const old = d.lines.find((x) => x.id === l.id);
        if (old && (old.reasonCode !== l.reasonCode || old.amount !== (Number(l.amount) || 0))) out.push({ label: 'FINE_TICKET_PAGE.H_LINE', from: `${old.reasonCode} ${old.amount}`, to: `${l.reasonCode} ${Number(l.amount) || 0}` });
      }
    }
    return out;
  }

  protected save(status: number): void {
    const preview = this.sensitiveChanges(status);
    if (preview.length) this.reasonFor.set({ status, preview });
    else void this.doSave(status, null);
  }

  protected reasonGiven(result: ReasonResult): void {
    const ask = this.reasonFor();
    this.reasonFor.set(null);
    if (ask) void this.doSave(ask.status, result.reason);
  }

  private async doSave(status: number, reason: string | null): Promise<void> {
    const d = this.detail();
    if (!d) return;
    this.busy.set(true);
    try {
      const result = await this.api.saveFineTicket(d.ticket.publicId, {
        status,
        discount: Number(this.discount) || 0,
        paid: Number(this.paid) || 0,
        note: this.note.trim() || null,
        manualAmount: this.lines().length === 0 ? Number(this.manualAmount) || 0 : null,
        lines: this.editable()
          ? this.lines().map((l) => ({ id: l.id, reasonCode: l.reasonCode, amount: Number(l.amount) || 0, barcode: l.id === null ? l.barcode?.trim() || null : null }))
          : [],
        deletedLineIds: this.editable() ? this.deleted : [],
        reason,
      });
      this.afterSave(result, status === 2 ? 'FINE_TICKET_PAGE.COMPLETED' : 'FINE_TICKET_PAGE.SAVED');
    } catch (e) {
      // Server là nơi quyết định cuối: cần lý do mà giao diện chưa hỏi → hỏi rồi thử lại.
      if (e instanceof HttpErrorResponse && (e.error as { code?: string } | null)?.code === 'REASON_REQUIRED' && !reason) this.reasonFor.set({ status, preview: [] });
      else this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async adjust(result: ReasonResult): Promise<void> {
    const d = this.detail();
    if (!d) return;
    this.adjusting.set(false);
    this.busy.set(true);
    try {
      this.afterSave(await this.api.adjustFineTicket(d.ticket.publicId, { amount: -(result.amount ?? 0), reason: result.reason }), 'FINE_TICKET_PAGE.ADJUSTED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private afterSave(result: { detail: FineTicketDetail | null; pending: { message: string } | null }, successKey: string): void {
    if (result.detail) {
      this.apply(result.detail);
      this.toastr.success(successKey, { code: result.detail.ticket.code });
    } else if (result.pending) {
      this.toastr.info('FINE_TICKET_PAGE.PENDING_SENT');
      void this.reload();
    }
  }

  protected async reload(): Promise<void> {
    const d = this.detail();
    if (d) this.apply(await this.api.fineTicket(d.ticket.publicId));
  }

  private apply(d: FineTicketDetail): void {
    this.detail.set(d);
    this.deleted = [];
    this.lines.set(d.lines.map((l: FineLine) => ({
      id: l.id, reasonCode: l.reasonCode, amount: l.amount, barcode: l.barcode, title: l.title, overdueDays: l.overdueDays, dueAt: l.dueAt, loanOpen: l.loanOpen,
    })));
    this.manualAmount = d.ticket.manualAmount ?? d.ticket.total;
    this.discount = d.ticket.discount;
    this.paid = d.ticket.paid;
    this.note = d.ticket.note ?? '';
  }
}
