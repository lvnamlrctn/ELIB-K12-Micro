import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, CrudPage, NotificationLog, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/** Nhật ký gửi tin — chỉ xem. Không lưu nội dung thư (có thể chứa mã OTP). */
@Component({
  selector: 'app-notification-logs',
  imports: [FormsModule, DatePipe, Loading, Paginator],
  template: `
    <div class="mb-5"><h4 class="page-title">Nhật ký gửi tin</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">Người nhận / tiêu đề</label>
          <input id="kw" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" placeholder="Nhập email hoặc tiêu đề..." />
        </div>
        <div>
          <label for="st" class="field-label">Kết quả</label>
          <select id="st" class="input" [(ngModel)]="status" (change)="search()">
            <option [ngValue]="null">Tất cả</option>
            <option [ngValue]="1">Đã gửi</option>
            <option [ngValue]="2">Lỗi</option>
            <option [ngValue]="3">Bỏ qua</option>
          </select>
        </div>
        <div>
          <label for="tpl" class="field-label">Mã mẫu</label>
          <input id="tpl" class="input font-mono" [(ngModel)]="templateCode" (keydown.enter)="search()" placeholder="VD: LOGIN_OTP" />
        </div>
        <div class="flex items-end">
          <button (click)="search()" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th w-44">Thời gian</th>
              <th class="th w-36">Mẫu</th>
              <th class="th">Người nhận</th>
              <th class="th">Tiêu đề</th>
              <th class="th w-32 !text-center">Kết quả</th>
              <th class="th w-32">Máy chủ</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td text-gray-500 whitespace-nowrap">{{ row.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
                <td class="td"><span class="font-mono text-[13px]">{{ row.templateCode }}</span></td>
                <td class="td break-all">{{ row.recipient ?? '—' }}</td>
                <td class="td">
                  <div class="line-clamp-2">{{ row.subject ?? '—' }}</div>
                  @if (row.error) { <div class="text-xs text-red-600 mt-0.5 line-clamp-2" [title]="row.error">{{ row.error }}</div> }
                </td>
                <td class="td text-center">
                  @switch (row.status) {
                    @case ('Sent') { <span class="badge-on">Đã gửi</span> }
                    @case ('Failed') { <span class="badge-danger">Lỗi</span> }
                    @default { <span class="badge-off">Bỏ qua</span> }
                  }
                </td>
                <td class="td text-gray-500 text-xs">{{ row.viaPlatform ? 'Nền tảng' : 'Của đơn vị' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 px-4 text-center text-gray-500">{{ loading() ? '' : 'Chưa có thư nào' }}</td></tr>
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
