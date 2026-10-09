import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BorrowKeyService } from '../../../services/receiption/borrow-key.service';
import { BorrowKey } from '../../../models/receiption/borrow-key';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-search-borrow-key',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, TenantFilterSelectComponent],
  templateUrl: './search-borrow-key.html'
})
export class SearchBorrowKeyPage implements OnInit, OnDestroy {
  private service   = inject(BorrowKeyService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'cardNo', 'fullName', 'cabinet', 'borrowDate', 'returnDate', 'status'];
  dataSource: BorrowKey[] = [];
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({
    cardNumber:     new FormControl<string>('', { nonNullable: true }),
    name:           new FormControl<string>('', { nonNullable: true }),
    borrowDateFrom: new FormControl<string>('', { nonNullable: true }),
    borrowDateTo:   new FormControl<string>('', { nonNullable: true }),
    onlyReturned:   new FormControl<boolean>(false, { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStatusLabel(s: number | undefined): string { return this.translate.instant(s === 2 ? 'BORROW_KEY.STATUS_RETURNED' : 'BORROW_KEY.STATUS_BORROWED'); }

  loadData(): void {
    this.isLoading.set(true);
    const f = this.searchForm.getRawValue();
    this.service.search({ cardNumber: f.cardNumber || null, firstName: f.name || null, borrowDateFrom: f.borrowDateFrom || null, borrowDateTo: f.borrowDateTo || null, onlyReturned: f.onlyReturned, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }
}
