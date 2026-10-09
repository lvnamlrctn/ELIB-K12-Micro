import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { firstValueFrom, Subject, takeUntil, map, Observable } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { ToastrService } from '../../../services/shared/toastr.service';
import { TaiLieuService, TaiLieu } from '../../../services/evaluate/tai-lieu.service';
import { MonHocService, MonHoc } from '../../../services/evaluate/mon-hoc.service';
import { BibService } from '../../../services/cataloging/bib.service';
import { Bib } from '../../../models/cataloging/bib';
import { EbookDocumentService } from '../../../services/ebook/ebook-document.service';
import { EbookDocument } from '../../../models/ebook/ebook-document';
import { PrintBookAndDigitalService } from '../../../services/cataloging/print-book-and-digital.service';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { EntitySearchPickerComponent, EntityPage, EntitySearchFilters, AdvancedFilterField } from '../../../shared/components/entity-search-picker/entity-search-picker';

@Component({
  selector: 'app-tai-lieu',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CanDirective, EntitySearchPickerComponent, NgSelectModule],
  templateUrl: './tai-lieu.html'
})
export class TaiLieuPage implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(TaiLieuService);
  private subjectService = inject(MonHocService);
  private bibService = inject(BibService);
  private ebookService = inject(EbookDocumentService);
  private linkService = inject(PrintBookAndDigitalService);
  public translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private destroy$ = new Subject<void>();

  monHocId = this.route.snapshot.paramMap.get('monHocId') || '';
  subject = signal<MonHoc | null>(null);
  /** Id số nguyên thật của môn học (khác `monHocId` — route param là publicId GUID) — dùng làm khóa ngoại khi tạo/tìm Tài liệu. */
  monHocNumericId = signal<number | null>(null);

  showModal = signal<boolean>(false);
  showConfirmDelete = signal<boolean>(false);
  editMode = signal<boolean>(false);
  itemToDelete = signal<string | null>(null);
  currentId = signal<string | null>(null);
  isLoading = signal<boolean>(false);

  dataForm = new FormGroup({
    title: new FormControl('', [Validators.required]),
    author: new FormControl(''),
    publisher: new FormControl(''),
    publishDate: new FormControl(''),
    lanXuatBan: new FormControl(''),
    loaiTaiLieu: new FormControl<number>(1),
    url: new FormControl(''),
    note: new FormControl(''),
    bibId: new FormControl<number | null>(null),
    ebookId: new FormControl<number | null>(null)
  });
  /** Nhãn hiển thị cho sách in/tài liệu số đang chọn trong form — chỉ có giá trị đầy đủ (tên) ngay sau khi chọn qua picker hoặc tự điền chéo từ liên kết có sẵn; khi mở sửa 1 dòng cũ chỉ hiển thị mã số (không có API tra tên theo bibId/ebookId đáng tin cậy). */
  pickedBibLabel = signal<string>('');
  pickedEbookLabel = signal<string>('');

  @ViewChild('bibPicker') bibPicker!: EntitySearchPickerComponent<Bib>;
  @ViewChild('ebookPicker') ebookPicker!: EntitySearchPickerComponent<EbookDocument>;

  fetchBibPage = (filters: EntitySearchFilters, page: number, pageSize: number): Observable<EntityPage<Bib>> =>
    this.bibService.search({
      title: filters.title, author: filters.author, publisher: filters.publisher,
      publishYear: filters.publishDate, mfnFrom: filters.mfnFrom, mfnTo: filters.mfnTo,
      pageIndex: page, pageSize
    }).pipe(map(r => ({ items: r.data, total: r.recordsTotal })));
  bibDisplayFn = (item: Bib): string => item.title || `#${item.bibId}`;
  bibSubDisplayFn = (item: Bib): string => item.author || '';
  bibAdvancedFields: AdvancedFilterField[] = ['title', 'author', 'publisher', 'publishDate', 'mfnFrom', 'mfnTo'];
  bibExtraDisplayFn = (item: Bib): string => item.publishDate ? `${this.translate.instant('TAI_LIEU.PUBLISH_YEAR_PREFIX')} ${item.publishDate}` : '';

  fetchEbookPage = (filters: EntitySearchFilters, page: number, pageSize: number): Observable<EntityPage<EbookDocument>> =>
    this.ebookService.search({
      title: filters.title, author: filters.author, publisher: filters.publisher,
      publishDateFrom: filters.publishDate, publishDateTo: filters.publishDate,
      pageIndex: page, pageSize
    }).pipe(map(r => ({ items: r.data, total: r.recordsTotal })));
  ebookDisplayFn = (item: EbookDocument): string => item.itemXml?.title || item.title || `#${item.id}`;
  ebookSubDisplayFn = (item: EbookDocument): string => item.itemXml?.author || '';
  ebookAdvancedFields: AdvancedFilterField[] = ['title', 'author', 'publisher', 'publishDate'];
  ebookExtraDisplayFn = (item: EbookDocument): string => item.itemXml?.publishDate ? `${this.translate.instant('TAI_LIEU.PUBLISH_YEAR_PREFIX')} ${item.itemXml.publishDate}` : '';

  displayedColumns: string[] = ['loaiTaiLieu', 'title', 'author', 'publisher', 'publishDate', 'linked', 'actions'];
  dataSource = new MatTableDataSource<TaiLieu>([]);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    if (!this.monHocId) { this.router.navigate(['/admin/subjects']); return; }
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.loadData());
    this.subjectService.getById(this.monHocId).pipe(takeUntil(this.destroy$)).subscribe(s => {
      this.subject.set(s);
      this.monHocNumericId.set(Number(s.id));
      this.loadData();
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.drawCount++;
    const params: DataTableParams = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      order: []
    };

    this.isLoading.set(true);
    this.service.getAll(params, this.monHocNumericId() ?? 0).pipe(takeUntil(this.destroy$)).subscribe({
      next: (response) => {
        this.dataSource.data = response.data;
        this.totalRecords = response.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error fetching data', err);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  triggerSearch() {
    this.searchTerm = (document.getElementById('searchTaiLieu') as HTMLInputElement)?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.pickedBibLabel.set('');
    this.pickedEbookLabel.set('');
    this.dataForm.reset({ loaiTaiLieu: 1 });
    this.showModal.set(true);
  }

  async openEditModal(id: string) {
    try {
      const item = await firstValueFrom(this.service.getById(id));
      this.currentId.set(item.publicId || item.id || id);
      this.dataForm.patchValue({
        title: item.title,
        author: item.author,
        publisher: item.publisher,
        publishDate: item.publishDate,
        lanXuatBan: item.lanXuatBan,
        loaiTaiLieu: item.loaiTaiLieu ?? 1,
        url: item.url,
        note: item.note,
        bibId: item.bibId ?? null,
        ebookId: item.ebookId ?? null
      });
      this.pickedBibLabel.set(item.bibId ? `#${item.bibId}` : '');
      this.pickedEbookLabel.set(item.ebookId ? `#${item.ebookId}` : '');
      this.editMode.set(true);
      this.showModal.set(true);
    } catch (error) {
      console.error('Error fetching data details', error);
      this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    }
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset({ loaiTaiLieu: 1 });
  }

  openBibPicker() { this.bibPicker.open(); }
  openEbookPicker() { this.ebookPicker.open(); }

  onBibPicked(bib: Bib) {
    this.dataForm.patchValue({ bibId: bib.bibId, title: bib.title, author: bib.author, publisher: bib.publisher, publishDate: bib.publishDate });
    this.pickedBibLabel.set(bib.title || `#${bib.bibId}`);
    // Nếu sách in này đã có sẵn liên kết 1-1 với 1 tài liệu số (PrintBookAndDigital) → tự điền luôn ebookId.
    this.linkService.getByBibId(bib.bibId).subscribe(linked => {
      if (linked) {
        this.dataForm.patchValue({ ebookId: linked.id });
        this.pickedEbookLabel.set(linked.title || `#${linked.id}`);
      }
    });
  }

  onEbookPicked(ebook: EbookDocument) {
    const ebookId = Number(ebook.id);
    const title = ebook.itemXml?.title || ebook.title || '';
    this.dataForm.patchValue({ ebookId, title, author: ebook.itemXml?.author || '', publisher: ebook.itemXml?.publisher || '', publishDate: ebook.itemXml?.publishDate || '' });
    this.pickedEbookLabel.set(title || `#${ebookId}`);
    // Nếu tài liệu số này đã có sẵn liên kết 1-1 với 1 sách in (PrintBookAndDigital) → tự điền luôn bibId.
    this.linkService.getByEbookId(ebookId).subscribe(linked => {
      if (linked) {
        this.dataForm.patchValue({ bibId: linked.bibid });
        this.pickedBibLabel.set(linked.title || `#${linked.bibid}`);
      }
    });
  }

  /** Gỡ liên kết tài liệu — xóa cả sách in lẫn tài liệu số cùng lúc vì cả 2 đại diện cho cùng 1 nhan đề đang chọn; xóa luôn thông tin đã tự điền để cán bộ chọn lại từ đầu. */
  clearBib() { this.resetLinkedDocument(); }
  clearEbook() { this.resetLinkedDocument(); }

  private resetLinkedDocument() {
    this.dataForm.patchValue({ bibId: null, ebookId: null, title: '', author: '', publisher: '', publishDate: '' });
    this.pickedBibLabel.set('');
    this.pickedEbookLabel.set('');
  }

  onSubmit() {
    if (this.dataForm.invalid) return;

    const v = this.dataForm.value;
    const payload: Partial<TaiLieu> = {
      title: v.title || '',
      author: v.author || '',
      publisher: v.publisher || '',
      publishDate: v.publishDate || '',
      lanXuatBan: v.lanXuatBan || '',
      loaiTaiLieu: v.loaiTaiLieu ?? 1,
      url: v.url || '',
      note: v.note || '',
      bibId: v.bibId ?? null,
      ebookId: v.ebookId ?? null,
      monHocId: this.monHocNumericId()
    };

    if (this.editMode() && this.currentId()) {
      this.service.update(this.currentId()!, payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        },
        error: (err) => { console.error('Update failed', err); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        },
        error: (err) => { console.error('Create failed', err); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
      });
    }
  }

  confirmDelete(id: string) {
    this.itemToDelete.set(id);
    this.showConfirmDelete.set(true);
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.itemToDelete.set(null);
  }

  confirmActionExecute() {
    const id = this.itemToDelete();
    if (!id) return;
    this.service.delete(id).subscribe({
      next: () => {
        this.closeConfirm();
        this.loadData();
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
      },
      error: (err) => { console.error('Delete failed', err); this.closeConfirm(); }
    });
  }

  goBack() {
    this.router.navigate(['/admin/subjects']);
  }
}
