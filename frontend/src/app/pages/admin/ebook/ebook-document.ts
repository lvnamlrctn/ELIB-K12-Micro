import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, FormArray, AbstractControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, debounceTime, distinctUntilChanged, switchMap, of, map } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { EbookDocumentService, ImportParams, ImportResult, BookMetadataResult } from '../../../services/ebook/ebook-document.service';
import { EbookDocument } from '../../../models/ebook/ebook-document';
import { EbookCollectionService } from '../../../services/ebook/collection.service';
import { EbookSubjectService } from '../../../services/ebook/subject.service';
import { EbookTopicService } from '../../../services/ebook/topic.service';
import { EbookDigTypeService } from '../../../services/ebook/dig-type.service';
import { DicPublisherService } from '../../../services/cataloging/dic-publisher.service';
import { DicAuthorService }    from '../../../services/cataloging/dic-author.service';
import { DicKeywordService }   from '../../../services/cataloging/dic-keyword.service';
import { MetadataFieldService } from '../../../services/ebook/metadata-field.service';
import { MetaDataFieldRegistery } from '../../../models/ebook/metadata-field';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { PrintBookAndDigitalService } from '../../../services/cataloging/print-book-and-digital.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { Bib } from '../../../models/cataloging/bib';
import { LinkedBibInfo } from '../../../models/cataloging/print-book-and-digital';
import { EntitySearchPickerComponent, EntityPage, EntitySearchFilters } from '../../../shared/components/entity-search-picker/entity-search-picker';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';

@Component({
  selector: 'app-ebook-document',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, EntitySearchPickerComponent, DateInputComponent],
  templateUrl: './ebook-document.html'
})
export class EbookDocumentPage implements OnInit, OnDestroy {
  private router       = inject(Router);
  private listState = inject(ListPageStateService);
  private service      = inject(EbookDocumentService);
  private collSvc      = inject(EbookCollectionService);
  private subjSvc      = inject(EbookSubjectService);
  private topicSvc     = inject(EbookTopicService);
  private digTypeSvc   = inject(EbookDigTypeService);
  private dicPublisher  = inject(DicPublisherService);
  private dicAuthor     = inject(DicAuthorService);
  private dicKeyword    = inject(DicKeywordService);
  private metaFieldSvc  = inject(MetadataFieldService);
  private toastr       = inject(ToastrService);
  private auth         = inject(Auth);
  private departmentService = inject(DepartmentService);
  private linkSvc       = inject(PrintBookAndDigitalService);
  private bibSvc        = inject(BibService);
  public  translate    = inject(TranslateService);
  private destroy$     = new Subject<void>();

  @ViewChild(EntitySearchPickerComponent) bibPicker!: EntitySearchPickerComponent<Bib>;

  readonly imageUrl = environment.imageUrl;

  // ── Liên kết Tài liệu in ─────────────────────────────────────────────────
  // Mã trường siêu dữ liệu cố định dùng để dựng lại EbookItemXml (xem EbookItemRepository.UpdateEbookAsync)
  private readonly METAFIELD_TITLE        = 64;
  private readonly METAFIELD_AUTHOR       = 3;
  private readonly METAFIELD_PUBLISHER    = 39;
  private readonly METAFIELD_PUBLISH_DATE = 15;

  currentEbookId  = signal<number | null>(null);
  linkedBib       = signal<LinkedBibInfo | null>(null);
  isLoadingLink   = signal(false);
  /** Tài liệu in đã chọn nhưng chưa lưu (chế độ Thêm mới) — sẽ tự động liên kết ngay sau khi Add thành công. */
  pendingLinkBib  = signal<Bib | null>(null);

  fetchBibPage = (filters: EntitySearchFilters, page: number, pageSize: number): import('rxjs').Observable<EntityPage<Bib>> =>
    this.bibSvc.search({ keyword: filters.keyword, pageIndex: page, pageSize }).pipe(map(r => ({ items: r.data, total: r.recordsTotal })));
  bibDisplayFn = (item: Bib): string => item.title || (item.mfn ? `MFN ${item.mfn}` : `#${item.bibId}`);
  bibSubDisplayFn = (item: Bib): string => item.author || '';

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];

  displayedColumns = this.isPrivileged
    ? ['select', 'stt', 'collection', 'info', 'tenant', 'views', 'files', 'status', 'actions']
    : ['select', 'stt', 'collection', 'info', 'views', 'files', 'status', 'actions'];
  dataSource: EbookDocument[] = [];
  selection = new SelectionModel<EbookDocument>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  // Đợt 17 — Lý do sửa (tuỳ chọn), gửi qua header X-Change-Reason cho Lịch sử thay đổi theo hồ sơ.
  changeReason      = signal('');
  currentId         = signal<string | number | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  isExporting       = signal(false);
  showConfirmDelete = signal(false);
  filtersCollapsed  = signal(false);
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  confirmDeleteId   = signal<string | number | null>(null);

  showImportModal       = signal(false);
  importType            = signal<'excel' | 'xml' | 'dspace'>('excel');
  importFiles           = signal<File[]>([]);
  isImporting           = signal(false);
  importCollectionId    = signal<number | null>(null);
  importSubjectId       = signal<number | null>(null);
  importTopicId         = signal<number | null>(null);
  importIsPublished     = signal(false);
  importIsFree          = signal(false);
  importAllowDownload   = signal(false);
  importResult          = signal<ImportResult | null>(null);

  // Export modal state
  showExportModal     = signal(false);
  exportFields:       any[] = [];
  exportFieldsLoading = signal(false);
  selectedExportKeys  = signal<string[]>([]);

  coverImageFile      = signal<File | null>(null);
  coverImagePreviewUrl = signal<string | null>(null);
  isAnalyzing         = signal<boolean>(false);
  analyzeFiles        = signal<File[]>([]);

  // Tạo ảnh bìa từ trang PDF
  showCoverPageDialog   = signal(false);
  coverPageNumber       = signal(1);
  coverPreviewUrl       = signal<string | null>(null);
  isLoadingCoverPreview = signal(false);
  isSavingCoverPage     = signal(false);

  // Autocomplete state
  publisherSugs    = signal<string[]>([]);
  showPublisherSug = signal(false);
  authorSugs       = signal<string[]>([]);
  activeAuthorIdx  = signal(-1);
  keywordSugs      = signal<string[]>([]);
  activeKeywordIdx = signal(-1);

  private pubSearch$  = new Subject<string>();
  private authSearch$ = new Subject<{ value: string; index: number }>();
  private kwSearch$   = new Subject<{ value: string; index: number }>();

  collections:    { id: number; label: string }[]  = [];
  subjects:       { id: number; label: string }[]  = [];
  topics:         { id: number; label: string }[]  = [];
  digTypes:       { id: number; name: string }[]   = [];
  metaDataFields: MetaDataFieldRegistery[]         = [];

  get activeMetaDataFields(): MetaDataFieldRegistery[] {
    return this.metaDataFields.filter(f => f.status === 2);
  }

  languages = [
    { id: 1,  name: 'Tiếng Việt' },
    { id: 2,  name: 'English' },
    { id: 3,  name: 'Français' },
    { id: 4,  name: 'Deutsch' },
    { id: 5,  name: '中文' },
    { id: 6,  name: '日本語' },
    { id: 7,  name: 'Pусский' },
    { id: 8,  name: 'Español' },
  ];

  shareTypes = [
    { value: 0, label: 'Không chia sẻ' },
    { value: 1, label: 'Chia sẻ công khai' },
    { value: 2, label: 'Chia sẻ có kiểm soát' },
  ];

  months = Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: `Tháng ${i + 1}` }));
  years  = Array.from({ length: new Date().getFullYear() - 1900 + 2 }, (_, i) => String(1900 + i)).reverse();

  searchForm = new FormGroup({
    keyword:         new FormControl(''),
    collectionId:    new FormControl<number | null>(null),
    subjectId:       new FormControl<number | null>(null),
    typeId:          new FormControl<number | null>(null),
    topicId:         new FormControl<number | null>(null),
    author:          new FormControl(''),
    publisher:       new FormControl(''),
    publishDateFrom: new FormControl(''),
    publishDateTo:   new FormControl(''),
    submitedFrom:    new FormControl(''),
    submitedTo:      new FormControl(''),
    status:          new FormControl<number | null>(null),
    tenantId:        new FormControl<string | null>(null),
  });

  docForm = new FormGroup({
    isPublished:   new FormControl(true),
    allowDownload: new FormControl(true),
    isFree:        new FormControl(true),
    subjectId:     new FormControl<number | null>(null),
    topicId:       new FormControl<number | null>(null),
    shareType:     new FormControl<number>(0),
    collectionId:  new FormControl<number | null>(null),
    title:         new FormControl(''),
    publisher:     new FormControl(''),
    publishDay:    new FormControl<number | null>(null),
    publishMonth:  new FormControl<number | null>(null),
    publishYear:   new FormControl<number | null>(null),
    pages:         new FormControl<number | null>(null),
    coverImage:    new FormControl(''),
    docTypeId:     new FormControl<number | null>(null),
    languageId:    new FormControl<number | null>(null),
    journalName:   new FormControl(''),
    abstract:      new FormControl(''),
    description:   new FormControl(''),
    printCopies:   new FormControl<number | null>(null),
    offlineDays:   new FormControl<number | null>(null),
    otherTitles:    new FormArray<FormControl<string>>([]),
    authors:        new FormArray<FormGroup>([]),
    volumes:        new FormArray<FormGroup>([]),
    reportPages:    new FormArray<FormControl<string>>([]),
    advisors:       new FormArray<FormGroup>([]),
    keywords:       new FormArray<FormControl<string>>([]),
    numbers:        new FormArray<FormGroup>([]),
    metaDataEntries: new FormArray<FormGroup>([]),
  });

  get otherTitlesArr():    FormArray<FormControl<string>> { return this.docForm.get('otherTitles')    as FormArray<FormControl<string>>; }
  get authorsArr():        FormArray<FormGroup>           { return this.docForm.get('authors')        as FormArray<FormGroup>; }
  get volumesArr():        FormArray<FormGroup>           { return this.docForm.get('volumes')        as FormArray<FormGroup>; }
  get reportPagesArr():    FormArray<FormControl<string>> { return this.docForm.get('reportPages')    as FormArray<FormControl<string>>; }
  get advisorsArr():       FormArray<FormGroup>           { return this.docForm.get('advisors')       as FormArray<FormGroup>; }
  get keywordsArr():       FormArray<FormControl<string>> { return this.docForm.get('keywords')       as FormArray<FormControl<string>>; }
  get numbersArr():        FormArray<FormGroup>           { return this.docForm.get('numbers')        as FormArray<FormGroup>; }
  get metaDataEntriesArr(): FormArray<FormGroup>          { return this.docForm.get('metaDataEntries') as FormArray<FormGroup>; }

  asControl(c: AbstractControl): FormControl<string> { return c as FormControl<string>; }
  asGroup(c: AbstractControl):   FormGroup           { return c as FormGroup; }

  addOtherTitle()          { this.otherTitlesArr.push(new FormControl('', { nonNullable: true })); }
  removeOtherTitle(i: number) { this.otherTitlesArr.removeAt(i); }

  addAuthor() {
    this.authorsArr.push(new FormGroup({
      lastName:  new FormControl('', { nonNullable: true }),
      firstName: new FormControl('', { nonNullable: true }),
    }));
  }
  removeAuthor(i: number) { this.authorsArr.removeAt(i); }

  addVolume() {
    this.volumesArr.push(new FormGroup({
      type:   new FormControl('', { nonNullable: true }),
      number: new FormControl('', { nonNullable: true }),
    }));
  }
  removeVolume(i: number) { this.volumesArr.removeAt(i); }

  addReportPage()          { this.reportPagesArr.push(new FormControl('', { nonNullable: true })); }
  removeReportPage(i: number) { this.reportPagesArr.removeAt(i); }

  addAdvisor() {
    this.advisorsArr.push(new FormGroup({
      lastName:  new FormControl('', { nonNullable: true }),
      firstName: new FormControl('', { nonNullable: true }),
      title:     new FormControl('', { nonNullable: true }),
    }));
  }
  removeAdvisor(i: number) { this.advisorsArr.removeAt(i); }

  addKeyword()          { this.keywordsArr.push(new FormControl('', { nonNullable: true })); }
  removeKeyword(i: number) { this.keywordsArr.removeAt(i); }

  numberTypes = [
    { value: 'issn',   label: 'ISSN' },
    { value: 'ismn',   label: 'ISMN' },
    { value: 'isbn',   label: 'ISBN' },
    { value: 'uri',    label: 'URI' },
    { value: 'govdoc', label: "Gov't Doc #" },
    { value: 'other',  label: 'Other' },
  ];

  addNumber() {
    this.numbersArr.push(new FormGroup({
      type:  new FormControl('issn', { nonNullable: true }),
      value: new FormControl('',     { nonNullable: true }),
    }));
  }
  removeNumber(i: number) { this.numbersArr.removeAt(i); }

  addMetaData() {
    this.metaDataEntriesArr.push(new FormGroup({
      entryId:         new FormControl(0, { nonNullable: true }),
      metaDataFieldId: new FormControl<number | null>(null),
      value:           new FormControl('', { nonNullable: true }),
      isNew:           new FormControl(true, { nonNullable: true }),
    }));
  }
  removeMetaData(i: number) { this.metaDataEntriesArr.removeAt(i); }

  ngOnInit(): void {
    const saved = this.listState.recall('ebook.ebook-document'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.addAuthor();
    this.addVolume();
    this.addReportPage();
    this.addAdvisor();
    this.addKeyword();

    this.pubSearch$.pipe(
      debounceTime(250), distinctUntilChanged(), takeUntil(this.destroy$),
      switchMap(q => q.trim()
        ? this.dicPublisher.getAll({ search: { value: q }, start: 0, length: 10, draw: 1 })
        : of({ data: [], recordsTotal: 0, recordsFiltered: 0, draw: 1 }))
    ).subscribe(res => this.publisherSugs.set(res.data.map((d: any) => d.name || '')));

    this.authSearch$.pipe(
      debounceTime(250), distinctUntilChanged((a, b) => a.value === b.value && a.index === b.index),
      takeUntil(this.destroy$),
      switchMap(({ value }) => value.trim()
        ? this.dicAuthor.getAll({ search: { value }, start: 0, length: 10, draw: 1 })
        : of({ data: [], recordsTotal: 0, recordsFiltered: 0, draw: 1 }))
    ).subscribe(res => this.authorSugs.set(res.data.map((d: any) => d.name || '')));

    this.kwSearch$.pipe(
      debounceTime(250), distinctUntilChanged((a, b) => a.value === b.value && a.index === b.index),
      takeUntil(this.destroy$),
      switchMap(({ value }) => value.trim()
        ? this.dicKeyword.getAll({ search: { value }, start: 0, length: 10, draw: 1 })
        : of({ data: [], recordsTotal: 0, recordsFiltered: 0, draw: 1 }))
    ).subscribe(res => this.keywordSugs.set(res.data.map((d: any) => d.name || '')));

    forkJoin({
      collections:    this.collSvc.getTree(),
      subjects:       this.subjSvc.getTree(),
      topics:         this.topicSvc.getTree(),
      digTypes:       this.digTypeSvc.search('', 1, 9999),
      metaDataFields: this.metaFieldSvc.getAll(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        this.collections    = this.collSvc.flattenForSelect(r.collections);
        this.subjects       = this.subjSvc.flattenForSelect(r.subjects);
        this.topics         = this.topicSvc.flattenForSelect(r.topics);
        this.digTypes       = r.digTypes.data.map(d => ({ id: d.id, name: d.descriptionVn || d.code }));
        this.metaDataFields = r.metaDataFields;
      }
    });

    // Super-admin: nạp danh sách đơn vị cho combobox lọc.
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }

    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.listState.remember('ebook.ebook-document', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({
      keyword:         s.keyword         || '',
      collectionId:    s.collectionId    ? +s.collectionId  : null,
      subjectId:       s.subjectId       ? +s.subjectId     : null,
      typeId:          s.typeId          ? +s.typeId        : null,
      topicId:         s.topicId         ? +s.topicId       : null,
      author:          s.author          || null,
      publisher:       s.publisher       || null,
      publishDateFrom: s.publishDateFrom || null,
      publishDateTo:   s.publishDateTo   || null,
      submitedFrom:    s.submitedFrom    || null,
      submitedTo:      s.submitedTo      || null,
      status:          s.status          !== null ? +s.status! : null,
      tenantId:        s.tenantId         ?? null,
      pageIndex:       this.pageIndex + 1,
      pageSize:        this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        console.log('[Books] pageIndex sent:', this.pageIndex + 1, '| totalRecords:', res.recordsTotal, '| items:', res.data.length);
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.selection.clear();
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  private clearAllArrays(): void {
    while (this.otherTitlesArr.length)    this.otherTitlesArr.removeAt(0);
    while (this.authorsArr.length)        this.authorsArr.removeAt(0);
    while (this.volumesArr.length)        this.volumesArr.removeAt(0);
    while (this.reportPagesArr.length)    this.reportPagesArr.removeAt(0);
    while (this.advisorsArr.length)       this.advisorsArr.removeAt(0);
    while (this.keywordsArr.length)       this.keywordsArr.removeAt(0);
    while (this.numbersArr.length)        this.numbersArr.removeAt(0);
    while (this.metaDataEntriesArr.length) this.metaDataEntriesArr.removeAt(0);
  }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentId.set(null);
    this.clearAllArrays();
    this.docForm.reset({ isPublished: true, allowDownload: true, isFree: true, shareType: 0 });
    this.addAuthor();
    this.addVolume();
    this.addReportPage();
    this.addAdvisor();
    this.addKeyword();
    this.addNumber();
    this.coverImageFile.set(null);
    this.coverImagePreviewUrl.set(null);
    this.analyzeFiles.set([]);
    this.currentEbookId.set(null);
    this.linkedBib.set(null);
    this.pendingLinkBib.set(null);
    this.autoFilledFromBib = {};
    this.changeReason.set('');
    this.showModal.set(true);
  }

  openEditModal(item: EbookDocument): void {
    this.editMode.set(true);
    this.currentId.set(item.publicId ?? item.id);
    this.linkedBib.set(null);
    this.changeReason.set('');
    this.isLoading.set(true);
    this.service.getById(item.publicId ?? item.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: doc => {
        this.isLoading.set(false);
        this.clearAllArrays();
        this.docForm.patchValue({
          isPublished:   doc.isPublished   === true,
          allowDownload: doc.allowDownload === true,
          isFree:        doc.isFree        === true,
          subjectId:     doc.subjectId     ?? null,
          topicId:       doc.topicId       ?? null,
          shareType:     doc.shareType     ?? 0,
          collectionId:  doc.collectionId  ?? null,
          title:         doc.title         ?? '',
          publisher:     doc.publisher     ?? '',
          publishDay:    doc.publishDay    ?? null,
          publishMonth:  doc.publishMonth  ?? null,
          publishYear:   doc.publishYear   ?? null,
          pages:         doc.pages         ?? null,
          coverImage:    doc.coverImage    ?? '',
          docTypeId:     doc.docTypeId     ?? null,
          languageId:    doc.languageId    ?? null,
          journalName:   doc.journalName   ?? '',
          abstract:      doc.abstract      ?? '',
          description:   doc.description   ?? '',
          printCopies:   doc.printCopies   ?? null,
          offlineDays:   doc.offlineDays   ?? null,
        });
        (doc.otherTitles ?? []).forEach(t =>
          this.otherTitlesArr.push(new FormControl(t, { nonNullable: true })));

        const authors = doc.authors?.length ? doc.authors : [{ lastName: '', firstName: '' }];
        authors.forEach(a => this.authorsArr.push(new FormGroup({
          lastName:  new FormControl(a.lastName  || '', { nonNullable: true }),
          firstName: new FormControl(a.firstName || '', { nonNullable: true }),
        })));

        const volumes = doc.volumes?.length ? doc.volumes : [{ type: '', number: '' }];
        volumes.forEach(v => this.volumesArr.push(new FormGroup({
          type:   new FormControl(v.type   || '', { nonNullable: true }),
          number: new FormControl(v.number || '', { nonNullable: true }),
        })));

        const reportPages = doc.reportPages?.length ? doc.reportPages : [''];
        reportPages.forEach(r => this.reportPagesArr.push(new FormControl(r, { nonNullable: true })));

        const advisors = doc.advisors?.length ? doc.advisors : [{ lastName: '', firstName: '', title: '' }];
        advisors.forEach(a => this.advisorsArr.push(new FormGroup({
          lastName:  new FormControl(a.lastName  || '', { nonNullable: true }),
          firstName: new FormControl(a.firstName || '', { nonNullable: true }),
          title:     new FormControl(a.title     || '', { nonNullable: true }),
        })));

        const keywords = doc.keywords?.length ? doc.keywords : [''];
        keywords.forEach(k => this.keywordsArr.push(new FormControl(k, { nonNullable: true })));

        if (doc.numbers?.length) {
          (doc.numbers as any[]).forEach(n => this.numbersArr.push(new FormGroup({
            type:  new FormControl(n.type  || 'issn', { nonNullable: true }),
            value: new FormControl(n.value || '',     { nonNullable: true }),
          })));
        } else {
          this.addNumber();
        }

        const metaEntries = doc.metaData?.length ? doc.metaData : [];
        metaEntries.forEach(m => {
          const fg = new FormGroup({
            entryId:         new FormControl(m.id ?? 0, { nonNullable: true }),
            metaDataFieldId: new FormControl<number | null>(m.metaDataFieldId),
            value:           new FormControl(m.value || '', { nonNullable: true }),
            isNew:           new FormControl(false, { nonNullable: true }),
          });
          fg.get('metaDataFieldId')?.disable();
          this.metaDataEntriesArr.push(fg);
        });
        if (!metaEntries.length) this.addMetaData();

        this.coverImageFile.set(null);
        this.coverImagePreviewUrl.set(doc.coverImage || null);
        this.analyzeFiles.set([]);

        const numericId = Number(doc.id);
        this.currentEbookId.set(numericId || null);
        if (numericId) this.loadLinkedBib(numericId);

        this.showModal.set(true);
      },
      error: () => {
        this.isLoading.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.coverImageFile.set(null);
    this.coverImagePreviewUrl.set(null);
    this.analyzeFiles.set([]);
    this.currentEbookId.set(null);
    this.linkedBib.set(null);
    this.pendingLinkBib.set(null);
    this.autoFilledFromBib = {};
    this.changeReason.set('');
  }

  // Đợt 17 — mở trang Lịch sử thay đổi cho tài liệu đang sửa.
  openHistory(): void {
    const id = this.currentId();
    if (!id || !this.editMode()) return;
    this.router.navigate(['/admin/entity-history'], { queryParams: { type: 'DigitalDocument', id } });
  }

  onSubmit(): void {
    this.isSaving.set(true);
    const v    = this.docForm.getRawValue();
    const file = this.coverImageFile();
    const id   = this.currentId();
    const mode = this.editMode();

    if (mode && id !== null) {
      // Edit mode: multipart/form-data — backend PUT accepts file upload + metaDataEntriesJson
      const fd = new FormData();
      fd.append('isPublished',   String(v.isPublished   ?? false));
      fd.append('allowDownload', String(v.allowDownload  ?? false));
      fd.append('isFree',        String(v.isFree         ?? false));
      fd.append('shareType',     String(v.shareType      ?? 0));
      if (v.subjectId    != null) fd.append('subjectId',    String(v.subjectId));
      if (v.topicId      != null) fd.append('topicId',      String(v.topicId));
      if (v.collectionId != null) fd.append('collectionId', String(v.collectionId));
      if (v.printCopies  != null) fd.append('printCopies',  String(v.printCopies));
      if (v.offlineDays  != null) fd.append('offlineDays',  String(v.offlineDays));
      if (file)                   fd.append('imageFile',    file);

      const metaEntries = (v.metaDataEntries as any[])
        .map((m: any) => ({ id: m.entryId ?? 0, metaDataFieldId: m.metaDataFieldId, value: m.value, sortOrder: 1 }))
        .filter((m: any) => m.metaDataFieldId && m.value?.trim());
      fd.append('metaDataEntriesJson', JSON.stringify(metaEntries));

      this.service.update(id, fd, this.changeReason()).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        },
        error: () => {
          this.isSaving.set(false);
        }
      });
      return;
    }

    // Add mode: JSON ([FromBody] EbookItemAddRequest) — ảnh bìa gửi base64 data URL ở field Images.
    const fullName = (a: any) => `${a.lastName || ''} ${a.firstName || ''}`.trim();
    const findNumber = (kind: string) =>
      (v.numbers as any[]).find((n: any) => (n.type || '').toLowerCase().includes(kind) && n.value?.trim())?.value || undefined;

    const payload: any = {
      Title:       v.title       || undefined,
      Publisher:   v.publisher   || undefined,
      Abstract:    v.abstract    || undefined,
      Description: v.description || undefined,
      PublishDate: v.publishYear != null ? String(v.publishYear) : undefined,
      CollectionId: v.collectionId ?? undefined,
      SubjectId:    v.subjectId    ?? undefined,
      TopicId:      v.topicId      ?? undefined,
      TypeId:       v.docTypeId    ?? undefined,
      Language:     v.languageId != null ? String(v.languageId) : undefined,
      IsPublished:   v.isPublished   ?? false,
      AllowDownload: v.allowDownload ?? false,
      IsFree:        v.isFree        ?? false,
      Share:         v.shareType     ?? 0,
      PrintCopies:   v.printCopies   ?? undefined,
      OfflineDays:   v.offlineDays   ?? undefined,
      OtherTitles: (v.otherTitles as string[]).map(t => t.trim()).filter(Boolean),
      Authors:  (v.authors  as any[]).filter((a: any) => a.lastName || a.firstName).map(fullName),
      Advisors: (v.advisors as any[]).filter((a: any) => a.lastName || a.firstName).map(fullName),
      Keywords: (v.keywords as string[]).map(k => k.trim()).filter(Boolean),
      Isbn: findNumber('isbn'),
      Issn: findNumber('issn'),
      ExtraMetadata: (v.metaDataEntries as any[])
        .filter((m: any) => m.metaDataFieldId && m.value?.trim())
        .map((m: any) => ({ MetaDataFieldId: m.metaDataFieldId, Value: m.value, SortOrder: 1 })),
    };

    const send = () => this.service.create(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        const pending = this.pendingLinkBib();
        const newId = res?.id;
        if (pending?.bibId != null && newId != null) {
          this.linkSvc.link(pending.bibId, Number(newId)).subscribe();
        }
        this.closeModal();
        this.pageIndex = 0;
        if (this.paginator) this.paginator.pageIndex = 0;
        this.loadData();
        this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });

    if (file) {
      this.fileToDataUrl(file).then(dataUrl => { payload.Images = dataUrl; send(); });
    } else {
      send();
    }
  }

  /** Đọc file ảnh thành data URL (base64) để gửi trong field Images của EbookItem/Add. */
  private fileToDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  }

  togglePublished(item: EbookDocument): void {
    const id = item.publicId ?? item.id;
    this.service.toggleStatus(id).pipe(takeUntil(this.destroy$)).subscribe({
      next:  () => { item.status = item.status === 2 ? 1 : 2; },
      error: () => {}
    });
  }

  handleDelete(item: EbookDocument): void {
    this.confirmDeleteId.set(item.publicId ?? item.id);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeleteId.set(null);
    this.showConfirmDelete.set(true);
  }

  showMoveCollection      = signal(false);
  moveTargetCollectionId  = signal<number | null>(null);
  isMoving                = signal(false);

  openMoveCollection(): void {
    if (!this.selection.selected.length) return;
    this.moveTargetCollectionId.set(null);
    this.showMoveCollection.set(true);
  }

  closeMoveCollection(): void {
    this.showMoveCollection.set(false);
    this.moveTargetCollectionId.set(null);
  }

  confirmMoveCollection(): void {
    const cid = this.moveTargetCollectionId();
    if (!cid || !this.selection.selected.length) return;
    this.isMoving.set(true);
    const publicIds = this.selection.selected.map(i => String(i.publicId ?? i.id));
    this.service.bulkMoveCollection(publicIds, cid).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isMoving.set(false);
        this.closeMoveCollection();
        this.selection.clear();
        this.loadData();
        this.toastr.success(this.translate.instant('EBOOK.MOVE_SUCCESS'));
      },
      error: (err: any) => {
        this.isMoving.set(false);
        this.toastr.error(err?.error?.message || this.translate.instant('EBOOK.MOVE_ERROR'));
      }
    });
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute(): void {
    const id = this.confirmDeleteId();
    if (id === null) {
      const reqs = this.selection.selected.map(item => this.service.delete(item.publicId ?? item.id));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.closeConfirm();
          this.loadData();
        },
        error: () => {
          this.closeConfirm();
          this.loadData();
        }
      });
      return;
    }
    this.service.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        this.closeConfirm();
        this.loadData();
      },
      error: () => {
        this.closeConfirm();
      }
    });
  }

  getTitleDisplay(item: EbookDocument): string {
    return item.itemXml?.title ?? item.title ?? '—';
  }

  getAuthorDisplay(item: EbookDocument): string {
    if (item.itemXml?.author) return item.itemXml.author;
    if (!item.authors?.length) return '';
    return item.authors.map(a => [a.lastName, a.firstName].filter(Boolean).join(' ')).join('; ');
  }

  getPublishYearDisplay(item: EbookDocument): string | null {
    if (item.itemXml?.publishDate) return item.itemXml.publishDate;
    if (item.publishYear) return String(item.publishYear);
    return null;
  }

  getPublisherDisplay(item: EbookDocument): string | null {
    return item.itemXml?.publisher ?? item.publisher ?? null;
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  // ===== LIÊN KẾT TÀI LIỆU IN =====
  loadLinkedBib(ebookId: number): void {
    this.isLoadingLink.set(true);
    this.linkSvc.getByEbookId(ebookId).pipe(takeUntil(this.destroy$)).subscribe({
      next: bib => { this.linkedBib.set(bib); this.isLoadingLink.set(false); },
      error: () => { this.isLoadingLink.set(false); }
    });
  }

  openBibPicker(): void { this.bibPicker.open(); }

  onBibPicked(bib: Bib): void {
    if (this.editMode()) this.copyBibIntoMetaEntries(bib);
    else this.copyBibIntoAddForm(bib);

    const ebookId = this.currentEbookId();
    if (!ebookId) { this.pendingLinkBib.set(bib); return; }
    if (bib.bibId == null) return;
    this.linkSvc.link(bib.bibId, ebookId).pipe(takeUntil(this.destroy$)).subscribe(ok => {
      if (ok) { this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS')); this.loadLinkedBib(ebookId); }
    });
  }

  clearPendingLinkBib(): void { this.pendingLinkBib.set(null); this.undoAutoFillFromBib(); }

  /** Giá trị do lần liên kết tài liệu in hiện tại tự điền vào form Thêm mới (port ELIB-LRC 09-22) — để gỡ liên
   *  kết thì xoá đúng các trường này, lần liên kết sau điền lại theo tài liệu mới. Trường người dùng tự gõ
   *  (trước hoặc sau khi liên kết) không bị đụng tới. */
  private autoFilledFromBib: { title?: string; publisher?: string; publishYear?: number; authors?: string } = {};

  private authorsSignature(): string {
    return this.authorsArr.controls.map(g => `${g.get('lastName')?.value ?? ''}|${g.get('firstName')?.value ?? ''}`).join(';');
  }

  private undoAutoFillFromBib(): void {
    const a = this.autoFilledFromBib, c = this.docForm.controls;
    if (a.title !== undefined && c.title.value === a.title) c.title.setValue('');
    if (a.publisher !== undefined && c.publisher.value === a.publisher) c.publisher.setValue('');
    if (a.publishYear !== undefined && c.publishYear.value === a.publishYear) c.publishYear.setValue(null);
    if (a.authors !== undefined && this.authorsSignature() === a.authors) { this.authorsArr.clear(); this.addAuthor(); }
    this.autoFilledFromBib = {};
  }

  unlinkBib(): void {
    const ebookId = this.currentEbookId(); if (!ebookId) return;
    this.linkSvc.unlink({ ebookId }).pipe(takeUntil(this.destroy$)).subscribe(ok => {
      if (ok) { this.linkedBib.set(null); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); }
    });
  }

  /** Điền vào 1 mục siêu dữ liệu (theo mã trường cố định) chỉ khi chưa có dữ liệu — không ghi đè. */
  private ensureMetaEntry(fieldId: number, value: string | null | undefined): void {
    if (!value) return;
    const existing = this.metaDataEntriesArr.controls.find(g => g.get('metaDataFieldId')?.value === fieldId);
    if (existing) {
      if (existing.get('value')?.value) return;
      existing.get('value')?.setValue(value);
      return;
    }
    this.metaDataEntriesArr.push(new FormGroup({
      entryId:         new FormControl(0, { nonNullable: true }),
      metaDataFieldId: new FormControl<number | null>(fieldId),
      value:           new FormControl(value, { nonNullable: true }),
      isNew:           new FormControl(true, { nonNullable: true }),
    }));
  }

  /** Điền các mục siêu dữ liệu Title/Author/Publisher/PublishDate từ tài liệu in vừa chọn (chỉ điền mục đang trống — người dùng tự bấm Lưu sau đó). */
  private copyBibIntoMetaEntries(bib: Bib | LinkedBibInfo): void {
    this.ensureMetaEntry(this.METAFIELD_TITLE,        bib.title);
    this.ensureMetaEntry(this.METAFIELD_AUTHOR,       bib.author);
    this.ensureMetaEntry(this.METAFIELD_PUBLISHER,    bib.publisher);
    this.ensureMetaEntry(this.METAFIELD_PUBLISH_DATE, bib.publishDate);
    this.toastr.success(this.translate.instant('EBOOK_DOC.COPY_FROM_BIB_DONE'));
  }

  /** Điền form Thêm mới (title/publisher/publishYear/authors) từ tài liệu in vừa chọn — chỉ điền trường đang trống. */
  private copyBibIntoAddForm(bib: Bib): void {
    // Chọn tài liệu in khác khi chưa gỡ liên kết cũ → xoá phần đã tự điền của lần trước rồi điền lại.
    this.undoAutoFillFromBib();
    const c = this.docForm.controls, filled = this.autoFilledFromBib;
    if (!c.title.value && bib.title) { c.title.setValue(bib.title); filled.title = bib.title; }
    if (!c.publisher.value && bib.publisher) { c.publisher.setValue(bib.publisher); filled.publisher = bib.publisher; }
    if (!c.publishYear.value && bib.publishDate) {
      const y = parseInt(bib.publishDate, 10);
      if (!isNaN(y)) { c.publishYear.setValue(y); filled.publishYear = y; }
    }
    const hasAuthor = this.authorsArr.controls.some(g => g.get('lastName')?.value || g.get('firstName')?.value);
    if (!hasAuthor && bib.author) {
      const names = bib.author.split(';').map(s => s.trim()).filter(Boolean);
      if (names.length) {
        this.authorsArr.clear();
        names.forEach(name => {
          const idx = name.indexOf(' ');
          const lastName  = idx === -1 ? name : name.slice(0, idx);
          const firstName = idx === -1 ? ''   : name.slice(idx + 1);
          this.authorsArr.push(new FormGroup({
            lastName:  new FormControl(lastName,  { nonNullable: true }),
            firstName: new FormControl(firstName, { nonNullable: true }),
          }));
        });
        filled.authors = this.authorsSignature();
      }
    }
    this.toastr.success(this.translate.instant('EBOOK_DOC.COPY_FROM_BIB_DONE'));
  }

  // ===== AUTOCOMPLETE =====
  onPublisherInput(v: string): void  { this.pubSearch$.next(v); this.showPublisherSug.set(true); }
  selectPublisher(name: string): void { this.docForm.get('publisher')?.setValue(name); this.showPublisherSug.set(false); }
  blurPublisher(): void              { setTimeout(() => this.showPublisherSug.set(false), 150); }

  onAuthorLastInput(v: string, i: number): void {
    this.activeAuthorIdx.set(i);
    this.authSearch$.next({ value: v, index: i });
  }
  selectAuthor(name: string, i: number): void {
    (this.authorsArr.at(i) as any).get('lastName')?.setValue(name);
    this.authorSugs.set([]);
    this.activeAuthorIdx.set(-1);
  }
  blurAuthor(): void { setTimeout(() => { this.authorSugs.set([]); this.activeAuthorIdx.set(-1); }, 150); }

  onKeywordInput(v: string, i: number): void {
    this.activeKeywordIdx.set(i);
    this.kwSearch$.next({ value: v, index: i });
  }
  selectKeyword(name: string, i: number): void {
    (this.keywordsArr.at(i) as FormControl).setValue(name);
    this.keywordSugs.set([]);
    this.activeKeywordIdx.set(-1);
  }
  blurKeyword(): void { setTimeout(() => { this.keywordSugs.set([]); this.activeKeywordIdx.set(-1); }, 150); }

  onCoverImageSelect(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.coverImageFile.set(file);
    if (file) {
      const reader = new FileReader();
      reader.onload = e => this.coverImagePreviewUrl.set(e.target?.result as string);
      reader.readAsDataURL(file);
    }
    (event.target as HTMLInputElement).value = '';
  }

  // ===== TẠO ẢNH BÌA TỪ TRANG PDF =====
  openCoverPageDialogForItem(item: EbookDocument): void {
    this.currentId.set(item.publicId ?? item.id);
    this.openCoverPageDialog();
  }

  openCoverPageDialog(): void {
    if (!this.currentId()) return;
    this.coverPageNumber.set(1);
    this.showCoverPageDialog.set(true);
    this.loadCoverPreview(1);
  }

  private loadCoverPreview(page: number): void {
    const id = this.currentId();
    if (!id) return;
    this.isLoadingCoverPreview.set(true);
    this.service.getCoverPreview(String(id), page).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.isLoadingCoverPreview.set(false);
        const prevUrl = this.coverPreviewUrl();
        this.coverPreviewUrl.set(URL.createObjectURL(blob));
        if (prevUrl) URL.revokeObjectURL(prevUrl);
      },
      error: () => {
        this.isLoadingCoverPreview.set(false);
        this.toastr.error(this.translate.instant('EBOOK_DOC.COVER_PAGE_LOAD_ERROR'));
      }
    });
  }

  changeCoverPage(delta: number): void {
    this.setCoverPageNumber(this.coverPageNumber() + delta);
  }

  setCoverPageNumber(value: number): void {
    const n = Math.max(1, Math.floor(value) || 1);
    this.coverPageNumber.set(n);
    this.loadCoverPreview(n);
  }

  closeCoverPageDialog(): void {
    this.showCoverPageDialog.set(false);
    const prevUrl = this.coverPreviewUrl();
    if (prevUrl) URL.revokeObjectURL(prevUrl);
    this.coverPreviewUrl.set(null);
  }

  confirmCoverFromPage(): void {
    const id = this.currentId();
    if (!id) return;
    this.isSavingCoverPage.set(true);
    this.service.setCoverFromPage(String(id), this.coverPageNumber()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isSavingCoverPage.set(false);
        this.coverImagePreviewUrl.set(res.images);
        this.coverImageFile.set(null);
        this.toastr.success(this.translate.instant('EBOOK_DOC.COVER_PAGE_OK'));
        this.closeCoverPageDialog();
        this.loadData();
      },
      error: () => {
        this.isSavingCoverPage.set(false);
        this.toastr.error(this.translate.instant('EBOOK_DOC.COVER_PAGE_ERROR'));
      }
    });
  }

  // ===== BIÊN MỤC THEO ẢNH BÌA (AI) =====
  /** Chọn nhiều ảnh riêng để gửi AI biên mục (bìa trước/sau, trang tên, bản quyền...). */
  onAnalyzeFilesSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = input.files ? Array.from(input.files) : [];
    if (files.length) this.analyzeFiles.set(files);
    input.value = '';
  }
  clearAnalyzeFiles(): void { this.analyzeFiles.set([]); }

  analyzeCover(): void {
    const files = this.analyzeFiles().length
      ? this.analyzeFiles()
      : (this.coverImageFile() ? [this.coverImageFile()!] : []);
    if (!files.length) return;
    this.isAnalyzing.set(true);
    this.service.analyzeBookImage(files).pipe(takeUntil(this.destroy$)).subscribe({
      next: meta => {
        this.isAnalyzing.set(false);
        if (!meta) { this.toastr.error(this.translate.instant('EBOOK_DOC.ANALYZE_ERROR')); return; }
        this.applyBookMetadata(meta);
        this.toastr.success(this.translate.instant('EBOOK_DOC.ANALYZE_SUCCESS'));
      },
      error: () => {
        this.isAnalyzing.set(false);
        this.toastr.error(this.translate.instant('EBOOK_DOC.ANALYZE_ERROR'));
      }
    });
  }

  private applyBookMetadata(meta: BookMetadataResult): void {
    if (meta.title)       this.docForm.patchValue({ title: meta.title });
    if (meta.publisher)   this.docForm.patchValue({ publisher: meta.publisher });
    if (meta.description) this.docForm.patchValue({ abstract: meta.description });
    if (meta.publishYear) {
      const y = parseInt(meta.publishYear, 10);
      if (!isNaN(y)) this.docForm.patchValue({ publishYear: y });
    }
    // Tác giả: tách theo dấu phẩy; quy ước VN: từ đầu = họ, phần còn lại = tên
    if (meta.author) {
      const names = meta.author.split(',').map(s => s.trim()).filter(Boolean);
      if (names.length) {
        this.authorsArr.clear();
        names.forEach(name => {
          const parts = name.split(/\s+/);
          const lastName = parts.shift() || '';
          this.authorsArr.push(new FormGroup({
            lastName:  new FormControl(lastName,        { nonNullable: true }),
            firstName: new FormControl(parts.join(' '), { nonNullable: true }),
          }));
        });
      }
    }
    // ISBN: thêm 1 dòng số định danh loại 'isbn'
    if (meta.isbn) {
      this.numbersArr.push(new FormGroup({
        type:  new FormControl('isbn',    { nonNullable: true }),
        value: new FormControl(meta.isbn, { nonNullable: true }),
      }));
    }
    // language (mã "vi"...) là FK languageId → không tự set; xem docs/ANALYZE_BOOK_IMAGE.md
  }

  // ===== EXPORT / IMPORT =====
  private buildSearchPayload() {
    const s = this.searchForm.getRawValue();
    return {
      keyword:         s.keyword         || '',
      collectionId:    s.collectionId    ? +s.collectionId  : null,
      subjectId:       s.subjectId       ? +s.subjectId     : null,
      typeId:          s.typeId          ? +s.typeId        : null,
      topicId:         s.topicId         ? +s.topicId       : null,
      author:          s.author          || null,
      publisher:       s.publisher       || null,
      publishDateFrom: s.publishDateFrom || null,
      publishDateTo:   s.publishDateTo   || null,
      submitedFrom:    s.submitedFrom    || null,
      submitedTo:      s.submitedTo      || null,
      status:          s.status !== null  ? +s.status! : null,
    };
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  openExportModal(): void {
    this.showExportModal.set(true);
    this.exportFieldsLoading.set(true);
    this.exportFields = [];
    this.selectedExportKeys.set([]);
    this.service.getExportFields().pipe(takeUntil(this.destroy$)).subscribe({
      next: fields => {
        this.exportFields = fields;
        this.selectedExportKeys.set(fields.map((f: any) => f.code ?? f.key ?? f.name ?? '').filter(Boolean));
        this.exportFieldsLoading.set(false);
      },
      error: () => this.exportFieldsLoading.set(false)
    });
  }

  closeExportModal(): void { this.showExportModal.set(false); }

  getFieldKey(f: any): string  { return f.code ?? f.key ?? f.name ?? ''; }
  getFieldLabel(f: any): string { return f.name ?? f.label ?? f.displayName ?? f.key ?? ''; }

  isExportFieldSelected(f: any): boolean {
    return this.selectedExportKeys().includes(this.getFieldKey(f));
  }

  toggleExportField(f: any): void {
    const key     = this.getFieldKey(f);
    const current = this.selectedExportKeys();
    this.selectedExportKeys.set(
      current.includes(key) ? current.filter(k => k !== key) : [...current, key]
    );
  }

  isAllExportSelected(): boolean {
    return this.exportFields.length > 0 && this.selectedExportKeys().length === this.exportFields.length;
  }

  toggleSelectAllExport(): void {
    const allKeys = this.exportFields.map((f: any) => this.getFieldKey(f)).filter(Boolean);
    this.selectedExportKeys.set(this.isAllExportSelected() ? [] : allKeys);
  }

  confirmExport(): void {
    this.service.exportExcel({ ...this.buildSearchPayload(), fields: this.selectedExportKeys() })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: blob => {
          this.downloadBlob(blob, `ebook_export_${Date.now()}.xlsx`);
          this.showExportModal.set(false);
        },
        error: () => this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'))
      });
  }

  exportMetadataFile(): void {
    if (this.isExporting()) return;
    this.isExporting.set(true);
    this.service.exportMetadataFile(this.buildSearchPayload()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.downloadBlob(blob, `ebook_metadata_file_${Date.now()}.zip`);
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  exportMetadata(): void {
    if (this.isExporting()) return;
    this.isExporting.set(true);
    this.service.exportMetadata(this.buildSearchPayload()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.downloadBlob(blob, `ebook_metadata_${Date.now()}.zip`);
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  openImportModal(): void {
    this.importFiles.set([]);
    this.importType.set('dspace');
    this.importCollectionId.set(null);
    this.importSubjectId.set(null);
    this.importTopicId.set(null);
    this.importIsPublished.set(false);
    this.importIsFree.set(false);
    this.importAllowDownload.set(false);
    this.importResult.set(null);
    this.showImportModal.set(true);
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
    this.importFiles.set([]);
    this.importResult.set(null);
  }

  onImportFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = input.files;
    if (files && files.length) {
      this.importFiles.set(Array.from(files));
    }
    input.value = '';
  }

  confirmImport(): void {
    const files = this.importFiles();
    if (!files.length) return;
    this.isImporting.set(true);
    this.importResult.set(null);

    const params: ImportParams = {
      collectionId:  this.importCollectionId(),
      subjectId:     this.importSubjectId(),
      topicId:       this.importTopicId(),
      status:        this.importIsPublished()   ? 2 : 1,
      isFree:        this.importIsFree()        ? 2 : 1,
      allowDownload: this.importAllowDownload() ? 2 : 1,
    };

    const type = this.importType();
    const obs$ = type === 'xml'    ? this.service.importXml(files, params)
               : type === 'dspace' ? this.service.importDSpace(files[0], params)
               :                     this.service.importExcel(files[0], params);

    obs$.pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isImporting.set(false);
        this.importResult.set(result);
        if ((result.imported ?? 0) > 0) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
          this.loadData();
        }
      },
      error: () => {
        this.isImporting.set(false);
      }
    });
  }

  openFiles(item: EbookDocument): void {
    const publicId = item.publicId ?? String(item.id);
    this.router.navigate(['/admin/ebook-files', publicId], {
      state: { ebookTitle: this.getTitleDisplay(item), ebookId: item.id }
    });
  }

  openLoans(item: EbookDocument): void {
    const publicId = item.publicId ?? String(item.id);
    this.router.navigate(['/admin/ebook-loans', publicId], {
      state: { ebookTitle: this.getTitleDisplay(item), ebookId: item.id }
    });
  }
}
