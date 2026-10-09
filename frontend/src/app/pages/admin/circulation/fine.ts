import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { ActivatedRoute } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { FineService } from '../../../services/circulation/fine.service';
import { CFineTypeService } from '../../../services/printbook/cfine-type.service';
import { Fine } from '../../../models/circulation/fine';
import { CFineType } from '../../../models/printbook/cfine-type';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-fine',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, TenantFilterSelectComponent],
  templateUrl: './fine.html'
})
export class FinePage implements OnInit, OnDestroy {
  private service   = inject(FineService);
  private reasonSvc = inject(CFineTypeService);
  private toastr    = inject(ToastrService);
  private route     = inject(ActivatedRoute);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  prefilledCardNo = signal<string | null>(null);

  statusOptions = [
    { value: null, label: 'FINE.ST_ALL' },
    { value: 1, label: 'FINE.ST_UNPAID' },
    { value: 2, label: 'FINE.ST_PAID' },
    { value: 3, label: 'FINE.ST_WAIVED' },
  ];

  displayedColumns = ['stt', 'cardNo', 'readerName', 'reason', 'fineDate', 'value', 'status', 'actions'];
  dataSource: Fine[] = [];
  reasons = signal<CFineType[]>([]);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  isSaving = signal(false);
  showModal = signal(false);

  searchForm = new FormGroup({
    cardNo: new FormControl<string>('', { nonNullable: true }),
    status: new FormControl<number | null>(null),
  });
  dataForm = new FormGroup({
    ReaderId:     new FormControl<number | null>(null, { validators: [Validators.required] }),
    BorrowId:     new FormControl<number | null>(null),
    ReasonFineId: new FormControl<number | null>(null),
    Value:        new FormControl<number>(0, { nonNullable: true }),
    FineDate:     new FormControl<string>('', { nonNullable: true }),
    Note:         new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.reasonSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.reasons.set(r.data), error: () => {} });
    this.loadData();

    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(p => {
      const readerId = p['readerId'] ? Number(p['readerId']) : null;
      if (!readerId) return;
      const borrowId = p['borrowId'] ? Number(p['borrowId']) : null;
      this.prefilledCardNo.set(p['cardNo'] || null);
      this.openAddModal();
      this.dataForm.patchValue({ ReaderId: readerId, BorrowId: borrowId });
    });
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getReasonName(id: number | null | undefined): string { if (!id) return '—'; return this.reasons().find(r => r.id === id)?.name || '—'; }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({ cardNo: s.cardNo || null, status: s.status ?? null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void { this.dataForm.reset({ Value: 0 }); this.showModal.set(true); }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset({ Value: 0 }); }
  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Fine> = { readerId: v.ReaderId ?? undefined, borrowId: v.BorrowId ?? undefined, reasonFineId: v.ReasonFineId ?? undefined, value: v.Value, fineDate: v.FineDate || undefined, note: v.Note || undefined, status: 1 };
    this.service.save(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.triggerSearch(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  pay(f: Fine): void { this.service.pay(f.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => { this.toastr.success(this.translate.instant('FINE.PAID_OK')); this.loadData(); }, error: () => {} }); }
  waive(f: Fine): void { this.service.waive(f.id).pipe(takeUntil(this.destroy$)).subscribe({ next: () => { this.toastr.success(this.translate.instant('FINE.WAIVED_OK')); this.loadData(); }, error: () => {} }); }

  statusClass(s: number | undefined): string { switch (s) { case 2: return 'bg-green-50 text-green-700'; case 3: return 'bg-gray-100 text-gray-600'; default: return 'bg-amber-50 text-amber-700'; } }
  statusLabel(s: number | undefined): string { return this.translate.instant(s === 2 ? 'FINE.ST_PAID' : s === 3 ? 'FINE.ST_WAIVED' : 'FINE.ST_UNPAID'); }
}
