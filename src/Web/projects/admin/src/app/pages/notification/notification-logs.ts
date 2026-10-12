import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, NotificationLog, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/** Nhật ký gửi tin — chỉ xem. Không lưu nội dung thư (có thể chứa mã OTP). */
@Component({
  selector: 'app-notification-logs',
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'NOTIFICATION_LOG.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">{{ 'NOTIFICATION_LOG.SEARCH_LABEL' | translate }}</label>
          <input id="kw" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" [placeholder]="'NOTIFICATION_LOG.SEARCH_PH' | translate" />
        </div>
        <div>
          <label for="st" class="field-label">{{ 'NOTIFICATION_LOG.RESULT' | translate }}</label>
          <select id="st" class="input" [(ngModel)]="status" (change)="search()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="1">{{ 'NOTIFICATION_LOG.SENT' | translate }}</option>
            <option [ngValue]="2">{{ 'NOTIFICATION_LOG.FAILED' | translate }}</option>
            <option [ngValue]="3">{{ 'NOTIFICATION_LOG.SKIPPED' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="tpl" class="field-label">{{ 'NOTIFICATION_LOG.TEMPLATE_CODE' | translate }}</label>
          <input id="tpl" class="input font-mono" [(ngModel)]="templateCode" (keydown.enter)="search()" placeholder="VD: LOGIN_OTP" />
        </div>
        <div class="flex items-end">
          <button (click)="search()" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th w-44">{{ 'NOTIFICATION_LOG.TIME' | translate }}</th>
              <th class="th w-36">{{ 'NOTIFICATION_LOG.TEMPLATE' | translate }}</th>
              <th class="th">{{ 'NOTIFICATION_LOG.RECIPIENT' | translate }}</th>
              <th class="th">{{ 'NOTIFICATION_LOG.SUBJECT' | translate }}</th>
              <th class="th w-32 !text-center">{{ 'NOTIFICATION_LOG.RESULT' | translate }}</th>
              <th class="th w-32">{{ 'NOTIFICATION_LOG.SERVER' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td text-gray-500 whitespace-nowrap">{{ row.createdAt | ldate: 'dd/MM/yyyy HH:mm:ss' }}</td>
                <td class="td"><span class="font-mono text-[13px]">{{ row.templateCode }}</span></td>
                <td class="td break-all">{{ row.recipient ?? '—' }}</td>
                <td class="td">
                  <div class="line-clamp-2">{{ row.subject ?? '—' }}</div>
                  @if (row.error) { <div class="text-xs text-red-600 mt-0.5 line-clamp-2" [title]="row.error">{{ row.error }}</div> }
                </td>
                <td class="td text-center">
                  @switch (row.status) {
                    @case ('Sent') { <span class="badge-on">{{ 'NOTIFICATION_LOG.SENT' | translate }}</span> }
                    @case ('Failed') { <span class="badge-danger">{{ 'NOTIFICATION_LOG.FAILED' | translate }}</span> }
                    @default { <span class="badge-off">{{ 'NOTIFICATION_LOG.SKIPPED' | translate }}</span> }
                  }
                </td>
                <td class="td text-gray-500 text-xs">{{ (row.viaPlatform ? 'NOTIFICATION_LOG.VIA_PLATFORM' : 'NOTIFICATION_LOG.VIA_TENANT') | translate }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : ('NOTIFICATION_LOG.EMPTY' | translate) }}</td></tr>
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
export class NotificationLogs implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected keyword = '';
  protected status: number | null = null;
  protected templateCode = '';
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<NotificationLog> | null>(null);
  protected readonly loading = signal(false);

  ngOnInit(): void {
    void this.load();
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
      this.page.set(await this.api.notificationLogs({
        keyword: this.keyword.trim() || undefined, status: this.status, templateCode: this.templateCode.trim() || null,
        pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
