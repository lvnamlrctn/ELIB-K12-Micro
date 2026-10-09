import { Component, computed, input } from '@angular/core';
import { TenantState } from '../../core/api';

const LABELS: Record<TenantState, [string, string, string]> = {
  Provisioning: ['Đang khởi tạo', 'badge-warn', 'bg-amber-500'],
  Active: ['Hoạt động', 'badge-on', 'bg-green-500'],
  Suspended: ['Tạm ngưng', 'badge-danger', 'bg-red-500'],
  ProvisioningFailed: ['Khởi tạo lỗi', 'badge-danger', 'bg-red-500'],
};

@Component({
  selector: 'app-tenant-status',
  template: `<span [class]="view()[1]"><span class="w-1.5 h-1.5 rounded-full" [class]="view()[2]"></span>{{ view()[0] }}</span>`,
})
export class TenantStatusBadge {
  readonly status = input.required<TenantState>();
  protected readonly view = computed(() => LABELS[this.status()] ?? [this.status(), 'badge-off', 'bg-gray-400']);
}
