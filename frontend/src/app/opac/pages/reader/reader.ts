import { TranslateModule } from '@ngx-translate/core';
import { Component, inject, signal, OnInit, OnDestroy, PLATFORM_ID, ViewChild, ElementRef, computed } from '@angular/core';
import { isPlatformBrowser, CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl, SafeUrl } from '@angular/platform-browser';
import { BookApiService } from '../../services/book-api.service';
import { AuthService } from '../../services/auth.service';
import { DocumentChatPanelComponent } from '../../components/document-chat-panel/document-chat-panel';
import { Subject, takeUntil } from 'rxjs';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
declare const $: any;

@Component({
  selector: 'app-reader',
  standalone: true,
  imports: [TranslateModule, RouterLink, CommonModule, DocumentChatPanelComponent],
  templateUrl: './reader.html'
})
export class ReaderComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);
  private platformId = inject(PLATFORM_ID);
  private isBrowser = isPlatformBrowser(this.platformId);
  private bookApi = inject(BookApiService);
  private authService = inject(AuthService);
  private destroy$ = new Subject<void>();

  // Signals quản lý trạng thái
  fileUrl = signal('');
  fileType = signal('');
  ebookId = signal('');
  safeUrl = signal<SafeUrl | SafeResourceUrl | null>(null);
  errorMessage = signal<string | null>(null);
  docTitle = signal('');

  // Phân quyền đọc/tải
  checking = signal(true);          // đang kiểm tra quyền
  permissionDenied = signal(false); // không có quyền đọc (tài liệu không miễn phí + chưa đăng nhập)
  allowDownload = signal(false);    // backend cho phép tải
  downloadUrl = signal('');         // URL tải kèm token

  @ViewChild(DocumentChatPanelComponent) private docChatPanel?: DocumentChatPanelComponent;

  toggleDocChat(): void {
    this.docChatPanel?.toggle();
  }

  /** Nguồn "Trang N" trong panel hỏi đáp → lật trình đọc dFlip tới trang đó. */
  goToPage(page: number): void {
    try { this.flipBookInstance?.gotoPage?.(page); } catch (e) { console.warn('DFlip gotoPage error:', e); }
  }

  // Audio Player State
  @ViewChild('audioPlayer') audioPlayer?: ElementRef<HTMLMediaElement>;
  isPlaying = signal(false);
  currentTime = signal(0);
  duration = signal(0);
  volume = signal(1);
  isMuted = signal(false);

  progress = computed(() => {
    const d = this.duration();
    return d > 0 ? (this.currentTime() / d) * 100 : 0;
  });

  private flipBookInstance: any = null;
  private initAttempts = 0;

  ngOnInit() {
    // Lắng nghe params từ URL
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(paramMap => {
      const id = paramMap.get('id') || '';

      this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
        const type = params.get('type') || 'unknown';
        const urlParam = params.get('url') || '';
        this.docTitle.set(params.get('title') || '');
        this.ebookId.set(params.get('ebookId') || '');
        // Cờ free/allowDownload truyền từ book-detail (backend trả isFree/allowDownload).
        const freeParam = params.get('free');
        const free = freeParam !== null ? Number(freeParam) : null;
        const allowDownload = Number(params.get('allowDownload')) === 2;
        this.loadWithPermission(id, type, urlParam, free, allowDownload);
      });
    });
  }

  // Quyết định quyền đọc từ cờ free + trạng thái đăng nhập; chỉ nạp file khi được phép.
  private loadWithPermission(id: string, type: string, urlParam: string, free: number | null, allowDownload: boolean) {
    if (!id) { this.checking.set(false); return; }

    this.checking.set(true);
    this.permissionDenied.set(false);
    this.allowDownload.set(false);

    const token = this.authService.currentUser()?.token;

    // free=2: miễn phí, ai cũng đọc. Khác: cần đăng nhập. Thiếu cờ (mở link trực tiếp) → để backend tự enforce.
    const allowed = free === null ? true : (free === 2 || this.authService.isAuthenticated());

    this.checking.set(false);

    if (!allowed) {
      this.permissionDenied.set(true);
      return;
    }

    this.allowDownload.set(allowDownload);

    // PDF lấy qua API (kèm token nếu có); loại khác dùng url truyền sẵn.
    const url = (type === 'pdf')
      ? this.bookApi.getFileUrlWithToken(id, token)
      : urlParam;

    this.downloadUrl.set(this.bookApi.getFileUrlWithToken(id, token));
    this.updateMediaSource(url, type);
  }

  private updateMediaSource(url: string, type: string) {
    // Reset trạng thái trước khi nạp tài liệu mới
    this.destroyFlipBook();
    this.fileUrl.set(url);
    this.fileType.set(type);
    this.errorMessage.set(null);
    this.safeUrl.set(null);

    if (!url) return;

    // Cấu hình Safe URL cho từng loại media
    const safe = (type === 'audio' || type === 'video')
      ? this.sanitizer.bypassSecurityTrustUrl(url)
      : this.sanitizer.bypassSecurityTrustResourceUrl(url);
    
    this.safeUrl.set(safe);

    // Khởi tạo dFlip nếu là PDF và đang ở môi trường trình duyệt
    if (this.isBrowser && type === 'pdf') {
      this.initAttempts = 0;
      this.initFlipBook();
    }
  }

  initFlipBook() {
    if (!this.isBrowser || this.fileType() !== 'pdf' || !this.fileUrl()) return;

    // Sử dụng setTimeout để đảm bảo Angular đã render @if (fileType() === 'pdf') trong HTML
    setTimeout(() => {
      const container = document.getElementById('flipbookContainer');

      if (container && typeof $ !== 'undefined' && $.fn.flipBook) {
        // Khởi tạo dFlip trực tiếp từ URL API GET
        this.flipBookInstance = $(container).flipBook(this.fileUrl(), {
          webgl: true,
          webglShadow: true,
          backgroundColor: "#f3f4f6",
          height: "100%",
          // PDF.js hỗ trợ Range Request thông qua API GET này
          pdfjsSrc: 'assets/dflip/js/pdf.js',
          pdfjsWorkerSrc: 'assets/dflip/js/pdf.worker.js'
        });
      } else {
        // Thử lại nếu DOM hoặc thư viện chưa sẵn sàng
        if (this.initAttempts < 15) {
          this.initAttempts++;
          setTimeout(() => this.initFlipBook(), 200);
        } else {
          this.errorMessage.set('Không thể khởi tạo trình đọc sách. Vui lòng thử lại.');
        }
      }
    }, 150);
  }

  private destroyFlipBook() {
    if (this.flipBookInstance) {
      try {
        if (this.flipBookInstance.dispose) this.flipBookInstance.dispose();
        const container = document.getElementById('flipbookContainer');
        if (container) $(container).empty();
      } catch (e) {
        console.warn('DFlip cleanup error:', e);
      }
      this.flipBookInstance = null;
    }
  }

  // --- Logic Audio (Giữ nguyên và tối ưu nhẹ) ---
  togglePlay() {
    const player = this.audioPlayer?.nativeElement;
    if (!player) return;
    this.isPlaying() ? player.pause() : player.play().catch(() => {});
  }

  onTimeUpdate() {
    this.currentTime.set(this.audioPlayer?.nativeElement.currentTime || 0);
  }

  onMetadataLoaded() {
    this.duration.set(this.audioPlayer?.nativeElement.duration || 0);
  }

  onPlayStateChange(playing: boolean) {
    this.isPlaying.set(playing);
  }

  seek(event: Event) {
    const input = event.target as HTMLInputElement;
    const time = (Number(input.value) / 100) * this.duration();
    if (this.audioPlayer) this.audioPlayer.nativeElement.currentTime = time;
  }

  changeVolume(event: Event) {
    const input = event.target as HTMLInputElement;
    const vol = Number(input.value);
    if (this.audioPlayer) this.audioPlayer.nativeElement.volume = vol;
    this.volume.set(vol);
    this.isMuted.set(vol === 0);
  }

  toggleMute() {
    this.isMuted.update(v => !v);
    if (this.audioPlayer) this.audioPlayer.nativeElement.muted = this.isMuted();
  }

  onMediaError(event: any) {
    const code = event.target?.error?.code;
    const messages: Record<number, string> = {
      1: 'Tiến trình tải bị dừng.',
      2: 'Lỗi mạng khi tải tệp.',
      3: 'Lỗi giải mã tệp.',
      4: 'Không tìm thấy tệp hoặc truy cập bị chặn.'
    };
    this.errorMessage.set(messages[code] || 'Đã xảy ra lỗi khi phát tệp.');
  }

  formatTime(seconds: number): string {
    if (isNaN(seconds)) return '00:00';
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  goBack() {
    window.history.back();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    this.destroyFlipBook();
  }
}