import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TranslatePipe } from '@ngx-translate/core';
import { Language } from '../core/i18n';
import { PortalAlbum, PortalApi, PortalPhoto, PortalVideo } from '../core/portal';

/** Chỉ nhúng từ hai nhà cung cấp video được phép (cũng là hai nguồn mà CSP frame-src cho phép). */
const EMBED_PREFIXES = ['https://www.youtube-nocookie.com/embed/', 'https://player.vimeo.com/video/'];

/** Hình ảnh & video: album ảnh (mở xem từng ảnh) và video nhúng YouTube/Vimeo. */
@Component({
  selector: 'opac-gallery',
  imports: [TranslatePipe],
  template: `
    <div class="max-w-6xl mx-auto px-4 py-8">
      <h1 class="text-2xl font-bold text-slate-900">{{ 'OPAC.PORTAL.GALLERY_TITLE' | translate }}</h1>
      @if (loading()) { <div class="flex justify-center py-16"><span class="material-icons animate-spin text-blue-600 text-3xl">autorenew</span></div> }
      @if (error(); as err) { <p class="mt-4 text-red-600 text-sm" role="alert">{{ err }}</p> }

      @if (albums().length) {
        <h2 class="mt-6 font-semibold text-slate-900">{{ 'OPAC.PORTAL.ALBUMS' | translate }}</h2>
        <div class="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          @for (a of albums(); track a.publicId) {
            <button type="button" class="text-left group bg-white rounded-xl border border-slate-200 overflow-hidden hover:border-blue-300 hover:shadow-md transition" (click)="open(a)">
              @if (a.image) { <img [src]="a.image" alt="" loading="lazy" class="h-36 w-full object-cover bg-slate-100" /> }
              @else { <div class="h-36 bg-gradient-to-br from-blue-100 to-sky-50 flex items-center justify-center text-blue-400"><span class="material-icons text-5xl">photo_library</span></div> }
              <div class="p-3"><div class="font-medium text-slate-900 line-clamp-1">{{ a.name }}</div><div class="text-xs text-slate-500">{{ 'OPAC.PORTAL.PHOTOS_N' | translate: { count: a.photoCount } }}</div></div>
            </button>
          }
        </div>
      }

      @if (embeds().length) {
        <h2 class="mt-8 font-semibold text-slate-900">{{ 'OPAC.PORTAL.VIDEOS' | translate }}</h2>
        <div class="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (v of embeds(); track v.video.publicId) {
            <figure class="bg-white rounded-xl border border-slate-200 overflow-hidden">
              <iframe [src]="v.src" [title]="v.video.name" class="w-full aspect-video" loading="lazy" referrerpolicy="strict-origin-when-cross-origin" allow="fullscreen; picture-in-picture" allowfullscreen></iframe>
              <figcaption class="p-3 text-sm font-medium text-slate-900">{{ v.video.name }}</figcaption>
            </figure>
          }
        </div>
      }

      @if (!loading() && !albums().length && !embeds().length && !error()) { <p class="py-16 text-center text-slate-500">{{ 'OPAC.PORTAL.NO_GALLERY' | translate }}</p> }
    </div>

    @if (current(); as album) {
      <div class="fixed inset-0 z-50 bg-black/80 flex flex-col" role="dialog" aria-modal="true" [attr.aria-label]="album.name">
        <div class="flex items-center justify-between px-4 py-3 text-white">
          <div class="font-medium">{{ album.name }}</div>
          <button type="button" class="p-1 rounded hover:bg-white/10" [attr.aria-label]="'OPAC.READER.BACK' | translate" (click)="current.set(null)"><span class="material-icons">close</span></button>
        </div>
        <div class="flex-1 overflow-auto p-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3 content-start">
          @for (p of photos(); track p.publicId) {
            <figure class="bg-white/5 rounded-lg overflow-hidden"><img [src]="p.image" [alt]="p.name" loading="lazy" class="w-full max-h-[70vh] object-contain" />
              <figcaption class="px-3 py-2 text-sm text-white">{{ p.name }}@if (p.brief) { <span class="block text-xs text-slate-300">{{ p.brief }}</span> }</figcaption></figure>
          } @empty { <p class="text-slate-300 col-span-full text-center py-10">{{ 'OPAC.PORTAL.NO_GALLERY' | translate }}</p> }
        </div>
      </div>
    }
  `,
})
export class Gallery implements OnInit {
  private readonly api = inject(PortalApi);
  private readonly language = inject(Language);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly albums = signal<PortalAlbum[]>([]);
  protected readonly videos = signal<PortalVideo[]>([]);
  protected readonly photos = signal<PortalPhoto[]>([]);
  protected readonly current = signal<PortalAlbum | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const lang = this.language.lang();
    Promise.all([this.api.albums(lang), this.api.videos(lang)]).then(
      ([albums, videos]) => {
        this.albums.set(albums);
        this.videos.set(videos);
        this.loading.set(false);
      },
      (e) => {
        this.error.set(this.language.apiError(e));
        this.loading.set(false);
      });
  }

  protected async open(album: PortalAlbum): Promise<void> {
    this.current.set(album);
    this.photos.set([]);
    try {
      this.photos.set(await this.api.photos(album.publicId));
    } catch (e) {
      this.error.set(this.language.apiError(e));
    }
  }

  /**
   * Địa chỉ nhúng chỉ được tin khi nằm trong danh sách nguồn cho phép — server đã dựng từ mã video đã kiểm, đây là lớp kiểm thứ hai. Tính một lần cho mỗi
   * lần tải danh sách (tạo lại SafeResourceUrl mỗi lần vẽ sẽ làm iframe tải lại liên tục).
   */
  protected readonly embeds = computed(() => this.videos()
    .filter((v) => EMBED_PREFIXES.some((p) => v.embedUrl.startsWith(p)))
    .map((v) => ({ video: v, src: this.sanitizer.bypassSecurityTrustResourceUrl(v.embedUrl) as SafeResourceUrl })));
}
