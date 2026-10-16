import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** sessionStorage: phiếu mượn cần in — quầy mượn trả ghi trước khi mở trang in. */
export const LOAN_SLIP_KEY = 'elib.print.loan-slip';

export interface LoanSlip {
  at: string;
  cardNo: string;
  fullName: string;
  className: string | null;
  loans: { title: string; barcode: string; loanedOn: string; dueOn: string; note: string | null }[];
}

/**
 * Phiếu mượn khổ nhỏ (ELIB-LRC: in biên lai ở màn mượn trả): số thẻ, họ tên, lớp, từng tài liệu đang mượn với ngày mượn / hạn trả.
 * Trang in riêng (ngoài khung quản trị) để bản in chỉ có nội dung phiếu.
 */
@Component({
  selector: 'app-loan-slip',
  imports: [RouterLink, TranslatePipe],
  styles: `
    :host { display: block; background: #fff; }
    @media print {
      @page { size: 80mm auto; margin: 4mm; }
      .no-print { display: none !important; }
    }
  `,
  template: `
    <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-4 py-3 flex items-center gap-3">
      <a routerLink="/borrow" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">arrow_back</span> {{ 'BORROW_PAGE.TITLE' | translate }}</a>
      <span class="flex-1"></span>
      <button type="button" class="btn-primary" [disabled]="!slip()" (click)="print()"><span class="material-icons text-[18px]">print</span> {{ 'BORROW_PAGE.PRINT' | translate }}</button>
    </div>
    @if (slip(); as s) {
      <div class="max-w-xs mx-auto my-4 font-mono text-xs text-black">
        <div class="text-center border-b border-dashed border-black pb-2 mb-2">
          <div class="font-bold text-sm">{{ 'BORROW_PAGE.RECEIPT_HEADER' | translate }}</div>
          <div>{{ s.at }}</div>
        </div>
        <div class="mb-2">
          <div>{{ 'BORROW_PAGE.CARD' | translate }}: {{ s.cardNo }}</div>
          <div>{{ s.fullName }}</div>
          @if (s.className) { <div>{{ 'BORROW_PAGE.CLASS' | translate: { name: s.className } }}</div> }
        </div>
        <div class="border-t border-dashed border-black pt-2">
          @for (l of s.loans; track $index) {
            <div class="mb-2">
              <div class="font-semibold">{{ l.title }}</div>
              <div>{{ l.barcode }}</div>
              <div class="flex justify-between"><span>{{ l.loanedOn }}</span><span>{{ 'BORROW_PAGE.DUE' | translate }}: {{ l.dueOn }}</span></div>
              @if (l.note) { <div>{{ l.note }}</div> }
            </div>
          }
        </div>
        <div class="border-t border-dashed border-black pt-2 text-center">{{ 'BORROW_PAGE.RECEIPT_TOTAL' | translate }}: {{ s.loans.length }}</div>
      </div>
    } @else {
      <p class="no-print text-center text-gray-500 py-24">{{ 'BORROW_PAGE.SLIP_EMPTY' | translate }}</p>
    }
  `,
})
export class LoanSlipPage implements OnInit {
  protected readonly slip = signal<LoanSlip | null>(null);

  ngOnInit(): void {
    try {
      const slip = JSON.parse(sessionStorage.getItem(LOAN_SLIP_KEY) ?? 'null') as LoanSlip | null;
      if (slip && Array.isArray(slip.loans)) this.slip.set(slip);
    } catch {
      /* chưa có phiếu — hiện trang trống */
    }
  }

  protected print(): void {
    window.print();
  }
}
