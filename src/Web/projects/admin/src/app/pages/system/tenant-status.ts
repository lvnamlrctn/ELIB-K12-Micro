import { Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TenantState } from '../../core/api';

/** Nhãn: khoá dịch TENANT_ADMIN.STATE_<trạng thái>. */
const LABELS: Record<TenantState, [string, string]> = {
  Provisioning: ['badge-warn', 'bg-amber-500'],
  Active: ['badge-on', 'bg-green-500'],
  Suspended: ['badge-danger', 'bg-red-500'],
  ProvisioningFailed: ['badge-danger', 'bg-red-500'],
};

@Component({
  selector: 'app-tenant-status',
  imports: [TranslatePipe],
  template: `<span [class]="view()[0]"><span class="w-1.5 h-1.5 rounded-full" [class]="view()[1]"></span>{{ 'TENANT_ADMIN.STATE_' + status() | translate }}</span>`,
})
export class TenantStatusBadge {
  readonly status = input.required<TenantState>();
  protected readonly view = computed(() => LABELS[this.status()] ?? ['badge-off', 'bg-gray-400']);
}
