import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EbookLoanService } from '../../../services/ebook/ebook-loan.service';
import { EbookLoan } from '../../../models/ebook/ebook-loan';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

type LoanUiStatus = 'active' | 'expired' | 'recalled';

@Component({
  selector: 'app-ebook-loan',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatIconModule, AppDatePipe],
  templateUrl: './ebook-loan.html'
})
export class EbookLoanPage implements OnInit, OnDestroy {
  private route     = inject(ActivatedRoute);
  private router    = inject(Router);
  private service   = inject(EbookLoanService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  ebookPublicId = '';
  ebookId       = 0;
  ebookTitle    = '';

  displayedColumns = ['stt', 'reader', 'checkedOutAt', 'expiresAt', 'status', 'actions'];
  dataSource: EbookLoan[] = [];
  totalCount = 0;

  isLoading           = signal(false);
  showConfirmRecall    = signal(false);
  showConfirmRecallAll = signal(false);
  recallTarget         = signal<EbookLoan | null>(null);
  recallReason          = new FormControl('');

  ngOnInit(): void {
    this.ebookPublicId = this.route.snapshot.paramMap.get('publicId') ?? '';
    const state         = history.state as any;
    this.ebookTitle     = state?.ebookTitle ?? '';
    this.ebookId        = state?.ebookId    ?? 0;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ ebookItemId: this.ebookId || null })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.dataSource = res.data;
          this.totalCount = res.totalCount;
          this.isLoading.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          this.isLoading.set(false);
        }
      });
  }

  goBack(): void {
    this.router.navigate(['/admin/ebooks']);
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

  get activeCount(): number {
    return this.dataSource.filter(l => this.uiStatus(l) === 'active').length;
  }

  // ── Thu hồi 1 lượt mượn ───────────────────────────────────────
  openRecall(loan: EbookLoan): void {
    this.recallTarget.set(loan);
    this.recallReason.setValue('');
    this.showConfirmRecall.set(true);
  }

  closeRecall(): void {
    this.showConfirmRecall.set(false);
    this.recallTarget.set(null);
  }

  confirmRecall(): void {
    const loan = this.recallTarget();
    if (!loan?.publicId) return;
    this.service.recall(loan.publicId, this.recallReason.value).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('EBOOK_LOAN.RECALL_SUCCESS'));
        this.closeRecall();
        this.loadData();
      },
      error: () => {
        this.closeRecall();
      }
    });
  }

  // ── Thu hồi tất cả đang hoạt động ───────────────────────────────
  openRecallAll(): void {
    this.recallReason.setValue('');
    this.showConfirmRecallAll.set(true);
  }

  closeRecallAll(): void {
    this.showConfirmRecallAll.set(false);
  }

  confirmRecallAll(): void {
    if (!this.ebookPublicId) return;
    this.service.recallAll(this.ebookPublicId, this.recallReason.value).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('EBOOK_LOAN.RECALL_SUCCESS'));
        this.closeRecallAll();
        this.loadData();
      },
      error: () => {
        this.closeRecallAll();
      }
    });
  }
}
