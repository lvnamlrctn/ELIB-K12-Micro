import { Component, inject, OnInit, OnDestroy, ViewChild, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { EbookReviewService, AdminReview } from '../../../services/ebook/ebook-review.service';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-ebook-review',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './ebook-review.html'
})
export class EbookReviewPage implements OnInit, OnDestroy {
  private service   = inject(EbookReviewService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'reviewer', 'doc', 'rating', 'content', 'time', 'status', 'actions'];
  dataSource: AdminReview[] = [];
  loading = signal(false);

  keyword = '';
  status: number | null = null;   // null = tất cả, 1 = chờ duyệt, 2 = đã duyệt

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // Xác nhận xóa
  showDeleteConfirm = signal(false);
  private deletingId: string | null = null;

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadList(); }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadList();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadList();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadList(): void {
    this.loading.set(true);
    this.service.search({
      keyword: this.keyword || '', status: this.status, tenantId: this.tenantId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize
    }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.dataSource = res.items;
      this.totalRecords = res.recordsTotal;
      this.loading.set(false);
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadList();
  }

  approve(row: AdminReview): void {
    this.service.approve(row.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.toastr.success(this.translate.instant('EBOOK_REVIEW.APPROVED'));
      this.loadList();
    });
  }

  unapprove(row: AdminReview): void {
    this.service.unapprove(row.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.toastr.success(this.translate.instant('EBOOK_REVIEW.UNAPPROVED'));
      this.loadList();
    });
  }

  confirmDelete(row: AdminReview): void {
    this.deletingId = row.publicId;
    this.showDeleteConfirm.set(true);
  }

  closeConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.deletingId = null;
  }

  doDelete(): void {
    if (this.deletingId == null) return;
    this.service.remove(this.deletingId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.toastr.success(this.translate.instant('EBOOK_REVIEW.DELETED'));
      this.closeConfirm();
      this.loadList();
    });
  }

  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }
  stars(n: number): number[] { return Array(Math.max(0, Math.min(5, Math.round(n || 0)))).fill(0); }
}
