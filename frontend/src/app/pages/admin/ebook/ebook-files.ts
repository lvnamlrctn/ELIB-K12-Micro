import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EbookFileService } from '../../../services/ebook/ebook-file.service';
import { EbookFile } from '../../../models/ebook/ebook-file';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

@Component({
  selector: 'app-ebook-files',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatIconModule, AppDatePipe, NgSelectModule],
  templateUrl: './ebook-files.html'
})
export class EbookFilesPage implements OnInit, OnDestroy {
  private route     = inject(ActivatedRoute);
  private router    = inject(Router);
  private service   = inject(EbookFileService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  ebookPublicId = '';
  ebookId       = 0;
  ebookTitle    = '';

  displayedColumns = ['stt', 'type', 'info', 'size', 'date', 'actions'];
  dataSource: EbookFile[] = [];
  totalCount = 0;

  isLoading         = signal(false);
  showUploadModal   = signal(false);
  isUploading       = signal(false);
  selectedFile      = signal<File | null>(null);
  showConfirmDelete = signal(false);
  deleteId          = signal<string | null>(null);

  uploadForm = new FormGroup({
    type:        new FormControl<string>('Document'),
    description: new FormControl(''),
    sortOrder:   new FormControl<number | null>(null),
  });

  // Loại file khi thêm mới: chỉ 1 loại "Toàn văn" (value = Document).
  fileTypes = [
    { value: 'Document', label: 'Toàn văn' },
  ];

  ngOnInit(): void {
    this.ebookPublicId = this.route.snapshot.paramMap.get('publicId') ?? '';
    const state        = history.state as any;
    this.ebookTitle    = state?.ebookTitle ?? '';
    this.ebookId       = state?.ebookId    ?? 0;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ ebookPublicId: this.ebookPublicId })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.dataSource = res.data;
          this.totalCount = res.totalCount;
          this.isLoading.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          this.isLoading.set(false);
        }
      });
  }

  goBack(): void {
    this.router.navigate(['/admin/ebooks']);
  }

  getFileTypeLabel(type: string | null | undefined): string {
    // 'main' là dữ liệu cũ, 'Document' là loại "Toàn văn" hiện tại.
    if (type === 'main' || type === 'Document') return 'Toàn văn';
    return this.fileTypes.find(f => f.value === type)?.label ?? type ?? '—';
  }

  getFileTypeColor(type: string | null | undefined): string {
    switch (type) {
      case 'main':
      case 'Document':   return 'bg-blue-100 text-blue-700';
      case 'cover':      return 'bg-purple-100 text-purple-700';
      case 'attachment': return 'bg-yellow-100 text-yellow-700';
      default:           return 'bg-gray-100 text-gray-700';
    }
  }

  getFileIcon(ext: string | null | undefined): string {
    switch (ext?.toLowerCase()) {
      case 'pdf':  return 'picture_as_pdf';
      case 'doc':
      case 'docx': return 'description';
      case 'xls':
      case 'xlsx': return 'table_chart';
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif':  return 'image';
      case 'mp4':
      case 'avi':
      case 'mov':  return 'videocam';
      default:     return 'insert_drive_file';
    }
  }

  formatFileSize(kb: number | null | undefined): string {
    if (kb == null) return '—';
    if (kb < 1024)  return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(2)} MB`;
  }

  // ── View / Download ──────────────────────────────────────────
  viewFile(file: EbookFile): void {
    const publicId = file.publicId;
    if (!publicId) return;
    this.service.getToken(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: tokenData => {
        const url = this.service.getViewUrl(tokenData.token);
        window.open(url, '_blank');
      },
      error: () => this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'))
    });
  }

  // ── Upload ──────────────────────────────────────────────────
  openUploadModal(): void {
    this.selectedFile.set(null);
    this.uploadForm.reset({ type: 'Document', description: '', sortOrder: null });
    this.showUploadModal.set(true);
  }

  closeUploadModal(): void {
    this.showUploadModal.set(false);
  }

  onFileSelect(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.selectedFile.set(file);
    (event.target as HTMLInputElement).value = '';
  }

  confirmUpload(): void {
    const file = this.selectedFile();
    if (!file) return;
    this.isUploading.set(true);

    const v    = this.uploadForm.getRawValue();
    const form = new FormData();
    form.append('file',        file);
    form.append('ebookId',     String(this.ebookId));
    form.append('type',        v.type ?? 'Document');
    form.append('description', v.description ?? '');
    if (v.sortOrder != null) form.append('sortOrder', String(v.sortOrder));

    this.service.upload(form).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isUploading.set(false);
        this.closeUploadModal();
        this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        this.loadData();
      },
      error: () => {
        this.isUploading.set(false);
      }
    });
  }

  // ── Delete ──────────────────────────────────────────────────
  handleDelete(file: EbookFile): void {
    this.deleteId.set(file.publicId ?? String(file.id));
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.deleteId.set(null);
  }

  confirmDelete(): void {
    const id = this.deleteId();
    if (!id) return;
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
}
