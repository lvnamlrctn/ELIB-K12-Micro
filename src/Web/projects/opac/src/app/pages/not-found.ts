import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'opac-not-found',
  imports: [RouterLink],
  template: `
    <div class="max-w-lg mx-auto text-center py-24 px-4">
      <div class="text-6xl font-bold text-slate-200">404</div>
      <p class="mt-4 text-slate-600">Không tìm thấy trang bạn cần.</p>
      <a routerLink="/" class="inline-flex items-center gap-1 mt-6 text-blue-600 hover:underline">
        <span class="material-icons text-[18px]">home</span>Về trang chủ
      </a>
    </div>
  `,
})
export class NotFound {}
