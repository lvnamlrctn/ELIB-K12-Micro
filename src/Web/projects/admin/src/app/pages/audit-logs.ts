import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, AuditLog, CrudPage, errorMessage } from '../core/api';
import { LDatePipe } from '../core/i18n';
import { ToastrService } from '../shared/toastr';
import { Loading, Paginator } from '../shared/ui';

/** Mã thao tác có tên dịch (AUDIT.ACTION.<mã>); mã lạ hiện nguyên văn. */
const KNOWN_ACTIONS = new Set([
  'ADD', 'UPDATE', 'DELETE', 'CHANGE_STATUS', 'IMPORT', 'LOGIN', 'LOGIN_FAILED', 'PASSWORD_CHANGE', 'USER_CREATE', 'USER_UPDATE', 'USER_ROLES', 'USER_RESET_PASSWORD', 'USER_UNLOCK', 'ROLE_CREATE', 'ROLE_UPDATE', 'ROLE_DELETE', 'IMPERSONATION_STARTED', 'TENANT_CREATE', 'TENANT_UPDATE', 'TENANT_SUSPEND', 'TENANT_ACTIVATE', 'TENANT_LICENSES', 'TENANT_PROVISION_RETRY', 'TENANT_RESYNC', 'TENANT_ADMIN_CREATE',
]);

const DANGER = new Set(['DELETE', 'LOGIN_FAILED', 'ROLE_DELETE', 'TENANT_SUSPEND', 'USER_RESET_PASSWORD']);
const WARN = new Set(['IMPERSONATION_STARTED', 'USER_ROLES', 'ROLE_UPDATE', 'ROLE_CREATE', 'TENANT_LICENSES', 'PASSWORD_CHANGE']);

/**
 * Nhật ký hệ thống (monolith: system-log, UserLog). scope "tenant" = nhật ký của đơn vị (quyền SYSTEM_LOG);
 * "platform" = nhật ký nền tảng của quản trị nền tảng (host hệ thống). Chỉ xem.
 */
@Component({
  selector: 'app-audit-logs',
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5">
      <h4 class="page-title">{{ (platform() ? 'AUDIT.TITLE_PLATFORM' : scope() === 'catalog' ? 'AUDIT.TITLE_CATALOG' : 'AUDIT.TITLE_TENANT') | translate }}</h4>
      <p class="text-sm text-gray-500 mt-1">
        {{ (platform() ? 'AUDIT.SUB_PLATFORM' : scope() === 'catalog' ? 'AUDIT.SUB_CATALOG' : 'AUDIT.SUB_TENANT') | translate }}
      </p>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-5 gap-4">
        <div class="xl:col-span-2">
          <label for="kw" class="field-label">{{ 'AUDIT.SEARCH_LABEL' | translate }}</label>
          <input id="kw" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" [placeholder]="'AUDIT.SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="act" class="field-label">{{ 'AUDIT.ACTION_LABEL' | translate }}</label>
          <select id="act" class="input" [(ngModel)]="action" (change)="search()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (a of actions(); track a) { <option [ngValue]="a">{{ label(a) | translate }}</option> }
          </select>
        </div>
        <div>
          <label for="from" class="field-label">{{ 'AUDIT.FROM' | translate }}</label>
          <input id="from" type="date" class="input" [(ngModel)]="from" (change)="search()" />
        </div>
        <div>
          <label for="to" class="field-label">{{ 'AUDIT.TO' | translate }}</label>
          <input id="to" type="date" class="input" [(ngModel)]="to" (change)="search()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="search()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead>
            <tr>
              <th class="th w-44">{{ 'AUDIT.TIME' | translate }}</th>
              <th class="th w-56">{{ 'AUDIT.ACTOR' | translate }}</th>
              <th class="th w-44">{{ 'AUDIT.ACTION_LABEL' | translate }}</th>
              <th class="th">{{ 'AUDIT.CONTENT' | translate }}</th>
              @if (platform()) { <th class="th w-48">{{ 'AUDIT.TENANT' | translate }}</th> }
              <th class="th w-36">IP</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.publicId) {
              <tr class="hover:bg-gray-50/50 align-top">
                <td class="td text-gray-500 whitespace-nowrap">{{ row.occurredAt | ldate: 'dd/MM/yyyy HH:mm:ss' }}</td>
                <td class="td">{{ row.actorName || (row.actorKind === 'system' ? ('AUDIT.SYSTEM' | translate) : '—') }}</td>
                <td class="td"><span [class]="badge(row.action)">{{ label(row.action) | translate }}</span></td>
                <td class="td"><div class="line-clamp-3 break-words" [title]="row.summary ?? ''">{{ row.summary || '—' }}</div>
                  <div class="text-[11px] text-gray-400 mt-0.5">{{ row.service }} · {{ row.entityType }}</div></td>
                @if (platform()) { <td class="td">{{ row.tenantName || '—' }}</td> }
                <td class="td font-mono text-[12px] text-gray-500">{{ row.ipAddress || '—' }}</td>
              </tr>
            } @empty {
              <tr><td [attr.colspan]="platform() ? 6 : 5" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
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
  /** Từ data của route: 'tenant' | 'platform' | 'catalog' (nhật ký biên mục — chỉ thao tác trên biểu ghi, quyền CATALOG_BIBS). */
  readonly scope = input<'tenant' | 'platform' | 'catalog'>('tenant');

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
    if (this.scope() === 'catalog') {
      this.actions.set(['ADD', 'UPDATE', 'DELETE', 'IMPORT', 'CHANGE_STATUS']);
      return;
    }
    try {
      this.actions.set(await this.api.auditActions(this.platform()));
    } catch {
      // Ô lọc thao tác để trống — không chặn xem nhật ký.
    }
  }

  protected label(action: string): string {
    return KNOWN_ACTIONS.has(action) ? `AUDIT.ACTION.${action}` : action;
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
      const body = { keyword: this.keyword.trim() || undefined, action: this.action, from, to, pageIndex: this.pageIndex, pageSize: this.pageSize };
      this.page.set(await (this.scope() === 'catalog' ? this.api.catalogLogs(body) : this.api.auditLogs(this.platform(), body)));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
