import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SearchIndexStatus, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';

/**
 * Chỉ mục tra cứu OPAC (service search, quyền SEARCH_INDEX): số biểu ghi/bản sách đã đánh chỉ mục, lần dựng lại gần nhất.
 * Chỉ mục tự cập nhật theo biên mục, kho, lưu thông; "Dựng lại" dùng khi nghi lệch (đọc lại toàn bộ từ các phân hệ).
 */
@Component({
  selector: 'app-search-index',
  imports: [LDatePipe, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'SEARCH_INDEX_PAGE.TITLE' | translate }}</h4></div>
    <div class="panel">
      @if (status(); as s) {
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div><div class="text-xs text-gray-500">{{ 'SEARCH_INDEX_PAGE.BIBS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.indexedBibs }}</div>
            <div class="text-xs text-gray-500">{{ 'SEARCH_INDEX_PAGE.VISIBLE' | translate: { count: s.visibleBibs } }}</div></div>
          <div><div class="text-xs text-gray-500">{{ 'SEARCH_INDEX_PAGE.ITEMS' | translate }}</div><div class="text-2xl font-semibold text-gray-800">{{ s.indexedItems }}</div></div>
          <div><div class="text-xs text-gray-500">{{ 'SEARCH_INDEX_PAGE.LAST_REBUILD' | translate }}</div>
            @switch (s.status) {
              @case ('Running') { <div class="text-amber-700 font-medium">{{ 'SEARCH_INDEX_PAGE.RUNNING' | translate: { time: (s.startedAt | ldate: 'HH:mm:ss') } }}</div> }
              @case ('Failed') { <div class="text-red-600 font-medium">{{ 'SEARCH_INDEX_PAGE.FAILED_AT' | translate: { time: (s.finishedAt | ldate: 'dd/MM/yyyy HH:mm') } }}</div><div class="text-xs text-red-600">{{ s.error }}</div> }
              @case ('Done') { <div class="text-gray-800">{{ s.finishedAt | ldate: 'dd/MM/yyyy HH:mm' }}</div>
                <div class="text-xs text-gray-500">{{ 'SEARCH_INDEX_PAGE.DONE_COUNTS' | translate: { bibs: s.bibs, items: s.items, loans: s.loans } }}</div> }
              @default { <div class="text-gray-500">{{ 'SEARCH_INDEX_PAGE.NEVER' | translate }}</div> }
            }
          </div>
        </div>
      }
      <p class="text-sm text-gray-600 mt-4">{{ 'SEARCH_INDEX_PAGE.HINT' | translate }}</p>
      @if (session.can('SEARCH_INDEX:edit')) {
        <div class="flex justify-end mt-4">
          <button class="btn-primary" [disabled]="busy() || status()?.status === 'Running'" (click)="rebuild()">
            <span class="material-icons text-[18px]">refresh</span> {{ 'SEARCH_INDEX_PAGE.REBUILD' | translate }}</button>
        </div>
      }
    </div>
  `,
})
export class SearchIndex implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly status = signal<SearchIndexStatus | null>(null);
  protected readonly busy = signal(false);
  private timer: ReturnType<typeof setInterval> | undefined;

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  protected async rebuild(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.rebuildSearchIndex();
      this.toastr.success('SEARCH_INDEX_PAGE.STARTED');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const s = await this.api.searchIndexStatus();
      this.status.set(s);
      clearInterval(this.timer);
      if (s.status === 'Running') this.timer = setInterval(() => void this.load(), 2000);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }
}
