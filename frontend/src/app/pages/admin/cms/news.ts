import { Router } from '@angular/router';
import { Component, inject, OnInit, OnDestroy, signal, ViewChild, PLATFORM_ID } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { CKEditorModule } from '@ckeditor/ckeditor5-angular';
import { ensureCkeditorStyles } from '../../../services/shared/ckeditor-styles';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NewsService } from '../../../services/cms/news';
import { NewsArticle } from '../../../models/cms/news';
import { CategoryService } from '../../../services/cms/category.service';
import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { resolveMediaUrl } from '../../../shared/utils/media-url';
import { MediaLibraryPickerComponent } from '../../../shared/components/media-library-picker/media-library-picker';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

const MEDIA_LIBRARY_ICON = '<svg viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path d="M2 4a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V4zm2 0v10h12V4H4zm2 8l2.5-3 2 2L14 7l3 5H6z"/></svg>';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
class CKEditorUploadAdapter {
  private xhr: XMLHttpRequest | null = null;

  constructor(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    private loader: any,
    private uploadUrl: string,
    private getToken: () => string | null
  ) {}

  upload(): Promise<{ default: string }> {
    return this.loader.file.then((file: File): Promise<{ default: string }> =>
      new Promise((resolve, reject) => {
        const ALLOWED = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
        const MAX_BYTES = 5 * 1024 * 1024;

        if (!ALLOWED.includes(file.type)) {
          reject(new Error('Chỉ chấp nhận ảnh JPEG, PNG, GIF hoặc WebP.'));
          return;
        }
        if (file.size > MAX_BYTES) {
          reject(new Error('Kích thước ảnh không được vượt quá 5MB.'));
          return;
        }

        const data = new FormData();
        data.append('file', file);

        this.xhr = new XMLHttpRequest();
        this.xhr.open('POST', this.uploadUrl, true);

        const token = this.getToken();
        if (token) this.xhr.setRequestHeader('Authorization', `Bearer ${token}`);

        this.xhr.addEventListener('load', () => {
          const { status, responseText } = this.xhr!;
          if (status < 200 || status >= 300) {
            reject(new Error(
              status === 401 || status === 403
                ? 'Không có quyền upload ảnh.'
                : `Upload thất bại (HTTP ${status}).`
            ));
            return;
          }
          try {
            const res = JSON.parse(responseText);
            const raw = res?.data?.url ?? res?.data?.path ?? res?.url ?? res?.path ?? '';
            const url = resolveMediaUrl(raw);
            url
              ? resolve({ default: url })
              : reject(new Error('Server không trả về URL ảnh hợp lệ.'));
          } catch {
            reject(new Error('Phản hồi từ server không hợp lệ.'));
          }
        });

        this.xhr.addEventListener('error', () =>
          reject(new Error('Lỗi kết nối khi upload ảnh.')));

        this.xhr.send(data);
      })
    );
  }

  abort(): void { this.xhr?.abort(); }
}

import { UnsavedChanges, UnsavedChangesPage, watchUnsavedChanges } from '../../../guards/unsaved-changes.guard';
@Component({
  selector: 'app-news',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CKEditorModule, NgSelectModule, MediaLibraryPickerComponent, AppDatePipe, DateInputComponent],
  templateUrl: './news.html'
})
export class News implements OnInit, OnDestroy, UnsavedChangesPage {
  private newsService = inject(NewsService);
  private router = inject(Router);
  private categoryService = inject(CategoryService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private platformId = inject(PLATFORM_ID);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];

  isBrowser = isPlatformBrowser(this.platformId);

  // CKEditor
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  public Editor = signal<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  public editorConfig = signal<any>(null);
  briefContent = signal<string>('');
  contentHtml = signal<string>('');
  // Giá trị khởi tạo cho [data] của <ckeditor> — CHỈ gán khi mở modal, không cập nhật theo (change).
  // Nếu bind [data] thẳng vào briefContent()/contentHtml() thì mỗi lần gõ hoặc chèn ảnh, signal đổi ->
  // Angular đẩy ngược giá trị vào [data] -> CKEditor Angular wrapper gọi editor.data.set() -> rebuild
  // toàn bộ model -> hủy giữa chừng phần tử ảnh đang upload (loader bị abort, ảnh không bao giờ chèn được).
  briefContentInitial = '';
  contentInitial = '';

  // Categories flat list for dropdown
  categories: { id: number; label: string }[] = [];

  // Image
  selectedThumb = signal<File | null>(null);
  thumbPreviewUrl = signal<string>('');

  // Table
  displayedColumns = ['select', 'index', 'categoryName', 'title', 'lastUpDate', 'totalView', 'status', 'actions'];

  private applyTenantColumn(): void {
    if (this.isPrivileged && !this.displayedColumns.includes('tenant')) {
      this.displayedColumns.splice(this.displayedColumns.length - 1, 0, 'tenant');
    }
  }

  private loadTenants(): void {
    if (!this.isPrivileged) return;
    this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
  }
  dataSource: NewsArticle[] = [];
  selection = new SelectionModel<NewsArticle>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  isLoading = signal(false);
  isSaving = signal(false);

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MediaLibraryPickerComponent) mediaPicker!: MediaLibraryPickerComponent;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private activeEditorForPicker: any = null;

  // Modal state
  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<number | null>(null);
  currentPublicId = signal<string | null>(null);
  showConfirmDelete = signal(false);
  pendingDeleteId = signal<string | null>(null);

  // Đợt 21 — cảnh báo chưa lưu: form + nội dung 2 trình soạn thảo + ảnh đại diện.
  private unsaved = new UnsavedChanges();
  private readingImage = false;
  private formSnapshot() {
    return { form: this.newsForm.getRawValue(), brief: this.briefContent(), content: this.contentHtml(),
             thumb: this.thumbPreviewUrl(), thumbFile: this.selectedThumb()?.name ?? null };
  }
  private hasUnsavedChanges(): boolean { return this.showModal() && this.unsaved.changed(this.formSnapshot()); }
  canLeave = watchUnsavedChanges(() => this.hasUnsavedChanges(), () => this.isSaving() || this.readingImage);

  async requestClose(): Promise<void> {
    if (await this.canLeave()) this.closeModal();
  }
  isBulkDelete = signal(false);

  // Filter form
  filterForm = new FormGroup({
    keyword: new FormControl<string>('', { nonNullable: true }),
    categoryId: new FormControl<number | null>(null),
    status: new FormControl<string>('', { nonNullable: true }),
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo: new FormControl<string>('', { nonNullable: true }),
    tenantId: new FormControl<string | null>(null)
  });

  // Data form
  newsForm = new FormGroup({
    Title: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    CategoryId: new FormControl<number | null>(null),
    StartTime: new FormControl<string>('', { nonNullable: true }),
    EndTime: new FormControl<string>('', { nonNullable: true }),
    Author: new FormControl<string>('', { nonNullable: true }),
    Source: new FormControl<string>('', { nonNullable: true }),
    Keyword: new FormControl<string>('', { nonNullable: true }),
    MetaTitle: new FormControl<string>('', { nonNullable: true }),
    MetaKeyword: new FormControl<string>('', { nonNullable: true }),
    MetaDescription: new FormControl<string>('', { nonNullable: true }),
    Language: new FormControl<string>('vi', { nonNullable: true }),
    Status: new FormControl<number>(1, { nonNullable: true }),
    AllowComment: new FormControl<number>(1, { nonNullable: true }),
    TypesHomepage: new FormControl<boolean>(false, { nonNullable: true }),
    TypesNotification: new FormControl<boolean>(false, { nonNullable: true })
  });

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.loadCategories();
    this.loadData();
    if (this.isBrowser) {
      this.initEditor();
    }
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private uploadUrl = '';

  private getUploadToken(): string | null {
    try {
      const u = localStorage.getItem('admin_user');
      return u ? JSON.parse(u).token : null;
    } catch { return null; }
  }

  // Gắn upload adapter sau khi editor thực sự sẵn sàng (thay vì extraPlugins — tránh trường hợp
  // FileRepository chưa gán xong createUploadAdapter lúc chọn ảnh, khiến chèn ảnh không có tác dụng).
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onEditorReady(editor: any, field?: 'brief' | 'content') {
    // CKEditor chuẩn hoá HTML lúc nạp (vd "abc" -> "<p>abc</p>") — nếu người dùng chưa gõ gì vào trường này thì
    // coi bản chuẩn hoá là giá trị gốc, tránh hỏi "chưa lưu" khi mở rồi đóng ngay.
    if (field) {
      const sig = field === 'brief' ? this.briefContent : this.contentHtml;
      const initial = field === 'brief' ? this.briefContentInitial : this.contentInitial;
      const data = editor.getData?.();
      if (typeof data === 'string' && sig() === initial) { sig.set(data); this.unsaved.normalizeField(field, data); }
    }
    const fileRepo = editor.plugins.get('FileRepository');
    fileRepo.createUploadAdapter = (loader: any) =>
      new CKEditorUploadAdapter(loader, this.uploadUrl, () => this.getUploadToken());

    editor.on('openMediaLibrary', () => {
      this.activeEditorForPicker = editor;
      this.mediaPicker.open();
    });
  }

  onMediaPicked(url: string) {
    this.activeEditorForPicker?.execute('insertImage', { source: url });
  }

  private async initEditor() {
    ensureCkeditorStyles();
    const {
      ClassicEditor, Essentials, Paragraph, Bold, Italic, Underline, Strikethrough,
      Heading, Link, List, BlockQuote, Table, TableToolbar,
      FontSize, FontColor, FontBackgroundColor, Alignment,
      Indent, IndentBlock, HorizontalLine, SourceEditing, GeneralHtmlSupport,
      Image, ImageToolbar, ImageCaption, ImageStyle, ImageResize, ImageInsert, ImageUpload,
      FileRepository, Plugin, ButtonView
    } = await import('ckeditor5');

    this.uploadUrl = `${environment.baseApiUrl}/api/Cms/News/UploadImage`;

    // Nút toolbar "Chọn từ thư viện" — phải đăng ký qua componentFactory trong init() của một
    // Plugin thật (chạy trước khi toolbar được dựng), không thể chờ tới sự kiện (ready).
    class MediaLibraryButtonPlugin extends Plugin {
      static get pluginName() { return 'MediaLibraryButtonPlugin'; }
      init() {
        const editor = this.editor;
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        editor.ui.componentFactory.add('mediaLibrary', (locale: any) => {
          const button = new ButtonView(locale);
          button.set({ label: 'Chọn từ thư viện', icon: MEDIA_LIBRARY_ICON, tooltip: true });
          button.on('execute', () => editor.fire('openMediaLibrary'));
          return button;
        });
      }
    }

    this.editorConfig.set({
      licenseKey: 'GPL',
      plugins: [
        Essentials, Paragraph, Bold, Italic, Underline, Strikethrough,
        Heading, Link, List, BlockQuote, Table, TableToolbar,
        FontSize, FontColor, FontBackgroundColor, Alignment,
        Indent, IndentBlock, HorizontalLine, SourceEditing, GeneralHtmlSupport,
        Image, ImageToolbar, ImageCaption, ImageStyle, ImageResize, ImageInsert, ImageUpload,
        FileRepository, MediaLibraryButtonPlugin
      ],
      toolbar: {
        items: [
          'sourceEditing', '|',
          'heading', '|',
          'bold', 'italic', 'underline', 'strikethrough', '|',
          'fontSize', 'fontColor', 'fontBackgroundColor', '|',
          'alignment', '|',
          'bulletedList', 'numberedList', 'indent', 'outdent', '|',
          'link', 'insertImage', 'mediaLibrary', 'blockQuote', 'insertTable', 'horizontalLine', '|',
          'undo', 'redo'
        ]
      },
      image: {
        toolbar: [
          'imageStyle:inline', 'imageStyle:block', 'imageStyle:side', '|',
          'toggleImageCaption', 'imageTextAlternative', '|',
          'resizeImage'
        ]
      },
      htmlSupport: {
        allow: [{ name: /./, attributes: true, classes: true, styles: true }]
      }
    });
    this.Editor.set(ClassicEditor);
  }

  loadCategories() {
    this.categoryService.getTree().pipe(takeUntil(this.destroy$)).subscribe(tree => {
      this.categories = this.categoryService.flattenForSelect(tree);
    });
  }

  loadData() {
    const vals = this.filterForm.getRawValue();
    this.isLoading.set(true);
    this.newsService.search({
      keyword: vals.keyword || undefined,
      categoryId: vals.categoryId ?? undefined,
      status: vals.status !== '' ? parseInt(vals.status) : undefined,
      dateFrom: vals.dateFrom || undefined,
      dateTo: vals.dateTo || undefined,
      tenantId: vals.tenantId ?? null,
      pageIndex: this.pageIndex + 1,
      pageSize: this.pageSize
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource = res.items;
        this.totalRecords = res.total;
        this.selection.clear();
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch() {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  rowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  isAllSelected(): boolean {
    return this.selection.selected.length === this.dataSource.length && this.dataSource.length > 0;
  }

  masterToggle() {
    this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource);
  }

  async openAddModal() {
    if (!(await this.canLeave())) return;
    this.editMode.set(false);
    this.currentId.set(null);
    this.currentPublicId.set(null);
    this.briefContent.set('');
    this.contentHtml.set('');
    this.briefContentInitial = '';
    this.contentInitial = '';
    this.thumbPreviewUrl.set('');
    this.selectedThumb.set(null);
    this.newsForm.reset({
      Title: '', CategoryId: null, StartTime: '', EndTime: '',
      Author: '', Source: '', Keyword: '',
      MetaTitle: '', MetaKeyword: '', MetaDescription: '',
      Language: 'vi', Status: 2, AllowComment: 1,
      TypesHomepage: false, TypesNotification: false
    });
    this.unsaved.capture(this.formSnapshot());
    this.showModal.set(true);
  }

  /** Trang quản lý file đính kèm của tin (như trang File của tài liệu số) — port ELIB-LRC 10-04. */
  openAttachments(article: NewsArticle): void {
    if (!article.publicId) return;
    this.router.navigate(['/admin/news-attachments', article.publicId], { state: { newsTitle: article.title ?? '' } });
  }

  async openEditModal(article: NewsArticle) {
    const id = article.publicId ?? article.id;
    if (!id) return;
    if (!(await this.canLeave())) return;
    this.newsService.getById(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: (item) => {
        this.editMode.set(true);
        this.currentId.set(item.id ?? null);
        this.currentPublicId.set(item.publicId ?? null);
        this.briefContent.set(item.brief ?? '');
        this.contentHtml.set(item.content ?? '');
        this.briefContentInitial = item.brief ?? '';
        this.contentInitial = item.content ?? '';
        this.thumbPreviewUrl.set(item.thumb ?? '');
        this.selectedThumb.set(null);

        const types = (item.types ?? '').split(',').map((t: string) => t.trim());
        this.newsForm.patchValue({
          Title: item.title ?? '',
          CategoryId: item.categoryId ?? null,
          StartTime: item.startTime ? item.startTime.split('T')[0] : '',
          EndTime: item.endTime ? item.endTime.split('T')[0] : '',
          Author: item.author ?? '',
          Source: item.source ?? '',
          Keyword: item.keyword ?? '',
          MetaTitle: item.metaTitle ?? '',
          MetaKeyword: item.metaKeyword ?? '',
          MetaDescription: item.metaDescription ?? '',
          Language: item.language ?? 'vi',
          Status: item.status ?? 1,
          AllowComment: item.allowComment ?? 1,
          TypesHomepage: types.includes('1'),
          TypesNotification: types.includes('2')
        });
        this.unsaved.capture(this.formSnapshot());
        this.showModal.set(true);
      },
      error: () => this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'))
    });
  }

  closeModal() {
    this.showModal.set(false);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onBriefChange(event: any) {
    const data = event?.editor?.getData?.();
    if (data !== undefined) this.briefContent.set(data);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onContentChange(event: any) {
    const data = event?.editor?.getData?.();
    if (data !== undefined) this.contentHtml.set(data);
  }

  onThumbSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.selectedThumb.set(file);
    this.readingImage = true;
    const reader = new FileReader();
    reader.onload = (e) => { this.thumbPreviewUrl.set(e.target?.result as string); this.readingImage = false; };
    reader.onerror = () => { this.readingImage = false; };
    reader.readAsDataURL(file);
  }

  private buildTypes(): string {
    const vals = this.newsForm.getRawValue();
    const types: string[] = [];
    if (vals.TypesHomepage) types.push('1');
    if (vals.TypesNotification) types.push('2');
    return types.join(',');
  }

  private buildPayload(): Partial<NewsArticle> {
    const vals = this.newsForm.getRawValue();
    return {
      title: vals.Title,
      categoryId: vals.CategoryId ?? undefined,
      startTime: vals.StartTime || undefined,
      endTime: vals.EndTime || undefined,
      author: vals.Author || undefined,
      source: vals.Source || undefined,
      keyword: vals.Keyword || undefined,
      metaTitle: vals.MetaTitle || undefined,
      metaKeyword: vals.MetaKeyword || undefined,
      metaDescription: vals.MetaDescription || undefined,
      language: vals.Language,
      status: vals.Status,
      allowComment: vals.AllowComment,
      types: this.buildTypes(),
      brief: this.briefContent(),
      content: this.contentHtml()
    };
  }

  onSubmit() {
    if (this.newsForm.get('Title')?.invalid) {
      this.newsForm.markAllAsTouched();
      return;
    }
    this.isSaving.set(true);
    const payload = this.buildPayload();

    const doSave = (thumbUrl?: string) => {
      if (thumbUrl) payload.thumb = thumbUrl;
      const id = this.currentPublicId() ?? this.currentId();
      const obs = this.editMode() && id
        ? this.newsService.update(id, payload)
        : this.newsService.create(payload);

      obs.pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
          this.closeModal();
          this.loadData();
          this.isSaving.set(false);
        },
        error: () => {
          this.isSaving.set(false);
        }
      });
    };

    const file = this.selectedThumb();
    if (file) {
      this.newsService.uploadImage(file).pipe(takeUntil(this.destroy$)).subscribe({
        next: (url) => doSave(url || this.thumbPreviewUrl()),
        error: () => doSave(this.thumbPreviewUrl() || undefined)
      });
    } else {
      doSave(this.thumbPreviewUrl() || undefined);
    }
  }

  toggleStatus(article: NewsArticle) {
    const id = article.publicId ?? String(article.id ?? '');
    const newStatus = (article.status ?? 1) === 2 ? 1 : 2;
    this.newsService.changeStatus(id, newStatus).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { article.status = newStatus; },
      error: () => {}
    });
  }

  handleDelete(article: NewsArticle) {
    this.isBulkDelete.set(false);
    this.pendingDeleteId.set(article.publicId ?? String(article.id ?? ''));
    this.showConfirmDelete.set(true);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.isBulkDelete.set(true);
    this.showConfirmDelete.set(true);
  }

  confirmActionExecute() {
    if (this.isBulkDelete()) {
      const obs = this.selection.selected.map(a =>
        this.newsService.delete(a.publicId ?? String(a.id ?? ''))
      );
      forkJoin(obs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.loadData();
          this.closeConfirm();
        },
        error: () => {
          this.closeConfirm();
        }
      });
    } else {
      this.newsService.delete(this.pendingDeleteId()!).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.loadData();
          this.closeConfirm();
        },
        error: () => {
          this.closeConfirm();
        }
      });
    }
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.pendingDeleteId.set(null);
  }
}
