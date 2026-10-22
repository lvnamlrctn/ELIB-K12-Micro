import { Component, computed, input } from '@angular/core';
import qrcode from 'qrcode-generator';

/**
 * Mã QR vẽ bằng SVG từ ma trận (không innerHTML, không canvas) — dùng chung cho Admin (thu phí tại quầy) và OPAC (Phí / Nợ của tôi).
 * Mức sửa lỗi M, vùng trắng 2 module để app ngân hàng quét dễ; chuỗi quá dài không mã hoá được thì không vẽ.
 */
@Component({
  selector: 'elib-qr',
  template: `
    @if (matrix(); as m) {
      <svg [attr.viewBox]="'0 0 ' + m.size + ' ' + m.size" class="block" [style.width]="cssSize()" [style.height]="cssSize()" shape-rendering="crispEdges"
           role="img" [attr.aria-label]="label()" [attr.data-qr-length]="value().length">
        <rect [attr.width]="m.size" [attr.height]="m.size" fill="#fff" />
        @for (p of m.modules; track $index) { <rect [attr.x]="p[0]" [attr.y]="p[1]" width="1" height="1" fill="#000" /> }
      </svg>
    }
  `,
})
export class QrCode {
  readonly value = input.required<string>();
  readonly cssSize = input('220px');
  readonly label = input('QR');

  protected readonly matrix = computed(() => {
    try {
      const code = qrcode(0, 'M');
      code.addData(this.value());
      code.make();
      const count = code.getModuleCount();
      const quiet = 2;
      const modules: [number, number][] = [];
      for (let r = 0; r < count; r++) for (let c = 0; c < count; c++) if (code.isDark(r, c)) modules.push([c + quiet, r + quiet]);
      return { size: count + quiet * 2, modules };
    } catch {
      return null;
    }
  });
}
