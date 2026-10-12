import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, PagedResult, TenantState, TenantSummary, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { TranslatePipe } from '@ngx-translate/core';
import { Loading, Paginator } from '../../shared/ui';
import { TenantStatusBadge } from './tenant-status';

/** Danh sách đơn vị (quản trị nền tảng) — bố cục thẻ lọc + thanh nút + bảng như các màn admin cũ. */
@Component({
  selector: 'app-tenants-list',
  imports: [FormsModule, RouterLink, TenantStatusBadge, Paginator, Loading, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'TENANT_ADMIN.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">{{ 'TENANT_ADMIN.SEARCH_LABEL' | translate }}</label>
          <input id="kw" class="input" [(ngModel)]="search" (keydown.enter)="load(1)" [placeholder]="'TENANT_ADMIN.SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="st" class="input" [(ngModel)]="status" (change)="load(1)">
            <option value="">{{ 'COMMON.ALL' | translate }}</option>
            <option value="Active">{{ 'TENANT_ADMIN.STATE_Active' | translate }}</option>
            <option value="Provisioning">{{ 'TENANT_ADMIN.STATE_Provisioning' | translate }}</option>
            <option value="ProvisioningFailed">{{ 'TENANT_ADMIN.STATE_ProvisioningFailed' | translate }}</option>
            <option value="Suspended">{{ 'TENANT_ADMIN.STATE_Suspended' | translate }}</option>
          </select>
        </div>
        <div class="hidden xl:block"></div>
        <div class="flex items-end">
          <button (click)="load(1)" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <a routerLink="/tenant/create" class="btn-add"><span class="material-icons text-[18px]">add_business</span> {{ 'TENANT_ADMIN.CREATE' | translate }}</a>
      <span class="text-sm text-gray-500">{{ 'TENANT_ADMIN.COUNT' | translate: { count: result()?.total ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[720px]">
          <thead>
            <tr>
              <th class="th w-16 !text-center">ID</th>
              <th class="th w-40">{{ 'TENANT_ADMIN.CODE' | translate }}</th>
              <th class="th">{{ 'TENANT_ADMIN.NAME' | translate }}</th>
              <th class="th">{{ 'TENANT_ADMIN.DOMAIN' | translate }}</th>
              <th class="th w-40 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (t of result()?.items ?? []; track t.publicId) {
              <tr class="hover:bg-gray-50/50 transition-colors">
                <td class="td text-center text-gray-500">{{ t.id }}</td>
                <td class="td font-mono text-[13px]"><a [routerLink]="['/tenant', t.publicId]" class="text-blue-600 hover:underline">{{ t.code }}</a></td>
                <td class="td font-medium text-gray-800">{{ t.name }}</td>
                <td class="td text-gray-500">{{ t.subdomain }}.{{ baseDomain }}</td>
                <td class="td text-center"><app-tenant-status [status]="t.status" /></td>
                <td class="td">
                  <div class="flex justify-center">
                    <a [routerLink]="['/tenant', t.publicId]" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'TENANT_ADMIN.DETAIL' | translate">
                      <span class="material-icons text-[18px]">visibility</span>
                    </a>
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('TENANT_ADMIN.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (result(); as r) {
        <app-paginator [total]="r.total" [pageIndex]="r.page" [pageSize]="r.pageSize" [sizes]="[20]" (pageChange)="load($event.pageIndex)" />
      }
    </div>
  `,
})
export class TenantsList implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly baseDomain = location.hostname;
  protected search = '';
  protected status: TenantState | '' = '';
  protected readonly result = signal<PagedResult<TenantSummary> | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    try {
      this.result.set(await this.api.tenants(this.search.trim(), this.status, page));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
