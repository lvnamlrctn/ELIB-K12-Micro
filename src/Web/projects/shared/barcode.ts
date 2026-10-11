import { Component, computed, input } from '@angular/core';
import { code128Bars } from './code128';

/**
 * Mã vạch Code 128 vẽ bằng SVG (in rõ ở mọi độ phân giải, không cần canvas). Dùng chung cho Admin (in thẻ) và OPAC (thẻ điện tử).
 * Chiều rộng co giãn theo khung chứa; data-code giữ chuỗi đã mã hoá để kiểm thử giao diện.
 */
@Component({
  selector: 'elib-barcode',
  template: `
    @if (bars(); as b) {
      <svg [attr.viewBox]="'0 0 ' + b.width + ' ' + height()" preserveAspectRatio="none" class="block w-full" [style.height]="cssHeight()"
           role="img" [attr.aria-label]="value()" [attr.data-code]="value()" [attr.data-widths]="widths()">
        <rect [attr.width]="b.width" [attr.height]="height()" fill="#fff" />
        @for (bar of b.bars; track $index) { <rect [attr.x]="bar.x" [attr.width]="bar.w" [attr.height]="height()" fill="#000" /> }
      </svg>
    }
  `,
})
export class Barcode {
  readonly value = input.required<string>();
  /** Chiều cao theo đơn vị module (tỉ lệ với chiều rộng vạch). */
  readonly height = input(40);
  /** Chiều cao hiển thị (CSS), ví dụ '10mm' khi in. */
  readonly cssHeight = input('48px');

  protected readonly bars = computed(() => {
    try {
      return code128Bars(this.value());
    } catch {
      return null; // ký tự không mã hoá được — không vẽ
    }
  });

  /** Dãy độ rộng vạch/khoảng trắng (bỏ vùng trắng) — để test giải mã lại từ giao diện. */
  protected readonly widths = computed(() => {
    const b = this.bars();
    if (!b) return '';
    const out: number[] = [];
    b.bars.forEach((bar, i) => {
      if (i > 0) out.push(bar.x - (b.bars[i - 1].x + b.bars[i - 1].w));
      out.push(bar.w);
    });
    return out.join(',');
  });
}
