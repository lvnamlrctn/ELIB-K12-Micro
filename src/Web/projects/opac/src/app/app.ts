import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { Library } from './core/library';

/** Khung OPAC: thanh trên (logo + tên thư viện + đăng nhập), nội dung, chân trang có thông tin liên hệ. */
@Component({
  selector: 'opac-root',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header class="bg-white border-b border-slate-200 sticky top-0 z-30">
      <div class="max-w-6xl mx-auto px-4 h-16 flex items-center gap-3">
        <a routerLink="/" class="flex items-center gap-3 min-w-0">
          @if (library.features()?.logoUrl; as logo) {
            <img [src]="logo" alt="" class="w-10 h-10 object-contain shrink-0" />
          } @else {
            <span class="w-10 h-10 rounded-lg bg-blue-600 text-white flex items-center justify-center shrink-0">
              <span class="material-icons">local_library</span>
            </span>
          }
          <span class="font-semibold text-slate-900 truncate">{{ library.shortName() }}</span>
        </a>
        <span class="flex-1"></span>
        <nav class="hidden md:flex items-center gap-1 text-sm">
          <a routerLink="/" class="px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-100">Trang chủ</a>
          @if (library.has('SEARCH')) {
            <a routerLink="/tim-kiem" class="px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-100">Tra cứu</a>
          }
          @if (library.has('DIGITAL')) {
            <a routerLink="/thu-vien-so" class="px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-100">Thư viện số</a>
          }
        </nav>
        <a routerLink="/tai-khoan" class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700">
          <span class="material-icons text-[18px]">person</span><span class="hidden sm:inline">Đăng nhập</span>
        </a>
      </div>
    </header>

    <main class="min-h-[calc(100vh-4rem)]">
      @if (!library.loaded()) {
        <div class="flex justify-center py-32"><span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span></div>
      } @else if (library.error(); as error) {
        <div class="max-w-md mx-auto text-center py-24 px-4">
          <span class="material-icons text-slate-300 text-6xl">domain_disabled</span>
          <p class="mt-4 text-lg text-slate-700">{{ error }}</p>
        </div>
      } @else {
        <router-outlet />
      }
    </main>

    <footer class="bg-slate-900 text-slate-300 text-sm">
      <div class="max-w-6xl mx-auto px-4 py-8 grid gap-6 md:grid-cols-3">
        <div>
          <div class="font-semibold text-white mb-2">{{ library.name() }}</div>
          @if (p()['LIBRARY_ADDR']; as v) {
            <div class="flex gap-2"><span class="material-icons text-[18px]">place</span>{{ v }}</div>
          }
        </div>
        <div class="space-y-1">
          @if (p()['LIBRARY_TEL']; as v) {
            <div class="flex gap-2"><span class="material-icons text-[18px]">call</span>{{ v }}</div>
          }
          @if (p()['LIBRARY_EMAIL']; as v) {
            <div class="flex gap-2"><span class="material-icons text-[18px]">mail</span>{{ v }}</div>
          }
          @if (p()['OPENHOUR']; as v) {
            <div class="flex gap-2"><span class="material-icons text-[18px]">schedule</span>{{ v }}</div>
          }
        </div>
        <div class="md:text-right text-slate-400">
          <a href="/admin/" class="hover:text-white">Quản trị thư viện</a>
          <div class="mt-2 text-xs">Vận hành trên nền tảng ELIB K12</div>
        </div>
      </div>
    </footer>
  `,
})
export class App implements OnInit {
  protected readonly library = inject(Library);
  protected readonly p = this.library.parameters;

  ngOnInit(): void {
    void this.library.load();
  }
}
