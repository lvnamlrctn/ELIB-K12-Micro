import { formatDate } from '@angular/common';
import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { Api, CheckoutLine, CircPlace, Loan, ReaderPanel, ReturnResult, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe, Language } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';

const PLACE_KEY = 'elib.circulation.place';

/**
 * Quầy mượn trả (monolith: /admin/borrow, quyền BORROW). Quét thẻ → thông tin bạn đọc + lượt đang mượn; quét ĐKCB → mượn ngay.
 * Ô "Trả sách" nhận ĐKCB của bất kỳ bạn đọc nào (không cần quét thẻ). Trả tài liệu quá hạn → hộp thoại như ELIB-LRC:
 * Huỷ / Lập phiếu phạt (trả + mở phiếu phạt quá hạn của bạn đọc) / Trả không phạt.
 */
@Component({
  selector: 'app-borrow',
  imports: [FormsModule, LDatePipe, LNumberPipe, Modal, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'BORROW_PAGE.TITLE' | translate }}</h4>
      <div class="ml-auto flex items-center gap-2">
        <label for="bw-place" class="text-sm text-gray-600">{{ 'BORROW_PAGE.PLACE' | translate }}</label>
        <select id="bw-place" class="input !w-64" [ngModel]="placeId()" (ngModelChange)="choosePlace($event)">
          @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}@if (p.kind === 2) { ({{ 'CIRC_PLACE_PAGE.KIND_READING' | translate }}) }</option> }
        </select>
      </div>
    </div>

    <div class="grid grid-cols-1 xl:grid-cols-3 gap-5">
      <div class="xl:col-span-2 space-y-5">
        <div class="panel !mb-0">
          <div class="flex gap-3 items-end flex-wrap">
            <div class="flex-1 min-w-[220px]">
              <label for="bw-card" class="form-label">{{ 'BORROW_PAGE.CARD' | translate }}</label>
              <input id="bw-card" #cardInput class="input font-mono" [(ngModel)]="cardNo" (keydown.enter)="loadReader()" [placeholder]="'BORROW_PAGE.CARD_PH' | translate" autocomplete="off" />
            </div>
            <button type="button" class="btn-primary" [disabled]="busy() || !cardNo.trim()" (click)="loadReader()">
              <span class="material-icons text-[18px]">person_search</span> {{ 'BORROW_PAGE.VIEW_READER' | translate }}</button>
          </div>

          @if (reader(); as r) {
            <div class="mt-4 flex gap-4 items-start border-t border-gray-100 pt-4">
              <div class="w-20 h-24 rounded-lg bg-gray-100 overflow-hidden flex items-center justify-center shrink-0">
                @if (photoUrl()) { <img [src]="photoUrl()" [alt]="'BORROW_PAGE.PHOTO_ALT' | translate" class="w-full h-full object-cover" /> }
                @else { <span class="material-icons text-gray-300 text-[40px]">person</span> }
              </div>
              <div class="flex-1 min-w-0">
                <div class="text-lg font-semibold text-gray-800">{{ r.fullName }}</div>
                <div class="text-sm text-gray-600">
                  <span [innerHTML]="'BORROW_PAGE.CARD_LINE' | translate: { card: r.cardNo }"></span>
                  @if (r.readerTypeName) { · {{ r.readerTypeName }} }
                  @if (r.className) { · {{ 'BORROW_PAGE.CLASS' | translate: { name: r.className } }} }
                  @if (r.courseName) { · {{ 'BORROW_PAGE.COURSE' | translate: { name: r.courseName } }} }
                  @if (r.expireDate) { · {{ 'BORROW_PAGE.CARD_EXPIRY' | translate: { date: (r.expireDate | ldate: 'dd/MM/yyyy') } }} }
                </div>
                <div class="text-xs text-gray-500 mt-1">
                  {{ policy(r) }}
                </div>
                @if (r.blockReason) { <div class="alert-error !block mt-2">{{ r.blockReason }}</div> }
                @if (r.unpaidFines > 0 || (r.hasOverdue && canFine())) {
                  <div class="mt-2 flex items-center gap-3 flex-wrap text-sm">
                    @if (r.unpaidFines > 0) { <span class="badge-danger">{{ 'BORROW_PAGE.UNPAID' | translate: { amount: (r.unpaidFines | lnumber: '1.0-0') } }}</span> }
                    @if (canFine()) {
                      <button type="button" class="btn-secondary !py-1 !px-2.5 !text-xs" [disabled]="busy()" (click)="fine([])">
                        <span class="material-icons text-[16px]">gavel</span> {{ 'BORROW_PAGE.CREATE_FINE' | translate }}</button>
                    }
                  </div>
                }
              </div>
            </div>

            @if (r.canBorrow && can('add')) {
              <div class="mt-4 flex gap-3 items-end flex-wrap">
                <div class="flex-1 min-w-[220px]">
                  <label for="bw-item" class="form-label">{{ 'BORROW_PAGE.SCAN_ITEM' | translate }}</label>
                  <input id="bw-item" #itemInput class="input font-mono" [(ngModel)]="barcode" (keydown.enter)="checkout()" [placeholder]="'BORROW_PAGE.BARCODE' | translate" autocomplete="off" />
                </div>
                <button type="button" class="btn-primary" [disabled]="busy() || !barcode.trim()" (click)="checkout()">
                  <span class="material-icons text-[18px]">outbox</span> {{ 'BORROW_PAGE.CHECKOUT' | translate }}</button>
              </div>
            }
            @if (lines().length) {
              <ul class="mt-3 space-y-1 text-sm">
                @for (l of lines(); track $index) {
                  <li [class]="l.success ? 'text-green-700' : 'text-red-600'"><span class="font-mono">{{ l.barcode }}</span>: {{ l.message }}</li>
                }
              </ul>
            }
          }
        </div>

        @if (reader()?.holds?.length) {
          <div class="panel !mb-0">
            <div class="font-semibold text-gray-800 mb-2">{{ 'BORROW_PAGE.HOLDS' | translate: { count: reader()!.holds!.length } }}</div>
            <ul class="space-y-1.5 text-sm">
              @for (h of reader()!.holds!; track h.publicId) {
                <li class="flex items-center gap-2 flex-wrap">
                  <span [class]="h.status === 2 ? 'badge-info' : 'badge-warn'">{{ (h.status === 2 ? 'BORROW_PAGE.HOLD_HELD' : 'BORROW_PAGE.HOLD_WAITING') | translate }}</span>
                  <span>{{ h.title ?? 'MFN ' + h.mfn }}</span>
                  @if (h.status === 2) { <span class="text-gray-600" [innerHTML]="'BORROW_PAGE.HOLD_COPY' | translate: { barcode: h.barcode, date: (h.expiresAt | ldate: 'dd/MM/yyyy') }"></span> }
                  @else if (h.queuePosition) { <span class="text-gray-500">{{ 'BORROW_PAGE.HOLD_QUEUE' | translate: { n: h.queuePosition } }}</span> }
                </li>
              }
            </ul>
          </div>
        }

        @if (reader(); as r) {
          <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-100 font-semibold text-gray-800">{{ 'BORROW_PAGE.ON_LOAN' | translate: { count: r.currentLoans.length } }}</div>
            <div class="overflow-x-auto">
              <table class="w-full text-left border-collapse min-w-[760px]">
                <thead><tr>
                  <th class="th w-32">{{ 'BORROW_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'BORROW_PAGE.TITLE_COL' | translate }}</th><th class="th w-28">{{ 'BORROW_PAGE.LOANED' | translate }}</th><th class="th w-28">{{ 'BORROW_PAGE.DUE' | translate }}</th>
                  <th class="th w-20 !text-center">{{ 'BORROW_PAGE.RENEWALS' | translate }}</th><th class="th w-40 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
                </tr></thead>
                <tbody>
                  @for (l of r.currentLoans; track l.publicId) {
                    <tr class="hover:bg-gray-50/50">
                      <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                      <td class="td">{{ l.title ?? '—' }} @if (l.note) { <div class="text-[11px] text-amber-700">{{ l.note }}</div> }</td>
                      <td class="td text-[13px]">{{ l.loanedAt | ldate: 'dd/MM/yyyy' }}</td>
                      <td class="td text-[13px]" [class.text-red-600]="overdue(l)" [class.font-semibold]="overdue(l)">{{ l.dueAt | ldate: 'dd/MM/yyyy' }}</td>
                      <td class="td text-center">{{ l.renewCount }}</td>
                      <td class="td">
                        @if (can('edit')) {
                          <div class="flex items-center justify-center gap-1.5">
                            <button class="btn-secondary !py-1 !px-2.5 !text-xs" [disabled]="busy()" (click)="returnLoan(l)">{{ 'BORROW_PAGE.RETURN' | translate }}</button>
                            <button class="btn-secondary !py-1 !px-2.5 !text-xs" [disabled]="busy()" (click)="openAction(l, 'renew')">{{ 'BORROW_PAGE.RENEW' | translate }}</button>
                            <button class="icon-btn text-gray-500 hover:bg-gray-100" [title]="'BORROW_PAGE.NOTE' | translate" (click)="openAction(l, 'note')"><span class="material-icons text-[18px]">edit_note</span></button>
                            @if (canFine()) {
                              <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'BORROW_PAGE.REPORT_LOST' | translate" (click)="fine([l.publicId])"><span class="material-icons text-[18px]">report</span></button>
                            }
                          </div>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr><td colspan="6" class="py-6 text-center text-gray-500">{{ 'BORROW_PAGE.NO_LOANS' | translate }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </div>

      <div class="panel !mb-0 h-fit">
        <label for="bw-return" class="form-label">{{ 'BORROW_PAGE.RETURN_SCAN' | translate }}</label>
        <div class="flex gap-2">
          <input id="bw-return" class="input font-mono" [(ngModel)]="returnCode" (keydown.enter)="quickReturn()" [placeholder]="'BORROW_PAGE.BARCODE' | translate" autocomplete="off" [disabled]="!can('edit')" />
          <button type="button" class="btn-primary" [disabled]="busy() || !returnCode.trim() || !can('edit')" (click)="quickReturn()">{{ 'BORROW_PAGE.RETURN' | translate }}</button>
        </div>
        <ul class="mt-4 space-y-2">
          @for (r of returns(); track r.loan.publicId) {
            <li class="text-sm border border-gray-100 rounded-lg p-2.5" [class.border-red-200]="r.overdueDays > 0" [class.bg-red-50]="r.overdueDays > 0">
              <div><span class="font-mono font-medium">{{ r.loan.barcode }}</span> — {{ r.loan.title ?? '' }}</div>
              <div class="text-xs text-gray-600">{{ 'BORROW_PAGE.RETURNED_LINE' | translate: { name: r.loan.readerName ?? '', card: r.loan.cardNo, date: (r.loan.loanedAt | ldate: 'dd/MM/yyyy') } }}</div>
              @if (r.overdueDays > 0) { <div class="text-xs font-semibold text-red-600">{{ 'BORROW_PAGE.OVERDUE_DAYS' | translate: { days: r.overdueDays } }}</div> }
              @if (r.holdFor; as h) {
                <div class="text-xs font-semibold text-blue-700 mt-1">{{ 'BORROW_PAGE.HOLD_SHELF' | translate: { name: h.readerName ?? '', card: h.cardNo, date: (h.expiresAt | ldate: 'dd/MM/yyyy') } }}</div>
              }
            </li>
          }
        </ul>
      </div>
    </div>

    @if (action(); as a) {
      <app-modal [title]="(a.kind === 'renew' ? 'BORROW_PAGE.RENEW_TITLE' : 'BORROW_PAGE.NOTE_TITLE') | translate: { barcode: a.loan.barcode }" widthClass="max-w-md" (closed)="action.set(null)">
        <div class="space-y-4">
          @if (a.kind === 'note') {
            <div>
              <label for="bw-note" class="form-label">{{ 'BORROW_PAGE.NOTE' | translate }}</label>
              <input id="bw-note" class="input" maxlength="1000" [(ngModel)]="noteText" />
            </div>
          } @else {
            <p class="text-sm text-gray-600">{{ 'BORROW_PAGE.CURRENT_DUE' | translate: { date: (a.loan.dueAt | ldate: 'dd/MM/yyyy'), count: a.loan.renewCount } }}</p>
          }
          <div>
            <label for="bw-reason" class="form-label">{{ 'BORROW_PAGE.REASON' | translate }}</label>
            <input id="bw-reason" class="input" maxlength="500" [(ngModel)]="reason" [placeholder]="'BORROW_PAGE.REQUIRED' | translate" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="action.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !reason.trim()" (click)="runAction(a.loan, a.kind)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (overdueReturn(); as o) {
      <app-modal [title]="'BORROW_PAGE.OVERDUE_CONFIRM_TITLE' | translate" widthClass="max-w-lg" (closed)="overdueReturn.set(null)">
        <div class="space-y-3 text-sm text-gray-700">
          <p [innerHTML]="'BORROW_PAGE.OVERDUE_CONFIRM' | translate: { barcode: o.barcode, name: o.readerName ?? '', card: o.cardNo, date: (o.dueAt | ldate: 'dd/MM/yyyy'), days: overdueDays(o) }"></p>
          @if (o.title) { <p class="text-gray-500">{{ o.title }}</p> }
          <p class="text-xs text-gray-500">{{ 'BORROW_PAGE.OVERDUE_CONFIRM_HINT' | translate }}</p>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" [disabled]="busy()" (click)="overdueReturn.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          @if (canFine()) {
            <button type="button" class="btn-danger" [disabled]="busy()" (click)="returnWithFine(o)">
              <span class="material-icons text-[18px]">gavel</span> {{ 'BORROW_PAGE.RETURN_WITH_FINE' | translate }}</button>
          }
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="returnNoFine(o)">{{ 'BORROW_PAGE.RETURN_NO_FINE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class Borrow implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);

  /** "Chính sách: mượn 14 ngày, tối đa 5 tài liệu, gia hạn tối đa 2 lần." */
  protected policy(r: ReaderPanel): string {
    let text = this.language.t('BORROW_PAGE.POLICY', { days: r.loanDays });
    if (r.maxLoans != null) text += this.language.t('BORROW_PAGE.POLICY_MAX', { count: r.maxLoans });
    if (r.maxRenewals != null) text += this.language.t('BORROW_PAGE.POLICY_RENEW', { count: r.maxRenewals });
    return text + '.';
  }
  private readonly router = inject(Router);
  private readonly itemInput = viewChild<ElementRef<HTMLInputElement>>('itemInput');

  protected readonly places = signal<CircPlace[]>([]);
  protected readonly placeId = signal<number | null>(null);
  protected readonly reader = signal<ReaderPanel | null>(null);
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly lines = signal<CheckoutLine[]>([]);
  protected readonly returns = signal<ReturnResult[]>([]);
  protected readonly busy = signal(false);
  protected readonly action = signal<{ loan: Loan; kind: 'renew' | 'note' } | null>(null);
  /** Lượt quá hạn đang chờ cán bộ chọn Huỷ / Lập phiếu phạt / Trả không phạt. */
  protected readonly overdueReturn = signal<Loan | null>(null);
  protected cardNo = '';
  protected barcode = '';
  protected returnCode = '';
  protected reason = '';
  protected noteText = '';

  async ngOnInit(): Promise<void> {
    try {
      const places = await this.api.crud<CircPlace>('circ-places', 'circulation').searchAll();
      this.places.set(places);
      const saved = Number(readSaved());
      this.placeId.set(places.find((p) => p.id === saved)?.id ?? places[0]?.id ?? null);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected can(action: string): boolean {
    return this.session.can(`BORROW:${action}`);
  }

  protected canFine(): boolean {
    return this.session.can('FINES:add');
  }

  /** Gom tài liệu quá hạn (+ lượt chọn — báo mất) vào phiếu phạt đang mở của bạn đọc, mở phiếu để thu tiền. */
  protected async fine(loanIds: string[]): Promise<void> {
    const r = this.reader();
    if (!r) return;
    await this.run(async () => {
      const detail = await this.api.buildFineTicket(r.cardNo, loanIds);
      await this.router.navigate(['/fine-ticket', detail.ticket.publicId]);
    });
  }

  protected overdue(l: Loan): boolean {
    return new Date(l.dueAt).getTime() < Date.now();
  }

  /** Số ngày quá hạn theo ngày lịch (trả trong ngày hết hạn = không quá hạn) — như Loan.OverdueDays của circulation. */
  protected overdueDays(l: Loan): number {
    const day = (d: Date) => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) / 86_400_000;
    return Math.max(0, day(new Date()) - day(new Date(l.dueAt)));
  }

  protected choosePlace(id: number | null): void {
    this.placeId.set(id);
    try {
      localStorage.setItem(PLACE_KEY, String(id ?? ''));
    } catch {
      /* trình duyệt chặn lưu — lần sau chọn lại */
    }
  }

  protected async loadReader(): Promise<void> {
    const card = this.cardNo.trim();
    if (!card) return;
    await this.run(async () => {
      const panel = await this.api.loanReader(card, this.placeId());
      this.reader.set(panel);
      this.lines.set([]);
      this.photoUrl.set(null);
      if (panel.photoId) this.api.fileUrl(panel.photoId).then((u) => this.photoUrl.set(u), () => this.photoUrl.set(null));
      setTimeout(() => this.itemInput()?.nativeElement.focus());
    });
  }

  protected async checkout(): Promise<void> {
    const code = this.barcode.trim();
    const r = this.reader();
    const place = this.placeId();
    if (!code || !r) return;
    if (place == null) {
      this.toastr.error('BORROW_PAGE.CHOOSE_PLACE');
      return;
    }
    await this.run(async () => {
      const result = await this.api.checkout(r.cardNo, [code], place);
      this.lines.update((all) => [...result.lines, ...all].slice(0, 20));
      this.barcode = '';
      await this.refresh();
    });
  }

  protected async returnLoan(l: Loan): Promise<void> {
    if (this.overdueDays(l) > 0) {
      this.overdueReturn.set(l);
      return;
    }
    await this.run(() => this.doReturn(l));
  }

  /** Trả theo ĐKCB quét được — tra lượt đang mở trước để hỏi khi quá hạn; không tra được (thiếu quyền...) thì trả thẳng như cũ. */
  protected async quickReturn(): Promise<void> {
    const code = this.returnCode.trim();
    if (!code) return;
    await this.run(async () => {
      const open = await this.api.searchLoans({ barcode: code, state: 'open', pageIndex: 1, pageSize: 1 }).then((p) => p.items[0] ?? null, () => null);
      if (open && this.overdueDays(open) > 0) {
        this.overdueReturn.set(open);
        return;
      }
      const result = await this.api.returnLoan({ barcode: code, circPlaceId: this.placeId() });
      this.afterReturn(result);
    });
  }

  protected async returnNoFine(l: Loan): Promise<void> {
    await this.run(async () => {
      await this.doReturn(l);
      this.overdueReturn.set(null);
    });
  }

  /** Lập phiếu phạt TRƯỚC khi trả (phiếu gom lượt đang mượn quá hạn, tính số ngày đến hôm nay), rồi trả và mở phiếu để thu tiền. */
  protected async returnWithFine(l: Loan): Promise<void> {
    await this.run(async () => {
      const detail = await this.api.buildFineTicket(l.cardNo, [], this.placeId());
      await this.doReturn(l);
      this.overdueReturn.set(null);
      await this.router.navigate(['/fine-ticket', detail.ticket.publicId]);
    });
  }

  private async doReturn(l: Loan): Promise<void> {
    this.afterReturn(await this.api.returnLoan({ loanId: l.publicId, circPlaceId: this.placeId() }));
  }

  private afterReturn(result: ReturnResult): void {
    this.returns.update((all) => [result, ...all].slice(0, 20));
    if (this.returnCode.trim() && result.loan.barcode.toUpperCase() === this.returnCode.trim().toUpperCase()) this.returnCode = '';
    if (this.reader()?.readerPublicId === result.loan.readerPublicId) void this.refresh();
  }

  protected openAction(loan: Loan, kind: 'renew' | 'note'): void {
    this.reason = '';
    this.noteText = loan.note ?? '';
    this.action.set({ loan, kind });
  }

  protected async runAction(loan: Loan, kind: 'renew' | 'note'): Promise<void> {
    await this.run(async () => {
      if (kind === 'renew') {
        const renewed = await this.api.renewLoan(loan.publicId, this.reason.trim());
        this.toastr.success('BORROW_PAGE.RENEWED', { barcode: renewed.barcode, date: formatDate(renewed.dueAt, 'dd/MM/yyyy', this.language.locale()) });
      } else {
        await this.api.noteLoan(loan.publicId, this.noteText, this.reason.trim());
        this.toastr.success('BORROW_PAGE.NOTE_SAVED');
      }
      this.action.set(null);
      await this.refresh();
    });
  }

  private async refresh(): Promise<void> {
    const r = this.reader();
    if (r) this.reader.set(await this.api.loanReader(r.cardNo, this.placeId()));
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function readSaved(): string | null {
  try {
    return localStorage.getItem(PLACE_KEY);
  } catch {
    return null;
  }
}
