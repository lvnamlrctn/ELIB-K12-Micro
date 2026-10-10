import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, FineLine, FineReason, FineTicketDetail, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';

interface EditLine { id: number | null; reasonCode: string; amount: number; barcode: string | null; title: string | null; overdueDays: number; dueAt: string | null; loanOpen: boolean; }

/**
 * Chi tiết phiếu phạt (monolith: /admin/fine-ticket/:publicId, quyền FINES). Sửa lý do/số tiền từng dòng, thêm dòng theo ĐKCB hoặc khoản
 * tự do, giảm trừ, tiền nộp, hoàn thành phiếu. Dòng lý do "Mất tài liệu" khi lưu đóng lượt mượn và báo kho mất sách.
 */
@Component({
  selector: 'app-fine-ticket',
  imports: [FormsModule, DatePipe, DecimalPipe, RouterLink, Loading],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/fines" class="icon-btn text-gray-500 hover:bg-gray-100" title="Danh sách phiếu phạt"><span class="material-icons">arrow_back</span></a>
      <h4 class="page-title">Phiếu phạt {{ detail()?.ticket?.code ?? '' }}</h4>
      @if (detail(); as d) {
        <span [class]="d.ticket.status === 2 ? 'badge-on' : 'badge-info'">{{ d.ticket.status === 2 ? 'Đã hoàn thành' : 'Đang xử lý' }}</span>
      }
    </div>

    @if (detail(); as d) {
      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 text-sm">
          <div><div class="text-gray-500 text-xs">Bạn đọc</div><div class="font-medium">{{ d.ticket.readerName ?? '—' }}</div><div class="font-mono text-xs text-gray-500">{{ d.ticket.cardNo }}</div></div>
          <div><div class="text-gray-500 text-xs">Ngày phạt</div><div>{{ d.ticket.fineDate | date: 'dd/MM/yyyy HH:mm' }}</div></div>
          <div><div class="text-gray-500 text-xs">Lần phạt</div><div>{{ d.ticket.round }}</div></div>
          <div><div class="text-gray-500 text-xs">Tiền phạt / ngày quá hạn</div><div>{{ d.finePerDay | number: '1.0-0' }} đ</div></div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mb-5">
        @if (busy()) { <app-loading /> }
        <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">Dòng phạt ({{ lines().length }})</div>
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>
              <th class="th w-32">Số ĐKCB</th><th class="th">Nhan đề</th><th class="th w-28">Hạn trả</th><th class="th w-24 !text-center">Ngày quá hạn</th>
              <th class="th w-56">Lý do</th><th class="th w-40 !text-right">Số tiền (đ)</th><th class="th w-14"></th>
            </tr></thead>
            <tbody>
              @for (l of lines(); track $index) {
                <tr>
                  <td class="td font-mono text-[13px]">
                    @if (l.id === null && editable()) {
                      <input class="input !py-1 font-mono" [(ngModel)]="l.barcode" placeholder="Không bắt buộc" [attr.aria-label]="'ĐKCB dòng ' + ($index + 1)" />
                    } @else { {{ l.barcode ?? '—' }} }
                  </td>
                  <td class="td">{{ l.title ?? (l.id === null ? 'Dòng mới' : '—') }}
                    @if (l.barcode && l.id !== null) { <div class="text-[11px]" [class.text-amber-700]="l.loanOpen" [class.text-gray-400]="!l.loanOpen">{{ l.loanOpen ? 'Đang mượn' : 'Đã đóng lượt mượn' }}</div> }
                  </td>
                  <td class="td text-[13px]">{{ l.dueAt ? (l.dueAt | date: 'dd/MM/yyyy') : '—' }}</td>
                  <td class="td text-center">{{ l.overdueDays || '' }}</td>
                  <td class="td">
                    <select class="input !py-1" [(ngModel)]="l.reasonCode" (ngModelChange)="reasonChanged(l)" [disabled]="!editable()" [attr.aria-label]="'Lý do dòng ' + ($index + 1)">
                      @for (r of reasons(); track r.code) { <option [value]="r.code">{{ r.name }}</option> }
                    </select>
                  </td>
                  <td class="td"><input type="number" min="0" class="input !py-1 text-right" [(ngModel)]="l.amount" (ngModelChange)="version.update(bump)" [disabled]="!editable()" [attr.aria-label]="'Số tiền dòng ' + ($index + 1)" /></td>
                  <td class="td text-center">
                    @if (editable()) {
                      <button class="icon-btn text-red-600 hover:bg-red-50" title="Xoá dòng" (click)="removeLine($index)"><span class="material-icons text-[18px]">close</span></button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="py-6 text-center text-gray-500">Phiếu thủ công — không có dòng tài liệu</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (editable()) {
          <div class="px-5 py-3 border-t border-gray-100">
            <button class="btn-secondary !py-1.5" (click)="addLine()"><span class="material-icons text-[18px]">add</span> Thêm dòng phạt</button>
          </div>
        }
      </div>

      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <label for="ft-total" class="field-label">Tổng tiền phạt (đ)</label>
            @if (lines().length === 0) {
              <input id="ft-total" type="number" min="0" class="input" [(ngModel)]="manualAmount" [disabled]="!can('edit')" />
            } @else {
              <div id="ft-total" class="input bg-gray-50">{{ total() | number: '1.0-0' }}</div>
            }
          </div>
          <div>
            <label for="ft-discount" class="field-label">Giảm trừ (đ)</label>
            <input id="ft-discount" type="number" min="0" class="input" [(ngModel)]="discount" [disabled]="!can('edit')" />
          </div>
          <div>
            <label for="ft-paid" class="field-label">Đã nộp (đ)</label>
            <div class="flex gap-2">
              <input id="ft-paid" type="number" min="0" class="input" [(ngModel)]="paid" [disabled]="!can('edit')" />
              @if (can('edit')) { <button class="btn-secondary !px-2 whitespace-nowrap" title="Nộp đủ số còn lại" (click)="payAll()">Nộp đủ</button> }
            </div>
          </div>
          <div>
            <div class="field-label">Còn phải nộp</div>
            <div class="text-xl font-semibold pt-1" [class.text-red-600]="remaining() > 0">{{ remaining() | number: '1.0-0' }} đ</div>
          </div>
          <div class="md:col-span-4">
            <label for="ft-note" class="field-label">Ghi chú</label>
            <input id="ft-note" class="input" maxlength="1000" [(ngModel)]="note" [disabled]="!can('edit')" />
          </div>
        </div>
        @if (can('edit')) {
          <div class="flex justify-end gap-2 mt-4">
            @if (d.ticket.status === 2) {
              <button class="btn-secondary" [disabled]="busy()" (click)="save(1)">Mở lại phiếu</button>
            } @else {
              <button class="btn-secondary" [disabled]="busy()" (click)="save(1)"><span class="material-icons text-[18px]">save</span> Lưu</button>
              <button class="btn-primary" [disabled]="busy()" (click)="save(2)"><span class="material-icons text-[18px]">task_alt</span> Lưu và hoàn thành</button>
            }
          </div>
        }
      </div>
    } @else if (!busy()) {
      <div class="panel text-gray-500">Không tìm thấy phiếu phạt.</div>
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

  protected async save(status: number): Promise<void> {
    const d = this.detail();
    if (!d) return;
    this.busy.set(true);
    try {
      const saved = await this.api.saveFineTicket(d.ticket.publicId, {
        status,
        discount: Number(this.discount) || 0,
        paid: Number(this.paid) || 0,
        note: this.note.trim() || null,
        manualAmount: this.lines().length === 0 ? Number(this.manualAmount) || 0 : null,
        lines: this.editable()
          ? this.lines().map((l) => ({ id: l.id, reasonCode: l.reasonCode, amount: Number(l.amount) || 0, barcode: l.id === null ? l.barcode?.trim() || null : null }))
          : [],
        deletedLineIds: this.editable() ? this.deleted : [],
      });
      this.apply(saved);
      this.toastr.success(status === 2 ? `Đã hoàn thành phiếu ${saved.ticket.code}.` : `Đã lưu phiếu ${saved.ticket.code}.`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
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
