import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild, WritableSignal, ViewEncapsulation } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { SelectionModel } from '@angular/cdk/collections';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, from, takeUntil, switchMap, concatMap, of, map, catchError } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Auth } from '../../../services/auth';
import { AbReceiptService } from '../../../services/cataloging/receipt.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { BudgetService } from '../../../services/acquisition/budget.service';
import { CurrencyService } from '../../../services/acquisition/currency.service';
import { AbSourceService } from '../../../services/acquisition/ab-source.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { WorkSheetService } from '../../../services/cataloging/worksheet.service';
import { WorksheetFieldService } from '../../../services/cataloging/worksheet-field.service';
import { WorksheetSubfieldService } from '../../../services/cataloging/worksheet-subfield.service';
import { MarcFieldService } from '../../../services/cataloging/marc-field.service';
import { MarcSubFieldService } from '../../../services/cataloging/marc-subfield.service';
import { MarcFieldDic } from '../../../models/cataloging/marc-field';
import { MarcSubFieldDic } from '../../../models/cataloging/marc-subfield';
import { WorkSheet } from '../../../models/cataloging/worksheet';
import { AbOrderService } from '../../../services/cataloging/order.service';
import { BibOrderService } from '../../../services/cataloging/bib-order.service';
import { AbOrderLine } from '../../../models/cataloging/order';
import { Z3950SearchService } from '../../../services/cataloging/z3950-search.service';
import { Z3950ConfigService } from '../../../services/cataloging/z3950-config.service';
import { Z3950ResultItem } from '../../../models/cataloging/z3950-result';
import { Z3950Config } from '../../../models/cataloging/z3950-config';
import { AbReceipt, AbReceiptLine, RegisteredBarcode } from '../../../models/cataloging/receipt';
import { AcquisitionReportService, AcquisitionPrintResult, AcquisitionPrintItem, StoreAllocationPrintResult } from '../../../services/acquisition/acquisition-report.service';
import { Store } from '../../../models/printbook/store';
import { Fund } from '../../../models/acquisition/fund';
import { Budget } from '../../../models/acquisition/budget';
import { Currency } from '../../../models/acquisition/currency';
import { BaseEntity } from '../../../models/shared/base-entity';
import { MarcField, Bib } from '../../../models/cataloging/bib';
import { BibType } from '../../../models/cataloging/bib-type';
import { MarcFieldsEditorComponent } from '../../../shared/marc-editor/marc-fields-editor';
import { defaultMarcFields, setMarcSubfieldValue, formatPriceForMarc, applyBookMetadataToFields } from '../../../shared/marc-editor/marc-defaults.util';
import { parseMarcText } from '../../../shared/marc-editor/marc-text-import.util';
import { PrintBookAndDigitalService } from '../../../services/cataloging/print-book-and-digital.service';
import { EbookDocumentService } from '../../../services/ebook/ebook-document.service';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { EbookDocument } from '../../../models/ebook/ebook-document';
import { LinkedEbookInfo } from '../../../models/cataloging/print-book-and-digital';
import { ClassLabelService } from '../../../services/cataloging/class-label.service';
import { EntitySearchPickerComponent, EntityPage, EntitySearchFilters } from '../../../shared/components/entity-search-picker/entity-search-picker';
import { UnsavedChanges, UnsavedChangesPage, watchUnsavedChanges } from '../../../guards/unsaved-changes.guard';

@Component({
  selector: 'app-ab-receipt-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, MatPaginatorModule, MarcFieldsEditorComponent, EntitySearchPickerComponent, DateInputComponent, NgSelectModule],
  templateUrl: './ab-receipt-detail.html',
  styles: [`@media print { @page { size: landscape; } }`],
  encapsulation: ViewEncapsulation.None
})
export class AbReceiptDetailPage implements OnInit, OnDestroy, UnsavedChangesPage {
  protected readonly Math = Math;
  private route        = inject(ActivatedRoute);
  private router       = inject(Router);
  private service      = inject(AbReceiptService);
  private supplierSvc  = inject(SupplierService);
  private fundSvc      = inject(FundService);
  private budgetSvc    = inject(BudgetService);
  private currencySvc  = inject(CurrencyService);
  private sourceSvc    = inject(AbSourceService);
  private storeSvc     = inject(StoreService);
  private barcodeStatusSvc = inject(BarcodeStatusService);
  private bibSvc       = inject(BibService);
  private bibTypeSvc   = inject(BibTypeService);
  private worksheetSvc = inject(WorkSheetService);
  private wsFieldSvc   = inject(WorksheetFieldService);
  private wsSubSvc     = inject(WorksheetSubfieldService);
  private marcFieldSvc = inject(MarcFieldService);
  private marcSubSvc   = inject(MarcSubFieldService);
  private orderSvc     = inject(AbOrderService);
  private bibOrderSvc  = inject(BibOrderService);
  private z3950Svc       = inject(Z3950SearchService);
  private z3950ConfigSvc = inject(Z3950ConfigService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private auth         = inject(Auth);
  private acquisitionReportSvc = inject(AcquisitionReportService);
  private classLabelSvc = inject(ClassLabelService);
  private linkSvc       = inject(PrintBookAndDigitalService);
  private ebookSvc       = inject(EbookDocumentService);
  private collectionSvc  = inject(EbookCollectionService);
  private destroy$     = new Subject<void>();

  @ViewChild(EntitySearchPickerComponent) ebookPicker!: EntitySearchPickerComponent<EbookDocument>;

  statusOptions        = [{ value: 1, label: 'AB_RECEIPT.ORDER_ST_PENDING' }, { value: 2, label: 'AB_RECEIPT.ORDER_ST_DONE' }];
  paymentStatusOptions = [{ value: 0, label: 'AB_RECEIPT.PS_UNPAID' }, { value: 2, label: 'AB_RECEIPT.PS_PAID' }];
  paymentMethodOptions = [{ value: 0, label: 'AB_RECEIPT.PM_CASH' }, { value: 1, label: 'AB_RECEIPT.PM_TRANSFER' }];

  receipt   = signal<AbReceipt | null>(null);
  lines     = signal<AbReceiptLine[]>([]);
  isLoading = signal(false);
  isSaving  = signal(false);

  isPrintingAcquisition = signal(false);
  acquisitionPrint = signal<AcquisitionPrintResult | null>(null);
  isPrintingStoreAllocation = signal(false);
  isPrintingLabels = signal(false);
  classLabels = signal<{ key: string; title?: string; author?: string; classSymbol?: string; authorMark?: string }[]>([]);
  labelHeader = signal<{ parentLibrary: string; libraryName: string }>({ parentLibrary: '', libraryName: '' });
  storeAllocationPrint = signal<StoreAllocationPrintResult | null>(null);

  // Đơn đã "Hoàn tất" (Status=2) → khoá sửa/thêm/xoá, trừ role đặc quyền (backend là nơi chặn thật).
  canEdit = signal(true);
  locked  = signal(false);

  suppliers = signal<{ id: number; name?: string }[]>([]);
  stores    = signal<Store[]>([]);
  funds     = signal<Fund[]>([]);
  budgets   = signal<Budget[]>([]);
  sources   = signal<BaseEntity[]>([]);
  currencies = signal<Currency[]>([]);
  collections = signal<{ id: number; label: string }[]>([]);

  currentUserName = computed(() => this.auth.getUser()?.name || this.auth.getUser()?.loginName || '—');

  dataForm = new FormGroup({
    ReceiptName:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    ReceiptDate:     new FormControl<string>('', { nonNullable: true }),
    Status:          new FormControl<number>(1, { nonNullable: true }),
    PaymentMethodId: new FormControl<number>(0, { nonNullable: true }),
    PaymentStatus:   new FormControl<number>(0, { nonNullable: true }),
    SourceId:        new FormControl<number | null>(null),
    FundId:          new FormControl<number | null>(null),
    BudgetId:        new FormControl<number | null>(null),
    SupplierId:      new FormControl<number | null>(null),
    StoreId:         new FormControl<number | null>(null),
    Note:            new FormControl<string>('', { nonNullable: true }),
  });

  // Thêm sách lẻ (tạo biểu ghi MARC mới)
  showAddNew  = signal(false);
  bibTypeId   = signal<number | null>(null);
  collectionId = signal<number | null>(null);
  bibTypes    = signal<BibType[]>([]);
  worksheets  = signal<WorkSheet[]>([]);
  worksheetId = signal<number | null>(null);
  marcFields  = signal<MarcField[]>([]);
  marcFieldDics    = signal<MarcFieldDic[]>([]);
  marcSubFieldDics = signal<MarcSubFieldDic[]>([]);
  newLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });
  isSavingLine = signal(false);
  /** Tài liệu số đã chọn nhưng chưa lưu — sẽ tự động liên kết ngay sau khi thêm dòng thành công. */
  pendingLinkEbookNew = signal<EbookDocument | null>(null);

  // Đợt 21 — cảnh báo chưa lưu cho "Thêm sách lẻ": MARC, loại/mẫu, bộ sưu tập, số lượng/giá, ảnh bìa, tài liệu số.
  private unsaved = new UnsavedChanges();
  private addNewSnapshot() {
    return { type: this.bibTypeId(), collection: this.collectionId(), worksheet: this.worksheetId(), fields: this.marcFields(),
             line: this.newLineForm.getRawValue(), cover: this.coverImagePreviewUrl(), ebook: this.pendingLinkEbookNew()?.id ?? null };
  }
  private hasUnsavedChanges(): boolean { return this.showAddNew() && this.unsaved.changed(this.addNewSnapshot()); }
  canLeave = watchUnsavedChanges(() => this.hasUnsavedChanges(), () => this.isSavingLine());

  // Tra trùng (tìm + chọn biểu ghi đã có trong danh mục)
  showPickExisting = signal(false);
  pickResults       = signal<Bib[]>([]);
  pickSelected      = signal<Bib | null>(null);
  isPicking         = signal(false);
  isSearchingTrace  = signal(false);
  pickPageIndex     = signal(0);
  pickPageSize      = 10;
  pickTotalRecords  = signal(0);
  traceSearchForm = new FormGroup({
    MfnFrom:     new FormControl<number | null>(null),
    MfnTo:       new FormControl<number | null>(null),
    Title:       new FormControl<string>('', { nonNullable: true }),
    Author:      new FormControl<string>('', { nonNullable: true }),
    Publisher:   new FormControl<string>('', { nonNullable: true }),
    PublishYear: new FormControl<string>('', { nonNullable: true }),
    BibTypeId:   new FormControl<number | null>(null),
  });
  pickLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });

  showConfirmDeleteLine = signal(false);
  deletingLine = signal<AbReceiptLine | null>(null);

  // ── Cảnh báo trùng ISBN khi thêm biểu ghi mới (Thêm sách lẻ / Từ Z3950 / Từ file Marc) ────────────
  showIsbnDuplicateWarning = signal(false);
  isbnDuplicateMatches = signal<{ bibId: number; mfn: number | null; title: string | null }[]>([]);
  private pendingIsbnDuplicateAction: (() => void) | null = null;

  private extractIsbn(fields: MarcField[]): string | null {
    const sf = fields.find(f => f.tag === '020')?.subFields?.find(s => s.code === 'a');
    return sf?.value?.trim() || null;
  }

  // ── Ảnh bìa tự động theo ISBN (Thêm sách lẻ) ────────────────────────────────
  coverImagePreviewUrl = signal<string | null>(null);
  isFetchingCover = signal(false);
  private lastAutoFetchedIsbn: string | null = null;

  fetchCoverPreview(): void {
    const isbn = this.extractIsbn(this.marcFields());
    if (!isbn) { this.toastr.warning(this.translate.instant('BIB_EDIT.COVER_NO_ISBN')); return; }
    if (isbn === this.lastAutoFetchedIsbn && this.coverImagePreviewUrl()) return;
    this.isFetchingCover.set(true);
    this.bibSvc.fetchCoverByIsbn(isbn).pipe(takeUntil(this.destroy$)).subscribe(url => {
      this.isFetchingCover.set(false);
      this.lastAutoFetchedIsbn = isbn;
      this.coverImagePreviewUrl.set(url);
      if (!url) this.toastr.warning(this.translate.instant('BIB_EDIT.COVER_NOT_FOUND'));
    });
  }
  // ── Biên mục từ ảnh bìa (AI) — "Thêm sách lẻ" ───────────────────────────────
  analysisImageFilesNew = signal<File[]>([]);
  isAnalyzingCoverNew    = signal(false);

  onAnalysisImagesSelectNew(event: Event): void {
    const files = Array.from((event.target as HTMLInputElement).files ?? []);
    if (files.length) this.analysisImageFilesNew.set(files);
  }

  analyzeCoverForAddNew(): void {
    const files = this.analysisImageFilesNew();
    if (!files.length || this.isAnalyzingCoverNew()) return;
    this.isAnalyzingCoverNew.set(true);
    this.bibSvc.analyzeBookImage(files).pipe(takeUntil(this.destroy$)).subscribe({
      next: meta => {
        this.isAnalyzingCoverNew.set(false);
        if (!meta) { this.toastr.error(this.translate.instant('BIB_EDIT.ANALYZE_ERROR')); return; }
        this.marcFields.set(applyBookMetadataToFields(this.marcFields(), meta));
        this.toastr.success(this.translate.instant('BIB_EDIT.ANALYZE_SUCCESS'));
      },
      error: () => { this.isAnalyzingCoverNew.set(false); this.toastr.error(this.translate.instant('BIB_EDIT.ANALYZE_ERROR')); }
    });
  }

  private pendingIsbnDuplicateCancel: (() => void) | null = null;
  private checkIsbnThenProceed(fields: MarcField[], proceed: () => void, onCancel?: () => void): void {
    const isbn = this.extractIsbn(fields);
    if (!isbn) { proceed(); return; }
    this.bibSvc.checkIsbn(isbn).pipe(takeUntil(this.destroy$)).subscribe({
      next: matches => {
        if (!matches.length) { proceed(); return; }
        this.isbnDuplicateMatches.set(matches);
        this.pendingIsbnDuplicateAction = proceed;
        this.pendingIsbnDuplicateCancel = onCancel ?? null;
        this.showIsbnDuplicateWarning.set(true);
      },
      error: () => proceed() // fail-open: lỗi kiểm tra không được chặn thêm sách
    });
  }
  confirmIsbnDuplicateProceed(): void {
    const action = this.pendingIsbnDuplicateAction;
    this.showIsbnDuplicateWarning.set(false);
    this.isbnDuplicateMatches.set([]);
    this.pendingIsbnDuplicateAction = null;
    this.pendingIsbnDuplicateCancel = null;
    action?.();
  }
  cancelIsbnDuplicateWarning(): void {
    const onCancel = this.pendingIsbnDuplicateCancel;
    this.showIsbnDuplicateWarning.set(false);
    this.isbnDuplicateMatches.set([]);
    this.pendingIsbnDuplicateAction = null;
    this.pendingIsbnDuplicateCancel = null;
    onCancel?.();
  }

  // Sửa biểu ghi MARC của 1 dòng đã có trong đơn
  showEditMarc     = signal(false);
  editMarcLine     = signal<AbReceiptLine | null>(null);
  editMarcFields   = signal<MarcField[]>([]);
  editMarcCollectionId = signal<number | null>(null);
  isSavingMarc     = signal(false);
  isLoadingEditMarc = signal(false);
  linkedEbookEditMarc   = signal<LinkedEbookInfo | null>(null);
  isLoadingLinkEditMarc = signal(false);
  editMarcLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });

  // Đăng ký số KCB (Riêng lẻ / Theo lô) cho 1 dòng phiếu nhập
  showKcbModal = signal(false);
  kcbTab       = signal<'single' | 'batch'>('single');
  kcbLine = signal<AbReceiptLine | null>(null);
  kcbList = signal<RegisteredBarcode[]>([]);
  isLoadingKcb = signal(false);
  isRegisteringKcb = signal(false);
  isRegisteringSingleKcb = signal(false);
  barcodeStatuses = signal<{ id: string; label: string }[]>([]);
  kcbRemaining = computed(() => (this.kcbLine()?.amount || 0) - this.kcbList().length);
  kcbSingleForm = new FormGroup({
    BarcodeValue: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    StoreId:      new FormControl<number | null>(null, { validators: [Validators.required] }),
  });
  kcbForm = new FormGroup({
    Prefix:      new FormControl<string>('', { nonNullable: true }),
    DigitLength: new FormControl<number>(6, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    StartNumber: new FormControl<number | null>(null),
    Quantity:    new FormControl<number>(1, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    StoreId:     new FormControl<number | null>(null, { validators: [Validators.required] }),
  });

  ngOnInit(): void {
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.fundSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.funds.set(r.data), error: () => {} });
    this.budgetSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.budgets.set(r.data), error: () => {} });
    this.currencySvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.currencies.set(r.data), error: () => {} });
    this.sourceSvc.getAll({ draw: 1, start: 0, length: 500, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.sources.set(r.data), error: () => {} });
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.collectionSvc.getTree().pipe(takeUntil(this.destroy$)).subscribe({ next: tree => this.collections.set(this.collectionSvc.flattenForSelect(tree)), error: () => {} });
    this.marcFieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcFieldDics.set(l), error: () => {} });
    this.marcSubSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcSubFieldDics.set(l), error: () => {} });
    this.barcodeStatusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const arr = list as any[];
        this.barcodeStatuses.set(arr.map(x => ({ id: String(x.id ?? ''), label: x.commentStatus ?? x.name ?? String(x.id ?? '') })));
      },
      error: () => {}
    });
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  back(): void { this.router.navigate(['/admin/ab-receipts']); }

  load(): void {
    const publicId = this.route.snapshot.paramMap.get('publicId');
    if (!publicId) return;
    this.isLoading.set(true);
    this.service.getByPublicId(publicId).pipe(
      takeUntil(this.destroy$),
      switchMap(r => {
        this.receipt.set(r);
        if (!r) return of([]);
        this.dataForm.patchValue({
          ReceiptName: r.receipt_Name ?? '', ReceiptDate: (r.receipt_Date ?? '').toString().substring(0, 10),
          Status: r.status ?? 1, PaymentMethodId: r.paymentMethodId ?? 0, PaymentStatus: r.payment_Status ?? 0,
          SourceId: r.source_Id ?? null, FundId: r.fundId ?? null, BudgetId: r.budgeT_ID ?? null,
          SupplierId: r.supplier_Id ?? null, StoreId: r.store_Id ?? null, Note: r.note ?? '',
        });
        return this.service.getLines(r.id);
      })
    ).subscribe({
      next: l => { this.lines.set(l); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
    this.service.canEdit(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { this.canEdit.set(r.canEdit); this.locked.set(r.locked); },
      error: () => {}
    });
  }

  getSupplierName(id: number | null | undefined): string { if (!id) return '—'; return this.suppliers().find(s => s.id === id)?.name || '—'; }
  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusLabel(code: string | null | undefined): string { if (!code) return '—'; return this.barcodeStatuses().find(s => s.id === code)?.label || code; }
  lineTotal(l: AbReceiptLine): number { return (l.amount || 0) * (l.price || 0) * (l.rate || 1); }

  save(): void {
    if (!this.canEdit()) return;
    const r = this.receipt(); const publicId = this.route.snapshot.paramMap.get('publicId');
    if (!r || !publicId) return;
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<AbReceipt> = {
      receipt_Name: v.ReceiptName, receipt_Date: v.ReceiptDate || undefined, status: v.Status,
      paymentMethodId: v.PaymentMethodId, payment_Status: v.PaymentStatus, source_Id: v.SourceId ?? undefined,
      fundId: v.FundId ?? undefined, budgeT_ID: v.BudgetId ?? undefined, supplier_Id: v.SupplierId ?? undefined,
      store_Id: v.StoreId ?? undefined, note: v.Note || undefined,
    };
    this.service.update(publicId, payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.load(); },
      error: () => { this.isSaving.set(false); }
    });
  }

  private reloadLines(): void {
    const r = this.receipt(); if (!r) return;
    this.service.getLines(r.id).pipe(takeUntil(this.destroy$)).subscribe(l => this.lines.set(l));
  }

  // ── Di chuyển hàng loạt dòng sang bộ sưu tập khác ───────────────────────
  lineSelection = new SelectionModel<AbReceiptLine>(true, []);
  // ── Tìm kiếm + phân trang các dòng của đơn (port ELIB-LRC 09-30; lọc trên trình duyệt vì đơn đã tải đủ dòng) ──
  readonly lineMinPageSize     = 10;
  readonly linePageSizeOptions = [10, 20, 50, 100];
  lineKeyword   = signal('');
  linePageIndex = signal(0);
  linePageSize  = signal(20);

  /** Không phân biệt hoa/thường và dấu tiếng Việt ("day hoc" khớp "Dạy học"). */
  private static fold(v: unknown): string {
    return String(v ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  }

  /** Lọc theo nhan đề, tác giả, NXB, năm và MFN; nhiều từ thì dòng phải chứa đủ các từ. */
  filteredLines = computed(() => {
    const terms = AbReceiptDetailPage.fold(this.lineKeyword()).split(/\s+/).filter(Boolean);
    if (!terms.length) return this.lines();
    return this.lines().filter(l => {
      const hay = AbReceiptDetailPage.fold([l.mfn ?? l.bibid, l.title, l.author, l.publisher, l.publishDate].join(' '));
      return terms.every(t => hay.includes(t));
    });
  });

  pagedLines = computed(() => {
    const all = this.filteredLines(), size = this.linePageSize();
    // Trang hiện tại có thể vượt quá sau khi xoá dòng/lọc lại → lùi về trang cuối còn dữ liệu.
    const page = Math.min(this.linePageIndex(), Math.max(0, Math.ceil(all.length / size) - 1));
    return all.slice(page * size, page * size + size);
  });

  onLineKeywordChange(value: string): void { this.lineKeyword.set(value ?? ''); this.linePageIndex.set(0); }
  onLinePageChange(e: PageEvent): void { this.linePageIndex.set(e.pageIndex); this.linePageSize.set(e.pageSize); }

  // "Chọn tất cả" áp dụng cho mọi dòng khớp bộ lọc hiện tại (không chỉ trang đang xem).
  isAllLinesSelected(): boolean {
    const rows = this.filteredLines();
    return rows.length > 0 && rows.every(l => this.lineSelection.isSelected(l));
  }
  toggleAllLines(): void {
    const rows = this.filteredLines();
    this.isAllLinesSelected() ? this.lineSelection.deselect(...rows) : this.lineSelection.select(...rows);
  }

  showMoveCollection      = signal(false);
  moveTargetCollectionId  = signal<number | null>(null);
  isMoving                = signal(false);

  openMoveCollection(): void {
    if (!this.lineSelection.selected.length) return;
    this.moveTargetCollectionId.set(null);
    this.showMoveCollection.set(true);
  }

  closeMoveCollection(): void {
    this.showMoveCollection.set(false);
    this.moveTargetCollectionId.set(null);
  }

  confirmMoveCollection(): void {
    const cid = this.moveTargetCollectionId();
    if (!cid || !this.lineSelection.selected.length) return;
    const bibIds = this.lineSelection.selected.map(l => l.bibid).filter((id): id is number => id != null);
    if (!bibIds.length) return;
    this.isMoving.set(true);
    this.bibSvc.bulkMoveCollection(bibIds, cid).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isMoving.set(false);
        this.closeMoveCollection();
        this.lineSelection.clear();
        this.reloadLines();
        this.toastr.success(this.translate.instant('EBOOK.MOVE_SUCCESS'));
      },
      error: (err: any) => {
        this.isMoving.set(false);
        this.toastr.error(err?.error?.message || this.translate.instant('EBOOK.MOVE_ERROR'));
      }
    });
  }

  // ── Đồng bộ Đơn giá vào MARC 020$c ───────────────────────────────────────
  private applyPriceToFields(fields: MarcField[], price: number, currency: string): MarcField[] {
    return setMarcSubfieldValue(fields, '020', 'c', formatPriceForMarc(price, currency));
  }
  /** Dùng khi không có sẵn mảng fields tại chỗ (VD "Sửa dòng"/"Tra trùng") — fetch biểu ghi Bib thật
   * theo Mfn, set 020$c, lưu lại. Fail-open: lỗi đồng bộ MARC không được chặn việc lưu dòng. */
  private syncPriceForExistingRecord(mfn: number | null | undefined, price: number, currency: string) {
    if (!mfn) return of(null);
    return this.bibSvc.getByMfn(mfn).pipe(
      switchMap(bib => bib?.fields ? this.bibSvc.save({ bibId: bib.bibId, mfn: bib.mfn, fields: this.applyPriceToFields(bib.fields, price, currency) }) : of(null)),
      catchError(() => of(null)),
    );
  }

  /** Sang trang Tra cứu đơn nhận (đủ các mẫu in theo khoảng mã đơn) — port ELIB-LRC 09-23. */
  openReceiptLookup(): void { this.router.navigate(['/admin/ab-receipts/lookup']); }

  /** In nhãn môn loại (DDC 082$a / Cutter 082$b) cho các dòng đang tick — tái dùng đúng luồng in nhãn của trang
   *  Tra cứu đơn nhận (ClassLabel/SearchByBib). Số nhãn mỗi dòng = số ĐKCB đã đăng ký, chưa đăng ký thì = số bản. */
  printClassLabels(): void {
    const rows = this.lineSelection.selected.filter(l => !!l.bibPublicId);
    if (!rows.length) { this.toastr.warning(this.translate.instant('AB_RECEIPT.SELECT_LINES_FOR_LABEL')); return; }
    this.isPrintingLabels.set(true);
    this.classLabelSvc.searchByBib(rows.map(r => r.bibPublicId!)).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        this.labelHeader.set({ parentLibrary: result.parentLibrary, libraryName: result.libraryName });
        const entries: { key: string; title?: string; author?: string; classSymbol?: string; authorMark?: string }[] = [];
        result.items.forEach(item => {
          const line = rows.find(r => r.bibPublicId === item.bibPublicId);
          const copies = (line?.registerCount ?? 0) > 0 ? line!.registerCount! : (line?.amount ?? 0);
          for (let i = 0; i < copies; i++)
            entries.push({ key: `${item.id}-${i}`, title: item.title, author: item.author, classSymbol: item.classSymbol, authorMark: item.authorMark });
        });
        if (!entries.length) { this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT')); return; }
        this.classLabels.set(entries);
        setTimeout(() => this.printAcq());
      },
      error: () => { this.isPrintingLabels.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  labelCaption(author?: string, title?: string): string { return `${author ? author : ''}-${title ?? ''}`; }

  // In "Danh mục sách bổ sung" cho riêng đơn nhận này — dữ liệu dòng đã có sẵn ở lines(),
  // chỉ cần gọi thêm SystemParameter (thư viện/phòng ban) qua LibraryHeader.
  printAcquisitionList(): void {
    const r = this.receipt(); if (!r) return;
    this.isPrintingAcquisition.set(true);
    this.acquisitionReportSvc.getLibraryHeader().pipe(takeUntil(this.destroy$)).subscribe({
      next: header => {
        this.isPrintingAcquisition.set(false);
        const items: AcquisitionPrintItem[] = this.lines().map((l, i) => {
          const price = l.price ?? 0; const amount = l.amount ?? 0;
          return { stt: i + 1, title: l.title, author: l.author, price, amount, total: price * amount };
        });
        this.acquisitionPrint.set({
          ...header,
          receiptCode: r.code ?? null,
          receiptDate: r.receipt_Date ?? null,
          items,
          totalCount: items.length,
          totalMoney: items.reduce((s, x) => s + x.total, 0),
        });
        setTimeout(() => this.printAcq());
      },
      error: () => {
        this.isPrintingAcquisition.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  formatVnDate(iso?: string | null): string {
    const d = iso ? new Date(iso) : new Date();
    return `Ngày ${d.getDate().toString().padStart(2, '0')} tháng ${(d.getMonth() + 1).toString().padStart(2, '0')} năm ${d.getFullYear()}`;
  }

  formatShortDate(iso?: string | null): string {
    const d = iso ? new Date(iso) : new Date();
    return `${d.getDate().toString().padStart(2, '0')}/${(d.getMonth() + 1).toString().padStart(2, '0')}/${d.getFullYear()}`;
  }

  // In "Báo cáo phân bổ kho" cho riêng đơn nhận này — dữ liệu barcode thực tế không có sẵn ở lines(),
  // luôn phải gọi API mới (StoreAllocationPrint), lọc theo đúng mã đơn nhận (Code) của trang này.
  printStoreAllocation(): void {
    const r = this.receipt(); if (!r) return;
    this.isPrintingStoreAllocation.set(true);
    this.acquisitionReportSvc.printStoreAllocation({ receiptCodeFrom: r.code ?? null, receiptCodeTo: r.code ?? null }).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingStoreAllocation.set(false);
        if (result.groups.length === 0) {
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        this.storeAllocationPrint.set(result);
        setTimeout(() => this.printAcq());
      },
      error: () => {
        this.isPrintingStoreAllocation.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  printAcq(): void {
    if (typeof window !== 'undefined') window.print();
  }

  // ── Thêm sách lẻ ──────────────────────────────────────────────────────────
  async openAddNew(): Promise<void> {
    if (!this.canEdit()) return;
    if (!(await this.canLeave())) return;
    this.bibTypeId.set(null); this.collectionId.set(null); this.worksheets.set([]); this.worksheetId.set(null);
    this.marcFields.set(defaultMarcFields());
    this.newLineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
    this.pendingLinkEbookNew.set(null);
    this.coverImagePreviewUrl.set(null);
    this.lastAutoFetchedIsbn = null;
    this.analysisImageFilesNew.set([]);
    this.unsaved.capture(this.addNewSnapshot());
    this.showAddNew.set(true);
  }
  closeAddNew(): void { this.showAddNew.set(false); }
  /** Đóng theo yêu cầu người dùng — hỏi trước nếu còn thay đổi chưa lưu (closeAddNew dùng sau khi lưu thành công). */
  async requestCloseAddNew(): Promise<void> {
    if (await this.canLeave()) this.closeAddNew();
  }

  // ── Liên kết Tài liệu số (dùng chung cho "Thêm sách lẻ" và "Sửa biểu ghi MARC") ─────────────
  fetchEbookPage = (filters: EntitySearchFilters, page: number, pageSize: number): import('rxjs').Observable<EntityPage<EbookDocument>> =>
    this.ebookSvc.search({ keyword: filters.keyword, pageIndex: page, pageSize }).pipe(map(r => ({ items: r.data, total: r.recordsTotal })));
  ebookDisplayFn = (item: EbookDocument): string => item.itemXml?.title || item.title || `#${item.id}`;
  ebookSubDisplayFn = (item: EbookDocument): string => item.itemXml?.author || '';

  openEbookPicker(): void { this.ebookPicker.open(); }

  onEbookPicked(ebook: EbookDocument): void {
    if (this.showEditMarc()) {
      this.copyEbookIntoFields(ebook, this.editMarcFields);
      const bibId = this.editMarcLine()?.bibid; if (!bibId) return;
      this.linkSvc.link(bibId, Number(ebook.id)).pipe(takeUntil(this.destroy$)).subscribe(ok => {
        if (ok) { this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.loadLinkedEbookForEditMarc(bibId); }
      });
    } else {
      this.copyEbookIntoFields(ebook, this.marcFields);
      this.pendingLinkEbookNew.set(ebook);
    }
  }
  clearPendingLinkEbookNew(): void { this.pendingLinkEbookNew.set(null); }

  loadLinkedEbookForEditMarc(bibId: number): void {
    this.isLoadingLinkEditMarc.set(true);
    this.linkSvc.getByBibId(bibId).pipe(takeUntil(this.destroy$)).subscribe({
      next: ebook => { this.linkedEbookEditMarc.set(ebook); this.isLoadingLinkEditMarc.set(false); },
      error: () => { this.isLoadingLinkEditMarc.set(false); }
    });
  }
  unlinkEbookEditMarc(): void {
    const bibId = this.editMarcLine()?.bibid; if (!bibId) return;
    this.linkSvc.unlink({ bibId }).pipe(takeUntil(this.destroy$)).subscribe(ok => {
      if (ok) { this.linkedEbookEditMarc.set(null); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); }
    });
  }
  /** Chỉ điền field/subfield MARC đang trống — không ghi đè dữ liệu biên mục đã có. */
  private ensureSubfieldOn(target: WritableSignal<MarcField[]>, tag: string, subcode: string, value: string | null | undefined): void {
    if (!value) return;
    const list = target();
    const field = list.find(f => f.tag === tag);
    if (!field) {
      target.set([...list, { tag, ind1: ' ', ind2: ' ', subFields: [{ code: subcode, value }] }]);
      return;
    }
    const sub = (field.subFields ?? []).find(s => s.code === subcode);
    if (sub) { if (sub.value) return; sub.value = value; }
    else { field.subFields = [...(field.subFields ?? []), { code: subcode, value }]; }
    target.set([...list]);
  }
  /** Điền field MARC từ dữ liệu của tài liệu số vừa chọn (chỉ điền field/subfield đang trống). */
  private copyEbookIntoFields(ebook: EbookDocument, target: WritableSignal<MarcField[]>): void {
    this.ensureSubfieldOn(target, '245', 'a', ebook.itemXml?.title ?? ebook.title ?? undefined);
    this.ensureSubfieldOn(target, '100', 'a', ebook.itemXml?.author ?? undefined);
    this.ensureSubfieldOn(target, '260', 'b', ebook.itemXml?.publisher ?? ebook.publisher ?? undefined);
    this.ensureSubfieldOn(target, '260', 'c', ebook.itemXml?.publishDate ?? (ebook.publishYear ? String(ebook.publishYear) : undefined));
    this.toastr.success(this.translate.instant('BIB_EDIT.COPY_DONE'));
  }
  onBibTypeChange(): void {
    const id = this.bibTypeId();
    this.worksheetId.set(null); this.worksheets.set([]);
    if (id == null) return;
    this.worksheetSvc.search({ bibTypeId: id, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.worksheets.set(res.data); if (res.data.length === 1) this.onWorksheetChange(res.data[0].id); },
      error: () => {}
    });
  }
  onWorksheetChange(id: number | null): void {
    this.worksheetId.set(id);
    if (id == null) return;
    this.wsFieldSvc.getByWorksheet(id).pipe(
      takeUntil(this.destroy$),
      switchMap(fields => {
        const fieldIds = fields.map(f => f.id).filter((fid): fid is number => !!fid);
        return this.wsSubSvc.getByFields(fieldIds).pipe(map(subfields => ({ fields, subfields })));
      })
    ).subscribe({
      next: ({ fields, subfields }) => {
        if (!fields.length) return;
        const marcFields: MarcField[] = fields.map(f => ({
          tag: f.field || '', ind1: f.l1 || ' ', ind2: f.l2 || ' ',
          subFields: subfields
            .filter(s => s.worksheet_Field_Id === f.id)
            .map(s => ({ code: s.subfield || '', value: s.value || '' }))
        }));
        this.marcFields.set(marcFields);
      },
      error: () => {}
    });
  }
  onCurrencyChangeForNew(): void {
    const code = this.newLineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.newLineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  addNewLine(): void {
    const r = this.receipt(); if (!r) return;
    this.checkIsbnThenProceed(this.marcFields(), () => this.doAddNewLine(r));
  }
  private doAddNewLine(r: AbReceipt): void {
    const v = this.newLineForm.getRawValue();
    this.isSavingLine.set(true);
    this.bibSvc.save({ bibTypeId: this.bibTypeId() ?? undefined, collectionId: this.collectionId() ?? undefined, images: this.coverImagePreviewUrl() ?? undefined, fields: this.applyPriceToFields(this.marcFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingLine.set(false); return; }
        const pendingEbook = this.pendingLinkEbookNew();
        if (pendingEbook && res.bibId) {
          this.linkSvc.link(res.bibId, Number(pendingEbook.id)).subscribe();
        }
        this.service.saveLine({ Receipt_Id: r.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
          .pipe(takeUntil(this.destroy$)).subscribe({
            next: () => { this.isSavingLine.set(false); this.closeAddNew(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
            error: () => { this.isSavingLine.set(false); }
          });
      },
      error: () => { this.isSavingLine.set(false); }
    });
  }

  // ── Tra trùng ─────────────────────────────────────────────────────────────
  openPickExisting(): void {
    if (!this.canEdit()) return;
    this.resetTraceSearch();
    this.pickSelected.set(null);
    this.pickMode.set('link');
    this.pickLineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
    this.showPickExisting.set(true);
  }
  closePickExisting(): void { this.showPickExisting.set(false); }
  resetTraceSearch(): void {
    this.traceSearchForm.reset({ MfnFrom: null, MfnTo: null, Title: '', Author: '', Publisher: '', PublishYear: '', BibTypeId: null });
    this.pickResults.set([]); this.pickTotalRecords.set(0); this.pickPageIndex.set(0);
  }
  searchExistingBibs(resetPage = true): void {
    if (resetPage) this.pickPageIndex.set(0);
    const v = this.traceSearchForm.getRawValue();
    this.isSearchingTrace.set(true);
    this.bibSvc.search({
      title: v.Title || null, author: v.Author || null, publisher: v.Publisher || null,
      publishYear: v.PublishYear || null, mfnFrom: v.MfnFrom ?? null, mfnTo: v.MfnTo ?? null,
      bibTypeId: v.BibTypeId ?? null, pageIndex: this.pickPageIndex() + 1, pageSize: this.pickPageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.pickResults.set(res.data); this.pickTotalRecords.set(res.recordsTotal); this.isSearchingTrace.set(false); },
      error: () => { this.pickResults.set([]); this.isSearchingTrace.set(false); }
    });
  }
  pickNextPage(): void { if ((this.pickPageIndex() + 1) * this.pickPageSize < this.pickTotalRecords()) { this.pickPageIndex.update(i => i + 1); this.searchExistingBibs(false); } }
  pickPrevPage(): void { if (this.pickPageIndex() > 0) { this.pickPageIndex.update(i => i - 1); this.searchExistingBibs(false); } }
  /** "Chọn" = thêm bản của CHÍNH biểu ghi đó; "Copy" = dùng biểu ghi làm mẫu, tạo biểu ghi mới (ấn bản/đầu sách
   *  khác gần giống) — port ELIB-LRC 09-23. Tra trùng/GetByMfn đã lọc theo đơn vị nên chỉ copy được biểu ghi cùng đơn vị. */
  pickMode = signal<'link' | 'copy'>('link');
  selectExistingBib(b: Bib): void { this.pickMode.set('link'); this.pickSelected.set(b); }
  selectExistingBibAsCopy(b: Bib): void { this.pickMode.set('copy'); this.pickSelected.set(b); }
  onCurrencyChangeForPick(): void {
    const code = this.pickLineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.pickLineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  confirmPickLine(): void {
    const r = this.receipt(); const b = this.pickSelected(); if (!r || !b) return;
    if (this.pickMode() === 'copy') { this.confirmPickLineAsCopy(r, b); return; }
    const v = this.pickLineForm.getRawValue();
    this.isPicking.set(true);
    this.syncPriceForExistingRecord(b.mfn, v.Price, v.Currency).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.service.saveLine({ Receipt_Id: r.id, Bibid: b.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
        .pipe(takeUntil(this.destroy$)).subscribe({
          next: () => { this.isPicking.set(false); this.closePickExisting(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
          error: () => { this.isPicking.set(false); }
        });
    });
  }

  private confirmPickLineAsCopy(r: AbReceipt, b: Bib): void {
    if (!b.mfn) return;
    const v = this.pickLineForm.getRawValue();
    this.isPicking.set(true);
    this.bibSvc.getByMfn(b.mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: source => {
        if (!source?.fields?.length) { this.isPicking.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
        this.checkIsbnThenProceed(source.fields, () => {
          this.bibSvc.save({ bibTypeId: source.bibTypeId ?? undefined, collectionId: source.collectionId ?? undefined,
                             fields: this.applyPriceToFields(source.fields!, v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
            next: res => {
              if (!res) { this.isPicking.set(false); return; }
              this.service.saveLine({ Receipt_Id: r.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
                .pipe(takeUntil(this.destroy$)).subscribe({
                  next: () => { this.isPicking.set(false); this.closePickExisting(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
                  error: () => { this.isPicking.set(false); }
                });
            },
            error: () => { this.isPicking.set(false); }
          });
        }, () => this.isPicking.set(false)); // huỷ cảnh báo trùng ISBN → nút không quay mãi (lỗi của bản LRC)
      },
      error: () => { this.isPicking.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  // ── Thêm sách từ Z3950 ────────────────────────────────────────────────────
  showZ3950 = signal(false);
  z3950Configs = signal<Z3950Config[]>([]);
  z3950Results = signal<Z3950ResultItem[]>([]);
  z3950Errors = signal<{ configId: number; configName: string; error: string }[]>([]);
  z3950PageIndex = signal(0); z3950PageSize = 10; z3950TotalRecords = signal(0);
  isSearchingZ3950 = signal(false);
  z3950Selected = signal<Z3950ResultItem | null>(null);
  z3950Fields = signal<MarcField[]>([]);
  isLoadingZ3950Marc = signal(false);
  isImportingZ3950 = signal(false);
  z3950ImportBibTypeId = signal<number | null>(null);
  z3950ImportCollectionId = signal<number | null>(null);
  z3950ImportWorksheetId = signal<number | null>(null);
  z3950Worksheets = signal<WorkSheet[]>([]);
  selectedZ3950ConfigIds = signal<Set<number>>(new Set());
  z3950SearchForm = new FormGroup({
    title:     new FormControl<string>('', { nonNullable: true }),
    author:    new FormControl<string>('', { nonNullable: true }),
    isbn:      new FormControl<string>('', { nonNullable: true }),
    issn:      new FormControl<string>('', { nonNullable: true }),
    publisher: new FormControl<string>('', { nonNullable: true }),
    keyword:   new FormControl<string>('', { nonNullable: true }),
  });
  z3950LineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });

  openZ3950(): void {
    if (!this.canEdit()) return;
    this.z3950SearchForm.reset({ title: '', author: '', isbn: '', issn: '', publisher: '', keyword: '' });
    this.z3950LineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
    this.z3950Results.set([]); this.z3950TotalRecords.set(0); this.z3950PageIndex.set(0);
    this.z3950Errors.set([]);
    this.z3950Selected.set(null);
    this.z3950Fields.set([]);
    this.isLoadingZ3950Marc.set(false);
    const firstType = this.bibTypes()[0]?.id ?? null;
    this.z3950ImportBibTypeId.set(firstType);
    this.z3950ImportCollectionId.set(null);
    this.z3950Worksheets.set([]); this.z3950ImportWorksheetId.set(null);
    if (firstType != null) this.onZ3950BibTypeChange(firstType);
    this.z3950ConfigSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        const configs = (res.data || []).filter(c => c.systax !== 'Internal');
        this.z3950Configs.set(configs);
        this.selectedZ3950ConfigIds.set(new Set(configs.map(c => c.id)));
      },
      error: () => {}
    });
    this.showZ3950.set(true);
  }
  closeZ3950(): void { this.showZ3950.set(false); }
  toggleZ3950Config(id: number): void {
    const set = new Set(this.selectedZ3950ConfigIds());
    set.has(id) ? set.delete(id) : set.add(id);
    this.selectedZ3950ConfigIds.set(set);
  }
  toggleAllZ3950Configs(): void {
    const all = this.z3950Configs();
    this.selectedZ3950ConfigIds.set(this.selectedZ3950ConfigIds().size === all.length ? new Set() : new Set(all.map(c => c.id)));
  }
  onZ3950BibTypeChange(id: number | null): void {
    this.z3950ImportBibTypeId.set(id);
    this.z3950ImportWorksheetId.set(null); this.z3950Worksheets.set([]);
    if (id == null) return;
    this.worksheetSvc.search({ bibTypeId: id, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.z3950Worksheets.set(res.data); if (res.data.length === 1) this.z3950ImportWorksheetId.set(res.data[0].id); },
      error: () => {}
    });
  }
  hasZ3950Query(): boolean { const v = this.z3950SearchForm.getRawValue(); return !!(v.title || v.author || v.isbn || v.issn || v.publisher || v.keyword); }
  searchZ3950(resetPage = true): void {
    if (!this.hasZ3950Query()) { this.toastr.error(this.translate.instant('AB_RECEIPT.Z3950_NEED_QUERY')); return; }
    if (this.selectedZ3950ConfigIds().size === 0) { this.toastr.error(this.translate.instant('AB_RECEIPT.Z3950_NEED_LIBRARY')); return; }
    if (resetPage) this.z3950PageIndex.set(0);
    const v = this.z3950SearchForm.getRawValue();
    this.isSearchingZ3950.set(true);
    this.z3950Svc.search({ title: v.title || null, author: v.author || null, isbn: v.isbn || null, issn: v.issn || null, publisher: v.publisher || null, keyword: v.keyword || null, configIds: Array.from(this.selectedZ3950ConfigIds()), pageIndex: this.z3950PageIndex() + 1, pageSize: this.z3950PageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.z3950Results.set(res.data); this.z3950TotalRecords.set(res.recordsTotal); this.z3950Errors.set(res.errors || []); this.isSearchingZ3950.set(false); },
        error: () => { this.toastr.error(this.translate.instant('AB_RECEIPT.Z3950_SEARCH_ERROR')); this.isSearchingZ3950.set(false); }
      });
  }
  z3950NextPage(): void { if ((this.z3950PageIndex() + 1) * this.z3950PageSize < this.z3950TotalRecords()) { this.z3950PageIndex.update(i => i + 1); this.searchZ3950(false); } }
  z3950PrevPage(): void { if (this.z3950PageIndex() > 0) { this.z3950PageIndex.update(i => i - 1); this.searchZ3950(false); } }
  selectZ3950Result(row: Z3950ResultItem): void {
    this.z3950Selected.set(row);
    this.z3950Fields.set([]);
    this.isLoadingZ3950Marc.set(true);
    this.z3950Svc.getMarcFields(row.id, row.configId).pipe(takeUntil(this.destroy$)).subscribe({
      next: fieldsRes => {
        this.isLoadingZ3950Marc.set(false);
        if (!fieldsRes.length) { this.toastr.error(this.translate.instant('AB_RECEIPT.Z3950_MARC_EMPTY')); }
        this.z3950Fields.set(fieldsRes);
      },
      error: () => { this.isLoadingZ3950Marc.set(false); this.toastr.error(this.translate.instant('AB_RECEIPT.Z3950_MARC_EMPTY')); }
    });
  }
  onCurrencyChangeForZ3950(): void {
    const code = this.z3950LineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.z3950LineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  confirmImportZ3950(): void {
    const r = this.receipt(); const row = this.z3950Selected(); const bibTypeId = this.z3950ImportBibTypeId();
    if (!r || !row) return;
    if (!bibTypeId) { this.toastr.error(this.translate.instant('AB_RECEIPT.LINE_BIB_TYPE')); return; }
    this.checkIsbnThenProceed(this.z3950Fields(), () => this.doConfirmImportZ3950(r, bibTypeId));
  }
  private doConfirmImportZ3950(r: AbReceipt, bibTypeId: number): void {
    const v = this.z3950LineForm.getRawValue();
    this.isImportingZ3950.set(true);
    this.bibSvc.save({ bibTypeId, collectionId: this.z3950ImportCollectionId() ?? undefined, fields: this.applyPriceToFields(this.z3950Fields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isImportingZ3950.set(false); return; }
        this.service.saveLine({ Receipt_Id: r.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
          .pipe(takeUntil(this.destroy$)).subscribe({
            next: () => { this.isImportingZ3950.set(false); this.closeZ3950(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
            error: () => { this.isImportingZ3950.set(false); }
          });
      },
      error: () => { this.isImportingZ3950.set(false); }
    });
  }

  // ── Thêm sách từ file Marc ────────────────────────────────────────────────
  showMarcFile = signal(false);
  isParsingMarcFile = signal(false);
  marcFileBibTypeId = signal<number | null>(null);
  marcFileCollectionId = signal<number | null>(null);
  marcFileFields = signal<MarcField[]>([]);
  marcFileLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });
  isSavingMarcFile = signal(false);
  openMarcFileImport(): void {
    if (!this.canEdit()) return;
    this.marcFileFields.set([]); this.marcFileBibTypeId.set(null); this.marcFileCollectionId.set(null);
    this.marcFileLineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
    this.marcTextPasteValue.set(''); this.showMarcTextPaste.set(false);
    this.showMarcFile.set(true);
  }
  closeMarcFileImport(): void { this.showMarcFile.set(false); }
  // ── Nhập MARC từ file .txt / dán văn bản (port ELIB-LRC 09-23) ──────────────
  isParsingMarcText  = signal(false);
  showMarcTextPaste  = signal(false);
  marcTextPasteValue = signal('');
  /** File .txt: nếu thực chất là ISO2709 nhị phân (có ký tự điều khiển 0x1E/0x1D — file "Marc_TIMESTAMP.txt" hệ cũ
   *  xuất ra) thì chuyển sang parser ISO2709 của backend; còn lại phân tích dạng văn bản 3 dòng/trường ở client. */
  onMarcTextFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0]; if (!file) return;
    this.isParsingMarcText.set(true);
    file.text().then(text => {
      this.isParsingMarcText.set(false);
      if (text.includes('\x1e') || text.includes('\x1d')) { this.onMarcFileSelected(event); return; }
      input.value = '';
      this.applyParsedMarcText(text);
    }).catch(() => { this.isParsingMarcText.set(false); input.value = ''; this.toastr.error(this.translate.instant('AB_RECEIPT.MARC_FILE_ERROR')); });
  }
  openMarcTextPaste(): void { this.marcTextPasteValue.set(''); this.showMarcTextPaste.set(true); }
  closeMarcTextPaste(): void { this.showMarcTextPaste.set(false); }
  applyMarcTextPaste(): void { if (this.applyParsedMarcText(this.marcTextPasteValue())) this.showMarcTextPaste.set(false); }
  private applyParsedMarcText(text: string): boolean {
    const fields = parseMarcText(text);
    if (!fields.length) { this.toastr.error(this.translate.instant('AB_RECEIPT.MARC_FILE_EMPTY')); return false; }
    this.marcFileFields.set(fields);
    this.toastr.success(this.translate.instant('AB_RECEIPT.MARC_TEXT_PARSED', { count: fields.length }));
    return true;
  }

  onMarcFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0]; if (!file) return;
    this.isParsingMarcFile.set(true);
    this.bibSvc.parseMarcFile(file).pipe(takeUntil(this.destroy$)).subscribe({
      next: records => {
        this.isParsingMarcFile.set(false);
        input.value = '';
        if (!records.length || !records[0].length) { this.toastr.error(this.translate.instant('AB_RECEIPT.MARC_FILE_EMPTY')); return; }
        this.marcFileFields.set(records[0]);
        this.toastr.success(this.translate.instant('AB_RECEIPT.MARC_FILE_PARSED', { count: records.length }));
      },
      error: () => { this.isParsingMarcFile.set(false); input.value = ''; this.toastr.error(this.translate.instant('AB_RECEIPT.MARC_FILE_ERROR')); }
    });
  }
  addMarcFileLine(): void {
    const r = this.receipt(); if (!r) return;
    const bibTypeId = this.marcFileBibTypeId();
    if (!bibTypeId) { this.toastr.error(this.translate.instant('AB_RECEIPT.LINE_BIB_TYPE')); return; }
    this.checkIsbnThenProceed(this.marcFileFields(), () => this.doAddMarcFileLine(r, bibTypeId));
  }
  private doAddMarcFileLine(r: AbReceipt, bibTypeId: number): void {
    const v = this.marcFileLineForm.getRawValue();
    this.isSavingMarcFile.set(true);
    this.bibSvc.save({ bibTypeId, collectionId: this.marcFileCollectionId() ?? undefined, fields: this.applyPriceToFields(this.marcFileFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingMarcFile.set(false); return; }
        this.service.saveLine({ Receipt_Id: r.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
          .pipe(takeUntil(this.destroy$)).subscribe({
            next: () => { this.isSavingMarcFile.set(false); this.closeMarcFileImport(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
            error: () => { this.isSavingMarcFile.set(false); }
          });
      },
      error: () => { this.isSavingMarcFile.set(false); }
    });
  }

  // ── Thêm sách từ đơn đặt: chọn nhiều ấn phẩm đã nhận được cùng lúc ─────────
  showOrderPick = signal(false);
  orderPickFilter = new FormGroup({
    CodeFrom: new FormControl<number | null>(null),
    CodeTo:   new FormControl<number | null>(null),
    MfnFrom:  new FormControl<number | null>(null),
    MfnTo:    new FormControl<number | null>(null),
    Title:    new FormControl<string>('', { nonNullable: true }),
    Author:   new FormControl<string>('', { nonNullable: true }),
  });
  orderPickResults = signal<AbOrderLine[]>([]);
  orderPickSelection = new SelectionModel<AbOrderLine>(true, []);
  isSearchingOrderPick = signal(false);
  isConfirmingOrderPick = signal(false);

  openOrderPick(): void {
    if (!this.canEdit()) return;
    this.orderPickFilter.reset({ CodeFrom: null, CodeTo: null, MfnFrom: null, MfnTo: null, Title: '', Author: '' });
    this.orderPickResults.set([]); this.orderPickSelection.clear();
    this.showOrderPick.set(true);
    this.searchOrderPick();
  }
  closeOrderPick(): void { this.showOrderPick.set(false); }
  resetOrderPickFilter(): void {
    this.orderPickFilter.reset({ CodeFrom: null, CodeTo: null, MfnFrom: null, MfnTo: null, Title: '', Author: '' });
  }
  searchOrderPick(): void {
    this.isSearchingOrderPick.set(true);
    const v = this.orderPickFilter.getRawValue();
    this.orderSvc.lookupReceivableLines({
      orderCodeFrom: v.CodeFrom, orderCodeTo: v.CodeTo, mfnFrom: v.MfnFrom, mfnTo: v.MfnTo,
      title: v.Title, author: v.Author, pageSize: 50
    }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.orderPickResults.set(res.data); this.orderPickSelection.clear(); this.isSearchingOrderPick.set(false);
    });
  }
  isAllOrderPickSelected(): boolean {
    return this.orderPickSelection.selected.length > 0 && this.orderPickSelection.selected.length === this.orderPickResults().length;
  }
  toggleAllOrderPick(): void {
    this.isAllOrderPickSelected() ? this.orderPickSelection.clear() : this.orderPickSelection.select(...this.orderPickResults());
  }
  confirmOrderPickSelection(): void {
    const r = this.receipt(); const selected = this.orderPickSelection.selected;
    if (!r || !selected.length) return;
    this.isConfirmingOrderPick.set(true);
    // Biểu ghi của dòng đơn đặt là bản nháp (BibOrder) — phải "thăng cấp" thành Bib thật (sinh Mfn/Bibid
    // mới) trước khi gắn vào Đơn nhận. Lặp tuần tự (concatMap) để tránh đua nhau ghi AbReceiptDetail.
    from(selected).pipe(
      concatMap(line => this.bibOrderSvc.promoteToBib(line.bibid!).pipe(
        switchMap(promoted => !promoted ? of(null) :
          this.syncPriceForExistingRecord(promoted.mfn, line.price ?? 0, line.currency ?? 'VND').pipe(
            switchMap(() => this.service.saveLine({
              Receipt_Id: r.id, Bibid: promoted.bibId,
              Amount: line.remainingAmount ?? line.amount ?? 1,
              Price: line.price ?? 0, CURRENCY: line.currency ?? 'VND', Rate: line.rate ?? 1,
              Order_Id: line.order_Id, OrderDetailId: line.id
            }))
          )
        )
      )),
      takeUntil(this.destroy$)
    ).subscribe({
      error: () => { this.isConfirmingOrderPick.set(false); },
      complete: () => {
        this.isConfirmingOrderPick.set(false); this.closeOrderPick(); this.reloadLines();
        this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
      }
    });
  }

  // ── Sửa biểu ghi MARC ─────────────────────────────────────────────────────
  openEditMarc(line: AbReceiptLine): void {
    if (!this.canEdit()) return;
    if (!line.mfn) { this.toastr.error(this.translate.instant('AB_RECEIPT.NO_MFN')); return; }
    this.editMarcLine.set(line); this.editMarcFields.set([]);
    this.editMarcLineForm.reset({ Amount: line.amount ?? 1, Currency: line.currency ?? 'VND', Price: line.price ?? 0, Rate: line.rate ?? 1 });
    this.linkedEbookEditMarc.set(null);
    this.showEditMarc.set(true);
    if (line.bibid != null) this.loadLinkedEbookForEditMarc(line.bibid);
    this.isLoadingEditMarc.set(true);
    this.bibSvc.getByMfn(line.mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: bib => {
        this.editMarcFields.set(bib?.fields?.length ? bib.fields : defaultMarcFields());
        this.editMarcCollectionId.set(bib?.collectionId ?? null);
        this.isLoadingEditMarc.set(false);
      },
      error: () => { this.isLoadingEditMarc.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.closeEditMarc(); }
    });
  }
  closeEditMarc(): void { this.showEditMarc.set(false); this.editMarcLine.set(null); }
  onCurrencyChangeForEditMarc(): void {
    const code = this.editMarcLineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.editMarcLineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  saveEditMarc(): void {
    const r = this.receipt(); const line = this.editMarcLine(); if (!r || !line) return;
    const v = this.editMarcLineForm.getRawValue();
    this.isSavingMarc.set(true);
    this.bibSvc.save({ bibId: line.bibid, mfn: line.mfn, collectionId: this.editMarcCollectionId() ?? undefined, fields: this.applyPriceToFields(this.editMarcFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingMarc.set(false); return; }
        this.service.saveLine({ Receipt_Id: r.id, Bibid: line.bibid, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
          .pipe(takeUntil(this.destroy$)).subscribe({
            next: () => {
              this.isSavingMarc.set(false);
              this.closeEditMarc(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
            },
            error: () => { this.isSavingMarc.set(false); }
          });
      },
      error: () => { this.isSavingMarc.set(false); }
    });
  }

  // ── Sửa dòng (Số lượng/Đơn giá/Loại tiền) ─────────────────────────────────
  showEditLine  = signal(false);
  editingLine   = signal<AbReceiptLine | null>(null);
  isSavingLineEdit = signal(false);
  editLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });
  openEditLine(line: AbReceiptLine): void {
    if (!this.canEdit()) return;
    this.editingLine.set(line);
    this.editLineForm.reset({ Amount: line.amount ?? 1, Currency: line.currency ?? 'VND', Price: line.price ?? 0, Rate: line.rate ?? 1 });
    this.showEditLine.set(true);
  }
  closeEditLine(): void { this.showEditLine.set(false); this.editingLine.set(null); }
  onCurrencyChangeForEditLine(): void {
    const code = this.editLineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.editLineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  saveEditLine(): void {
    const r = this.receipt(); const line = this.editingLine(); if (!r || !line) return;
    const v = this.editLineForm.getRawValue();
    this.isSavingLineEdit.set(true);
    this.syncPriceForExistingRecord(line.mfn, v.Price, v.Currency).pipe(takeUntil(this.destroy$)).subscribe(() => {
      // SaveDetail khớp theo (Receipt_Id, Bibid) nên gọi lại với cùng Bibid sẽ UPDATE dòng cũ, không tạo trùng.
      this.service.saveLine({ Receipt_Id: r.id, Bibid: line.bibid, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
        .pipe(takeUntil(this.destroy$)).subscribe({
          next: () => { this.isSavingLineEdit.set(false); this.closeEditLine(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); },
          error: () => { this.isSavingLineEdit.set(false); }
        });
    });
  }

  // ── Xóa dòng / ĐKCB ───────────────────────────────────────────────────────
  handleDeleteLine(line: AbReceiptLine): void { if (!this.canEdit()) return; this.deletingLine.set(line); this.showConfirmDeleteLine.set(true); }
  closeConfirmDeleteLine(): void { this.showConfirmDeleteLine.set(false); this.deletingLine.set(null); }
  confirmDeleteLine(): void {
    const line = this.deletingLine(); if (!line) return;
    this.service.deleteLine(line.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.closeConfirmDeleteLine(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      error: (err: any) => { this.closeConfirmDeleteLine(); this.toastr.error(err?.error?.message || this.translate.instant('AB_RECEIPT.DELETE_LINE_BLOCKED')); }
    });
  }

  openKcbModal(line: AbReceiptLine): void {
    this.kcbLine.set(line); this.kcbList.set([]); this.kcbTab.set('single');
    const storeId = this.receipt()?.store_Id ?? null;
    // Số lượng khoá theo số còn lại của dòng đơn nhận (port ELIB-LRC 09-22) — backend cũng chặn vượt.
    this.kcbForm.reset({ Prefix: '', DigitLength: 6, StartNumber: null, Quantity: Math.max(this.kcbRemaining(), 0), StoreId: storeId });
    this.kcbSingleForm.reset({ BarcodeValue: '', StoreId: storeId });
    this.showKcbModal.set(true);
    this.loadKcbList(line.id);
  }
  closeKcbModal(): void { this.showKcbModal.set(false); this.kcbLine.set(null); this.kcbList.set([]); }
  private loadKcbList(receiptLineId: number): void {
    this.isLoadingKcb.set(true);
    this.service.getBarcodes(receiptLineId).pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.kcbList.set(list); this.isLoadingKcb.set(false); this.kcbForm.controls.Quantity.setValue(Math.max(this.kcbRemaining(), 0)); },
      error: () => { this.isLoadingKcb.set(false); }
    });
  }
  registerKcb(): void {
    if (!this.canEdit()) return;
    const line = this.kcbLine();
    if (!line || this.kcbForm.invalid) { this.kcbForm.markAllAsTouched(); return; }
    const v = this.kcbForm.getRawValue();
    this.isRegisteringKcb.set(true);
    this.service.registerBarcodes({ receiptLineId: line.id, prefix: v.Prefix.trim(), digitLength: v.DigitLength, quantity: v.Quantity, storeId: v.StoreId!, startNumber: v.StartNumber ?? undefined })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isRegisteringKcb.set(false); this.toastr.success(this.translate.instant('AB_RECEIPT.KCB_REGISTER_SUCCESS')); this.loadKcbList(line.id); this.reloadLines(); },
        error: () => { this.isRegisteringKcb.set(false); }
      });
  }
  registerSingleKcb(): void {
    if (!this.canEdit()) return;
    const line = this.kcbLine();
    if (!line || this.kcbSingleForm.invalid) { this.kcbSingleForm.markAllAsTouched(); return; }
    const v = this.kcbSingleForm.getRawValue();
    this.isRegisteringSingleKcb.set(true);
    this.service.registerSingleBarcode({ receiptLineId: line.id, barcodeValue: v.BarcodeValue.trim(), storeId: v.StoreId! })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => {
          this.isRegisteringSingleKcb.set(false);
          if (!res) { return; }
          this.kcbSingleForm.patchValue({ BarcodeValue: '' });
          this.toastr.success(this.translate.instant('AB_RECEIPT.KCB_REGISTER_SUCCESS'));
          this.loadKcbList(line.id); this.reloadLines();
        },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        error: (err: any) => { this.isRegisteringSingleKcb.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
  }
  deleteKcbItem(item: RegisteredBarcode): void {
    if (!this.canEdit()) return;
    const line = this.kcbLine(); if (!line || !item.id) return;
    this.service.deleteBarcode(item.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadKcbList(line.id); this.reloadLines(); },
      error: () => { }
    });
  }

  // Đợt 17 — Lịch sử thay đổi cho 1 ĐKCB.
  openBarcodeHistory(item: RegisteredBarcode): void {
    if (!item.publicId) return;
    this.router.navigate(['/admin/entity-history'], { queryParams: { type: 'Barcode', id: item.publicId } });
  }
}
