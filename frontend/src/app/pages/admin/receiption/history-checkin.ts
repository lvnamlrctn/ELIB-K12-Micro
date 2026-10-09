import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CheckInService } from '../../../services/receiption/checkin.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CheckLog } from '../../../models/receiption/checkin';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-history-checkin',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, TenantFilterSelectComponent],
  templateUrl: './history-checkin.html'
})
export class HistoryCheckInPage implements OnInit, OnDestroy {
  private service   = inject(CheckInService);
  private circPlaceSvc = inject(CircPlaceService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'cardNo', 'fullName', 'readerType', 'checkIn', 'checkOut', 'actions'];
  dataSource: CheckLog[] = [];
  stores = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({
    cardNumber:   new FormControl<string>('', { nonNullable: true }),
    name:         new FormControl<string>('', { nonNullable: true }),
    checkInFrom:  new FormControl<string>('', { nonNullable: true }),
    checkInTo:    new FormControl<string>('', { nonNullable: true }),
    storeId:      new FormControl<number | null>(null),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadStores(); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadStores(): void { this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} }); }

  private buildParams() {
    const f = this.searchForm.getRawValue();
    return { cardNumber: f.cardNumber || null, firstName: f.name || null, checkInFrom: f.checkInFrom || null, checkInTo: f.checkInTo || null, storeId: f.storeId, tenantId: this.tenantId };
  }
  loadData(): void {
    this.isLoading.set(true);
    this.service.searchHistory({ ...this.buildParams(), pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  exportExcel(): void {
    this.service.exportHistory(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe(blob => {
      if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      const url = URL.createObjectURL(blob); const a = document.createElement('a');
      a.href = url; a.download = 'checkin-history.xlsx'; a.click(); URL.revokeObjectURL(url);
    });
  }

  handleDelete(item: CheckLog): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmDelete(): void {
    const id = this.confirmDeletePublicId(); if (!id) return;
    this.service.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
