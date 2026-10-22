import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ReadingSessionPage, ReadingStats, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/**
 * Theo dõi đọc tài liệu số (LRC: ReadingTracking, quyền DIGITAL_READING): thống kê theo khoảng ngày (lượt đọc, bạn đọc, thời gian đọc, lượt xem/tải,
 * sách và bạn đọc nổi bật, theo ngày) và danh sách từng lượt đọc. Chỉ có lượt đọc của bạn đọc đăng nhập; log quá 2 năm tự xoá.
 */
@Component({
  selector: 'app-reading-tracking',
  imports: [FormsModule, LDatePipe, LNumberPipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5 flex items-end gap-3 flex-wrap">
      <h4 class="page-title">{{ 'READING_PAGE.TITLE' | translate }}</h4>
      <div class="ml-auto flex items-end gap-2 flex-wrap">
        <div><label for="rt-from" class="form-label">{{ 'READING_PAGE.FROM' | translate }}</label><input id="rt-from" type="date" class="input" [(ngModel)]="from" /></div>
        <div><label for="rt-to" class="form-label">{{ 'READING_PAGE.TO' | translate }}</label><input id="rt-to" type="date" class="input" [(ngModel)]="to" /></div>
        <div><label for="rt-kw" class="form-label">{{ 'COMMON.SEARCH' | translate }}</label><input id="rt-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="load()" /></div>
        <button type="button" class="btn-primary" [disabled]="loading()" (click)="load()"><span class="material-icons text-[18px]">query_stats</span> {{ 'READING_PAGE.VIEW' | translate }}</button>
      </div>
    </div>

    <div class="relative">
      @if (loading()) { <app-loading /> }
      @if (stats(); as s) {
        <div class="grid grid-cols-2 lg:grid-cols-6 gap-x-4">
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.SESSIONS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.sessions | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.READERS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.readers | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.EBOOKS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.ebooks | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.HOURS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.activeSeconds / 3600 | lnumber: '1.0-1' }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.VIEWS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.views | lnumber }}</div></div>
          <div class="panel"><div class="text-xs text-gray-500">{{ 'READING_PAGE.DOWNLOADS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.totals.downloads | lnumber }}</div></div>
        </div>

        <div class="panel mb-5">
          <div class="font-medium text-gray-800 mb-3">{{ 'READING_PAGE.PER_DAY' | translate }}</div>
          <div class="flex items-end gap-px h-32 overflow-x-auto" role="img" [attr.aria-label]="'READING_PAGE.PER_DAY' | translate">
            @for (d of s.daily; track d.date) {
              <div class="flex-1 min-w-[6px] flex flex-col justify-end h-full" [title]="(d.date | ldate: 'dd/MM/yyyy') + ': ' + d.sessions">
                <div class="bg-blue-500" [style.height.%]="(d.sessions / maxDay()) * 100"></div>
              </div>
            }
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-x-5">
          <div class="panel">
            <div class="font-medium text-gray-800 mb-2">{{ 'READING_PAGE.TOP_EBOOKS' | translate }}</div>
            <table class="w-full text-left border-collapse">
              <thead><tr><th class="th">{{ 'READING_PAGE.EBOOK' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.SESSIONS' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.READERS' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.MINUTES' | translate }}</th></tr></thead>
              <tbody>
                @for (e of s.topEbooks; track e.ebookPublicId) {
                  <tr><td class="td max-w-[260px] truncate" [title]="e.title">{{ e.title }}</td><td class="td text-right">{{ e.sessions | lnumber }}</td><td class="td text-right">{{ e.readers | lnumber }}</td>
                    <td class="td text-right">{{ e.activeSeconds / 60 | lnumber: '1.0-0' }}</td></tr>
                } @empty { <tr><td colspan="4" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
              </tbody>
            </table>
          </div>
          <div class="panel">
            <div class="font-medium text-gray-800 mb-2">{{ 'READING_PAGE.TOP_READERS' | translate }}</div>
            <table class="w-full text-left border-collapse">
              <thead><tr><th class="th">{{ 'READING_PAGE.READER' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.SESSIONS' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.MINUTES' | translate }}</th></tr></thead>
              <tbody>
                @for (r of s.topReaders; track r.readerPublicId) {
                  <tr><td class="td">{{ r.fullName ?? '—' }} <span class="text-gray-400 text-xs font-mono">{{ r.cardNo }}</span></td><td class="td text-right">{{ r.sessions | lnumber }}</td>
                    <td class="td text-right">{{ r.activeSeconds / 60 | lnumber: '1.0-0' }}</td></tr>
                } @empty { <tr><td colspan="3" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
              </tbody>
            </table>
          </div>
        </div>
      }

      @if (sessions(); as p) {
        <div class="panel">
          <div class="font-medium text-gray-800 mb-2">{{ 'READING_PAGE.LIST' | translate }}</div>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead><tr>
                <th class="th">{{ 'READING_PAGE.EBOOK' | translate }}</th><th class="th">{{ 'READING_PAGE.READER' | translate }}</th><th class="th">{{ 'READING_PAGE.STARTED' | translate }}</th>
                <th class="th !text-right">{{ 'READING_PAGE.PAGES' | translate }}</th><th class="th !text-right">{{ 'READING_PAGE.MINUTES' | translate }}</th><th class="th">{{ 'READING_PAGE.DEVICE' | translate }}</th>
              </tr></thead>
              <tbody>
                @for (r of p.items; track r.publicId) {
                  <tr><td class="td max-w-[260px] truncate" [title]="r.ebookTitle">{{ r.ebookTitle }}</td>
                    <td class="td">{{ r.fullName ?? '—' }} <span class="text-gray-400 text-xs font-mono">{{ r.cardNo }}</span></td>
                    <td class="td whitespace-nowrap">{{ r.startedAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                    <td class="td text-right">{{ r.maxPage }} / {{ r.pageCount }}</td><td class="td text-right">{{ r.activeSeconds / 60 | lnumber: '1.0-0' }}</td><td class="td">{{ r.device }}</td></tr>
                } @empty { <tr><td colspan="6" class="td text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
              </tbody>
            </table>
          </div>
          <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" />
        </div>
      }
    </div>
  `,
})
export class ReadingTracking implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly stats = signal<ReadingStats | null>(null);
  protected readonly sessions = signal<ReadingSessionPage | null>(null);
  protected readonly loading = signal(false);
  protected readonly maxDay = computed(() => Math.max(1, ...(this.stats()?.daily.map((d) => d.sessions) ?? [1])));
  protected from = '';
  protected to = '';
  protected keyword = '';
  protected pageIndex = 1;
  protected pageSize = 25;

  ngOnInit(): void {
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load(false);
  }

  protected async load(resetPage = true): Promise<void> {
    if (resetPage) this.pageIndex = 1;
    this.loading.set(true);
    try {
      const range = { from: this.from || null, to: this.to || null };
      const [stats, sessions] = await Promise.all([
        this.api.readingStatistics(range),
        this.api.readingSessions({ ...range, keyword: this.keyword.trim() || null, ebookPublicId: null, pageIndex: this.pageIndex, pageSize: this.pageSize }),
      ]);
      this.stats.set(stats);
      this.sessions.set(sessions);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
