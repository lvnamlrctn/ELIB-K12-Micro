import { Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import qrcode from 'qrcode-generator';
import { Barcode } from '../../../../shared/barcode';
import { LDatePipe } from '../core/i18n';

/**
 * Thẻ thư viện điện tử (ELIB-LRC: opac-lrc/pages/reader-card): tên thư viện, họ tên, số thẻ, hạn thẻ, mã vạch Code 128 và mã QR của số thẻ
 * — bạn đọc đưa điện thoại cho quầy/cổng quét thay thẻ nhựa. QR vẽ SVG từ ma trận (không innerHTML).
 */
@Component({
  selector: 'opac-reader-card',
  imports: [Barcode, LDatePipe, TranslatePipe],
  template: `
    <div class="rounded-2xl overflow-hidden border border-gray-200 bg-white shadow-sm max-w-md" [attr.data-card]="cardNo()">
      <div class="bg-main text-white px-5 py-3 flex items-center gap-3">
        <i class="fas fa-building-columns"></i>
        <div class="min-w-0">
          <div class="text-xs uppercase tracking-wider opacity-90">{{ 'OPAC.MY_LIBRARY.E_CARD' | translate }}</div>
          <div class="font-semibold truncate">{{ library() }}</div>
        </div>
      </div>
      <div class="p-5 flex gap-4 items-start">
        <div class="min-w-0 flex-1 text-sm space-y-1">
          <div class="text-lg font-bold text-gray-900 uppercase leading-tight">{{ fullName() }}</div>
          <div class="text-gray-600">{{ 'OPAC.MY_LIBRARY.CARD_NO' | translate }}: <span class="font-mono font-semibold text-gray-800">{{ cardNo() }}</span></div>
          <div [class.text-red-600]="expired()" [class.text-gray-600]="!expired()">
            {{ 'OPAC.MY_LIBRARY.EXPIRE_DATE' | translate }}: {{ expireDate() ? (expireDate() | ldate) : ('OPAC.MY_LIBRARY.NO_EXPIRY' | translate) }}
          </div>
        </div>
        @if (qr(); as q) {
          <svg [attr.viewBox]="'0 0 ' + q.size + ' ' + q.size" class="w-24 h-24 shrink-0" shape-rendering="crispEdges" role="img" [attr.aria-label]="'QR ' + cardNo()">
            <rect [attr.width]="q.size" [attr.height]="q.size" fill="#fff" />
            @for (m of q.modules; track $index) { <rect [attr.x]="m[0]" [attr.y]="m[1]" width="1" height="1" fill="#000" /> }
          </svg>
        }
      </div>
      <div class="px-5 pb-4">
        <elib-barcode [value]="cardNo()" [height]="36" cssHeight="56px" />
        <div class="text-center font-mono tracking-widest text-sm text-gray-700 mt-1">{{ cardNo() }}</div>
      </div>
    </div>
  `,
})
export class ReaderCard {
  readonly cardNo = input.required<string>();
  readonly fullName = input.required<string>();
  readonly expireDate = input<string | null>(null);
  readonly library = input('');

  protected readonly expired = computed(() => !!this.expireDate() && this.expireDate()! < new Date().toISOString().slice(0, 10));

  /** Ma trận QR (mức sửa lỗi M) + vùng trắng 2 module. */
  protected readonly qr = computed(() => {
    try {
      const code = qrcode(0, 'M');
      code.addData(this.cardNo());
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
