import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Api, SearchIndexStatus, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';

/**
 * Chỉ mục tra cứu OPAC (service search, quyền SEARCH_INDEX): số biểu ghi/bản sách đã đánh chỉ mục, lần dựng lại gần nhất.
 * Chỉ mục tự cập nhật theo biên mục, kho, lưu thông; "Dựng lại" dùng khi nghi lệch (đọc lại toàn bộ từ các phân hệ).
 */
@Component({
  selector: 'app-search-index',
  imports: [DatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">Chỉ mục tra cứu (OPAC)</h4></div>
    <div class="panel">
      @if (status(); as s) {
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div><div class="text-xs text-gray-500">Biểu ghi trong chỉ mục</div><div class="text-2xl font-semibold text-gray-800">{{ s.indexedBibs }}</div>
            <div class="text-xs text-gray-500">{{ s.visibleBibs }} biểu ghi hiện trên OPAC</div></div>
          <div><div class="text-xs text-gray-500">Bản sách</div><div class="text-2xl font-semibold text-gray-800">{{ s.indexedItems }}</div></div>
          <div><div class="text-xs text-gray-500">Lần dựng lại gần nhất</div>
            @switch (s.status) {
              @case ('Running') { <div class="text-amber-700 font-medium">Đang dựng lại... (bắt đầu {{ s.startedAt | date: 'HH:mm:ss' }})</div> }
              @case ('Failed') { <div class="text-red-600 font-medium">Thất bại lúc {{ s.finishedAt | date: 'dd/MM/yyyy HH:mm' }}</div><div class="text-xs text-red-600">{{ s.error }}</div> }
              @case ('Done') { <div class="text-gray-800">{{ s.finishedAt | date: 'dd/MM/yyyy HH:mm' }}</div>
                <div class="text-xs text-gray-500">{{ s.bibs }} biểu ghi, {{ s.items }} bản sách, {{ s.loans }} lượt đang mượn</div> }
              @default { <div class="text-gray-500">Chưa dựng lại lần nào</div> }
            }
          </div>
        </div>
      }
      <p class="text-sm text-gray-600 mt-4">Chỉ mục tự cập nhật khi biên mục, đăng ký cá biệt, mượn trả. Chỉ cần dựng lại khi kết quả tra cứu trên OPAC không khớp dữ liệu.</p>
      @if (session.can('SEARCH_INDEX:edit')) {
        <div class="flex justify-end mt-4">
          <button class="btn-primary" [disabled]="busy() || status()?.status === 'Running'" (click)="rebuild()">
            <span class="material-icons text-[18px]">refresh</span> Dựng lại chỉ mục</button>
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
      this.toastr.success('Đang dựng lại chỉ mục tra cứu.');
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
