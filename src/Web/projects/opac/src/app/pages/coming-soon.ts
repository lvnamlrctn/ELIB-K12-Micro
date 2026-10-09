import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Chức năng chưa chuyển sang nền tảng mới (theo lộ trình docs 09) — giữ đường dẫn để menu/link không gãy. */
@Component({
  selector: 'opac-coming-soon',
  imports: [RouterLink],
  template: `
    <div class="max-w-lg mx-auto text-center py-24 px-4">
      <span class="material-icons text-blue-200 text-7xl">construction</span>
      <h1 class="mt-4 text-2xl font-semibold text-slate-900">{{ feature() }}</h1>
      <p class="mt-2 text-slate-500">Chức năng đang được xây dựng trên hệ thống mới và sẽ sớm mở.</p>
      <a routerLink="/" class="inline-flex items-center gap-1 mt-6 text-blue-600 hover:underline">
        <span class="material-icons text-[18px]">arrow_back</span>Về trang chủ
      </a>
    </div>
  `,
})
export class ComingSoon {
  /** Từ data của route. */
  readonly feature = input('Chức năng');
}
