import { Component, OnInit, input, signal } from '@angular/core';
import { CrudConfig, CrudPage } from '../../shared/crud-page';

/**
 * Vỏ nạp muộn cho các danh mục báo/tạp chí: cấu hình nằm ở <c>catalogs.ts</c> và chỉ tải khi mở một màn của phân hệ —
 * không làm phình gói khởi động của trang quản trị. Khoá `catalog` đến từ `data` của route.
 */
@Component({
  selector: 'app-serial-catalog-route',
  imports: [CrudPage],
  template: `@if (config(); as c) { <app-crud-page [config]="c" /> }`,
})
export class SerialCatalogRoute implements OnInit {
  readonly catalog = input.required<string>();

  protected readonly config = signal<CrudConfig | null>(null);

  async ngOnInit(): Promise<void> {
    const all = await import('./catalogs');
    const configs: Record<string, CrudConfig> = { types: all.SERIAL_TYPES, frequencies: all.SERIAL_FREQUENCIES, patterns: all.SERIAL_PATTERNS };
    this.config.set(configs[this.catalog()] ?? null);
  }
}
