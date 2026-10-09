import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { PaymentQrDialogComponent } from '../../../components/payment-qr-dialog/payment-qr-dialog';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { PhotoCopyService } from '../../../services/circulation/photocopy.service';
import {
  PhotoCopyRow, PhotoCopyReaderLookup, PhotoCopyDocLookup, PhotoCopySearchParams
} from '../../../models/circulation/photocopy';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-photocopy',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, TenantFilterSelectComponent, PaymentQrDialogComponent],
  templateUrl: './photocopy.html'
})
export class PhotoCopyPage implements OnInit, OnDestroy {
  private service   = inject(PhotoCopyService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'cardNo', 'readerName', 'unitName', 'bibTitle', 'photoDate',
                      'pages', 'copy', 'price', 'totalAmount', 'isPaid', 'actions'];
  dataSource: PhotoCopyRow[] = [];
  selection = new SelectionModel<PhotoCopyRow>(true, []);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  filtersCollapsed = signal(false);
  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  paymentQrRow = signal<PhotoCopyRow | null>(null);
  confirmDeletePublicId = signal<string | null>(null);

  // Tổng tiền trên toàn bộ kết quả lọc
  sumTotal  = signal(0);
  sumPaid   = signal(0);
  sumUnpaid = signal(0);

  // Tra cứu số thẻ / Số ĐKCB trong modal
  readerLookup   = signal<PhotoCopyReaderLookup | null>(null);
  docLookup      = signal<PhotoCopyDocLookup | null>(null);
  readerLoading  = signal(false);
  docLoading     = signal(false);
  readerNotFound = signal(false);
  docNotFound    = signal(false);

  // Thành tiền tính live theo dữ liệu đang nhập trên form
  previewTotal = signal(0);

  searchForm = new FormGroup({
    cardNo:        new FormControl<string>('', { nonNullable: true }),
    lastName:      new FormControl<string>('', { nonNullable: true }),
    firstName:     new FormControl<string>('', { nonNullable: true }),
    bibTitle:      new FormControl<string>('', { nonNullable: true }),
    photoDateFrom: new FormControl<string>('', { nonNullable: true }),
    photoDateTo:   new FormControl<string>('', { nonNullable: true }),
    isPaid:        new FormControl<number | null>(null),
  });

  dataForm = new FormGroup({
    CardNo:    new FormControl<string>('',  { nonNullable: true, validators: [Validators.required] }),
    Barcode:   new FormControl<string>('',  { nonNullable: true, validators: [Validators.required] }),
    Frompage:  new FormControl<number>(1,   { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    ToPage:    new FormControl<number>(1,   { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    Copy:      new FormControl<number>(1,   { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    Price:     new FormControl<number>(0,   { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    PhotoDate: new FormControl<string>('',  { nonNullable: true, validators: [Validators.required] }),
    IsPaid:    new FormControl<boolean>(false, { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.dataForm.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.recalcPreview());
    this.loadData();
  }
  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }

  private buildParams(): PhotoCopySearchParams {
    const f = this.searchForm.getRawValue();
    return {
      cardNo: f.cardNo || null, lastName: f.lastName || null, firstName: f.firstName || null,
      bibTitle: f.bibTitle || null, photoDateFrom: f.photoDateFrom || null, photoDateTo: f.photoDateTo || null,
      isPaid: f.isPaid ?? null, tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize,
    };
  }

  loadData(): void {
    this.isLoading.set(true);
    const params = this.buildParams();
    this.service.search(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
    this.service.totals(params).pipe(takeUntil(this.destroy$)).subscribe(t => {
      this.sumTotal.set(t?.totalAmountSum ?? 0);
      this.sumPaid.set(t?.totalPaid ?? 0);
      this.sumUnpaid.set(t?.totalUnpaid ?? 0);
    });
  }

  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  resetFilters(): void { this.searchForm.reset({ cardNo: '', lastName: '', firstName: '', bibTitle: '', photoDateFrom: '', photoDateTo: '', isPaid: null }); this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }
  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length; }
  masterToggle(): void { this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r)); }

  // ===== Modal thêm/sửa =====

  private clearLookups(): void {
    this.readerLookup.set(null); this.docLookup.set(null);
    this.readerNotFound.set(false); this.docNotFound.set(false);
    this.readerLoading.set(false); this.docLoading.set(false);
  }

  private recalcPreview(): void {
    const v = this.dataForm.getRawValue();
    const pages = (Number(v.ToPage) || 0) - (Number(v.Frompage) || 0) + 1;
    if (pages < 1) { this.previewTotal.set(0); return; }
    this.previewTotal.set(pages * (Number(v.Copy) || 0) * (Number(v.Price) || 0));
  }

  pagesCount(): number {
    const v = this.dataForm.getRawValue();
    const pages = (Number(v.ToPage) || 0) - (Number(v.Frompage) || 0) + 1;
    return pages > 0 ? pages : 0;
  }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    this.clearLookups();
    this.dataForm.reset({
      CardNo: '', Barcode: '', Frompage: 1, ToPage: 1, Copy: 1, Price: 0,
      PhotoDate: new Date().toISOString().substring(0, 10), IsPaid: false,
    });
    this.recalcPreview();
    this.showModal.set(true);
  }

  openPaymentQr(row: PhotoCopyRow): void { this.paymentQrRow.set(row); }
  onPaymentQrPaid(): void { this.paymentQrRow.set(null); this.loadData(); }

  openEditModal(item: PhotoCopyRow): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.clearLookups();
    this.dataForm.reset({
      CardNo:    item.cardNo ?? '',
      Barcode:   item.barcode ?? '',
      Frompage:  item.frompage ?? 1,
      ToPage:    item.toPage ?? 1,
      Copy:      item.copy ?? 1,
      Price:     item.price ?? 0,
      PhotoDate: item.photoDate ? item.photoDate.substring(0, 10) : new Date().toISOString().substring(0, 10),
      IsPaid:    item.isPaid === 2,
    });
    // Điền sẵn tên bạn đọc / nhan đề đã biết từ dòng dữ liệu, đỡ phải gọi API lại.
    if (item.readerId) this.readerLookup.set({ readerId: item.readerId, cardNo: item.cardNo, fullName: item.readerName, unitName: item.unitName });
    if (item.barcode)  this.docLookup.set({ barcode: item.barcode, bibTitle: item.bibTitle });
    this.recalcPreview();
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); this.clearLookups(); }

  onCardBlur(): void {
    const cardNo = (this.dataForm.controls.CardNo.value || '').trim();
    this.readerLookup.set(null); this.readerNotFound.set(false);
    if (!cardNo) return;
    this.readerLoading.set(true);
    this.service.lookupCard(cardNo).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.readerLoading.set(false);
      this.readerLookup.set(res);
      this.readerNotFound.set(!res);
    });
  }

  onBarcodeBlur(): void {
    const barcode = (this.dataForm.controls.Barcode.value || '').trim();
    this.docLookup.set(null); this.docNotFound.set(false);
    if (!barcode) return;
    this.docLoading.set(true);
    this.service.lookupBarcode(barcode).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.docLoading.set(false);
      this.docLookup.set(res);
      this.docNotFound.set(!res);
    });
  }

  canSave(): boolean {
    return this.dataForm.valid
      && !!this.readerLookup() && !!this.docLookup()
      && this.pagesCount() >= 1
      && !this.isSaving();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    if (!this.canSave()) return;
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload = {
      cardNo:    v.CardNo,
      barcode:   v.Barcode,
      frompage:  Number(v.Frompage),
      toPage:    Number(v.ToPage),
      copy:      Number(v.Copy),
      price:     Number(v.Price),
      photoDate: v.PhotoDate,
      isPaid:    v.IsPaid ? 2 : 1,
    };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false); this.closeModal();
        (mode ? this.loadData() : this.triggerSearch());
        this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: err => {
        this.isSaving.set(false);
        const msg = err?.error?.message || this.translate.instant(mode ? 'COMMON.UPDATE_ERROR' : 'COMMON.ADD_ERROR');
        this.toastr.error(msg);
      }
    });
  }

  // ===== Xoá =====

  handleDelete(item: PhotoCopyRow): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  deleteSelected(): void { if (!this.selection.selected.length) return; this.confirmDeletePublicId.set(null); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId();
    if (publicId === null) {
      const reqs = this.selection.selected.filter(i => !!i.publicId).map(i => this.service.delete(i.publicId!));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.selection.clear(); this.closeConfirm(); this.loadData(); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.DELETE_ERROR')); this.closeConfirm(); this.loadData(); }
      });
      return;
    }
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.DELETE_ERROR')); this.closeConfirm(); }
    });
  }
}
