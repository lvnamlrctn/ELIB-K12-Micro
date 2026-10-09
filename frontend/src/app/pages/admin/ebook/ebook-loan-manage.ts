import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EbookLoanService } from '../../../services/ebook/ebook-loan.service';
import { EbookLoan } from '../../../models/ebook/ebook-loan';
import { EbookReservationService } from '../../../services/ebook/ebook-reservation.service';
import { EbookReservation } from '../../../models/ebook/ebook-reservation';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

type LoanUiStatus = 'active' | 'expired' | 'recalled';
type Tab = 'loan' | 'reservation';

const RESERVATION_STATUS_LABEL: Record<number, string> = {
  1: 'EBOOK_LOAN_MANAGE.RES_PENDING',
  2: 'EBOOK_LOAN_MANAGE.RES_READY',
  3: 'EBOOK_LOAN_MANAGE.RES_FULFILLED',
  4: 'EBOOK_LOAN_MANAGE.RES_CANCELLED',
  5: 'EBOOK_LOAN_MANAGE.RES_EXPIRED',
};

@Component({
  selector: 'app-ebook-loan-manage',
  standalone: true,
  imports: [CanDirective, CommonModule, FormsModule, TranslateModule, MatIconModule, AppDatePipe, NgSelectModule],
  templateUrl: './ebook-loan-manage.html'
})
export class EbookLoanManagePage implements OnInit, OnDestroy {
  private loanSvc        = inject(EbookLoanService);
  private reservationSvc = inject(EbookReservationService);
  private toastr         = inject(ToastrService);
  public  translate      = inject(TranslateService);
  private destroy$       = new Subject<void>();

  tab = signal<Tab>('loan');
  Math = Math;

  // ── Tab Mượn/Trả ──────────────────────────────────────────────
  loanKeyword = '';
  loanStatus: number | null = null;
  loanData: EbookLoan[] = [];
  isLoadingLoans = signal(false);
  showConfirmRecall = signal(false);
  recallTarget = signal<EbookLoan | null>(null);
  recallReason = '';

  // ── Tab Đặt trước ─────────────────────────────────────────────
  reservationKeyword = '';
  reservationStatus: number | null = null;
  reservationData: EbookReservation[] = [];
  reservationTotal = 0;
  reservationPageIndex = 0;
  reservationPageSize = 10;
  isLoadingReservations = signal(false);

  ngOnInit(): void { this.loadLoans(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  setTab(t: Tab): void {
    this.tab.set(t);
    if (t === 'loan' && this.loanData.length === 0) this.loadLoans();
    if (t === 'reservation' && this.reservationData.length === 0) this.loadReservations();
  }

  // ── Mượn/Trả ──────────────────────────────────────────────────
  loadLoans(): void {
    this.isLoadingLoans.set(true);
    this.loanSvc.search({ keyword: this.loanKeyword || null, loanStatus: this.loanStatus }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.loanData = res.data; this.isLoadingLoans.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoadingLoans.set(false); }
    });
  }

  uiStatus(loan: EbookLoan): LoanUiStatus {
    if (loan.status === 2) return 'recalled';
    if (loan.expiresAt && new Date(loan.expiresAt).getTime() < Date.now()) return 'expired';
    return 'active';
  }
  statusLabel(loan: EbookLoan): string {
    switch (this.uiStatus(loan)) {
      case 'recalled': return this.translate.instant('EBOOK_LOAN.STATUS_RECALLED');
      case 'expired':  return this.translate.instant('EBOOK_LOAN.STATUS_EXPIRED');
      default:         return this.translate.instant('EBOOK_LOAN.STATUS_ACTIVE');
    }
  }
  statusColor(loan: EbookLoan): string {
    switch (this.uiStatus(loan)) {
      case 'recalled': return 'bg-red-100 text-red-700';
      case 'expired':  return 'bg-gray-100 text-gray-600';
      default:         return 'bg-green-100 text-green-700';
    }
  }

  openRecall(loan: EbookLoan): void {
    this.recallTarget.set(loan);
    this.recallReason = '';
    this.showConfirmRecall.set(true);
  }
  closeRecall(): void { this.showConfirmRecall.set(false); this.recallTarget.set(null); }
  confirmRecall(): void {
    const loan = this.recallTarget();
    if (!loan?.publicId) return;
    this.loanSvc.recall(loan.publicId, this.recallReason).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('EBOOK_LOAN.RECALL_SUCCESS')); this.closeRecall(); this.loadLoans(); },
      error: () => { this.closeRecall(); }
    });
  }

  // ── Đặt trước (chỉ đọc) ───────────────────────────────────────
  loadReservations(resetPage = true): void {
    if (resetPage) this.reservationPageIndex = 0;
    this.isLoadingReservations.set(true);
    this.reservationSvc.search({
      keyword: this.reservationKeyword || null,
      reservationStatus: this.reservationStatus,
      pageIndex: this.reservationPageIndex + 1,
      pageSize: this.reservationPageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.reservationData = res.data; this.reservationTotal = res.recordsTotal; this.isLoadingReservations.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoadingReservations.set(false); }
    });
  }
  reservationNextPage(): void { if ((this.reservationPageIndex + 1) * this.reservationPageSize < this.reservationTotal) { this.reservationPageIndex++; this.loadReservations(false); } }
  reservationPrevPage(): void { if (this.reservationPageIndex > 0) { this.reservationPageIndex--; this.loadReservations(false); } }

  reservationStatusLabel(r: EbookReservation): string {
    const key = r.status != null ? RESERVATION_STATUS_LABEL[r.status] : undefined;
    return key ? this.translate.instant(key) : '';
  }
  reservationStatusColor(r: EbookReservation): string {
    switch (r.status) {
      case 2: return 'bg-blue-100 text-blue-700';
      case 3: return 'bg-green-100 text-green-700';
      case 4: return 'bg-gray-100 text-gray-600';
      case 5: return 'bg-red-100 text-red-700';
      default: return 'bg-amber-100 text-amber-700';
    }
  }
}
