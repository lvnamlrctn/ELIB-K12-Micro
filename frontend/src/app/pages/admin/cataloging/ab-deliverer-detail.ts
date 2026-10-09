import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil, switchMap, of } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { DelivererService } from '../../../services/cataloging/deliverer.service';
import { DelivererStatusService } from '../../../services/cataloging/deliverer-status.service';
import { AbReceiptService } from '../../../services/cataloging/receipt.service';
import { StoreService } from '../../../services/printbook/store.service';
import { Deliverer, DelivererBarcodeSearchResult, DelivererLine } from '../../../models/cataloging/deliverer';
import { DelivererStatus } from '../../../models/cataloging/deliverer-status';
import { AbReceipt } from '../../../models/cataloging/receipt';
import { Store } from '../../../models/printbook/store';

@Component({
  selector: 'app-ab-deliverer-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, DateInputComponent, NgSelectModule, MatPaginatorModule],
  templateUrl: './ab-deliverer-detail.html'
})
export class AbDelivererDetailPage implements OnInit, OnDestroy {
  private route      = inject(ActivatedRoute);
  private router     = inject(Router);
  private service    = inject(DelivererService);
  private statusSvc  = inject(DelivererStatusService);
  private receiptSvc = inject(AbReceiptService);
  private storeSvc   = inject(StoreService);
  private toastr     = inject(ToastrService);
  public  translate  = inject(TranslateService);
  private destroy$   = new Subject<void>();

  deliverer = signal<Deliverer | null>(null);
  lines     = signal<DelivererLine[]>([]);
  isLoading = signal(false);
  isSaving  = signal(false);

  // Đơn đã ký nhận (Sign=2) → khoá sửa/thêm/xoá, trừ role đặc quyền (backend là nơi chặn thật).
  canEdit = signal(true);
  locked  = signal(false);

  statuses = signal<DelivererStatus[]>([]);
  receipts = signal<AbReceipt[]>([]);
  stores   = signal<Store[]>([]);

  dataForm = new FormGroup({
    DelivererDate:    new FormControl<string>('', { nonNullable: true }),
    Receipt_Id:       new FormControl<number | null>(null),
    Status:           new FormControl<number | null>(null),
    DelivererName:    new FormControl<string>('', { nonNullable: true }),
    DelivererAddress: new FormControl<string>('', { nonNullable: true }),
    ReceiptName:      new FormControl<string>('', { nonNullable: true }),
    ReceiptAddress:   new FormControl<string>('', { nonNullable: true }),
    Note:             new FormControl<string>('', { nonNullable: true }),
    Store_Id:         new FormControl<number | null>(null),
  });

  // Xóa dòng sách
  showConfirmDeleteLine = signal(false);
  deletingLine = signal<DelivererLine | null>(null);

  // Xóa cả đơn
  showConfirmDeleteAll = signal(false);

  // Thêm sách vào đơn
  showAddBook = signal(false);
  searchBy = signal<'mfn' | 'barcode'>('mfn');
  searchKeyword = signal('');
  isSearchingBook = signal(false);
  bookResults = signal<DelivererBarcodeSearchResult[]>([]);
  bookResultsTotal = signal(0);
  isAddingBook = signal(false);
  bookFiltersCollapsed = signal(true);
  bookPageIndex = signal(1);
  bookPageSize = signal(20);
  readonly bookPageSizeOptions = [10, 20, 50, 100];
  // Đa chọn (port ELIB-LRC 09-16) — giữ theo barcodeId để lựa chọn còn nguyên khi chuyển trang kết quả.
  selectedBookIds = signal<Set<number>>(new Set());
  bookFilterForm = new FormGroup({
    Title:       new FormControl<string>('', { nonNullable: true }),
    Author:      new FormControl<string>('', { nonNullable: true }),
    Publisher:   new FormControl<string>('', { nonNullable: true }),
    PublishDate: new FormControl<string>('', { nonNullable: true }),
    MfnFrom:     new FormControl<number | null>(null),
    MfnTo:       new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    this.statusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.statuses.set(l), error: () => {} });
    this.receiptSvc.searchAvailableForDeliverer().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.receipts.set(list); this.ensureReceiptOption(this.deliverer()?.receipt_Id); },
      error: () => {}
    });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  // Dropdown "Đơn nhận" chỉ liệt kê đơn còn sách chưa phân bổ — nếu đơn phân bổ đang mở đã link với
  // 1 đơn nhận không còn thoả điều kiện đó (VD đã phân bổ hết), vẫn phải hiện nó trong danh sách để
  // không mất lựa chọn đã lưu.
  private ensureReceiptOption(receiptId: number | null | undefined): void {
    if (!receiptId || this.receipts().some(r => r.id === receiptId)) return;
    this.receiptSvc.getById(receiptId).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { if (r && !this.receipts().some(x => x.id === r.id)) this.receipts.update(list => [...list, r]); },
      error: () => {}
    });
  }

  load(): void {
    const publicId = this.route.snapshot.paramMap.get('publicId');
    if (!publicId) return;
    this.isLoading.set(true);
    this.service.getByPublicId(publicId).pipe(
      takeUntil(this.destroy$),
      switchMap(d => {
        this.deliverer.set(d);
        if (!d) return of([]);
        this.dataForm.patchValue({
          DelivererDate:    (d.delivererDate ?? '').toString().substring(0, 10),
          Receipt_Id:       d.receipt_Id ?? null,
          Status:           d.status ?? null,
          DelivererName:    d.delivererName ?? '',
          DelivererAddress: d.delivererAddress ?? '',
          ReceiptName:      d.receiptName ?? '',
          ReceiptAddress:   d.receiptAddress ?? '',
          Note:             d.note ?? '',
          Store_Id:         d.store_Id ?? null,
        });
        this.ensureReceiptOption(d.receipt_Id);
        return this.service.getLines(d.id);
      })
    ).subscribe({
      next: lines => { this.lines.set(lines); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
    this.service.canEdit(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { this.canEdit.set(r.canEdit); this.locked.set(r.locked); },
      error: () => {}
    });
  }

  reloadLines(): void {
    const d = this.deliverer(); if (!d) return;
    this.service.getLines(d.id).pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.lines.set(l), error: () => {} });
  }

  save(): void {
    if (!this.canEdit()) return;
    const d = this.deliverer(); if (!d?.publicId) return;
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Deliverer> = {
      delivererDate: v.DelivererDate || undefined, receipt_Id: v.Receipt_Id ?? undefined, status: v.Status ?? undefined,
      delivererName: v.DelivererName || undefined, delivererAddress: v.DelivererAddress || undefined,
      receiptName: v.ReceiptName || undefined, receiptAddress: v.ReceiptAddress || undefined,
      note: v.Note || undefined, store_Id: v.Store_Id ?? undefined,
    };
    this.service.update(d.publicId, payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.load(); },
      error: () => { this.isSaving.set(false); }
    });
  }

  exit(): void { this.router.navigate(['/admin/ab-deliverers']); }

  // ── Xóa dòng sách ────────────────────────────────────────────────────────
  handleDeleteLine(line: DelivererLine): void { if (!this.canEdit()) return; this.deletingLine.set(line); this.showConfirmDeleteLine.set(true); }
  closeConfirmDeleteLine(): void { this.showConfirmDeleteLine.set(false); this.deletingLine.set(null); }
  confirmDeleteLine(): void {
    const line = this.deletingLine(); if (!line) return;
    this.service.deleteLine(line.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.closeConfirmDeleteLine(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
      error: () => { this.closeConfirmDeleteLine(); }
    });
  }

  // ── Xóa cả đơn ───────────────────────────────────────────────────────────
  handleDeleteAll(): void { if (!this.canEdit()) return; this.showConfirmDeleteAll.set(true); }
  closeConfirmDeleteAll(): void { this.showConfirmDeleteAll.set(false); }
  confirmDeleteAll(): void {
    const d = this.deliverer(); if (!d?.publicId) return;
    this.service.delete(d.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.exit(); },
      error: () => { this.closeConfirmDeleteAll(); }
    });
  }

  // ── Thêm sách vào đơn ────────────────────────────────────────────────────
  openAddBook(): void {
    if (!this.canEdit()) return;
    if (!this.dataForm.getRawValue().Receipt_Id) {
      this.toastr.error(this.translate.instant('AB_DELIVERER.SELECT_RECEIPT_FIRST'));
      return;
    }
    this.searchBy.set('mfn'); this.searchKeyword.set('');
    this.bookFilterForm.reset();
    this.bookFiltersCollapsed.set(true);
    this.bookPageIndex.set(1);
    this.bookResults.set([]); this.bookResultsTotal.set(0);
    this.selectedBookIds.set(new Set());
    this.showAddBook.set(true);
    this.searchBook();
  }
  closeAddBook(): void { this.showAddBook.set(false); }
  toggleBookFilters(): void { this.bookFiltersCollapsed.update(v => !v); }

  triggerBookSearch(): void { this.bookPageIndex.set(1); this.selectedBookIds.set(new Set()); this.searchBook(); }
  onBookPageChange(e: PageEvent): void { this.bookPageIndex.set(e.pageIndex + 1); this.bookPageSize.set(e.pageSize); this.searchBook(); }

  isBookSelected(r: DelivererBarcodeSearchResult): boolean { return this.selectedBookIds().has(r.barcodeId); }
  toggleBook(r: DelivererBarcodeSearchResult): void {
    const next = new Set(this.selectedBookIds());
    if (next.has(r.barcodeId)) next.delete(r.barcodeId); else next.add(r.barcodeId);
    this.selectedBookIds.set(next);
  }
  bookPageAllSelected(): boolean { const rows = this.bookResults(); return rows.length > 0 && rows.every(r => this.isBookSelected(r)); }
  toggleBookPage(): void {
    const next = new Set(this.selectedBookIds());
    const selectAll = !this.bookPageAllSelected();
    for (const r of this.bookResults()) { if (selectAll) next.add(r.barcodeId); else next.delete(r.barcodeId); }
    this.selectedBookIds.set(next);
  }

  searchBook(): void {
    const receiptId = this.dataForm.getRawValue().Receipt_Id;
    if (!receiptId) { this.bookResults.set([]); this.bookResultsTotal.set(0); return; }
    const f = this.bookFilterForm.getRawValue();
    this.isSearchingBook.set(true);
    this.service.searchBarcode({
      receiptId,
      searchBy:    this.searchBy(),
      keyword:     this.searchKeyword().trim(),
      title:       f.Title, author: f.Author, publisher: f.Publisher, publishDate: f.PublishDate,
      mfnFrom:     f.MfnFrom, mfnTo: f.MfnTo,
      pageIndex:   this.bookPageIndex(), pageSize: this.bookPageSize(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: page => { this.bookResults.set(page.items); this.bookResultsTotal.set(page.totalCount); this.isSearchingBook.set(false); },
      error: () => { this.isSearchingBook.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }
  confirmAddSelected(): void {
    const d = this.deliverer(); const ids = [...this.selectedBookIds()];
    if (!d || !ids.length) return;
    this.isAddingBook.set(true);
    this.service.addLines(d.id, ids, this.dataForm.getRawValue().Store_Id)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => {
          this.isAddingBook.set(false); this.closeAddBook(); this.reloadLines();
          this.toastr.success(this.translate.instant('AB_DELIVERER.ADDED_COUNT', { count: res?.added ?? ids.length }));
        },
        error: (err: { error?: { message?: string } }) => { this.isAddingBook.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
  }
}
