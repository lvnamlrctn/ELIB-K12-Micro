import { Component, inject, OnInit, OnDestroy, AfterViewInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ShelvingService } from '../../../services/printbook/shelving.service';
import { StoreService } from '../../../services/printbook/store.service';
import { UnshelvedBarcode } from '../../../models/printbook/shelving';
import { Store } from '../../../models/printbook/store';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-shelving',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './shelving.html'
})
export class ShelvingPage implements OnInit, OnDestroy, AfterViewInit {
  private service  = inject(ShelvingService);
  private storeSvc = inject(StoreService);
  private toastr   = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth     = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  stores = signal<Store[]>([]);
  // Kho gán khi xếp giá (khác storeId của form tìm kiếm — ô đó chỉ để lọc danh sách).
  targetStoreId = signal<number | null>(null);

  searchForm = new FormGroup({
    receiptCode: new FormControl<string>('', { nonNullable: true }),
    barcodeFrom: new FormControl<string>('', { nonNullable: true }),
    barcodeTo:   new FormControl<string>('', { nonNullable: true }),
    storeId:     new FormControl<number | null>(null),
  });

  displayedColumns = ['select', 'barcode', 'bibTitle', 'receiptCode', 'store'];
  dataSource = new MatTableDataSource<UnshelvedBarcode>([]);
  selection = new SelectionModel<UnshelvedBarcode>(true, []);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [10, 25, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  isLoading  = signal(false);
  isShelving = signal(false);

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.loadData();
  }
  ngAfterViewInit(): void { this.paginator?.page.pipe(takeUntil(this.destroy$)).subscribe(() => { this.pageIndex = this.paginator.pageIndex; this.pageSize = this.paginator.pageSize; this.loadData(); }); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  loadData(): void {
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.service.searchUnshelved({
      receiptCode: v.receiptCode || null, barcodeFrom: v.barcodeFrom || null, barcodeTo: v.barcodeTo || null, storeId: v.storeId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource.data = res.data; this.totalRecords = res.recordsTotal; this.selection.clear(); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }

  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.data.length; }
  toggleAllRows(): void { this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource.data); }

  shelveSelected(): void {
    const ids = this.selection.selected.map(r => r.id);
    if (ids.length === 0) return;
    this.isShelving.set(true);
    this.service.shelve(ids, this.targetStoreId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isShelving.set(false); this.toastr.success(this.translate.instant('SHELVING.OK')); this.loadData(); },
      error: () => { this.isShelving.set(false); }
    });
  }
}
