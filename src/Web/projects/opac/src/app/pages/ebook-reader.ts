import { Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { DigitalApi, DigitalBookmark, DigitalItemDetail } from '../core/digital';
import { Language } from '../core/i18n';
import { ReaderAuth } from '../core/reader-auth';

/** Phần pdf.js dùng — chỉ khai báo những gì cần, thư viện nạp lười khi mở trình đọc (không vào gói chính của OPAC). */
interface PdfPage { getViewport(o: { scale: number }): { width: number; height: number }; render(o: unknown): { promise: Promise<void>; cancel(): void } }
interface PdfDocument { numPages: number; getPage(n: number): Promise<PdfPage>; destroy(): Promise<void> }

/** File lớn hơn mức này không đọc trong trang (nạp cả file vào bộ nhớ) — mở ở tab mới. */
const MAX_INLINE_BYTES = 150 * 1024 * 1024;
const HEARTBEAT_MS = 20_000;

/**
 * Trình đọc PDF trong trang (pdf.js, vẽ từng trang ra canvas khi cuộn tới): nhớ trang đang đọc, dấu trang, ghi nhịp đọc cho thủ thư thống kê.
 * Không có nút tải/in và chặn chuột phải — chỉ là cản trở giao diện, KHÔNG phải DRM (người dùng kỹ thuật vẫn lấy được nội dung hiển thị).
 * File lấy qua URL ký có hạn rồi giữ trong bộ nhớ nên hết hạn URL không làm gián đoạn việc đọc.
 */
@Component({
  selector: 'opac-ebook-reader',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="fixed inset-0 z-40 flex flex-col bg-slate-100" (contextmenu)="$event.preventDefault()">
      <header class="flex items-center gap-2 bg-slate-900 text-white px-3 py-2 text-sm flex-wrap">
        <a [routerLink]="['/thu-vien-so', itemId]" class="flex items-center gap-1 hover:underline"><span class="material-icons text-[18px]">arrow_back</span> {{ 'OPAC.READER.BACK' | translate }}</a>
        <div class="font-medium truncate min-w-0 flex-1">{{ item()?.title }}</div>
        @if (pages() > 0) {
          <div class="flex items-center gap-1">
            <button type="button" class="px-2 py-1 rounded hover:bg-slate-700" [attr.aria-label]="'OPAC.READER.PREV' | translate" (click)="go(page() - 1)">‹</button>
            <input type="number" min="1" [max]="pages()" class="w-14 rounded bg-slate-800 border border-slate-600 px-1 py-0.5 text-center" [ngModel]="page()" (change)="go(+$any($event.target).value)" [attr.aria-label]="'OPAC.READER.PAGE' | translate" />
            <span>/ {{ pages() }}</span>
            <button type="button" class="px-2 py-1 rounded hover:bg-slate-700" [attr.aria-label]="'OPAC.READER.NEXT' | translate" (click)="go(page() + 1)">›</button>
          </div>
          <div class="flex items-center gap-1">
            <button type="button" class="px-2 py-1 rounded hover:bg-slate-700" [attr.aria-label]="'OPAC.READER.ZOOM_OUT' | translate" (click)="zoom(-0.2)">−</button>
            <span class="w-12 text-center">{{ zoomPercent() }}%</span>
            <button type="button" class="px-2 py-1 rounded hover:bg-slate-700" [attr.aria-label]="'OPAC.READER.ZOOM_IN' | translate" (click)="zoom(0.2)">+</button>
          </div>
          @if (auth.loggedIn()) {
            <button type="button" class="flex items-center gap-1 px-2 py-1 rounded hover:bg-slate-700" (click)="addBookmark()"><span class="material-icons text-[18px]">bookmark_add</span> {{ 'OPAC.READER.BOOKMARK' | translate }}</button>
            <button type="button" class="flex items-center gap-1 px-2 py-1 rounded hover:bg-slate-700" (click)="panel.set(!panel())"><span class="material-icons text-[18px]">bookmarks</span> {{ bookmarks().length }}</button>
          }
        }
      </header>

      <div class="flex flex-1 min-h-0">
        <div #scroller class="flex-1 overflow-auto p-4 select-none" (scroll)="onScroll()">
          @if (loading()) { <p class="text-center text-slate-500 mt-10" role="status">{{ 'OPAC.READER.LOADING' | translate: { percent: progress() } }}</p> }
          @if (error(); as err) {
            <div class="max-w-md mx-auto mt-10 bg-white rounded-xl border border-slate-200 p-6 text-center">
              <p class="text-red-600 text-sm" role="alert">{{ err }}</p>
              @if (fallbackUrl(); as url) { <a [href]="url" target="_blank" rel="noopener noreferrer" class="inline-block mt-3 text-blue-700 underline text-sm">{{ 'OPAC.READER.OPEN_TAB' | translate }}</a> }
            </div>
          }
          @for (n of pageNumbers(); track n) {
            <div class="mx-auto mb-4 bg-white shadow relative" [style.width.px]="boxW()" [style.height.px]="boxH()" [attr.data-page]="n">
              <canvas class="block w-full h-full" [attr.data-canvas]="n" [attr.aria-label]="('OPAC.READER.PAGE_N' | translate: { n })"></canvas>
            </div>
          }
        </div>

        @if (panel()) {
          <aside class="w-64 shrink-0 bg-white border-l border-slate-200 overflow-auto p-3 text-sm">
            <h2 class="font-semibold text-slate-900 mb-2">{{ 'OPAC.READER.BOOKMARKS' | translate }}</h2>
            <ul class="space-y-1">
              @for (b of bookmarks(); track b.publicId) {
                <li class="flex items-start gap-1">
                  <button type="button" class="flex-1 text-left rounded px-2 py-1 hover:bg-slate-50" (click)="go(b.page)">
                    <span class="font-medium">{{ 'OPAC.READER.PAGE_N' | translate: { n: b.page } }}</span>
                    @if (b.note) { <span class="block text-slate-500 text-xs">{{ b.note }}</span> }
                  </button>
                  <button type="button" class="text-red-600 px-1" [attr.aria-label]="'OPAC.READER.REMOVE' | translate" (click)="removeBookmark(b)"><span class="material-icons text-[16px]">close</span></button>
                </li>
              } @empty { <li class="text-slate-500">{{ 'OPAC.READER.NO_BOOKMARKS' | translate }}</li> }
            </ul>
          </aside>
        }
      </div>
    </div>
  `,
})
export class EbookReader implements OnInit {
  private readonly api = inject(DigitalApi);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(Language);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(ReaderAuth);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  protected itemId = '';
  private fileId = '';
  private readonly sessionKey = crypto.randomUUID();
  protected readonly item = signal<DigitalItemDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly progress = signal(0);
  protected readonly error = signal<string | null>(null);
  protected readonly fallbackUrl = signal<string | null>(null);
  protected readonly pages = signal(0);
  protected readonly page = signal(1);
  protected readonly scale = signal(1.2);
  protected readonly panel = signal(false);
  protected readonly bookmarks = signal<DigitalBookmark[]>([]);
  protected readonly zoomPercent = computed(() => Math.round(this.scale() * 100));
  protected readonly pageNumbers = computed(() => Array.from({ length: this.pages() }, (_, i) => i + 1));
  protected readonly boxW = signal(600);
  protected readonly boxH = signal(800);

  private pdf: PdfDocument | null = null;
  private observer: IntersectionObserver | null = null;
  private readonly rendered = new Set<number>();
  private renderToken = 0;
  private activeSeconds = 0;
  private lastBeatSeconds = -1;
  private readonly timers: ReturnType<typeof setInterval>[] = [];
  private watermark = '';

  ngOnInit(): void {
    this.itemId = this.route.snapshot.paramMap.get('publicId') ?? '';
    this.fileId = this.route.snapshot.paramMap.get('fileId') ?? '';
    this.destroyRef.onDestroy(() => this.stop());
    void this.start();
  }

  protected go(n: number): void {
    const target = Math.min(Math.max(1, Math.round(n) || 1), this.pages() || 1);
    this.page.set(target);
    this.scroller()?.nativeElement.querySelector(`[data-page="${target}"]`)?.scrollIntoView({ block: 'start' });
  }

  protected zoom(delta: number): void {
    this.scale.set(Math.min(3, Math.max(0.4, +(this.scale() + delta).toFixed(2))));
    void this.relayout();
  }

  protected onScroll(): void {
    const host = this.scroller()?.nativeElement;
    if (!host) return;
    const top = host.getBoundingClientRect().top;
    const nodes = host.querySelectorAll<HTMLElement>('[data-page]');
    for (const node of Array.from(nodes)) {
      if (node.getBoundingClientRect().bottom - top > host.clientHeight * 0.35) {
        this.page.set(Number(node.dataset['page']));
        return;
      }
    }
  }

  protected async addBookmark(): Promise<void> {
    try {
      await this.api.saveBookmark(this.itemId, this.page(), null);
      this.bookmarks.set(await this.api.bookmarks(this.itemId));
      this.panel.set(true);
    } catch (e) {
      this.error.set(this.language.apiError(e));
    }
  }

  protected async removeBookmark(b: DigitalBookmark): Promise<void> {
    await this.api.deleteBookmark(b.publicId);
    this.bookmarks.set(this.bookmarks().filter((x) => x.publicId !== b.publicId));
  }

  private async start(): Promise<void> {
    try {
      this.item.set(await this.api.detail(this.itemId));
      const url = await this.api.fileUrl(this.itemId, this.fileId, false);
      if (this.auth.loggedIn()) {
        void this.loadReaderState();
        this.timers.push(setInterval(() => this.tickSecond(), 1000), setInterval(() => void this.beat(), HEARTBEAT_MS));
      }
      const data = await this.download(url.url);
      if (!data) return;
      const pdfjs = await import('pdfjs-dist');
      pdfjs.GlobalWorkerOptions.workerSrc = new URL('assets/pdf.worker.min.mjs', window.location.origin).href;
      this.pdf = (await pdfjs.getDocument({ data, isEvalSupported: false }).promise) as unknown as PdfDocument;
      const first = await this.pdf.getPage(1);
      this.fitFirstPage(first);
      this.pages.set(this.pdf.numPages);
      this.loading.set(false);
      setTimeout(() => {
        this.observe();
        const resume = this.resumePage;
        if (resume > 1) this.go(resume);
      });
    } catch (e) {
      this.loading.set(false);
      this.error.set(this.language.apiError(e));
    }
  }

  private resumePage = 1;

  private async loadReaderState(): Promise<void> {
    try {
      const [progress, marks, me] = await Promise.all([this.api.readingProgress(this.itemId), this.api.bookmarks(this.itemId), this.auth.me()]);
      this.bookmarks.set(marks);
      this.watermark = `${me.cardNo} · ${me.fullName}`;
      if (progress && progress.lastPage > 1) {
        this.resumePage = progress.lastPage;
        if (this.pages() > 0) this.go(progress.lastPage);
      }
    } catch {
      /* không có tiến độ/dấu trang cũng đọc được */
    }
  }

  /** Tải cả file (có tiến độ) — file quá lớn thì mở ở tab mới thay vì đọc trong trang. */
  private async download(url: string): Promise<Uint8Array | null> {
    const response = await fetch(url);
    if (!response.ok || !response.body) throw new Error('OPAC.READER.LOAD_FAILED');
    const total = Number(response.headers.get('content-length') ?? 0);
    if (total > MAX_INLINE_BYTES) {
      this.loading.set(false);
      this.fallbackUrl.set(url);
      this.error.set(this.language.t('OPAC.READER.TOO_LARGE'));
      return null;
    }
    const reader = response.body.getReader();
    const chunks: Uint8Array[] = [];
    let received = 0;
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      chunks.push(value);
      received += value.length;
      if (received > MAX_INLINE_BYTES) throw new Error('OPAC.READER.TOO_LARGE');
      if (total) this.progress.set(Math.round((received / total) * 100));
    }
    const all = new Uint8Array(received);
    let offset = 0;
    for (const c of chunks) {
      all.set(c, offset);
      offset += c.length;
    }
    return all;
  }

  private fitFirstPage(first: PdfPage): void {
    const base = first.getViewport({ scale: 1 });
    const host = this.scroller()?.nativeElement;
    const avail = Math.max(280, (host?.clientWidth ?? window.innerWidth) - 40);
    this.scale.set(Math.min(1.4, Math.max(0.6, +(avail / base.width).toFixed(2))));
    this.boxW.set(Math.round(base.width * this.scale()));
    this.boxH.set(Math.round(base.height * this.scale()));
    this.baseW = base.width;
    this.baseH = base.height;
  }

  private baseW = 600;
  private baseH = 800;

  private async relayout(): Promise<void> {
    this.boxW.set(Math.round(this.baseW * this.scale()));
    this.boxH.set(Math.round(this.baseH * this.scale()));
    this.renderToken++;
    this.rendered.clear();
    const current = this.page();
    setTimeout(() => {
      this.observer?.disconnect();
      this.observe();
      this.go(current);
    });
  }

  private observe(): void {
    const host = this.scroller()?.nativeElement;
    if (!host) return;
    this.observer?.disconnect();
    this.observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) void this.renderPage(Number((entry.target as HTMLElement).dataset['page']));
      }
    }, { root: host, rootMargin: '600px 0px' });
    host.querySelectorAll('[data-page]').forEach((el) => this.observer!.observe(el));
  }

  private async renderPage(n: number): Promise<void> {
    const pdf = this.pdf;
    const canvas = this.scroller()?.nativeElement.querySelector<HTMLCanvasElement>(`canvas[data-canvas="${n}"]`);
    if (!pdf || !canvas || this.rendered.has(n)) return;
    this.rendered.add(n);
    const token = this.renderToken;
    const page = await pdf.getPage(n);
    if (token !== this.renderToken) return;
    const ratio = Math.min(2, window.devicePixelRatio || 1);
    const viewport = page.getViewport({ scale: this.scale() * ratio });
    canvas.width = Math.round(viewport.width);
    canvas.height = Math.round(viewport.height);
    const context = canvas.getContext('2d');
    if (!context) return;
    await page.render({ canvasContext: context, viewport }).promise;
    if (this.watermark) {
      context.save();
      context.globalAlpha = 0.12;
      context.fillStyle = '#334155';
      context.font = `${Math.round(canvas.width / 28)}px sans-serif`;
      context.translate(canvas.width / 2, canvas.height / 2);
      context.rotate(-Math.PI / 6);
      context.textAlign = 'center';
      context.fillText(this.watermark, 0, 0);
      context.restore();
    }
  }

  /** Mỗi giây đang xem trang (tab hiện, cửa sổ có tiêu điểm) tính là một giây đọc. */
  private tickSecond(): void {
    if (document.visibilityState === 'visible' && document.hasFocus()) this.activeSeconds++;
  }

  private async beat(): Promise<void> {
    if (!this.auth.loggedIn() || this.pages() === 0 || this.activeSeconds === this.lastBeatSeconds) return;
    this.lastBeatSeconds = this.activeSeconds;
    try {
      await this.api.readingBeat({
        ebookPublicId: this.itemId, filePublicId: this.fileId, sessionKey: this.sessionKey, page: this.page(), pageCount: this.pages(),
        activeSeconds: this.activeSeconds, device: window.innerWidth < 768 ? 'mobile' : 'desktop',
      });
    } catch {
      /* mất mạng thoáng qua — nhịp sau gửi lại tổng số giây */
    }
  }

  private stop(): void {
    void this.beat();
    this.timers.forEach(clearInterval);
    this.timers.length = 0;
    this.observer?.disconnect();
    void this.pdf?.destroy();
    this.pdf = null;
  }
}
