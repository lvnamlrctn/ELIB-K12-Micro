import { DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Library } from '../core/library';
import { PortalMenus } from '../core/portal';
import { ReaderAuth } from '../core/reader-auth';
import { Nav } from './nav';

/** Chân trang OPAC (giao diện ELIB-TVXA): nền chủ đạo, 3 cột (liên hệ / liên kết nhanh / thống kê truy cập) và dòng bản quyền. */
@Component({
  selector: 'opac-footer',
  imports: [RouterLink, DecimalPipe, TranslatePipe],
  template: `
    <footer class="bg-main text-white pt-12 pb-6 mt-16">
      <div class="container mx-auto px-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-10">
          <div>
            <h4 class="text-lg font-bold mb-4 border-b-2 border-white pb-2 inline-block">{{ library.name() }}</h4>
            @if (p()['LIBRARY_ADDR']; as v) { <div class="text-sm mb-2 whitespace-pre-line"><i class="fas fa-location-dot w-5"></i> {{ v }}</div> }
            @if (p()['LIBRARY_TEL']; as v) { <p class="text-sm mb-2"><i class="fas fa-phone w-5"></i> {{ v }}</p> }
            @if (p()['LIBRARY_EMAIL']; as v) { <p class="text-sm mb-2"><i class="fas fa-envelope w-5"></i> {{ v }}</p> }
            @if (p()['OPENHOUR']; as v) { <p class="text-sm whitespace-pre-line"><i class="fas fa-clock w-5"></i> {{ v }}</p> }
          </div>
          <div>
            <h4 class="text-lg font-bold mb-4 border-b-2 border-white pb-2 inline-block">{{ 'OPAC.FOOTER.QUICK_LINKS' | translate }}</h4>
            <ul class="list-none p-0 m-0">
              @if (library.has('SEARCH')) { <li class="mb-2"><a routerLink="/tim-kiem" class="text-white/80 hover:text-white transition-colors">{{ 'OPAC.NAVBAR.SEARCH_LINK' | translate }}</a></li> }
              @if (library.has('DIGITAL')) { <li class="mb-2"><a routerLink="/thu-vien-so" class="text-white/80 hover:text-white transition-colors">{{ 'OPAC.NAVBAR.EBOOK' | translate }}</a></li> }
              @if (library.has('PORTAL')) {
                @for (m of footerMenu(); track m.id) {
                  <li class="mb-2"><a [href]="m.href" [target]="m.newTab ? '_blank' : null" [rel]="m.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, m)" class="text-white/80 hover:text-white transition-colors">{{ m.name }}</a></li>
                }
                <li class="mb-2"><a routerLink="/hinh-anh-video" class="text-white/80 hover:text-white transition-colors">{{ 'OPAC.PORTAL.GALLERY_TITLE' | translate }}</a></li>
              }
              <li><a href="/admin/" class="text-white/80 hover:text-white transition-colors">{{ 'OPAC.FOOTER.ADMIN' | translate }}</a></li>
            </ul>
          </div>
          <div>
            @if (portal.counter(); as c) {
              <h4 class="text-lg font-bold mb-4 border-b-2 border-white pb-2 inline-block">{{ 'OPAC.FOOTER.STATS' | translate }}</h4>
              <div class="bg-white/10 p-4 rounded">
                <div class="flex justify-between mb-2 text-sm"><span>{{ 'OPAC.FOOTER.TODAY' | translate }}:</span> <strong>{{ c.today | number }}</strong></div>
                <div class="flex justify-between mb-2 text-sm"><span>{{ 'OPAC.FOOTER.LAST_7_DAYS' | translate }}:</span> <strong>{{ c.last7Days | number }}</strong></div>
                <div class="flex justify-between text-sm"><span>{{ 'OPAC.FOOTER.TOTAL' | translate }}:</span> <strong>{{ c.total | number }}</strong></div>
              </div>
            }
          </div>
        </div>
        <div class="text-center mt-10 pt-6 border-t border-white/20 text-sm">
          <div>&copy; {{ year }} {{ library.name() }}</div>
          <div class="mt-1 text-white/70 text-xs">{{ 'OPAC.FOOTER.POWERED_BY' | translate }}</div>
        </div>
      </div>
    </footer>
  `,
})
export class Footer {
  protected readonly library = inject(Library);
  protected readonly portal = inject(PortalMenus);
  protected readonly nav = inject(Nav);
  private readonly auth = inject(ReaderAuth);
  protected readonly p = this.library.parameters;
  protected readonly year = new Date().getFullYear();
  protected readonly footerMenu = computed(() => this.portal.items('footer').filter((m) => !m.requiresLogin || this.auth.loggedIn()));
}
