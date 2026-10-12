import { TranslatePipe } from '@ngx-translate/core';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, TenantSummary } from '../core/api';
import { MODULE_NAMES, Session } from '../core/session';
import { TenantStatusBadge } from './system/tenant-status';

/** label: khoá dịch; valueKey: value cũng là khoá dịch ("Toàn quyền", "Hoạt động"). */
interface Stat { label: string; value: string | number; valueKey?: boolean; icon: string; color: string; link?: string; }

/** Bảng điều khiển — thẻ số liệu và lối tắt kiểu dashboard admin cũ (số liệu nghiệp vụ có khi các phân hệ được port). */
@Component({
  selector: 'app-home',
  imports: [RouterLink, TenantStatusBadge, TranslatePipe],
  template: `
    <div class="mb-5">
      <h4 class="page-title">{{ 'PAGE_TITLE.HOME' | translate }}</h4>
      <p class="text-sm text-gray-500 mt-1">{{ 'HOME_PAGE.HELLO' | translate: { name: session.me()?.fullName } }}</p>
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-5">
      @for (s of stats(); track s.label) {
        <a [routerLink]="s.link ?? null" class="bg-white rounded-xl shadow-sm border border-gray-100 p-5 flex items-center gap-4 hover:shadow-md transition-shadow">
          <div class="w-12 h-12 rounded-xl flex items-center justify-center" [class]="s.color">
            <span class="material-icons">{{ s.icon }}</span>
          </div>
          <div class="min-w-0">
            <div class="text-2xl font-bold text-gray-800 truncate">{{ s.valueKey ? ($any(s.value) | translate) : s.value }}</div>
            <div class="text-sm text-gray-500">{{ s.label | translate }}</div>
          </div>
        </a>
      }
    </div>

    @if (session.isSystem()) {
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h5 class="font-bold text-gray-800">{{ 'HOME_PAGE.TENANTS' | translate }}</h5>
          <a routerLink="/tenant" class="text-sm text-blue-600 hover:underline">{{ 'HOME_PAGE.VIEW_ALL' | translate }}</a>
        </div>
        <table class="w-full text-left border-collapse">
          <thead><tr><th class="th">{{ 'COMMON.CODE' | translate }}</th><th class="th">{{ 'TENANT_ADMIN.NAME' | translate }}</th><th class="th w-40">{{ 'COMMON.STATUS' | translate }}</th></tr></thead>
          <tbody>
            @for (t of tenants(); track t.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono"><a [routerLink]="['/tenant', t.publicId]" class="text-blue-600 hover:underline">{{ t.code }}</a></td>
                <td class="td">{{ t.name }}</td>
                <td class="td"><app-tenant-status [status]="t.status" /></td>
              </tr>
            } @empty {
              <tr><td colspan="3" class="py-8 text-center text-gray-500 text-sm">{{ 'HOME_PAGE.NO_TENANTS' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div class="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
        <h5 class="font-bold text-gray-800 mb-4">{{ 'HOME_PAGE.MODULES' | translate }}</h5>
        @if (modules().length) {
          <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-3">
            @for (m of modules(); track m.code) {
              <a [routerLink]="['/phan-he', m.code]" class="flex items-center gap-3 p-3 rounded-lg border border-gray-100 hover:border-blue-200 hover:bg-blue-50/50 transition-colors">
                <span class="material-icons text-blue-600">extension</span>
                <span class="text-sm font-medium text-gray-700">{{ m.name | translate }}</span>
              </a>
            }
          </div>
        } @else {
          <p class="text-sm text-gray-500">{{ 'HOME_PAGE.NO_MODULES' | translate }}</p>
        }
      </div>
    }
  `,
})
export class Home implements OnInit {
  private readonly api = inject(Api);
  protected readonly session = inject(Session);
  protected readonly tenants = signal<TenantSummary[]>([]);
  private readonly counts = signal<Record<string, number> | null>(null);

  protected readonly modules = computed(() => (this.session.features()?.modules ?? []).map((code) => ({ code, name: MODULE_NAMES[code] ?? code })));

  protected readonly stats = computed<Stat[]>(() => {
    if (this.session.isSystem()) {
      const c = this.counts();
      const n = (key: string): number | string => (c ? c[key] : '…');
      return [
        { label: 'HOME_PAGE.STAT_TENANTS', value: n('all'), icon: 'business', color: 'bg-blue-50 text-blue-600', link: '/tenant' },
        { label: 'HOME_PAGE.STAT_ACTIVE', value: n('Active'), icon: 'check_circle', color: 'bg-emerald-50 text-emerald-600' },
        { label: 'HOME_PAGE.STAT_PROVISIONING', value: n('Provisioning'), icon: 'hourglass_top', color: 'bg-amber-50 text-amber-600' },
        { label: 'HOME_PAGE.STAT_PROBLEM', value: c ? c['Suspended'] + c['ProvisioningFailed'] : '…', icon: 'block', color: 'bg-red-50 text-red-600' },
      ];
    }
    const f = this.session.features();
    return [
      { label: 'HOME_PAGE.STAT_TENANT', value: f?.code ?? '', icon: 'corporate_fare', color: 'bg-blue-50 text-blue-600' },
      { label: 'HOME_PAGE.STAT_MODULES', value: f?.modules.length ?? 0, icon: 'apps', color: 'bg-emerald-50 text-emerald-600' },
      { label: 'HOME_PAGE.STAT_PERMISSIONS', value: this.session.me()?.permissions.includes('*') ? 'HOME_PAGE.ALL_PERMISSIONS' : (this.session.me()?.permissions.length ?? 0),
        valueKey: !!this.session.me()?.permissions.includes('*'), icon: 'verified_user', color: 'bg-purple-50 text-purple-600' },
      { label: 'COMMON.STATUS', value: f?.status === 'Active' ? 'HOME_PAGE.ACTIVE' : (f?.status ?? ''), valueKey: f?.status === 'Active', icon: 'monitor_heart', color: 'bg-amber-50 text-amber-600' },
    ];
  });

  async ngOnInit(): Promise<void> {
    if (!this.session.isSystem()) return;
    try {
      const statuses = ['Active', 'Provisioning', 'Suspended', 'ProvisioningFailed'] as const;
      const [page, ...totals] = await Promise.all([this.api.tenants(undefined, '', 1), ...statuses.map((s) => this.api.tenantCount(s))]);
      this.tenants.set(page.items.slice(0, 8));
      this.counts.set({ all: page.total, ...Object.fromEntries(statuses.map((s, i) => [s, totals[i]])) });
    } catch {
      /* bảng điều khiển không chặn khi API lỗi */
    }
  }
}
