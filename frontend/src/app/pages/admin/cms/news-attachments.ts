import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, firstValueFrom } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AttachFileService } from '../../../services/cms/attach-file.service';
import { AttachFile, AttachFileSyncResult, ATTACH_ALLOWED_EXTENSIONS, ATTACH_MAX_BYTES } from '../../../models/cms/attach-file';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

/** Trang "File đính kèm" của 1 tin tức — theo khuôn trang File của tài liệu số (ebook-files). */
@Component({
  selector: 'app-news-attachments',
  standalone: true,
  imports: [CanDirective, CommonModule, FormsModule, TranslateModule, MatIconModule, AppDatePipe],
  templateUrl: './news-attachments.html'
})
export class NewsAttachmentsPage implements OnInit, OnDestroy {
  private route     = inject(ActivatedRoute);
  private router    = inject(Router);
  private service   = inject(AttachFileService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  readonly allowed = ATTACH_ALLOWED_EXTENSIONS;
  readonly accept  = ATTACH_ALLOWED_EXTENSIONS.map(e => '.' + e).join(',');

  newsPublicId = '';
  newsTitle    = '';

  readonly files     = signal<AttachFile[]>([]);
  readonly isLoading = signal(false);
  readonly legacyCount = computed(() => this.files().filter(f => this.isLegacy(f)).length);

  // Tải lên
  readonly showUploadModal = signal(false);
  readonly selected        = signal<File[]>([]);
  readonly rejected        = signal<string[]>([]);
  readonly uploading       = signal(false);
  readonly progress        = signal(0);
  displayName = '';

  // Đổi tên / xoá / đồng bộ
  readonly renaming   = signal<AttachFile | null>(null);
  renameValue = '';
  readonly deleting   = signal<AttachFile | null>(null);
  readonly showSync   = signal(false);
  readonly syncing    = signal(false);
  readonly syncResult = signal<AttachFileSyncResult | null>(null);

  ngOnInit(): void {
    this.newsPublicId = this.route.snapshot.paramMap.get('publicId') ?? '';
    this.newsTitle    = (history.state as any)?.newsTitle ?? '';
    this.load();
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  load(): void {
    this.isLoading.set(true);
    this.service.list(this.newsPublicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.files.set(list); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }

  goBack(): void { this.router.navigate(['/admin/news']); }

  isLegacy(f: AttachFile): boolean { return !!f.url && f.url.toLowerCase().startsWith('upload'); }

  ext(f: AttachFile): string {
    const n = f.name || f.url || '';
    const i = n.lastIndexOf('.');
    return i >= 0 ? n.slice(i + 1).toLowerCase() : '';
  }

  icon(ext: string): string {
    switch (ext) {
      case 'pdf': return 'picture_as_pdf';
      case 'doc': case 'docx': case 'odt': case 'rtf': case 'txt': return 'description';
      case 'xls': case 'xlsx': case 'ods': case 'csv': return 'table_chart';
      case 'ppt': case 'pptx': case 'odp': return 'slideshow';
      case 'jpg': case 'jpeg': case 'png': case 'gif': case 'webp': return 'image';
      case 'mp3': return 'audiotrack';
      case 'mp4': return 'videocam';
      case 'zip': case 'rar': case '7z': return 'folder_zip';
      default: return 'insert_drive_file';
    }
  }

  formatSize(kb: number | null | undefined): string {
    if (kb == null) return '—';
    return kb < 1024 ? `${kb.toFixed(1)} KB` : `${(kb / 1024).toFixed(2)} MB`;
  }

  // ── Xem / tải ────────────────────────────────────────────────
  /** PDF/ảnh/văn bản mở tab mới; loại khác lưu về với tên file. */
  open(f: AttachFile, forceDownload = false): void {
    const ext = this.ext(f);
    const viewable = !forceDownload && ['pdf', 'jpg', 'jpeg', 'png', 'gif', 'webp', 'txt', 'mp3', 'mp4'].includes(ext);
    const tab = viewable ? window.open('', '_blank') : null;
    this.service.download(f.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        if (tab) tab.location.href = url;
        else {
          const a = document.createElement('a');
          a.href = url; a.download = f.name || 'file'; a.click();
        }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: async (err: HttpErrorResponse) => {
        tab?.close();
        this.toastr.error(await this.blobError(err) ?? this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  private async blobError(err: HttpErrorResponse): Promise<string | null> {
    try { return err.error instanceof Blob ? JSON.parse(await err.error.text())?.message ?? null : err.error?.message ?? null; }
    catch { return null; }
  }

  // ── Tải lên ──────────────────────────────────────────────────
  openUpload(): void {
    this.selected.set([]); this.rejected.set([]); this.displayName = ''; this.progress.set(0);
    this.showUploadModal.set(true);
  }

  closeUpload(): void { if (!this.uploading()) this.showUploadModal.set(false); }

  onSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const ok: File[] = [], bad: string[] = [];
    for (const file of Array.from(input.files ?? [])) {
      const ext = file.name.includes('.') ? file.name.split('.').pop()!.toLowerCase() : '';
      if (!this.allowed.includes(ext)) bad.push(this.translate.instant('NEWS.ATTACH.BAD_TYPE', { name: file.name }));
      else if (file.size > ATTACH_MAX_BYTES) bad.push(this.translate.instant('NEWS.ATTACH.TOO_BIG', { name: file.name }));
      else if (file.size === 0) bad.push(this.translate.instant('NEWS.ATTACH.EMPTY', { name: file.name }));
      else ok.push(file);
    }
    this.selected.set([...this.selected(), ...ok.filter(f => !this.selected().some(s => s.name === f.name && s.size === f.size))]);
    this.rejected.set(bad);
    input.value = '';
  }

  removeSelected(file: File): void { this.selected.set(this.selected().filter(f => f !== file)); }

  /** Tải lần lượt từng file; file lỗi không chặn các file sau (lỗi đã được interceptor báo). */
  async upload(): Promise<void> {
    const list = this.selected();
    if (!list.length) return;
    this.uploading.set(true);
    let done = 0, ok = 0;
    for (const file of list) {
      try {
        await firstValueFrom(this.service.upload(this.newsPublicId, file, list.length === 1 ? this.displayName : null));
        ok++;
      } catch { /* interceptor đã hiện lỗi */ }
      this.progress.set(Math.round(++done * 100 / list.length));
    }
    this.uploading.set(false);
    if (ok) this.toastr.success(this.translate.instant('NEWS.ATTACH.UPLOADED', { n: ok }));
    if (ok === list.length) this.showUploadModal.set(false);
    else this.selected.set([]);
    this.load();
  }

  // ── Đổi tên ──────────────────────────────────────────────────
  startRename(f: AttachFile): void { this.renaming.set(f); this.renameValue = f.name ?? ''; }

  saveRename(): void {
    const f = this.renaming();
    if (!f || !this.renameValue.trim()) return;
    this.service.rename(f.publicId, this.renameValue.trim()).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
      this.renaming.set(null);
      this.load();
    });
  }

  // ── Xoá ──────────────────────────────────────────────────────
  confirmDelete(): void {
    const f = this.deleting();
    if (!f) return;
    this.service.delete(f.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.deleting.set(null); this.load(); },
      error: () => this.deleting.set(null)
    });
  }

  // ── Đồng bộ file cũ (chỉ khi admin bấm) ──────────────────────
  openSync(): void { this.syncResult.set(null); this.showSync.set(true); }

  runSync(): void {
    this.syncing.set(true);
    this.service.syncLegacy().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { this.syncing.set(false); this.syncResult.set(r); this.load(); },
      error: () => this.syncing.set(false)
    });
  }
}
