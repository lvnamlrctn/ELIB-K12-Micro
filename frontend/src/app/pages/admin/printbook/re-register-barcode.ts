import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ReRegisterBarcodeService, BarcodeImportResult } from '../../../services/printbook/re-register-barcode.service';
import { StoreService } from '../../../services/printbook/store.service';
import { AdminTaskService } from '../../../services/system/admin-task.service';
import { CanDirective } from '../../../directives/can.directive';

interface BarcodeInfo { barcode?: string; bibTitle?: string; author?: string; storeId?: number; status?: string }

@Component({
  selector: 'app-re-register-barcode',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule, NgSelectModule, CanDirective],
  templateUrl: './re-register-barcode.html'
})
export class ReRegisterBarcodePage implements OnInit, OnDestroy {
  private service        = inject(ReRegisterBarcodeService);
  private storeSvc       = inject(StoreService);
  private toastr         = inject(ToastrService);
  public  translate      = inject(TranslateService);
  private destroy$       = new Subject<void>();
  private route          = inject(ActivatedRoute);
  private router         = inject(Router);
  private adminTaskService = inject(AdminTaskService);

  stores = signal<{ id: number; name: string }[]>([]);
  info = signal<BarcodeInfo | null>(null);
  isLooking = signal(false);
  isSaving = signal(false);
  notFound = signal(false);

  form = new FormGroup({
    OldBarcode: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    NewBarcode: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    StoreId:    new FormControl<number | null>(null),
  });

  constructor() { this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} }); }
  /** Đợt 21 — mở từ "Sửa ngay" ở trang Chất lượng dữ liệu: ?barcode=<mã cũ> điền sẵn và tra luôn. Giữ nguyên chuỗi
   *  (không trim) vì cảnh báo trùng mã chính là do khác nhau khoảng trắng/hoa thường. */
  private exactOldBarcode: string | null = null;
  /** Mã cũ gửi lên server: giữ nguyên nếu là đúng mã mở từ "Sửa ngay", còn lại cắt khoảng trắng như trước. */
  private oldBarcodeValue(): string {
    const raw = this.form.getRawValue().OldBarcode || '';
    return raw === this.exactOldBarcode ? raw : raw.trim();
  }
  ngOnInit(): void {
    const barcode = this.route.snapshot.queryParamMap.get('barcode');
    if (barcode) { this.exactOldBarcode = barcode; this.form.patchValue({ OldBarcode: barcode }); this.lookup(); }
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  lookup(): void {
    const bc = this.oldBarcodeValue(); if (!bc.trim()) return;
    this.isLooking.set(true); this.notFound.set(false); this.info.set(null);
    this.service.lookup(bc).pipe(takeUntil(this.destroy$)).subscribe({
      next: d => { this.isLooking.set(false); if (d) { this.info.set(d); this.form.patchValue({ StoreId: d.storeId ?? null }); } else { this.notFound.set(true); } },
      error: () => { this.isLooking.set(false); this.notFound.set(true); }
    });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.isSaving.set(true);
    this.service.reRegister({ oldBarcode: this.oldBarcodeValue(), newBarcode: v.NewBarcode.trim(), storeId: v.StoreId ?? undefined }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.toastr.success(this.translate.instant('RE_REGISTER.OK')); this.reset(); },
      error: () => { this.isSaving.set(false); }
    });
  }
  reset(): void { this.form.reset({ OldBarcode: '', NewBarcode: '', StoreId: null }); this.info.set(null); this.notFound.set(false); }

  // ===== ĐỢT 22.5 — NHẬP EXCEL HÀNG LOẠT =====
  showImportModal   = signal(false);
  importFile        = signal<File | null>(null);
  importBackground  = signal(false);
  adminTasksEnabled = signal(false);
  isImporting       = signal(false);
  importResult      = signal<BarcodeImportResult | null>(null);

  openImportModal(): void {
    this.importFile.set(null);
    this.importResult.set(null);
    this.importBackground.set(false);
    this.showImportModal.set(true);
    this.adminTaskService.health().pipe(takeUntil(this.destroy$)).subscribe(h => this.adminTasksEnabled.set(h.enabled));
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
    this.importFile.set(null);
    this.importResult.set(null);
    this.importBackground.set(false);
  }

  onImportFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.importFile.set(file);
    input.value = '';
  }

  confirmImport(): void {
    const file = this.importFile();
    if (!file) return;

    if (this.importBackground()) {
      this.isImporting.set(true);
      this.service.importExcelBackground(file).pipe(takeUntil(this.destroy$)).subscribe({
        next: task => {
          this.isImporting.set(false);
          if (task) { this.closeImportModal(); this.router.navigate(['/admin/admin-tasks']); }
          else this.importResult.set({ successCount: 0, failedCount: 1, errors: ['Không tạo được tác vụ nền'], message: '' });
        },
        error: () => { this.isImporting.set(false); this.importResult.set({ successCount: 0, failedCount: 1, errors: ['Lỗi kết nối'], message: '' }); }
      });
      return;
    }

    this.isImporting.set(true);
    this.service.importExcel(file).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => { this.isImporting.set(false); this.importResult.set(result); },
      error: () => { this.isImporting.set(false); this.importResult.set({ successCount: 0, failedCount: 1, errors: ['Lỗi kết nối'], message: '' }); }
    });
  }
}
