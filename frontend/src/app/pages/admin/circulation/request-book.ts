import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { RequestBookService } from '../../../services/circulation/request-book.service';
import { RequestBook } from '../../../models/circulation/request-book';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-request-book',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, TenantFilterSelectComponent],
  templateUrl: './request-book.html'
})
export class RequestBookPage implements OnInit, OnDestroy {
  private service   = inject(RequestBookService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'cardNo', 'readerName', 'bibTitle', 'requestDate', 'status', 'actions'];
  dataSource: RequestBook[] = [];

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({ cardNo: new FormControl<string>('', { nonNullable: true }) });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ cardNo: this.searchForm.getRawValue().cardNo || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  approve(r: RequestBook): void { this.service.approve(r.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => { this.toastr.success(this.translate.instant('REQUEST_BOOK.APPROVED_OK')); this.loadData(); }, error: () => {} }); }
  reject(r: RequestBook): void { this.service.reject(r.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => { this.toastr.success(this.translate.instant('REQUEST_BOOK.REJECTED_OK')); this.loadData(); }, error: () => {} }); }

  statusClass(s: number | undefined): string { switch (s) { case 1: return 'bg-green-50 text-green-700'; case 2: return 'bg-red-50 text-red-700'; default: return 'bg-amber-50 text-amber-700'; } }
  statusLabel(s: number | undefined): string { return this.translate.instant(s === 1 ? 'REQUEST_BOOK.ST_APPROVED' : s === 2 ? 'REQUEST_BOOK.ST_REJECTED' : 'REQUEST_BOOK.ST_PENDING'); }
}
