import { Component, Input, inject } from '@angular/core';
import { Auth } from '../../services/auth';

// Khối ký xác nhận cuối báo cáo in (ngày lập / người lập báo cáo / thủ trưởng đơn vị) — dùng chung
// cho mọi trang báo cáo, đặt bên trong khối in "hidden print:block" đã có sẵn ở từng trang, ngay
// sau bảng dữ liệu in.
@Component({
  selector: 'app-report-signoff',
  standalone: true,
  template: `
    <div class="mt-8 text-sm">
      <p class="text-right mb-6">{{ place }}, {{ formatVnDate() }}</p>
      <div class="flex justify-around text-center">
        <div>
          <div class="font-bold uppercase">{{ preparerLabel }}</div>
          <div class="italic">(Ký, ghi rõ họ tên)</div>
          <div class="mt-16 font-semibold">{{ resolvedPreparerName() }}</div>
        </div>
        <div>
          <div class="font-bold uppercase">{{ unitHeadLabel }}</div>
          <div class="italic">(Ký, ghi rõ họ tên)</div>
        </div>
      </div>
    </div>
  `
})
export class ReportSignoff {
  private auth = inject(Auth);

  @Input() preparerName?: string;
  @Input() reportDate: Date = new Date();
  @Input() place = 'Thái Nguyên';
  @Input() preparerLabel = 'NGƯỜI LẬP BÁO CÁO';
  @Input() unitHeadLabel = 'THỦ TRƯỞNG ĐƠN VỊ';

  resolvedPreparerName(): string {
    return this.preparerName || this.auth.getUser()?.name || this.auth.getUser()?.loginName || '';
  }

  formatVnDate(): string {
    const d = this.reportDate;
    return `ngày ${d.getDate().toString().padStart(2, '0')} tháng ${(d.getMonth() + 1).toString().padStart(2, '0')} năm ${d.getFullYear()}`;
  }
}
