import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LANGS, Language } from '../core/i18n';
import { Library } from '../core/library';
import { PortalMenuItem, PortalMenus } from '../core/portal';
import { ReaderAuth } from '../core/reader-auth';
import { Nav } from './nav';

/** Đầu trang OPAC (giao diện ELIB-TVXA): logo tròn + tên, menu có nhóm con rê chuột, tài khoản, quản trị, ngôn ngữ; thu gọn thành menu trượt trên điện thoại. */
@Component({
  selector: 'opac-header',
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <header class="bg-white shadow-md sticky top-0 z-50">
      <div class="container mx-auto px-4 h-20 flex justify-between items-center">
        <a routerLink="/" class="flex items-center min-w-0" (click)="closeMenu()">
          @if (library.features()?.logoUrl; as logo) {
            <img [src]="logo" alt="" class="w-12 h-12 object-contain mr-3 shrink-0" />
          } @else {
            <div class="w-12 h-12 bg-main rounded-full flex items-center justify-center text-white font-bold text-base mr-3 shrink-0 overflow-hidden">{{ initials() }}</div>
          }
          <div class="flex flex-col min-w-0">
            <h1 class="text-main font-bold text-lg leading-tight m-0 truncate">{{ library.name() }}</h1>
            <p class="text-accent font-bold text-sm m-0 truncate">{{ 'OPAC.NAVBAR.SUBTITLE' | translate }}</p>
          </div>
        </a>

        <button type="button" class="md:hidden text-gray-600 hover:text-main focus:outline-none p-2" (click)="mobileOpen.set(!mobileOpen())"
                [attr.aria-label]="'OPAC.NAVBAR.MENU' | translate" [attr.aria-expanded]="mobileOpen()">
          <i class="fas text-2xl" [class.fa-bars]="!mobileOpen()" [class.fa-xmark]="mobileOpen()"></i>
        </button>

        <nav class="hidden md:flex h-full" [attr.aria-label]="'OPAC.NAVBAR.MENU' | translate">
          <ul class="flex h-full m-0 p-0 list-none">
            <li class="h-full">
              <a routerLink="/" routerLinkActive="text-main border-main" [routerLinkActiveOptions]="{ exact: true }" class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">{{ 'OPAC.NAVBAR.HOME' | translate }}</a>
            </li>
            @if (library.has('SEARCH')) {
              <li class="h-full">
                <a routerLink="/tim-kiem" routerLinkActive="text-main border-main" class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">{{ 'OPAC.NAVBAR.SEARCH_LINK' | translate }}</a>
              </li>
            }
            @if (library.has('DIGITAL')) {
              <li class="h-full">
                <a routerLink="/thu-vien-so" routerLinkActive="text-main border-main" class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">{{ 'OPAC.NAVBAR.EBOOK' | translate }}</a>
              </li>
            }
            @for (m of menu(); track m.id) {
              <li class="h-full relative group">
                <a [href]="m.href" [target]="m.newTab ? '_blank' : null" [rel]="m.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, m)"
                   class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">
                  {{ m.name }}@if (m.children.length) { <i class="fas fa-chevron-down ml-1 text-xs"></i> }
                </a>
                @if (m.children.length) {
                  <ul class="absolute top-full left-0 w-56 bg-white shadow-lg rounded-b-lg opacity-0 invisible group-hover:opacity-100 group-hover:visible group-focus-within:opacity-100 group-focus-within:visible transition-all duration-300 flex flex-col py-2 z-50 list-none m-0">
                    @for (c of m.children; track c.id) {
                      <li class="relative group/sub">
                        <a [href]="c.href" [target]="c.newTab ? '_blank' : null" [rel]="c.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, c)"
                           class="flex justify-between items-center px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-main">
                          {{ c.name }}@if (c.children.length) { <i class="fas fa-chevron-right text-xs"></i> }
                        </a>
                        @if (c.children.length) {
                          <ul class="absolute top-0 left-full w-48 bg-white shadow-lg rounded-lg opacity-0 invisible group-hover/sub:opacity-100 group-hover/sub:visible group-focus-within/sub:opacity-100 group-focus-within/sub:visible transition-all duration-300 flex flex-col py-2 z-50 -ml-1 list-none m-0">
                            @for (s of c.children; track s.id) {
                              <li><a [href]="s.href" [target]="s.newTab ? '_blank' : null" [rel]="s.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, s)"
                                     class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-main">{{ s.name }}</a></li>
                            }
                          </ul>
                        }
                      </li>
                    }
                  </ul>
                }
              </li>
            }
            @if (auth.loggedIn()) {
              <li class="h-full relative group">
                <button type="button" class="flex items-center h-full px-4 text-main font-semibold hover:text-blue-700 transition-colors border-b-2 border-transparent focus:outline-none max-w-56">
                  <i class="fas fa-circle-user mr-2 text-lg"></i><span class="truncate">{{ auth.displayName() }}</span><i class="fas fa-chevron-down ml-1 text-xs"></i>
                </button>
                <ul class="absolute top-full right-0 w-48 bg-white shadow-lg rounded-b-lg opacity-0 invisible group-hover:opacity-100 group-hover:visible group-focus-within:opacity-100 group-focus-within:visible transition-all duration-300 flex flex-col py-2 z-50 list-none m-0">
                  <li><a routerLink="/tai-khoan" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">{{ 'OPAC.NAVBAR.MY_ACCOUNT' | translate }}</a></li>
                  <li><button type="button" (click)="logout()" class="w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-gray-100">{{ 'OPAC.NAVBAR.LOGOUT' | translate }}</button></li>
                </ul>
              </li>
            } @else {
              <li class="h-full">
                <a routerLink="/tai-khoan" routerLinkActive="text-main border-main" class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">{{ 'OPAC.NAVBAR.LOGIN' | translate }}</a>
              </li>
            }
            <li class="h-full">
              <a href="/admin/" class="flex items-center h-full px-4 text-dark font-semibold hover:text-main transition-colors border-b-2 border-transparent">
                <i class="fas fa-user-shield mr-1"></i> {{ 'OPAC.NAVBAR.ADMIN' | translate }}
              </a>
            </li>
            <li class="h-full relative group ml-2 items-center flex">
              <button type="button" class="flex items-center h-8 px-3 rounded-md bg-gray-100 text-dark font-semibold hover:bg-main hover:text-white transition-colors focus:outline-none text-sm"
                      [attr.aria-label]="'OPAC.NAVBAR.LANGUAGE' | translate">
                <i class="fas fa-globe mr-1"></i> <span class="uppercase">{{ language.lang() }}</span> <i class="fas fa-chevron-down ml-1 text-[10px]"></i>
              </button>
              <ul class="absolute top-full right-0 mt-1 w-24 bg-white shadow-lg rounded-lg opacity-0 invisible group-hover:opacity-100 group-hover:visible group-focus-within:opacity-100 group-focus-within:visible transition-all duration-300 flex flex-col overflow-hidden z-50 border border-gray-100 list-none p-0 m-0">
                @for (l of langs; track l) {
                  <li><button type="button" (click)="language.use(l)" class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 hover:text-main transition-colors uppercase"
                              [class.bg-blue-50]="language.lang() === l" [attr.aria-pressed]="language.lang() === l">{{ l }}</button></li>
                }
              </ul>
            </li>
          </ul>
        </nav>
      </div>

      <div class="md:hidden bg-white border-t border-gray-100 overflow-hidden transition-all duration-300 ease-in-out" [class.max-h-0]="!mobileOpen()" [class.max-h-screen]="mobileOpen()" [class.overflow-y-auto]="mobileOpen()">
        <ul class="flex flex-col m-0 p-0 list-none py-2">
          @if (auth.loggedIn()) {
            <li class="px-6 py-4 bg-gray-50 border-b border-gray-100 flex items-center justify-between gap-3">
              <div class="flex items-center min-w-0">
                <div class="w-10 h-10 bg-main rounded-full flex items-center justify-center text-white font-bold mr-3 shrink-0">{{ auth.displayName().charAt(0) }}</div>
                <div class="font-bold text-dark truncate">{{ auth.displayName() }}</div>
              </div>
              <a routerLink="/tai-khoan" (click)="closeMenu()" class="text-sm text-main hover:text-blue-700 font-semibold whitespace-nowrap">{{ 'OPAC.NAVBAR.MY_ACCOUNT' | translate }}</a>
            </li>
          }
          <li><a routerLink="/" (click)="closeMenu()" routerLinkActive="text-main bg-gray-50" [routerLinkActiveOptions]="{ exact: true }" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ 'OPAC.NAVBAR.HOME' | translate }}</a></li>
          @if (library.has('SEARCH')) {
            <li><a routerLink="/tim-kiem" (click)="closeMenu()" routerLinkActive="text-main bg-gray-50" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ 'OPAC.NAVBAR.SEARCH_LINK' | translate }}</a></li>
          }
          @if (library.has('DIGITAL')) {
            <li><a routerLink="/thu-vien-so" (click)="closeMenu()" routerLinkActive="text-main bg-gray-50" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ 'OPAC.NAVBAR.EBOOK' | translate }}</a></li>
          }
          @for (m of menu(); track m.id) {
            <li>
              @if (m.children.length) {
                <button type="button" (click)="toggleDropdown(m.id)" class="w-full flex justify-between items-center px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent focus:outline-none"
                        [attr.aria-expanded]="openId() === m.id">
                  {{ m.name }}<i class="fas transition-transform duration-300" [class.fa-chevron-down]="openId() !== m.id" [class.fa-chevron-up]="openId() === m.id"></i>
                </button>
                <ul class="bg-gray-50 overflow-hidden transition-all duration-300 list-none m-0 p-0" [class.max-h-0]="openId() !== m.id" [class.max-h-[500px]]="openId() === m.id">
                  @for (c of m.children; track c.id) {
                    <li>
                      <a [href]="c.href" [target]="c.newTab ? '_blank' : null" [rel]="c.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, c); closeMenu()" class="block px-10 py-2 text-sm text-gray-600 hover:text-main">{{ c.name }}</a>
                      @for (s of c.children; track s.id) {
                        <a [href]="s.href" [target]="s.newTab ? '_blank' : null" [rel]="s.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, s); closeMenu()" class="block pl-14 pr-10 py-2 text-sm text-gray-500 hover:text-main">{{ s.name }}</a>
                      }
                    </li>
                  }
                </ul>
              } @else {
                <a [href]="m.href" [target]="m.newTab ? '_blank' : null" [rel]="m.newTab ? 'noopener noreferrer' : null" (click)="nav.go($event, m); closeMenu()" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ m.name }}</a>
              }
            </li>
          }
          @if (auth.loggedIn()) {
            <li><button type="button" (click)="logout()" class="w-full text-left px-6 py-3 text-red-600 font-semibold hover:bg-gray-50 border-l-4 border-transparent hover:border-red-600">{{ 'OPAC.NAVBAR.LOGOUT' | translate }}</button></li>
          } @else {
            <li><a routerLink="/tai-khoan" (click)="closeMenu()" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ 'OPAC.NAVBAR.LOGIN' | translate }}</a></li>
          }
          <li><a href="/admin/" class="block px-6 py-3 text-dark font-semibold hover:bg-gray-50 hover:text-main border-l-4 border-transparent hover:border-main">{{ 'OPAC.NAVBAR.ADMIN' | translate }}</a></li>
          <li class="px-6 py-3 flex gap-2" role="group" [attr.aria-label]="'OPAC.NAVBAR.LANGUAGE' | translate">
            @for (l of langs; track l) {
              <button type="button" (click)="language.use(l)" [attr.aria-pressed]="language.lang() === l"
                      class="px-3 py-1.5 rounded-md text-sm font-semibold uppercase" [class]="language.lang() === l ? 'bg-main text-white' : 'bg-gray-100 text-dark'">{{ l }}</button>
            }
          </li>
        </ul>
      </div>
    </header>
  `,
})
export class Header {
  protected readonly library = inject(Library);
  protected readonly auth = inject(ReaderAuth);
  protected readonly language = inject(Language);
  protected readonly nav = inject(Nav);
  private readonly portal = inject(PortalMenus);
  protected readonly langs = LANGS;

  protected readonly mobileOpen = signal(false);
  protected readonly openId = signal<number | null>(null);
  protected readonly menu = computed<PortalMenuItem[]>(() =>
    this.library.has('PORTAL') ? this.portal.items('main').filter((m) => !m.requiresLogin || this.auth.loggedIn()) : []);
  protected readonly initials = computed(() => (this.library.shortName() || '?').slice(0, 4));

  protected toggleDropdown(id: number): void {
    this.openId.update((v) => (v === id ? null : id));
  }

  protected closeMenu(): void {
    this.mobileOpen.set(false);
    this.openId.set(null);
  }

  protected logout(): void {
    this.closeMenu();
    void this.auth.logout();
  }
}
