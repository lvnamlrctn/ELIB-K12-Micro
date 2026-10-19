import { Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CircPlace, GateStatus, Visit, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';

const PLACE_KEY = 'elib.checkinout.place';

/**
 * Check-in / Check-out (monolith: Receiption/Check, quyền CHECK_IN_OUT) — quét thẻ vào/ra cổng thư viện, khác mượn/trả tài liệu
 * (/borrow). Thẻ bạn đọc tái dùng panel của quầy mượn trả (lưu thông tin đang mượn/nợ phạt) + lớp trạng thái vào/ra riêng.
 * Quét vào 2 lần không tạo trùng — backend tự trả lại lượt đang mở (idempotent).
 */
@Component({
  selector: 'app-check-in-out',
  imports: [FormsModule, LDatePipe, LNumberPipe, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center justify-between">
      <h4 class="page-title">{{ 'CHECK_IN_OUT_PAGE.TITLE' | translate }}</h4>
      <button type="button" class="btn-secondary" (click)="history()">
        <span class="material-icons text-[18px]">history</span> {{ 'CHECK_IN_OUT_PAGE.HISTORY_LINK' | translate }}</button>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-3 items-start">
        <div>
          <label for="gate-place" class="form-label">{{ 'CHECK_IN_OUT_PAGE.PLACE' | translate }}</label>
          <select id="gate-place" class="input" [ngModel]="placeId()" (ngModelChange)="choosePlace($event)">
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div class="md:col-span-2">
          <label for="gate-card" class="form-label">{{ 'CHECK_IN_OUT_PAGE.CARD' | translate }}</label>
          <div class="flex gap-2">
            <input id="gate-card" #cardInput class="input font-mono" [(ngModel)]="cardNo" (keydown.enter)="lookup()"
                   [placeholder]="'CHECK_IN_OUT_PAGE.CARD_PH' | translate" autocomplete="off" />
            <button type="button" class="btn-primary !px-3" [disabled]="busy() || !cardNo.trim()" (click)="lookup()">
              <span class="material-icons text-[18px]">person_search</span></button>
          </div>
        </div>
      </div>
    </div>

    @if (status(); as s) {
      <div class="panel">
        <div class="flex flex-col md:flex-row md:items-center gap-5">
          <div class="flex-1 min-w-0 grid grid-cols-2 md:grid-cols-3 gap-x-4 gap-y-1 text-sm">
            <div><span class="text-gray-400">{{ 'CHECK_IN_OUT_PAGE.CARD' | translate }}:</span> <span class="font-semibold font-mono">{{ s.reader.cardNo }}</span></div>
            <div class="md:col-span-2 flex items-center gap-2 flex-wrap">
              <span class="font-semibold">{{ s.reader.fullName }}</span>
              <span [class]="s.reader.isLocked ? 'badge-danger' : 'badge-on'">{{ (s.reader.isLocked ? 'BORROW_PAGE.LOCKED' : 'BORROW_PAGE.NORMAL') | translate }}</span>
              <span [class]="s.checkedIn ? 'badge-on' : 'badge-off'">{{ (s.checkedIn ? 'CHECK_IN_OUT_PAGE.INSIDE' : 'CHECK_IN_OUT_PAGE.OUTSIDE') | translate }}</span>
            </div>
            <div><span class="text-gray-400">{{ 'BORROW_PAGE.READER_TYPE' | translate }}:</span> {{ s.reader.readerTypeName ?? '—' }}</div>
            <div><span class="text-gray-400">{{ 'BORROW_PAGE.CLASS_LABEL' | translate }}:</span> {{ s.reader.className ?? '—' }}</div>
            <div><span class="text-gray-400">{{ 'CHECK_IN_OUT_PAGE.VISIT_COUNT' | translate }}:</span> {{ s.visitCount }}</div>
            @if (s.checkedIn && s.checkedInAt) {
              <div class="col-span-2 md:col-span-3"><span class="text-gray-400">{{ 'CHECK_IN_OUT_PAGE.CHECKED_IN_AT' | translate }}:</span> {{ s.checkedInAt | ldate: 'dd/MM/yyyy HH:mm' }}</div>
            }
          </div>
          <button type="button" class="btn-primary !px-6 !py-3 text-base shrink-0" [class.!bg-amber-600]="s.checkedIn" [class.hover:!bg-amber-700]="s.checkedIn"
                  [disabled]="busy() || placeId() == null" (click)="s.checkedIn ? checkOut(s) : checkIn()">
            <span class="material-icons">{{ s.checkedIn ? 'logout' : 'login' }}</span>
            {{ (s.checkedIn ? 'CHECK_IN_OUT_PAGE.SCAN_OUT' : 'CHECK_IN_OUT_PAGE.SCAN_IN') | translate }}</button>
        </div>
        @if (s.reader.currentLoans.length || s.reader.unpaidFines > 0) {
          <div class="mt-3 pt-3 border-t border-gray-100 flex gap-4 flex-wrap text-xs text-gray-500">
            <span>{{ 'BORROW_PAGE.STAT_BORROWING' | translate }}: {{ s.reader.currentLoans.length }}</span>
            @if (s.reader.unpaidFines > 0) { <span class="text-red-600 font-medium">{{ 'BORROW_PAGE.UNPAID' | translate: { amount: (s.reader.unpaidFines | lnumber: '1.0-0') } }}</span> }
          </div>
        }
      </div>
    } @else {
      <div class="panel text-center text-gray-400 py-12">
        <span class="material-icons text-5xl mb-2 text-gray-300">badge</span>
        <p>{{ 'CHECK_IN_OUT_PAGE.SCAN_CARD' | translate }}</p>
      </div>
    }

    @if (recent().length) {
      <div class="panel">
        <div class="font-semibold text-gray-800 mb-2">{{ 'CHECK_IN_OUT_PAGE.RECENT' | translate }}</div>
        <ul class="space-y-1.5 text-sm">
          @for (v of recent(); track v.publicId) {
            <li class="flex items-center gap-2 flex-wrap">
              <span [class]="v.checkedOutAt ? 'badge-off' : 'badge-on'">{{ (v.checkedOutAt ? 'CHECK_IN_OUT_PAGE.OUTSIDE' : 'CHECK_IN_OUT_PAGE.INSIDE') | translate }}</span>
              <span class="font-mono font-medium">{{ v.cardNo }}</span> — {{ v.readerName }}
              <span class="text-gray-500">{{ v.checkedInAt | ldate: 'HH:mm' }}@if (v.checkedOutAt) { → {{ v.checkedOutAt | ldate: 'HH:mm' }} }</span>
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class CheckInOut implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly cardInput = viewChild<ElementRef<HTMLInputElement>>('cardInput');

  protected readonly places = signal<CircPlace[]>([]);
  protected readonly placeId = signal<number | null>(null);
  protected readonly status = signal<GateStatus | null>(null);
  protected readonly recent = signal<Visit[]>([]);
  protected readonly busy = signal(false);
  protected cardNo = '';

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

  protected choosePlace(id: number | null): void {
    this.placeId.set(id);
    try {
      localStorage.setItem(PLACE_KEY, String(id ?? ''));
    } catch {
      /* trình duyệt chặn lưu — lần sau chọn lại */
    }
  }

  protected async lookup(): Promise<void> {
    const card = this.cardNo.trim();
    if (!card) return;
    await this.run(async () => {
      this.status.set(await this.api.gateStatus(card, this.placeId()));
    });
  }

  protected async checkIn(): Promise<void> {
    const place = this.placeId();
    const card = this.cardNo.trim();
    if (place == null || !card) return;
    await this.run(async () => {
      const visit = await this.api.checkInGate(card, place);
      this.recent.update((all) => [visit, ...all].slice(0, 20));
      this.status.set(await this.api.gateStatus(card, place));
      this.toastr.success('CHECK_IN_OUT_PAGE.SCAN_IN_DONE', { name: visit.readerName ?? visit.cardNo });
      this.afterScan();
    });
  }

  protected async checkOut(s: GateStatus): Promise<void> {
    if (!s.visitPublicId) return;
    await this.run(async () => {
      const visit = await this.api.checkOutGate(s.visitPublicId!, this.placeId());
      this.recent.update((all) => [visit, ...all].slice(0, 20));
      this.status.set(await this.api.gateStatus(s.reader.cardNo, this.placeId()));
      this.toastr.success('CHECK_IN_OUT_PAGE.SCAN_OUT_DONE', { name: visit.readerName ?? visit.cardNo });
      this.afterScan();
    });
  }

  private afterScan(): void {
    this.cardNo = '';
    setTimeout(() => this.cardInput()?.nativeElement.focus());
  }

  protected history(): void {
    void this.router.navigate(['/checkin-history']);
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
