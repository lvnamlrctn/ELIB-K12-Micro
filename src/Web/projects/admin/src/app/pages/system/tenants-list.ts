import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, PagedResult, TenantState, TenantSummary, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';
import { TenantStatusBadge } from './tenant-status';

/** Danh sách đơn vị (quản trị nền tảng) — bố cục thẻ lọc + thanh nút + bảng như các màn admin cũ. */
@Component({
  selector: 'app-tenants-list',
  imports: [FormsModule, RouterLink, TenantStatusBadge, Paginator, Loading],
  template: `
    <div class="mb-5"><h4 class="page-title">Đơn vị</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">Mã / tên / tên miền</label>
          <input id="kw" class="input" [(ngModel)]="search" (keydown.enter)="load(1)" placeholder="Nhập mã, tên, tên miền..." />
        </div>
        <div>
          <label for="st" class="field-label">Trạng thái</label>
          <select id="st" class="input" [(ngModel)]="status" (change)="load(1)">
            <option value="">Tất cả</option>
            <option value="Active">Hoạt động</option>
            <option value="Provisioning">Đang khởi tạo</option>
            <option value="ProvisioningFailed">Khởi tạo lỗi</option>
            <option value="Suspended">Tạm ngưng</option>
          </select>
        </div>
        <div class="hidden xl:block"></div>
        <div class="flex items-end">
          <button (click)="load(1)" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <a routerLink="/tenant/create" class="btn-add"><span class="material-icons text-[18px]">add_business</span> Tạo đơn vị</a>
      <span class="text-sm text-gray-500">{{ result()?.total ?? 0 }} đơn vị</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[720px]">
          <thead>
            <tr>
              <th class="th w-16 !text-center">ID</th>
              <th class="th w-40">Mã đơn vị</th>
              <th class="th">Tên đơn vị</th>
              <th class="th">Tên miền</th>
              <th class="th w-40 !text-center">Trạng thái</th>
              <th class="th w-24 !text-center">Thao tác</th>
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
                    <a [routerLink]="['/tenant', t.publicId]" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Chi tiết">
                      <span class="material-icons text-[18px]">visibility</span>
                    </a>
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Chưa có đơn vị nào.' }}</td></tr>
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
