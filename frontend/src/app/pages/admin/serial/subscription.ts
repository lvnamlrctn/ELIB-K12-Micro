import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SubscriptionService } from '../../../services/serial/subscription.service';
import { FrequencyMagazineService } from '../../../services/serial/frequency-magazine.service';
import { PatternMagazineService } from '../../../services/serial/pattern-magazine.service';
import { MagazineTypeService } from '../../../services/serial/magazine-type.service';
import { StoreService } from '../../../services/printbook/store.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { Subscription } from '../../../models/serial/subscription';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-serial-subscription',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './subscription.html'
})
export class SubscriptionPage implements OnInit, OnDestroy {
  private service     = inject(SubscriptionService);
  private frequencySvc = inject(FrequencyMagazineService);
  private patternSvc  = inject(PatternMagazineService);
  private magTypeSvc  = inject(MagazineTypeService);
  private storeSvc    = inject(StoreService);
  private supplierSvc = inject(SupplierService);
  private router      = inject(Router);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private auth        = inject(Auth);
  private destroy$    = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  // tùy chọn chuẩn ILS
  typeOptions   = [ { id: 1, label: 'MANUAL' }, { id: 2, label: 'PREDICTED' } ];
  unitOptions   = [ { id: 1, label: 'ISSUES' }, { id: 2, label: 'WEEKS' }, { id: 3, label: 'MONTHS' } ];
  statusOptions = [ { id: 1, label: 'ACTIVE' }, { id: 2, label: 'EXPIRED' }, { id: 3, label: 'CANCELLED' } ];

  displayedColumns = ['stt', 'title', 'issn', 'frequency', 'startTime', 'endTime', 'store', 'status', 'approved', 'actions'];
  dataSource: Subscription[] = [];
  frequencies   = signal<{ id: number; name: string }[]>([]);
  patterns      = signal<{ id: number; name: string }[]>([]);
  magazineTypes = signal<{ id: number; name: string }[]>([]);
  stores        = signal<{ id: number; name: string }[]>([]);
  suppliers     = signal<{ id: number; name: string }[]>([]);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);
  showConfirmApprove = signal(false);
  confirmApprovePublicId = signal<string | null>(null);
  isApproving = signal(false);

  searchForm = new FormGroup({
    title:     new FormControl<string>('', { nonNullable: true }),
    author:    new FormControl<string>('', { nonNullable: true }),
    issn:      new FormControl<string>('', { nonNullable: true }),
  });
  dataForm = new FormGroup({
    BibId:          new FormControl<number | null>(null),
    Title:          new FormControl<string>('', { nonNullable: true }),
    Issn:           new FormControl<string>('', { nonNullable: true }),
    MagazineTypeId: new FormControl<number | null>(null),
    FrequencyId:    new FormControl<number | null>(null),
    PatternId:      new FormControl<number | null>(null),
    StoreId:        new FormControl<number | null>(null),
    SupplierId:     new FormControl<number | null>(null),
    StartTime:      new FormControl<string>('', { nonNullable: true }),
    EndTime:        new FormControl<string>('', { nonNullable: true }),
    FirstTime:      new FormControl<string>('', { nonNullable: true }),
    StartX:         new FormControl<number>(1, { nonNullable: true }),
    StartY:         new FormControl<number>(1, { nonNullable: true }),
    StartZ:         new FormControl<number>(0, { nonNullable: true }),
    // điều khiển chuẩn ILS
    SubscriptionType:   new FormControl<number>(2, { nonNullable: true }),
    SubscriptionLength: new FormControl<number>(12, { nonNullable: true }),
    LengthUnit:         new FormControl<number>(1, { nonNullable: true }),
    GraceDays:          new FormControl<number>(7, { nonNullable: true }),
    Status:             new FormControl<number>(1, { nonNullable: true }),
    CallNumber:         new FormControl<string>('', { nonNullable: true }),
    Locate:             new FormControl<string>('', { nonNullable: true }),
    PublicNote:         new FormControl<string>('', { nonNullable: true }),
    InternalNote:       new FormControl<string>('', { nonNullable: true }),
    Note:               new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadDropdowns(); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadDropdowns(): void {
    this.frequencySvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.frequencies.set(list.map(f => ({ id: Number(f.id), name: f.name }))), error: () => {} });
    this.patternSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.patterns.set(list.map(p => ({ id: Number(p.id), name: p.name }))), error: () => {} });
    this.magTypeSvc.search({ pageSize: 1000 }).pipe(takeUntil(this.destroy$)).subscribe({ next: res => this.magazineTypes.set((res.data || []).map(t => ({ id: Number(t.id), name: t.name }))), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} });
    this.supplierSvc.search({ pageSize: 1000 }).pipe(takeUntil(this.destroy$)).subscribe({ next: res => this.suppliers.set((res.data || []).map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} });
  }
  getFrequencyName(id: number | null | undefined): string { if (!id) return '—'; return this.frequencies().find(f => f.id === id)?.name || '—'; }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusLabel(id: number | null | undefined): string { const o = this.statusOptions.find(s => s.id === (id ?? 1)); return this.translate.instant('SUBSCRIPTION.STATUS_' + (o ? o.label : 'ACTIVE')); }
  statusClass(id: number | null | undefined): string { switch (id) { case 2: return 'bg-gray-100 text-gray-600'; case 3: return 'bg-red-100 text-red-700'; default: return 'bg-green-100 text-green-700'; } }

  loadData(): void {
    this.isLoading.set(true);
    const f = this.searchForm.getRawValue();
    this.service.search({ title: f.title || null, author: f.author || null, issn: f.issn || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    this.dataForm.reset({ BibId: null, Title: '', Issn: '', MagazineTypeId: null, FrequencyId: null, PatternId: null, StoreId: null, SupplierId: null, StartTime: '', EndTime: '', FirstTime: '', StartX: 1, StartY: 1, StartZ: 0, SubscriptionType: 2, SubscriptionLength: 12, LengthUnit: 1, GraceDays: 7, Status: 1, CallNumber: '', Locate: '', PublicNote: '', InternalNote: '', Note: '' });
    this.showModal.set(true);
  }
  openEditModal(item: Subscription): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.dataForm.patchValue({
        BibId: d.bibId ?? null, Title: d.title ?? '', Issn: d.issn ?? '', MagazineTypeId: d.magazineTypeId ?? null,
        FrequencyId: d.frequencyId ?? null, PatternId: d.patternId ?? null, StoreId: d.storeId ?? null, SupplierId: d.supplierId ?? null,
        StartTime: (d.startTime || '').substring(0, 10), EndTime: (d.endTime || '').substring(0, 10), FirstTime: (d.firstTime || '').substring(0, 10),
        StartX: d.startX ?? 1, StartY: d.startY ?? 1, StartZ: d.startZ ?? 0,
        SubscriptionType: d.subscriptionType ?? 2, SubscriptionLength: d.subscriptionLength ?? 12, LengthUnit: d.lengthUnit ?? 1, GraceDays: d.graceDays ?? 7, Status: d.status ?? 1,
        CallNumber: d.callNumber ?? '', Locate: d.locate ?? '', PublicNote: d.publicNote ?? '', InternalNote: d.internalNote ?? '', Note: d.note ?? '',
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Subscription> = {
      bibId: v.BibId ?? undefined, title: v.Title, issn: v.Issn, magazineTypeId: v.MagazineTypeId ?? undefined,
      frequencyId: v.FrequencyId ?? undefined, patternId: v.PatternId ?? undefined, storeId: v.StoreId ?? undefined, supplierId: v.SupplierId ?? undefined,
      startTime: v.StartTime || undefined, endTime: v.EndTime || undefined, firstTime: v.FirstTime || undefined,
      startX: v.StartX, startY: v.StartY, startZ: v.StartZ,
      subscriptionType: (v.SubscriptionType as Subscription['subscriptionType']), subscriptionLength: v.SubscriptionLength, lengthUnit: (v.LengthUnit as Subscription['lengthUnit']), graceDays: v.GraceDays, status: (v.Status as Subscription['status']),
      callNumber: v.CallNumber, locate: v.Locate, publicNote: v.PublicNote, internalNote: v.InternalNote, note: v.Note,
    };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  // mở trang nhận kỳ của đăng ký này
  openIssues(item: Subscription): void { this.router.navigate(['/admin/serial-issues'], { queryParams: { subscriptionId: item.id, title: item.title || '', patternId: item.patternId ?? '' } }); }

  handleDelete(item: Subscription): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  // duyệt đơn đặt — 1 chiều, sau khi duyệt khóa Sửa/Xóa trên dòng đó
  requestApprove(item: Subscription): void { if (!item.publicId) return; this.confirmApprovePublicId.set(item.publicId); this.showConfirmApprove.set(true); }
  closeConfirmApprove(): void { this.showConfirmApprove.set(false); this.confirmApprovePublicId.set(null); }
  confirmApproveExecute(): void {
    const publicId = this.confirmApprovePublicId(); if (publicId == null) return;
    this.isApproving.set(true);
    this.service.approve(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isApproving.set(false); this.toastr.success(this.translate.instant('SUBSCRIPTION.APPROVE_OK')); this.closeConfirmApprove(); this.loadData(); },
      error: () => { this.isApproving.set(false); this.closeConfirmApprove(); }
    });
  }

  // in nhanh 1 đơn đặt (cẩm nang mục VII.6: "Danh mục tạp chí đặt")
  printSubject = signal<Subscription[]>([]);
  printOrder(item: Subscription): void { this.printSubject.set([item]); setTimeout(() => { if (typeof window !== 'undefined') window.print(); }); }
}
