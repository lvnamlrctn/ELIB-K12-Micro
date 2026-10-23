import { Component, ElementRef, OnDestroy, OnInit, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, EbookFile, EbookFileSearch, EbookItem, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ItemsApi } from './items-api';

/** Phần pdf.js dùng ở đây (nạp lười nên không import kiểu của thư viện vào gói khởi động). */
interface PdfPage {
  getViewport(options: { scale: number }): { width: number; height: number };
  render(options: { canvasContext: CanvasRenderingContext2D; viewport: { width: number; height: number } }): { promise: Promise<void> };
}
interface PdfDocument { numPages: number; getPage(n: number): Promise<PdfPage>; destroy(): Promise<void>; }

/** Bề rộng ảnh bìa tối đa — vừa đủ nét và nằm dưới trần 2 MB của ảnh bìa ở media. */
const COVER_WIDTH = 800;

/**
 * Tạo ảnh bìa từ một trang của file PDF (LRC: CoverPreview + SetCoverFromPage). Khác LRC: trang được vẽ ngay trong trình duyệt bằng pdf.js
 * (nạp lười khi mở hộp thoại) rồi tải lên như một ảnh bìa bình thường — server không cần bộ vẽ PDF.
 */
@Component({
  selector: 'app-item-cover',
  imports: [FormsModule, Modal, TranslatePipe],
  template: `
    <app-modal title="DIGITAL_DOC.COVER_TITLE" widthClass="max-w-lg" (closed)="closed.emit()">
      <div class="h-80 border border-gray-200 rounded-xl bg-gray-50 flex items-center justify-center overflow-hidden relative">
        @if (loading()) { <span class="material-icons animate-spin text-[32px] text-blue-600">autorenew</span> }
        <canvas #canvas class="max-h-full max-w-full" [class.hidden]="loading() || !!error()"></canvas>
        @if (error(); as e) { <p class="text-sm text-gray-500 px-6 text-center">{{ e | translate }}</p> }
      </div>
      <div class="mt-4 flex items-center justify-center gap-3">
        <button type="button" class="w-9 h-9 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 flex items-center justify-center" [disabled]="page() <= 1 || loading()" (click)="go(page() - 1)" [attr.aria-label]="'DIGITAL_DOC.COVER_PREV' | translate">
          <span class="material-icons">chevron_left</span></button>
        <label class="text-sm text-gray-600 flex items-center gap-2">{{ 'DIGITAL_DOC.COVER_PAGE' | translate }}
          <input type="number" class="input !w-20 text-center" min="1" [max]="pages()" [ngModel]="page()" (change)="go(+$any($event.target).value)" /> / {{ pages() }}
        </label>
        <button type="button" class="w-9 h-9 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 flex items-center justify-center" [disabled]="page() >= pages() || loading()" (click)="go(page() + 1)" [attr.aria-label]="'DIGITAL_DOC.COVER_NEXT' | translate">
          <span class="material-icons">chevron_right</span></button>
      </div>
      <ng-container footer>
        <button type="button" class="btn-secondary" (click)="closed.emit()">{{ 'COMMON.CANCEL' | translate }}</button>
        <button type="button" class="btn-primary" [disabled]="saving() || loading() || !!error()" (click)="use()">
          <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'image' }}</span> {{ 'DIGITAL_DOC.COVER_USE' | translate }}
        </button>
      </ng-container>
    </app-modal>
  `,
})
export class ItemCover implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly itemsApi = inject(ItemsApi);
  private readonly toastr = inject(ToastrService);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  readonly item = input.required<EbookItem>();
  readonly closed = output<void>();
  /** URL ảnh bìa mới sau khi đã lưu vào tài liệu. */
  readonly saved = output<string>();

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly pages = signal(1);
  private pdf: PdfDocument | null = null;

  ngOnInit(): void {
    void this.start();
  }

  ngOnDestroy(): void {
    void this.pdf?.destroy();
  }

  private async start(): Promise<void> {
    try {
      const files = await this.api.crud<EbookFile>('files', 'digital').search({ ebookItemPublicId: this.item().publicId, pageSize: 200 } as EbookFileSearch);
      const pdf = [...files.items].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
        .find((f) => (f.fileExt ?? '').toLowerCase().replace('.', '') === 'pdf' || f.fileType === 'application/pdf');
      if (!pdf) {
        this.fail('DIGITAL_DOC.COVER_NO_PDF');
        return;
      }
      const url = await this.api.fileUrl(pdf.mediaFileId);
      const pdfjs = await import('pdfjs-dist');
      pdfjs.GlobalWorkerOptions.workerSrc = new URL('assets/pdf.worker.min.mjs', window.location.origin).href;
      this.pdf = (await pdfjs.getDocument({ url, isEvalSupported: false }).promise) as unknown as PdfDocument;
      this.pages.set(this.pdf.numPages);
      await this.render(1);
    } catch (e) {
      this.fail(errorMessage(e));
    }
  }

  private fail(message: string): void {
    this.error.set(message);
    this.loading.set(false);
  }

  protected async go(n: number): Promise<void> {
    if (!this.pdf || !Number.isFinite(n)) return;
    await this.render(Math.min(Math.max(1, Math.trunc(n)), this.pages()));
  }

  private async render(n: number): Promise<void> {
    if (!this.pdf) return;
    this.loading.set(true);
    try {
      const page = await this.pdf.getPage(n);
      const natural = page.getViewport({ scale: 1 });
      const viewport = page.getViewport({ scale: Math.min(2, COVER_WIDTH / natural.width) });
      const canvas = this.canvas().nativeElement;
      canvas.width = Math.floor(viewport.width);
      canvas.height = Math.floor(viewport.height);
      const context = canvas.getContext('2d');
      if (!context) throw new Error('canvas');
      await page.render({ canvasContext: context, viewport }).promise;
      this.page.set(n);
      this.error.set(null);
    } catch (e) {
      this.fail(errorMessage(e));
      return;
    }
    this.loading.set(false);
  }

  protected async use(): Promise<void> {
    this.saving.set(true);
    try {
      const blob = await new Promise<Blob | null>((resolve) => this.canvas().nativeElement.toBlob(resolve, 'image/jpeg', 0.85));
      if (!blob) throw new Error('canvas');
      const uploaded = await this.api.upload('ebook-cover', new File([blob], 'cover.jpg', { type: 'image/jpeg' }));
      const item = await this.itemsApi.setItemCover(this.item().publicId, uploaded.url);
      this.toastr.success('DIGITAL_DOC.COVER_SAVED');
      this.saved.emit(item.coverUrl ?? uploaded.url ?? '');
      this.closed.emit();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
