import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** Chức năng chưa chuyển sang nền tảng mới (theo lộ trình docs 09) — giữ đường dẫn để menu/link không gãy. */
@Component({
  selector: 'opac-coming-soon',
  imports: [RouterLink, TranslatePipe],
  template: `
    <div class="max-w-lg mx-auto text-center py-24 px-4">
      <i class="fas fa-screwdriver-wrench text-gray-300 text-7xl"></i>
      <h1 class="mt-4 text-2xl font-semibold text-gray-900">{{ feature() | translate }}</h1>
      <p class="mt-2 text-gray-500">{{ 'OPAC.COMING_SOON.TEXT' | translate }}</p>
      <a routerLink="/" class="inline-flex items-center gap-1 mt-6 text-main hover:underline">
        <i class="fas fa-arrow-left text-[18px]"></i>{{ 'OPAC.COMMON.BACK_HOME' | translate }}
      </a>
    </div>
  `,
})
export class ComingSoon {
  /** Khoá dịch tên chức năng, từ data của route. */
  readonly feature = input('OPAC.COMING_SOON.FEATURE');
}
