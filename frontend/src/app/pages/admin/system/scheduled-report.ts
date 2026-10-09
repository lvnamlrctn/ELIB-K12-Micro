import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ScheduledReportService } from '../../../services/system/scheduled-report.service';
import { ScheduledReport } from '../../../models/system/scheduled-report';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-scheduled-report',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './scheduled-report.html'
})
export class ScheduledReportPage implements OnInit, OnDestroy {
  private service   = inject(ScheduledReportService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'name', 'reportType', 'frequency', 'recipients', 'lastRun', 'status', 'actions'];
  dataSource: ScheduledReport[] = [];
  selection = new SelectionModel<ScheduledReport>(true, []);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  keyword = '';

  showModal = signal(false);
  editMode  = signal(false);
  currentPublicId = signal<string | null>(null);
  isSaving  = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  dataForm = new FormGroup({
    name:            new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    reportType:      new FormControl<'CIRCULATION' | 'STORE_BOOK'>('CIRCULATION', { nonNullable: true }),
    frequencyType:   new FormControl<number>(1, { nonNullable: true }),
    dayOfWeek:       new FormControl<number | null>(null),
    dayOfMonth:      new FormControl<number | null>(null),
    timeOfDay:       new FormControl<string>('06:00', { nonNullable: true, validators: [Validators.required] }),
    recipientEmails: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    status:          new FormControl<number>(2, { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search(this.keyword, this.pageIndex + 1, this.pageSize, this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.selection.clear(); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length; }
  toggleAllRows(): void { this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource); }

  frequencyLabel(item: ScheduledReport): string {
    if (item.frequencyType === 2) return this.translate.instant('SCHEDULED_REPORT.WEEKLY') + ' — ' + this.translate.instant('SCHEDULED_REPORT.DOW_' + (item.dayOfWeek ?? 0)) + ' ' + item.timeOfDay;
    if (item.frequencyType === 3) return this.translate.instant('SCHEDULED_REPORT.MONTHLY') + ' — ' + this.translate.instant('SCHEDULED_REPORT.DAY_OF_MONTH', { p0: item.dayOfMonth ?? 1 }) + ' ' + item.timeOfDay;
    return this.translate.instant('SCHEDULED_REPORT.DAILY') + ' — ' + item.timeOfDay;
  }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    this.dataForm.reset({ name: '', reportType: 'CIRCULATION', frequencyType: 1, dayOfWeek: null, dayOfMonth: null, timeOfDay: '06:00', recipientEmails: '', status: 2 });
    this.showModal.set(true);
  }
  openEditModal(item: ScheduledReport): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      if (!d) return;
      this.dataForm.patchValue({
        name: d.name, reportType: d.reportType, frequencyType: d.frequencyType,
        dayOfWeek: d.dayOfWeek ?? null, dayOfMonth: d.dayOfMonth ?? null,
        timeOfDay: d.timeOfDay, recipientEmails: d.recipientEmails, status: d.status,
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload = v as ScheduledReport;
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  confirmDelete(item: ScheduledReport): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (!publicId) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
