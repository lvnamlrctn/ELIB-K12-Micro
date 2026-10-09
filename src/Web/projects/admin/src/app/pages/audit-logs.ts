import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, AuditLog, CrudPage, errorMessage } from '../core/api';
import { ToastrService } from '../shared/toastr';
import { Loading, Paginator } from '../shared/ui';

/** Tên tiếng Việt của mã thao tác trong nhật ký (mã lạ hiện nguyên văn). */
const ACTION_LABELS: Record<string, string> = {
  ADD: 'Thêm mới',
  UPDATE: 'Sửa',
  DELETE: 'Xoá',
  CHANGE_STATUS: 'Đổi trạng thái',
  LOGIN: 'Đăng nhập',
  LOGIN_FAILED: 'Đăng nhập thất bại',
  PASSWORD_CHANGE: 'Đổi mật khẩu',
  USER_CREATE: 'Tạo tài khoản',
  USER_UPDATE: 'Sửa tài khoản',
  USER_ROLES: 'Gán vai trò',
  USER_RESET_PASSWORD: 'Đặt lại mật khẩu',
  USER_UNLOCK: 'Mở khoá tài khoản',
  ROLE_CREATE: 'Tạo vai trò',
  ROLE_UPDATE: 'Sửa vai trò',
  ROLE_DELETE: 'Xoá vai trò',
  IMPERSONATION_STARTED: 'Quản trị nền tảng vào xem',
  TENANT_CREATE: 'Tạo đơn vị',
  TENANT_UPDATE: 'Sửa đơn vị',
  TENANT_SUSPEND: 'Tạm ngưng đơn vị',
  TENANT_ACTIVATE: 'Kích hoạt đơn vị',
  TENANT_LICENSES: 'Cập nhật phân hệ',
  TENANT_PROVISION_RETRY: 'Khởi tạo lại đơn vị',
  TENANT_RESYNC: 'Đồng bộ đơn vị',
  TENANT_ADMIN_CREATE: 'Tạo quản trị đơn vị',
};

const DANGER = new Set(['DELETE', 'LOGIN_FAILED', 'ROLE_DELETE', 'TENANT_SUSPEND', 'USER_RESET_PASSWORD']);
const WARN = new Set(['IMPERSONATION_STARTED', 'USER_ROLES', 'ROLE_UPDATE', 'ROLE_CREATE', 'TENANT_LICENSES', 'PASSWORD_CHANGE']);

/**
 * Nhật ký hệ thống (monolith: system-log, UserLog). scope "tenant" = nhật ký của đơn vị (quyền SYSTEM_LOG);
 * "platform" = nhật ký nền tảng của quản trị nền tảng (host hệ thống). Chỉ xem.
 */
@Component({
  selector: 'app-audit-logs',
  imports: [FormsModule, DatePipe, Loading, Paginator],
  template: `
    <div class="mb-5">
      <h4 class="page-title">{{ platform() ? 'Nhật ký nền tảng' : 'Nhật ký hệ thống' }}</h4>
      <p class="text-sm text-gray-500 mt-1">
        {{ platform() ? 'Thao tác của quản trị nền tảng: đơn vị, phân hệ, đăng nhập, vào xem đơn vị' : 'Đăng nhập và thao tác thêm/sửa/xoá của cán bộ trong đơn vị' }}
      </p>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-5 gap-4">
        <div class="xl:col-span-2">
          <label for="kw" class="field-label">Nội dung / người thực hiện / IP</label>
          <input id="kw" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" placeholder="Nhập từ khoá..." />
        </div>
        <div>
          <label for="act" class="field-label">Thao tác</label>
          <select id="act" class="input" [(ngModel)]="action" (change)="search()">
            <option [ngValue]="null">Tất cả</option>
            @for (a of actions(); track a) { <option [ngValue]="a">{{ label(a) }}</option> }
          </select>
        </div>
        <div>
          <label for="from" class="field-label">Từ ngày</label>
          <input id="from" type="date" class="input" [(ngModel)]="from" (change)="search()" />
        </div>
        <div>
          <label for="to" class="field-label">Đến ngày</label>
          <input id="to" type="date" class="input" [(ngModel)]="to" (change)="search()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="search()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead>
            <tr>
              <th class="th w-44">Thời gian</th>
              <th class="th w-56">Người thực hiện</th>
              <th class="th w-44">Thao tác</th>
              <th class="th">Nội dung</th>
              @if (platform()) { <th class="th w-48">Đơn vị</th> }
              <th class="th w-36">IP</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.publicId) {
              <tr class="hover:bg-gray-50/50 align-top">
                <td class="td text-gray-500 whitespace-nowrap">{{ row.occurredAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
                <td class="td">{{ row.actorName || (row.actorKind === 'system' ? 'Hệ thống' : '—') }}</td>
                <td class="td"><span [class]="badge(row.action)">{{ label(row.action) }}</span></td>
                <td class="td"><div class="line-clamp-3 break-words" [title]="row.summary ?? ''">{{ row.summary || '—' }}</div>
                  <div class="text-[11px] text-gray-400 mt-0.5">{{ row.service }} · {{ row.entityType }}</div></td>
                @if (platform()) { <td class="td">{{ row.tenantName || '—' }}</td> }
                <td class="td font-mono text-[12px] text-gray-500">{{ row.ipAddress || '—' }}</td>
              </tr>
            } @empty {
              <tr><td [attr.colspan]="platform() ? 6 : 5" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : 'Không có dữ liệu' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class AuditLogs implements OnInit {
  /** Từ data của route: 'tenant' | 'platform'. */
  readonly scope = input<'tenant' | 'platform'>('tenant');

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected keyword = '';
  protected action: string | null = null;
  protected from = '';
  protected to = '';
  protected pageIndex = 1;
  protected pageSize = 25;
  protected readonly page = signal<CrudPage<AuditLog> | null>(null);
  protected readonly actions = signal<string[]>([]);
  protected readonly loading = signal(false);

  protected platform(): boolean {
    return this.scope() === 'platform';
  }

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.actions.set(await this.api.auditActions(this.platform()));
    } catch {
      // Ô lọc thao tác để trống — không chặn xem nhật ký.
    }
  }

  protected label(action: string): string {
    return ACTION_LABELS[action] ?? action;
  }

  protected badge(action: string): string {
    return DANGER.has(action) ? 'badge-danger' : WARN.has(action) ? 'badge-warn' : action === 'LOGIN' ? 'badge-info' : 'badge-off';
  }

  protected search(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      // Ngày theo giờ trình duyệt: "Đến ngày" tính hết ngày đó.
      const from = this.from ? new Date(this.from + 'T00:00:00').toISOString() : null;
      const to = this.to ? new Date(this.to + 'T23:59:59.999').toISOString() : null;
      this.page.set(await this.api.auditLogs(this.platform(), {
        keyword: this.keyword.trim() || undefined, action: this.action, from, to, pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
