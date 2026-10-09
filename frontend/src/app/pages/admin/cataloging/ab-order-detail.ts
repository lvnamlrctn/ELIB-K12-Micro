import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil, switchMap, of, map, catchError } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { AbOrderService } from '../../../services/cataloging/order.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { CurrencyService } from '../../../services/acquisition/currency.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { BibOrderService } from '../../../services/cataloging/bib-order.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { WorkSheetService } from '../../../services/cataloging/worksheet.service';
import { WorksheetFieldService } from '../../../services/cataloging/worksheet-field.service';
import { WorksheetSubfieldService } from '../../../services/cataloging/worksheet-subfield.service';
import { MarcFieldService } from '../../../services/cataloging/marc-field.service';
import { MarcSubFieldService } from '../../../services/cataloging/marc-subfield.service';
import { MarcFieldDic } from '../../../models/cataloging/marc-field';
import { MarcSubFieldDic } from '../../../models/cataloging/marc-subfield';
import { WorkSheet } from '../../../models/cataloging/worksheet';
import { Z3950SearchService } from '../../../services/cataloging/z3950-search.service';
import { Z3950ConfigService } from '../../../services/cataloging/z3950-config.service';
import { Z3950ResultItem } from '../../../models/cataloging/z3950-result';
import { Z3950Config } from '../../../models/cataloging/z3950-config';
import { AbOrder, AbOrderLine } from '../../../models/cataloging/order';
import { Currency } from '../../../models/acquisition/currency';
import { MarcField, Bib } from '../../../models/cataloging/bib';
import { BibType } from '../../../models/cataloging/bib-type';
import { MarcFieldsEditorComponent } from '../../../shared/marc-editor/marc-fields-editor';
import { defaultMarcFields, setMarcSubfieldValue, formatPriceForMarc, applyBookMetadataToFields } from '../../../shared/marc-editor/marc-defaults.util';
import { parseMarcText } from '../../../shared/marc-editor/marc-text-import.util';
import { UnsavedChanges, UnsavedChangesPage, watchUnsavedChanges } from '../../../guards/unsaved-changes.guard';

@Component({
  selector: 'app-ab-order-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule, MarcFieldsEditorComponent, DateInputComponent, NgSelectModule],
  templateUrl: './ab-order-detail.html'
})
export class AbOrderDetailPage implements OnInit, OnDestroy, UnsavedChangesPage {
  protected readonly Math = Math;
  private route        = inject(ActivatedRoute);
  private router       = inject(Router);
  private service      = inject(AbOrderService);
  private supplierSvc  = inject(SupplierService);
  private fundSvc      = inject(FundService);
  private currencySvc  = inject(CurrencyService);
  private bibSvc       = inject(BibService);
  private bibOrderSvc  = inject(BibOrderService);
  private bibTypeSvc   = inject(BibTypeService);
  private worksheetSvc = inject(WorkSheetService);
  private wsFieldSvc   = inject(WorksheetFieldService);
  private wsSubSvc     = inject(WorksheetSubfieldService);
  private marcFieldSvc = inject(MarcFieldService);
  private marcSubSvc   = inject(MarcSubFieldService);
  private z3950Svc       = inject(Z3950SearchService);
  private z3950ConfigSvc = inject(Z3950ConfigService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private destroy$     = new Subject<void>();

  statusOptions = [
    { value: 0, label: 'AB_ORDER.ST_DRAFT' },
    { value: 1, label: 'AB_ORDER.ST_ORDERED' },
    { value: 2, label: 'AB_ORDER.ST_RECEIVED' },
  ];
  paymentMethodOptions = [{ value: 0, label: 'AB_RECEIPT.PM_CASH' }, { value: 1, label: 'AB_RECEIPT.PM_TRANSFER' }];

  order     = signal<AbOrder | null>(null);
  lines     = signal<AbOrderLine[]>([]);
  isLoading = signal(false);
  isSaving  = signal(false);

  suppliers = signal<{ id: number; name?: string }[]>([]);
  funds     = signal<{ id: number; name?: string }[]>([]);
  currencies = signal<Currency[]>([]);
  bibTypes    = signal<BibType[]>([]);
  marcFieldDics    = signal<MarcFieldDic[]>([]);
  marcSubFieldDics = signal<MarcSubFieldDic[]>([]);

  dataForm = new FormGroup({
    OrderName:       new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    OrderDate:       new FormControl<string>('', { nonNullable: true }),
    DueDate:         new FormControl<string>('', { nonNullable: true }),
    Status:          new FormControl<number>(0, { nonNullable: true }),
    PaymentMethodId: new FormControl<number>(0, { nonNullable: true }),
    SupplierId:      new FormControl<number | null>(null),
    FundId:          new FormControl<number | null>(null),
    Note:            new FormControl<string>('', { nonNullable: true }),
  });

  // Thêm sách lẻ (tạo biểu ghi MARC nháp mới — BibOrder, không phải Bib thật; không có Bộ sưu tập vì
  // BibOrder không có CollectionId)
  showAddNew  = signal(false);
  bibTypeId   = signal<number | null>(null);
  worksheets  = signal<WorkSheet[]>([]);
  worksheetId = signal<number | null>(null);
  marcFields  = signal<MarcField[]>([]);
  newLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });
  isSavingLine = signal(false);

  // Đợt 21 — cảnh báo chưa lưu cho "Thêm sách lẻ": MARC, loại/mẫu, số lượng/giá/tiền tệ/tỷ giá.
  private unsaved = new UnsavedChanges();
  private addNewSnapshot() {
    return { type: this.bibTypeId(), worksheet: this.worksheetId(), fields: this.marcFields(), line: this.newLineForm.getRawValue() };
  }
  private hasUnsavedChanges(): boolean { return this.showAddNew() && this.unsaved.changed(this.addNewSnapshot()); }
  canLeave = watchUnsavedChanges(() => this.hasUnsavedChanges(), () => this.isSavingLine());

  // Tra trùng (tìm 1 biểu ghi đã có trong danh mục THẬT, dùng làm mẫu — chọn xong sẽ sao chép MARC
  // thành 1 BibOrder mới, không gắn thẳng Bibid thật vào dòng đơn đặt)
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
  deletingLine = signal<AbOrderLine | null>(null);

  // Sửa biểu ghi MARC của 1 dòng đã có trong đơn
  showEditMarc     = signal(false);
  editMarcLine     = signal<AbOrderLine | null>(null);
  editMarcFields   = signal<MarcField[]>([]);
  isSavingMarc     = signal(false);
  isLoadingEditMarc = signal(false);
  editMarcLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });

  // Sửa dòng (Số lượng/Đơn giá/Loại tiền)
  showEditLine  = signal(false);
  editingLine   = signal<AbOrderLine | null>(null);
  isSavingLineEdit = signal(false);
  editLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.fundSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.funds.set(r.data), error: () => {} });
    this.currencySvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.currencies.set(r.data), error: () => {} });
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.marcFieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcFieldDics.set(l), error: () => {} });
    this.marcSubSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcSubFieldDics.set(l), error: () => {} });
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  back(): void { this.router.navigate(['/admin/ab-orders']); }

  load(): void {
    const publicId = this.route.snapshot.paramMap.get('publicId');
    if (!publicId) return;
    this.isLoading.set(true);
    this.service.getByPublicId(publicId).pipe(
      takeUntil(this.destroy$),
      switchMap(o => {
        this.order.set(o);
        if (!o) return of([]);
        this.dataForm.patchValue({
          OrderName: o.order_Name ?? '', OrderDate: (o.date_Order ?? '').toString().substring(0, 10),
          DueDate: (o.duedate ?? '').toString().substring(0, 10), Status: o.status ?? 0,
          PaymentMethodId: o.paymentMethodId ?? 0, SupplierId: o.supplier_Id ?? null, FundId: o.fundId ?? null,
          Note: o.note ?? '',
        });
        return this.service.getLines(o.id);
      })
    ).subscribe({
      next: l => { this.lines.set(l); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }

  getSupplierName(id: number | null | undefined): string { if (!id) return '—'; return this.suppliers().find(s => s.id === id)?.name || '—'; }
  lineTotal(l: AbOrderLine): number { return (l.amount || 0) * (l.price || 0) * (l.rate || 1); }

  save(): void {
    const o = this.order(); const publicId = this.route.snapshot.paramMap.get('publicId');
    if (!o || !publicId) return;
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<AbOrder> = {
      order_Name: v.OrderName, date_Order: v.OrderDate || undefined, duedate: v.DueDate || undefined,
      status: v.Status, paymentMethodId: v.PaymentMethodId, supplier_Id: v.SupplierId ?? undefined,
      fundId: v.FundId ?? undefined, note: v.Note || undefined,
    };
    this.service.update(publicId, payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.load(); },
      error: () => { this.isSaving.set(false); }
    });
  }

  private reloadLines(): void {
    const o = this.order(); if (!o) return;
    this.service.getLines(o.id).pipe(takeUntil(this.destroy$)).subscribe(l => this.lines.set(l));
  }

  // ── Đồng bộ Đơn giá vào MARC 020$c ───────────────────────────────────────
  private applyPriceToFields(fields: MarcField[], price: number, currency: string): MarcField[] {
    return setMarcSubfieldValue(fields, '020', 'c', formatPriceForMarc(price, currency));
  }
  /** Dùng khi không có sẵn mảng fields tại chỗ (VD "Sửa dòng") — fetch biểu ghi BibOrder theo Mfn, set
   * 020$c, lưu lại. Fail-open: lỗi đồng bộ MARC không được chặn việc lưu dòng. */
  private syncPriceForExistingRecord(mfn: number | null | undefined, price: number, currency: string) {
    if (!mfn) return of(null);
    return this.bibOrderSvc.getByMfn(mfn).pipe(
      switchMap(bib => bib?.fields ? this.bibOrderSvc.save({ bibId: bib.bibId, mfn: bib.mfn, fields: this.applyPriceToFields(bib.fields, price, currency) }) : of(null)),
      catchError(() => of(null)),
    );
  }

  // ── Ảnh bìa tự động theo ISBN + Biên mục từ ảnh bìa (AI) — "Thêm sách lẻ" ──
  coverImagePreviewUrl = signal<string | null>(null);
  isFetchingCover = signal(false);
  private lastAutoFetchedIsbn: string | null = null;

  private extractIsbn(fields: MarcField[]): string | null {
    const sf = fields.find(f => f.tag === '020')?.subFields?.find(s => s.code === 'a');
    return sf?.value?.trim() || null;
  }

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

  // ── Thêm sách lẻ ──────────────────────────────────────────────────────────
  async openAddNew(): Promise<void> {
    if (!(await this.canLeave())) return;
    this.bibTypeId.set(null); this.worksheets.set([]); this.worksheetId.set(null);
    this.marcFields.set(defaultMarcFields());
    this.newLineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
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
    const o = this.order(); if (!o) return;
    const v = this.newLineForm.getRawValue();
    this.isSavingLine.set(true);
    this.bibOrderSvc.save({ bibTypeId: this.bibTypeId() ?? undefined, fields: this.applyPriceToFields(this.marcFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingLine.set(false); return; }
        this.service.saveLine({ Order_Id: o.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
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
    this.resetTraceSearch();
    this.pickSelected.set(null);
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
  selectExistingBib(b: Bib): void { this.pickSelected.set(b); }
  onCurrencyChangeForPick(): void {
    const code = this.pickLineForm.getRawValue().Currency;
    const c = this.currencies().find(x => x.code === code);
    this.pickLineForm.patchValue({ Rate: c?.exchangeRate ?? 1 });
  }
  /** Biểu ghi chọn được ở danh mục thật chỉ dùng làm MẪU — sao chép MARC của nó thành 1 BibOrder mới,
   * không gắn thẳng Bibid thật vào dòng đơn đặt (BibOrder.Bibid và Bib.Bibid là 2 chuỗi identity riêng). */
  confirmPickLine(): void {
    const o = this.order(); const b = this.pickSelected(); if (!o || !b || !b.mfn) return;
    const v = this.pickLineForm.getRawValue();
    this.isPicking.set(true);
    this.bibSvc.getByMfn(b.mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: source => {
        if (!source?.fields?.length) { this.isPicking.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
        this.bibOrderSvc.save({ bibTypeId: source.bibTypeId ?? undefined, fields: this.applyPriceToFields(source.fields, v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
          next: res => {
            if (!res) { this.isPicking.set(false); return; }
            this.service.saveLine({ Order_Id: o.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
              .pipe(takeUntil(this.destroy$)).subscribe({
                next: () => { this.isPicking.set(false); this.closePickExisting(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
                error: () => { this.isPicking.set(false); }
              });
          },
          error: () => { this.isPicking.set(false); }
        });
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
    this.z3950SearchForm.reset({ title: '', author: '', isbn: '', issn: '', publisher: '', keyword: '' });
    this.z3950LineForm.reset({ Amount: 1, Currency: 'VND', Price: 0, Rate: 1 });
    this.z3950Results.set([]); this.z3950TotalRecords.set(0); this.z3950PageIndex.set(0);
    this.z3950Errors.set([]);
    this.z3950Selected.set(null);
    this.z3950Fields.set([]);
    this.isLoadingZ3950Marc.set(false);
    const firstType = this.bibTypes()[0]?.id ?? null;
    this.z3950ImportBibTypeId.set(firstType);
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
    const o = this.order(); const row = this.z3950Selected(); const bibTypeId = this.z3950ImportBibTypeId();
    if (!o || !row) return;
    if (!bibTypeId) { this.toastr.error(this.translate.instant('AB_RECEIPT.LINE_BIB_TYPE')); return; }
    const v = this.z3950LineForm.getRawValue();
    this.isImportingZ3950.set(true);
    this.bibOrderSvc.save({ bibTypeId, fields: this.applyPriceToFields(this.z3950Fields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isImportingZ3950.set(false); return; }
        this.service.saveLine({ Order_Id: o.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
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
  marcFileFields = signal<MarcField[]>([]);
  marcFileLineForm = new FormGroup({
    Amount:   new FormControl<number>(1, { nonNullable: true }),
    Currency: new FormControl<string>('VND', { nonNullable: true }),
    Price:    new FormControl<number>(0, { nonNullable: true }),
    Rate:     new FormControl<number>(1, { nonNullable: true }),
  });
  isSavingMarcFile = signal(false);
  openMarcFileImport(): void {
    this.marcFileFields.set([]); this.marcFileBibTypeId.set(null);
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
    const o = this.order(); if (!o) return;
    const bibTypeId = this.marcFileBibTypeId();
    if (!bibTypeId) { this.toastr.error(this.translate.instant('AB_RECEIPT.LINE_BIB_TYPE')); return; }
    const v = this.marcFileLineForm.getRawValue();
    this.isSavingMarcFile.set(true);
    this.bibOrderSvc.save({ bibTypeId, fields: this.applyPriceToFields(this.marcFileFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingMarcFile.set(false); return; }
        this.service.saveLine({ Order_Id: o.id, Bibid: res.bibId, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
          .pipe(takeUntil(this.destroy$)).subscribe({
            next: () => { this.isSavingMarcFile.set(false); this.closeMarcFileImport(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
            error: () => { this.isSavingMarcFile.set(false); }
          });
      },
      error: () => { this.isSavingMarcFile.set(false); }
    });
  }

  // ── Sửa biểu ghi MARC ─────────────────────────────────────────────────────
  openEditMarc(line: AbOrderLine): void {
    if (!line.mfn) { this.toastr.error(this.translate.instant('AB_RECEIPT.NO_MFN')); return; }
    this.editMarcLine.set(line); this.editMarcFields.set([]);
    this.editMarcLineForm.reset({ Amount: line.amount ?? 1, Currency: line.currency ?? 'VND', Price: line.price ?? 0, Rate: line.rate ?? 1 });
    this.showEditMarc.set(true);
    this.isLoadingEditMarc.set(true);
    this.bibOrderSvc.getByMfn(line.mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: bib => {
        this.editMarcFields.set(bib?.fields?.length ? bib.fields : defaultMarcFields());
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
    const o = this.order(); const line = this.editMarcLine(); if (!o || !line) return;
    const v = this.editMarcLineForm.getRawValue();
    this.isSavingMarc.set(true);
    this.bibOrderSvc.save({ bibId: line.bibid, mfn: line.mfn, fields: this.applyPriceToFields(this.editMarcFields(), v.Price, v.Currency) }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (!res) { this.isSavingMarc.set(false); return; }
        this.service.saveLine({ Order_Id: o.id, Bibid: line.bibid, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
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
  openEditLine(line: AbOrderLine): void {
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
    const o = this.order(); const line = this.editingLine(); if (!o || !line) return;
    const v = this.editLineForm.getRawValue();
    this.isSavingLineEdit.set(true);
    this.syncPriceForExistingRecord(line.mfn, v.Price, v.Currency).pipe(takeUntil(this.destroy$)).subscribe(() => {
      // SaveDetail khớp theo (Order_Id, Bibid) nên gọi lại với cùng Bibid sẽ UPDATE dòng cũ, không tạo trùng.
      this.service.saveLine({ Order_Id: o.id, Bibid: line.bibid, Amount: v.Amount, Price: v.Price, CURRENCY: v.Currency, Rate: v.Rate })
        .pipe(takeUntil(this.destroy$)).subscribe({
          next: () => { this.isSavingLineEdit.set(false); this.closeEditLine(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); },
          error: () => { this.isSavingLineEdit.set(false); }
        });
    });
  }

  // ── Xóa dòng ──────────────────────────────────────────────────────────────
  handleDeleteLine(line: AbOrderLine): void { this.deletingLine.set(line); this.showConfirmDeleteLine.set(true); }
  closeConfirmDeleteLine(): void { this.showConfirmDeleteLine.set(false); this.deletingLine.set(null); }
  confirmDeleteLine(): void {
    const line = this.deletingLine(); if (!line) return;
    this.service.deleteLine(line.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.closeConfirmDeleteLine(); this.reloadLines(); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      error: (err: any) => { this.closeConfirmDeleteLine(); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.DELETE_ERROR')); }
    });
  }
}
