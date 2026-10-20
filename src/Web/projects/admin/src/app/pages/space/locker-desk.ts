import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SpaceLockerLoan, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';

/**
 * Quầy mượn/trả tủ đựng đồ (monolith: /admin/borrow-keys — "Receiption/BorrowKey") — 2 ô quét: số thẻ bạn đọc rồi mã ngăn
 * tủ. Mã ngăn tự chuyển mượn↔trả: nếu bạn đọc đang quét đang giữ đúng ngăn đó thì trả, ngược lại thì mượn (đúng hành vi
 * onScanKey của bản gốc). Panel "Ngăn tủ" trong sơ đồ tầng (floor-map-editor.ts) dùng cho thủ thư xem nhanh khi chỉnh sơ
 * đồ; trang này là quầy lưu thông dùng hàng ngày, không phụ thuộc màn sơ đồ.
 */
@Component({
  selector: 'app-space-locker-desk',
  imports: [FormsModule, LDatePipe, TranslatePipe],
  template: `
    <div class="mb-5">
      <h4 class="page-title">{{ 'SPACE_LOCKER_DESK_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label for="desk-card" class="form-label">{{ 'SPACE_LOCKER_DESK_PAGE.CARD' | translate }}</label>
          <input id="desk-card" #cardInput class="input font-mono" [(ngModel)]="cardNo" (keydown.enter)="scanCard()"
                 [placeholder]="'SPACE_LOCKER_DESK_PAGE.CARD_PH' | translate" autocomplete="off" />
        </div>
        <div>
          <label for="desk-compartment" class="form-label">{{ 'SPACE_LOCKER_DESK_PAGE.COMPARTMENT' | translate }}</label>
          <input id="desk-compartment" #compartmentInput class="input font-mono" [(ngModel)]="compartmentCode" (keydown.enter)="scanCompartment()"
                 [disabled]="!reader()" [placeholder]="'SPACE_LOCKER_DESK_PAGE.COMPARTMENT_PH' | translate" autocomplete="off" />
        </div>
      </div>
    </div>

    @if (reader(); as r) {
      <div class="panel">
        <div class="flex items-center gap-2 mb-2">
          <span class="font-semibold font-mono">{{ r.cardNo }}</span>
          <span class="badge-on">{{ 'SPACE_LOCKER_DESK_PAGE.READY_TO_SCAN' | translate }}</span>
        </div>
        @if (readerLoans().length) {
          <div class="text-sm text-gray-600 mb-1">{{ 'SPACE_LOCKER_DESK_PAGE.CURRENTLY_HOLDING' | translate }}</div>
          <ul class="flex flex-wrap gap-1.5">
            @for (l of readerLoans(); track l.id) {
              <li class="badge-warn">{{ l.compartmentCode }}@if (l.cabinetCode) { ({{ l.cabinetCode }}) }</li>
            }
          </ul>
        } @else {
          <p class="text-sm text-gray-400">{{ 'SPACE_LOCKER_DESK_PAGE.HOLDING_NONE' | translate }}</p>
        }
      </div>
    } @else {
      <div class="panel text-center text-gray-400 py-12">
        <span class="material-icons text-5xl mb-2 text-gray-300">badge</span>
        <p>{{ 'SPACE_LOCKER_DESK_PAGE.SCAN_CARD_FIRST' | translate }}</p>
      </div>
    }

    @if (recent().length) {
      <div class="panel">
        <div class="font-semibold text-gray-800 mb-2">{{ 'SPACE_LOCKER_DESK_PAGE.RECENT' | translate }}</div>
        <ul class="space-y-1.5 text-sm">
          @for (l of recent(); track l.publicId) {
            <li class="flex items-center gap-2 flex-wrap">
              <span [class]="l.returnedAt ? 'badge-off' : 'badge-on'">{{ (l.returnedAt ? 'SPACE_LOCKER_DESK_PAGE.RETURNED' : 'SPACE_LOCKER_DESK_PAGE.BORROWED') | translate }}</span>
              <span class="font-mono font-medium">{{ l.compartmentCode }}</span> — <span class="font-mono">{{ l.cardNo }}</span>
              <span class="text-gray-500">{{ l.borrowedAt | ldate: 'HH:mm' }}@if (l.returnedAt) { → {{ l.returnedAt | ldate: 'HH:mm' }} }</span>
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class SpaceLockerDesk {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly compartmentInput = viewChild<ElementRef<HTMLInputElement>>('compartmentInput');

  protected readonly reader = signal<{ cardNo: string } | null>(null);
  protected readonly readerLoans = signal<SpaceLockerLoan[]>([]);
  protected readonly recent = signal<SpaceLockerLoan[]>([]);
  protected readonly busy = signal(false);
  protected cardNo = '';
  protected compartmentCode = '';

  protected async scanCard(): Promise<void> {
    const card = this.cardNo.trim();
    if (!card) return;
    await this.run(async () => {
      const loans = await this.api.lockerLoanSnapshot(card);
      this.reader.set({ cardNo: card });
      this.readerLoans.set(loans);
      this.compartmentCode = '';
      setTimeout(() => this.compartmentInput()?.nativeElement.focus());
    });
  }

  protected async scanCompartment(): Promise<void> {
    const r = this.reader();
    const code = this.compartmentCode.trim();
    if (!r || !code) return;
    const held = this.readerLoans().find((l) => l.compartmentCode?.toUpperCase() === code.toUpperCase());
    await this.run(async () => {
      if (held) {
        const returned = await this.api.returnLocker(code);
        this.recent.update((all) => [returned, ...all].slice(0, 20));
        this.toastr.success('SPACE_LOCKER_DESK_PAGE.RETURN_DONE', { code });
      } else {
        const borrowed = await this.api.borrowLocker(code, r.cardNo);
        this.recent.update((all) => [borrowed, ...all].slice(0, 20));
        this.toastr.success('SPACE_LOCKER_DESK_PAGE.BORROW_DONE', { code });
      }
      this.readerLoans.set(await this.api.lockerLoanSnapshot(r.cardNo));
      this.compartmentCode = '';
      setTimeout(() => this.compartmentInput()?.nativeElement.focus());
    });
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
