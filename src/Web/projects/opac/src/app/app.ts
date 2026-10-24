import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Library } from './core/library';
import { PortalMenus } from './core/portal';
import { ReaderAuth } from './core/reader-auth';
import { Footer } from './layout/footer';
import { Header } from './layout/header';

/** Khung OPAC (giao diện ELIB-TVXA): đầu trang, nội dung, chân trang. */
@Component({
  selector: 'opac-root',
  imports: [RouterOutlet, TranslatePipe, Header, Footer],
  template: `
    <div class="min-h-screen flex flex-col bg-gray-50 font-sans text-gray-800">
      <opac-header />
      <main class="flex-grow">
        @if (!library.loaded()) {
          <div class="flex justify-center py-32"><i class="fas fa-circle-notch fa-spin text-main text-4xl"></i></div>
        } @else if (library.error(); as error) {
          <div class="max-w-md mx-auto text-center py-24 px-4">
            <i class="fas fa-building-circle-xmark text-gray-300 text-6xl"></i>
            <p class="mt-4 text-lg text-gray-700">{{ error | translate }}</p>
          </div>
        } @else {
          <router-outlet />
        }
      </main>
      <opac-footer />
    </div>
  `,
})
export class App implements OnInit {
  protected readonly library = inject(Library);
  private readonly auth = inject(ReaderAuth);
  private readonly portal = inject(PortalMenus);

  ngOnInit(): void {
    void this.library.load().then(() => {
      if (this.library.has('PORTAL')) void this.portal.countVisit();
    });
    void this.auth.restore();
  }
}
