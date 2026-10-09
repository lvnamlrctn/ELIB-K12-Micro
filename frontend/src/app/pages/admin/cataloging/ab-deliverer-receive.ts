import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { DelivererService } from '../../../services/cataloging/deliverer.service';
import { DelivererStatusService } from '../../../services/cataloging/deliverer-status.service';
import { StoreService } from '../../../services/printbook/store.service';
import { UserService } from '../../../services/system/user.service';
import { Deliverer } from '../../../models/cataloging/deliverer';
import { DelivererStatus } from '../../../models/cataloging/deliverer-status';
import { Store } from '../../../models/printbook/store';
import { User } from '../../../models/system/user';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

/** Hàng đợi "chờ ký nhận" — chỉ hiển thị đơn phân bổ Status=2 (Hoàn thành nhận) & Sign=1 (chưa ký). */
@Component({
  selector: 'app-ab-deliverer-receive',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './ab-deliverer-receive.html'
})
export class AbDelivererReceivePage implements OnInit, OnDestroy {
  private service    = inject(DelivererService);
  private listState = inject(ListPageStateService);
  private statusSvc  = inject(DelivererStatusService);
  private storeSvc   = inject(StoreService);
  private userSvc    = inject(UserService);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private router     = inject(Router);
  private auth       = inject(Auth);
  private destroy$   = new Subject<void>();

  /** Quyền của trang này tái dùng đúng permission module AB_DELIVERERS (route khác nên phải override). */
  readonly permUrl = '/admin/ab-deliverers';

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'code', 'delivererDate', 'delivererName', 'receiptName', 'store', 'status', 'actions'];
  dataSource: Deliverer[] = [];
  stores = signal<Store[]>([]);
  statuses = signal<DelivererStatus[]>([]);
  users = signal<User[]>([]);
  filtersCollapsed = signal(false);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  isLoading = signal(false);
  showConfirmSign = signal(false);
  confirmSignPublicId = signal<string | null>(null);
  isSigning = signal(false);

  searchForm = new FormGroup({
    codeFrom:          new FormControl<number | null>(null),
    codeTo:            new FormControl<number | null>(null),
    delivererName:     new FormControl<string>('', { nonNullable: true }),
    receiptName:       new FormControl<string>('', { nonNullable: true }),
    createdBy:         new FormControl<number | null>(null),
    delivererDateFrom: new FormControl<string>('', { nonNullable: true }),
    delivererDateTo:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('cataloging.ab-deliverer-receive'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.statusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.statuses.set(l), error: () => {} });
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.users.set(l), error: () => {} });
    this.loadData();
  }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusName(id: number | null | undefined): string { if (id == null) return '—'; return this.statuses().find(s => s.id === id)?.name || '—'; }

  loadData(): void {
    this.listState.remember('cataloging.ab-deliverer-receive', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const v = this.searchForm.getRawValue();
    this.service.search({
      codeFrom:          v.codeFrom          ?? null,
      codeTo:            v.codeTo            ?? null,
      delivererName:     v.delivererName     || null,
      receiptName:       v.receiptName       || null,
      createdBy:         v.createdBy         ?? null,
      delivererDateFrom: v.delivererDateFrom || null,
      delivererDateTo:   v.delivererDateTo   || null,
      status: 2, sign: 1,
      tenantId: this.tenantId,
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openDetail(item: Deliverer): void {
    if (!item.publicId) return;
    this.router.navigate(['/admin/ab-deliverers', item.publicId]);
  }

  handleSign(item: Deliverer): void { if (!item.publicId) return; this.confirmSignPublicId.set(item.publicId); this.showConfirmSign.set(true); }
  closeConfirmSign(): void { this.showConfirmSign.set(false); this.confirmSignPublicId.set(null); }
  confirmSignExecute(): void {
    const publicId = this.confirmSignPublicId(); if (!publicId) return;
    this.isSigning.set(true);
    this.service.signConfirm(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSigning.set(false); this.toastr.success(this.translate.instant('AB_DELIVERER_RECEIVE.SIGN_SUCCESS')); this.closeConfirmSign(); this.loadData(); },
      error: () => { this.isSigning.set(false); this.toastr.error(this.translate.instant('AB_DELIVERER_RECEIVE.SIGN_ERROR')); this.closeConfirmSign(); }
    });
  }
}
