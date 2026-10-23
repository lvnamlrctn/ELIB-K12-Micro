import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LDatePipe, Language } from '../core/i18n';
import { PortalApi, PortalHome } from '../core/portal';

/**
 * Khối nội dung cổng thông tin trên trang chủ OPAC (khi đơn vị có phân hệ Cổng thông tin): banner trượt, tin nổi bật & mới nhất, quảng cáo, liên kết.
 * Chưa có nội dung nào thì không hiện gì (trang chủ giữ nguyên như trước). Banner/quảng cáo mở liên kết ở tab mới.
 */
@Component({
  selector: 'opac-portal-home',
  imports: [RouterLink, LDatePipe, TranslatePipe],
  template: `
    @if (home(); as h) {
      @if (h.banners.length) {
        <section class="relative bg-slate-100" [attr.aria-label]="'OPAC.PORTAL.BANNERS' | translate">
          <div class="max-w-6xl mx-auto relative overflow-hidden">
            @for (b of h.banners; track b.publicId; let i = $index) {
              @if (i === slide()) {
                @if (b.link) {
                  <a [href]="b.link" target="_blank" rel="noopener noreferrer"><img [src]="b.image" [alt]="b.name" class="w-full max-h-[360px] object-cover" /></a>
                } @else { <img [src]="b.image" [alt]="b.name" class="w-full max-h-[360px] object-cover" /> }
              }
            }
            @if (h.banners.length > 1) {
              <div class="absolute bottom-2 left-0 right-0 flex justify-center gap-1.5">
                @for (b of h.banners; track b.publicId; let i = $index) {
                  <button type="button" class="w-2.5 h-2.5 rounded-full" [class]="i === slide() ? 'bg-white' : 'bg-white/50'" [attr.aria-label]="b.name" (click)="slide.set(i)"></button>
                }
              </div>
            }
          </div>
        </section>
      }

      @if (h.latest.length) {
        <section class="max-w-6xl mx-auto px-4 pt-10">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-xl font-semibold text-slate-900">{{ 'OPAC.PORTAL.LATEST_NEWS' | translate }}</h2>
            <a routerLink="/tin-tuc" class="text-sm text-blue-600 hover:underline">{{ 'OPAC.PORTAL.ALL_NEWS' | translate }}</a>
          </div>
          <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            @for (n of h.latest.slice(0, 4); track n.publicId) {
              <a [routerLink]="['/tin-tuc', n.slug]" class="group bg-white rounded-xl border border-slate-200 overflow-hidden hover:border-blue-300 hover:shadow-md transition">
                @if (n.thumb || n.images; as img) { <img [src]="img" alt="" loading="lazy" class="h-32 w-full object-cover bg-slate-100" /> }
                @else { <div class="h-32 bg-gradient-to-br from-blue-100 to-sky-50 flex items-center justify-center text-blue-400"><span class="material-icons text-4xl">article</span></div> }
                <div class="p-3"><div class="text-xs text-slate-500">{{ n.publishedAt | ldate }}</div><div class="mt-1 font-medium text-slate-900 group-hover:text-blue-700 line-clamp-2">{{ n.title }}</div></div>
              </a>
            }
          </div>
        </section>
      }

      @if (h.adGroups.length) {
        <section class="max-w-6xl mx-auto px-4 pt-8 flex flex-wrap gap-4 justify-center" [attr.aria-label]="'OPAC.PORTAL.ADS' | translate">
          @for (g of h.adGroups; track g.publicId) {
            @for (a of g.ads; track a.publicId) {
              @if (a.link) { <a [href]="a.link" target="_blank" rel="noopener noreferrer sponsored" (click)="adClicked(a.publicId)"><img [src]="a.image" [alt]="a.name" [style.max-width.px]="a.width" class="rounded-lg" /></a> }
              @else { <img [src]="a.image" [alt]="a.name" [style.max-width.px]="a.width" class="rounded-lg" /> }
            }
          }
        </section>
      }

      @if (h.linkGroups.length) {
        <section class="max-w-6xl mx-auto px-4 pt-8">
          @for (g of h.linkGroups; track g.publicId) {
            <h2 class="font-semibold text-slate-900 mb-2">{{ g.name }}</h2>
            <div class="flex flex-wrap gap-3 mb-4">
              @for (l of g.links; track l.publicId) {
                <a [href]="l.url" target="_blank" rel="noopener noreferrer" class="inline-flex items-center gap-2 bg-white border border-slate-200 rounded-lg px-3 py-2 text-sm text-slate-700 hover:border-blue-300 hover:text-blue-700">
                  @if (l.image) { <img [src]="l.image" alt="" class="h-6 w-auto" /> }{{ l.name }}</a>
              }
            </div>
          }
        </section>
      }
    }
  `,
})
export class PortalHomeBlock implements OnInit, OnDestroy {
  private readonly api = inject(PortalApi);

  adClicked(publicId: string): void { this.api.adClick(publicId); }
  private readonly language = inject(Language);

  protected readonly home = signal<PortalHome | null>(null);
  protected readonly slide = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.api.home(this.language.lang()).then((h) => {
      this.home.set(h);
      if (h.banners.length > 1) this.timer = setInterval(() => this.slide.update((i) => (i + 1) % h.banners.length), 6000);
    }, () => this.home.set(null));
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }
}
