import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

/** Đường dẫn trang (giao diện ELIB-TVXA): Trang chủ › [mục cha] › trang hiện tại. */
@Component({
  selector: 'opac-breadcrumb',
  imports: [RouterLink, TranslatePipe],
  template: `
    <nav class="flex items-center text-sm text-gray-500 mb-6 bg-white p-3 rounded shadow-sm border-l-4 border-main" [attr.aria-label]="'OPAC.COMMON.BREADCRUMB' | translate">
      <a routerLink="/" class="hover:text-main flex items-center shrink-0"><i class="fas fa-home mr-2"></i><span>{{ 'OPAC.NAVBAR.HOME' | translate }}</span></a>
      @if (parentLabel(); as label) {
        <i class="fas fa-chevron-right mx-3 text-xs text-gray-300"></i>
        <a [routerLink]="parentLink()" class="hover:text-main shrink-0">{{ label | translate }}</a>
      }
      @if (current()) {
        <i class="fas fa-chevron-right mx-3 text-xs text-gray-300"></i>
        <span class="text-main font-semibold truncate">{{ current() }}</span>
      }
    </nav>
  `,
})
export class Breadcrumb {
  /** Tên trang hiện tại (đã dịch / tiêu đề tài liệu). */
  readonly current = input<string | null | undefined>('');
  /** Khoá dịch của mục cha, bỏ trống nếu trang nằm ngay dưới Trang chủ. */
  readonly parentLabel = input<string | null>(null);
  readonly parentLink = input('/');
}
