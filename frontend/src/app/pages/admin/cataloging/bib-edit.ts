import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, switchMap, map } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { WorkSheetService } from '../../../services/cataloging/worksheet.service';
import { WorksheetFieldService } from '../../../services/cataloging/worksheet-field.service';
import { WorksheetSubfieldService } from '../../../services/cataloging/worksheet-subfield.service';
import { MarcFieldService } from '../../../services/cataloging/marc-field.service';
import { MarcSubFieldService } from '../../../services/cataloging/marc-subfield.service';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { PrintBookAndDigitalService } from '../../../services/cataloging/print-book-and-digital.service';
import { EbookDocumentService } from '../../../services/ebook/ebook-document.service';
import { Bib, MarcField } from '../../../models/cataloging/bib';
import { BibType } from '../../../models/cataloging/bib-type';
import { MarcFieldDic } from '../../../models/cataloging/marc-field';
import { MarcSubFieldDic } from '../../../models/cataloging/marc-subfield';
import { WorkSheet } from '../../../models/cataloging/worksheet';
import { RegisteredBarcode } from '../../../models/cataloging/receipt';
import { Store } from '../../../models/printbook/store';
import { LinkedEbookInfo } from '../../../models/cataloging/print-book-and-digital';
import { EbookDocument } from '../../../models/ebook/ebook-document';
import { MarcFieldsEditorComponent } from '../../../shared/marc-editor/marc-fields-editor';
import { defaultMarcFields, applyBookMetadataToFields } from '../../../shared/marc-editor/marc-defaults.util';
import { EntitySearchPickerComponent, EntityPage, EntitySearchFilters } from '../../../shared/components/entity-search-picker/entity-search-picker';

@Component({
  selector: 'app-bib-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, TranslateModule, MatIconModule, MarcFieldsEditorComponent, EntitySearchPickerComponent, NgSelectModule],
  templateUrl: './bib-edit.html'
})
export class BibEditPage implements OnInit, OnDestroy {
  private route       = inject(ActivatedRoute);
  private router      = inject(Router);
  private service     = inject(BibService);
  private bibTypeSvc  = inject(BibTypeService);
  private worksheetSvc = inject(WorkSheetService);
  private wsFieldSvc  = inject(WorksheetFieldService);
  private wsSubSvc    = inject(WorksheetSubfieldService);
  private marcFieldSvc = inject(MarcFieldService);
  private marcSubSvc  = inject(MarcSubFieldService);
  private collectionSvc = inject(EbookCollectionService);
  private storeSvc    = inject(StoreService);
  private barcodeStatusSvc = inject(BarcodeStatusService);
  private linkSvc     = inject(PrintBookAndDigitalService);
  private ebookSvc    = inject(EbookDocumentService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  @ViewChild(EntitySearchPickerComponent) ebookPicker!: EntitySearchPickerComponent<EbookDocument>;

  isNew = signal(true);
  mfn = signal<number | null>(null);
  bibId = signal<number | null>(null);
  // Đợt 17 — Lịch sử thay đổi theo hồ sơ.
  bibPublicId = signal<string | null>(null);
  changeReason = signal('');
  bibTypeId = signal<number | null>(null);
  bibTypes = signal<BibType[]>([]);
  collectionId = signal<number | null>(null);
  collections = signal<{ id: number; label: string }[]>([]);
  fields = signal<MarcField[]>([]);
  items = signal<RegisteredBarcode[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  activeTab = signal<'marc' | 'items' | 'ebook'>('marc');

  // ── Liên kết Tài liệu số ─────────────────────────────────────────────────
  linkedEbook = signal<LinkedEbookInfo | null>(null);
  isLoadingLink = signal(false);
  /** Tài liệu số đã chọn nhưng chưa lưu (biểu ghi đang tạo mới) — sẽ tự động liên kết ngay sau khi lưu thành công. */
  pendingLinkEbook = signal<EbookDocument | null>(null);

  fetchEbookPage = (filters: EntitySearchFilters, page: number, pageSize: number): import('rxjs').Observable<EntityPage<EbookDocument>> =>
    this.ebookSvc.search({ keyword: filters.keyword, pageIndex: page, pageSize }).pipe(map(r => ({ items: r.data, total: r.recordsTotal })));
  ebookDisplayFn = (item: EbookDocument): string => item.itemXml?.title || item.title || `#${item.id}`;
  ebookSubDisplayFn = (item: EbookDocument): string => item.itemXml?.author || '';

  // ── Biên mục từ ảnh bìa (AI) ────────────────────────────────────────────────
  analysisImageFiles = signal<File[]>([]);
  isAnalyzingCover    = signal(false);

  onAnalysisImagesSelect(event: Event): void {
    const files = Array.from((event.target as HTMLInputElement).files ?? []);
    if (files.length) this.analysisImageFiles.set(files);
  }

  analyzeCover(): void {
    const files = this.analysisImageFiles();
    if (!files.length || this.isAnalyzingCover()) return;
    this.isAnalyzingCover.set(true);
    this.service.analyzeBookImage(files).pipe(takeUntil(this.destroy$)).subscribe({
      next: meta => {
        this.isAnalyzingCover.set(false);
        if (!meta) { this.toastr.error(this.translate.instant('BIB_EDIT.ANALYZE_ERROR')); return; }
        this.fields.set(applyBookMetadataToFields(this.fields(), meta));
        this.toastr.success(this.translate.instant('BIB_EDIT.ANALYZE_SUCCESS'));
      },
      error: () => { this.isAnalyzingCover.set(false); this.toastr.error(this.translate.instant('BIB_EDIT.ANALYZE_ERROR')); }
    });
  }

  worksheets  = signal<WorkSheet[]>([]);
  worksheetId = signal<number | null>(null);
  marcFieldDics    = signal<MarcFieldDic[]>([]);
  marcSubFieldDics = signal<MarcSubFieldDic[]>([]);

  showWorksheetConfirm = signal(false);
  pendingWorksheetId   = signal<number | null>(null);

  // ── Đăng ký ĐKCB trực tiếp cho biểu ghi (tab "Items") ──────────────────────
  stores = signal<Store[]>([]);
  barcodeStatuses = signal<{ id: string; label: string }[]>([]);
  kcbTab = signal<'single' | 'batch'>('single');
  isLoadingItems = signal(false);
  isRegisteringKcb = signal(false);
  isRegisteringSingleKcb = signal(false);
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
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.collectionSvc.getTree().pipe(takeUntil(this.destroy$)).subscribe({ next: tree => this.collections.set(this.collectionSvc.flattenForSelect(tree)), error: () => {} });
    this.marcFieldSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcFieldDics.set(l), error: () => {} });
    this.marcSubSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.marcSubFieldDics.set(l), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.barcodeStatusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const arr = list as any[];
        this.barcodeStatuses.set(arr.map(x => ({ id: String(x.id ?? ''), label: x.commentStatus ?? x.name ?? String(x.id ?? '') })));
      },
      error: () => {}
    });
    const param = this.route.snapshot.paramMap.get('mfn');
    if (param && param !== 'new') {
      this.isNew.set(false);
      this.mfn.set(Number(param));
      this.loadBib(Number(param));
    } else {
      this.isNew.set(true);
      this.fields.set(defaultMarcFields());
      this.pendingLinkEbook.set(null);
    }
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadBib(mfn: number): void {
    this.isLoading.set(true);
    this.service.getByMfn(mfn).pipe(takeUntil(this.destroy$)).subscribe({
      next: (bib: Bib | null) => {
        if (bib) {
          this.bibId.set(bib.bibId ?? null);
          this.bibPublicId.set(bib.publicId ?? null);
          this.bibTypeId.set(bib.bibTypeId ?? null);
          this.onBibTypeChange();
          this.collectionId.set(bib.collectionId ?? null);
          this.fields.set(bib.fields?.length ? bib.fields : defaultMarcFields());
          if (bib.bibId != null) { this.loadItems(bib.bibId); this.loadLinkedEbook(bib.bibId); }
        } else { this.fields.set(defaultMarcFields()); }
        this.isLoading.set(false);
      },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.fields.set(defaultMarcFields()); this.isLoading.set(false); }
    });
  }

  loadItems(bibId: number): void {
    this.isLoadingItems.set(true);
    this.service.getItems(bibId).pipe(takeUntil(this.destroy$)).subscribe({
      next: it => { this.items.set(it); this.isLoadingItems.set(false); },
      error: () => { this.isLoadingItems.set(false); }
    });
  }

  // ── Liên kết Tài liệu số ─────────────────────────────────────────────────
  loadLinkedEbook(bibId: number): void {
    this.isLoadingLink.set(true);
    this.linkSvc.getByBibId(bibId).pipe(takeUntil(this.destroy$)).subscribe({
      next: ebook => { this.linkedEbook.set(ebook); this.isLoadingLink.set(false); },
      error: () => { this.isLoadingLink.set(false); }
    });
  }

  openEbookPicker(): void { this.ebookPicker.open(); }

  onEbookPicked(ebook: EbookDocument): void {
    this.copyFieldsFromEbook(ebook);
    const bibId = this.bibId();
    if (!bibId) { this.pendingLinkEbook.set(ebook); return; }
    this.linkSvc.link(bibId, Number(ebook.id)).pipe(takeUntil(this.destroy$)).subscribe(ok => {
      if (ok) { this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.loadLinkedEbook(bibId); }
    });
  }

  clearPendingLinkEbook(): void { this.pendingLinkEbook.set(null); }

  unlinkEbook(): void {
    const bibId = this.bibId(); if (!bibId) return;
    this.linkSvc.unlink({ bibId }).pipe(takeUntil(this.destroy$)).subscribe(ok => {
      if (ok) { this.linkedEbook.set(null); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); }
    });
  }

  /** Chỉ điền field/subfield MARC đang trống — không ghi đè dữ liệu biên mục đã có. */
  private ensureSubfield(tag: string, subcode: string, value: string | null | undefined): void {
    if (!value) return;
    const list = this.fields();
    const field = list.find(f => f.tag === tag);
    if (!field) {
      this.fields.set([...list, { tag, ind1: ' ', ind2: ' ', subFields: [{ code: subcode, value }] }]);
      return;
    }
    const sub = (field.subFields ?? []).find(s => s.code === subcode);
    if (sub) { if (sub.value) return; sub.value = value; }
    else { field.subFields = [...(field.subFields ?? []), { code: subcode, value }]; }
    this.fields.set([...list]);
  }

  /** Điền field/subfield MARC từ dữ liệu của tài liệu số vừa chọn (chỉ điền trường đang trống). */
  private copyFieldsFromEbook(ebook: EbookDocument): void {
    this.ensureSubfield('245', 'a', ebook.itemXml?.title ?? ebook.title ?? undefined);
    this.ensureSubfield('100', 'a', ebook.itemXml?.author ?? undefined);
    this.ensureSubfield('260', 'b', ebook.itemXml?.publisher ?? ebook.publisher ?? undefined);
    this.ensureSubfield('260', 'c', ebook.itemXml?.publishDate ?? (ebook.publishYear ? String(ebook.publishYear) : undefined));
    this.toastr.success(this.translate.instant('BIB_EDIT.COPY_DONE'));
  }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }
  getStatusLabel(code: string | null | undefined): string { if (!code) return '—'; return this.barcodeStatuses().find(s => s.id === code)?.label || code; }

  registerKcb(): void {
    const bibId = this.bibId(); if (!bibId || this.kcbForm.invalid) { this.kcbForm.markAllAsTouched(); return; }
    const v = this.kcbForm.getRawValue();
    this.isRegisteringKcb.set(true);
    this.service.registerBarcodes({ bibId, prefix: v.Prefix.trim(), digitLength: v.DigitLength, quantity: v.Quantity, storeId: v.StoreId!, startNumber: v.StartNumber ?? undefined })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.isRegisteringKcb.set(false); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); this.loadItems(bibId); },
        error: () => { this.isRegisteringKcb.set(false); }
      });
  }

  registerSingleKcb(): void {
    const bibId = this.bibId(); if (!bibId || this.kcbSingleForm.invalid) { this.kcbSingleForm.markAllAsTouched(); return; }
    const v = this.kcbSingleForm.getRawValue();
    this.isRegisteringSingleKcb.set(true);
    this.service.registerSingleBarcode({ bibId, barcodeValue: v.BarcodeValue.trim(), storeId: v.StoreId! })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => {
          this.isRegisteringSingleKcb.set(false);
          if (!res) { return; }
          this.kcbSingleForm.patchValue({ BarcodeValue: '' });
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
          this.loadItems(bibId);
        },
        error: (err: unknown) => {
          this.isRegisteringSingleKcb.set(false);
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          this.toastr.error((err as any)?.error?.message || this.translate.instant('COMMON.SAVE_ERROR'));
        }
      });
  }

  deleteKcbItem(item: RegisteredBarcode): void {
    const bibId = this.bibId(); if (!bibId || !item.id) return;
    this.service.deleteBarcode(item.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadItems(bibId); },
      error: () => { }
    });
  }

  /** Khi đổi loại biểu ghi, nạp danh sách worksheet để người dùng chọn mẫu trường MARC. */
  onBibTypeChange(): void {
    const id = this.bibTypeId();
    this.worksheetId.set(null); this.worksheets.set([]);
    if (id == null) return;
    this.worksheetSvc.search({ bibTypeId: id, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.worksheets.set(res.data); if (res.data.length === 1 && this.isNew()) this.onWorksheetChange(res.data[0].id); },
      error: () => {}
    });
  }

  /** Chọn worksheet ở khung select — khi đang sửa (không phải tạo mới), hỏi xác nhận trước vì sẽ ghi đè bảng MARC hiện có. */
  onWorksheetSelect(id: number | null): void {
    if (!this.isNew()) { this.pendingWorksheetId.set(id); this.showWorksheetConfirm.set(true); return; }
    this.onWorksheetChange(id);
  }
  confirmWorksheetOverwrite(): void { this.showWorksheetConfirm.set(false); this.onWorksheetChange(this.pendingWorksheetId()); }
  cancelWorksheetOverwrite(): void { this.showWorksheetConfirm.set(false); this.pendingWorksheetId.set(null); }

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
        this.fields.set(marcFields);
      },
      error: () => {}
    });
  }

  save(): void {
    this.isSaving.set(true);
    const payload: Partial<Bib> = {
      mfn: this.mfn() ?? undefined,
      bibId: this.bibId() ?? undefined,
      bibTypeId: this.bibTypeId() ?? undefined,
      collectionId: this.collectionId() ?? undefined,
      fields: this.fields(),
    };
    this.service.save(payload, this.changeReason()).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
        const newBibId = res?.bibId ?? res?.data?.bibId ?? null;
        const pendingEbook = this.pendingLinkEbook();
        if (newBibId && pendingEbook) {
          this.linkSvc.link(newBibId, Number(pendingEbook.id)).subscribe();
        }
        const newMfn = res?.mfn ?? res?.data?.mfn ?? this.mfn();
        if (newMfn) this.router.navigate(['/admin/catalog-bibs/edit', newMfn]);
        else this.router.navigate(['/admin/catalog-bibs']);
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  back(): void { this.router.navigate(['/admin/catalog-bibs']); }

  // Đợt 17 — mở trang Lịch sử thay đổi cho biểu ghi đang sửa.
  openHistory(): void {
    const publicId = this.bibPublicId();
    if (!publicId) return;
    this.router.navigate(['/admin/entity-history'], { queryParams: { type: 'PrintBib', id: publicId } });
  }
}
