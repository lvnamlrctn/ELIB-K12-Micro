import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';
import { BookApiService } from '../../services/book-api.service';

@Component({
  selector: 'app-page-view',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './page-view.html'
})
export class PageViewComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);
  private bookApi = inject(BookApiService);
  private destroy$ = new Subject<void>();
  private objectUrl: string | null = null;

  ebookFileId = signal('');
  pageNumber = signal(0);
  pageUrl = signal<SafeResourceUrl | null>(null);
  loading = signal(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const ebookFileId = params.get('ebookFileId') || '';
      const page = Number(params.get('page')) || 0;
      this.ebookFileId.set(ebookFileId);
      this.pageNumber.set(page);
      this.loadPage(ebookFileId, page);
    });
  }

  private loadPage(ebookFileId: string, page: number): void {
    this.revokeObjectUrl();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.pageUrl.set(null);

    if (!ebookFileId || page <= 0) {
      this.loading.set(false);
      this.errorMessage.set('Thiếu thông tin trang cần xem.');
      return;
    }

    this.bookApi.getPage(ebookFileId, page).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.objectUrl = URL.createObjectURL(blob);
        this.pageUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        if (err.status === 400) this.errorMessage.set('Chức năng xem theo trang chỉ hỗ trợ file PDF.');
        else if (err.status === 404) this.errorMessage.set('Không tìm thấy trang này trong tài liệu.');
        else this.errorMessage.set('Đã xảy ra lỗi khi tải trang tài liệu. Vui lòng thử lại.');
      }
    });
  }

  private revokeObjectUrl(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }

  goBack(): void {
    window.history.back();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.revokeObjectUrl();
  }
}
