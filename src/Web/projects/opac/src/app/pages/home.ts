import { Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Library } from '../core/library';
import { PortalHomeBlock } from './portal-home';

interface Service { module: string; title: string; text: string; icon: string; link: string; }

/** Dịch vụ OPAC theo phân hệ đơn vị đã mua — phân hệ chưa mua không hiện (features của service tenant). */
/** title/text: khoá dịch OPAC.HOME.SERVICE_<MODULE>(_TEXT). */
const SERVICES: Service[] = [
  { module: 'SEARCH', title: 'OPAC.HOME.SERVICE_SEARCH', text: 'OPAC.HOME.SERVICE_SEARCH_TEXT', icon: 'search', link: '/tim-kiem' },
  { module: 'DIGITAL', title: 'OPAC.HOME.SERVICE_DIGITAL', text: 'OPAC.HOME.SERVICE_DIGITAL_TEXT', icon: 'menu_book', link: '/thu-vien-so' },
  { module: 'CIRCULATION', title: 'OPAC.HOME.SERVICE_CIRCULATION', text: 'OPAC.HOME.SERVICE_CIRCULATION_TEXT', icon: 'swap_horiz', link: '/tai-khoan' },
  { module: 'SPACE', title: 'OPAC.HOME.SERVICE_SPACE', text: 'OPAC.HOME.SERVICE_SPACE_TEXT', icon: 'meeting_room', link: '/dat-phong' },
  { module: 'AI', title: 'OPAC.HOME.SERVICE_AI', text: 'OPAC.HOME.SERVICE_AI_TEXT', icon: 'smart_toy', link: '/tro-ly' },
  { module: 'PORTAL', title: 'OPAC.HOME.SERVICE_PORTAL', text: 'OPAC.HOME.SERVICE_PORTAL_TEXT', icon: 'campaign', link: '/tin-tuc' },
];

const CONTACT_KEYS = ['LIBRARY_ADDR', 'OPENHOUR', 'LIBRARY_TEL', 'LIBRARY_EMAIL'];

@Component({
  selector: 'opac-home',
  imports: [FormsModule, RouterLink, TranslatePipe, PortalHomeBlock],
  template: `
    <section class="bg-gradient-to-br from-blue-700 via-blue-600 to-sky-500 text-white">
      <div class="max-w-6xl mx-auto px-4 py-16 md:py-24">
        <h1 class="text-3xl md:text-4xl font-bold leading-tight max-w-3xl">{{ library.name() }}</h1>
        <p class="mt-3 text-blue-100 max-w-2xl">{{ 'OPAC.HOME.HERO_SUBTITLE' | translate }}</p>
        @if (library.has('SEARCH')) {
          <form (ngSubmit)="search()" class="mt-8 flex max-w-2xl bg-white rounded-xl shadow-lg overflow-hidden">
            <span class="material-icons text-slate-400 self-center pl-4">search</span>
            <input name="q" [(ngModel)]="q" class="flex-1 px-3 py-4 text-slate-800 outline-none min-w-0"
                   [placeholder]="'OPAC.HOME.SEARCH_PLACEHOLDER' | translate" [attr.aria-label]="'OPAC.SEARCH.KEYWORD_ARIA' | translate" />
            <button type="submit" class="px-6 bg-blue-600 hover:bg-blue-700 font-medium">{{ 'OPAC.HOME.SEARCH_BTN' | translate }}</button>
          </form>
        }
      </div>
    </section>

    @if (library.has('PORTAL')) { <opac-portal-home /> }

    <section class="max-w-6xl mx-auto px-4 py-12">
      <h2 class="text-xl font-semibold text-slate-900 mb-6">{{ 'OPAC.HOME.SERVICES_TITLE' | translate }}</h2>
      @if (services().length) {
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (s of services(); track s.module) {
            <a [routerLink]="s.link" class="group bg-white rounded-xl border border-slate-200 p-5 hover:border-blue-300 hover:shadow-md transition">
              <span class="w-11 h-11 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center group-hover:bg-blue-600 group-hover:text-white transition">
                <span class="material-icons">{{ s.icon }}</span>
              </span>
              <div class="mt-4 font-semibold text-slate-900">{{ s.title | translate }}</div>
              <p class="mt-1 text-sm text-slate-500">{{ s.text | translate }}</p>
            </a>
          }
        </div>
      } @else {
        <p class="text-slate-500">{{ 'OPAC.HOME.NO_SERVICES' | translate }}</p>
      }
    </section>

    @if (hasContact()) {
      <section class="max-w-6xl mx-auto px-4 pb-16">
        <div class="bg-white rounded-xl border border-slate-200 p-6 grid gap-4 md:grid-cols-3 text-sm">
          @if (p()['LIBRARY_ADDR']; as v) {
            <div><div class="text-slate-500 mb-1">{{ 'OPAC.FOOTER.ADDRESS' | translate }}</div><div class="font-medium">{{ v }}</div></div>
          }
          @if (p()['OPENHOUR']; as v) {
            <div><div class="text-slate-500 mb-1">{{ 'OPAC.FOOTER.OPEN_HOURS' | translate }}</div><div class="font-medium whitespace-pre-line">{{ v }}</div></div>
          }
          @if (p()['LIBRARY_TEL'] || p()['LIBRARY_EMAIL']) {
            <div>
              <div class="text-slate-500 mb-1">{{ 'OPAC.FOOTER.CONTACT_INFO' | translate }}</div>
              <div class="font-medium">{{ p()['LIBRARY_TEL'] }}</div>
              <div>{{ p()['LIBRARY_EMAIL'] }}</div>
            </div>
          }
        </div>
      </section>
    }
  `,
})
export class Home {
  protected readonly library = inject(Library);
  private readonly router = inject(Router);
  protected readonly p = this.library.parameters;
  protected q = '';

  protected readonly services = computed(() => SERVICES.filter((s) => this.library.has(s.module)));
  protected readonly hasContact = computed(() => CONTACT_KEYS.some((k) => this.p()[k]));

  protected search(): void {
    void this.router.navigate(['/tim-kiem'], { queryParams: { q: this.q.trim() || null } });
  }
}
