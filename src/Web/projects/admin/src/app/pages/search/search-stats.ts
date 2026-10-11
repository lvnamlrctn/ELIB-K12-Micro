import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, SearchStatsSummary, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';

/**
 * Thống kê chất lượng tìm kiếm OPAC (service search, quyền SEARCH_STATS): lượt tìm, tỉ lệ không có kết quả, tỉ lệ bạn đọc mở kết quả,
 * vị trí kết quả được mở; câu tìm nhiều nhất và câu tìm không ra kết quả — gợi ý bổ sung tài liệu, sửa biên mục, thêm từ khoá.
 */
@Component({
  selector: 'app-search-stats',
  imports: [DatePipe, DecimalPipe, FormsModule, PercentPipe],
  template: `
    <div class="mb-5 flex items-end gap-3 flex-wrap">
      <h4 class="page-title">Thống kê tra cứu (OPAC)</h4>
      <div class="ml-auto flex items-end gap-2 flex-wrap">
        <div><label for="ss-from" class="form-label">Từ ngày</label><input id="ss-from" type="date" class="input" [(ngModel)]="from" /></div>
        <div><label for="ss-to" class="form-label">Đến ngày</label><input id="ss-to" type="date" class="input" [(ngModel)]="to" /></div>
        <button type="button" class="btn-primary" [disabled]="loading()" (click)="load()"><span class="material-icons text-[18px]">query_stats</span> Xem</button>
      </div>
    </div>

    @if (stats(); as s) {
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-x-4">
        <div class="panel"><div class="text-xs text-gray-500">Lượt tìm</div><div class="text-2xl font-semibold text-gray-800">{{ s.searches | number }}</div>
          <div class="text-xs text-gray-500">{{ s.advancedSearches | number }} lượt tìm nâng cao</div></div>
        <div class="panel"><div class="text-xs text-gray-500">Không có kết quả</div>
          <div class="text-2xl font-semibold" [class]="rate(s.zeroResults) > 0.3 ? 'text-red-600' : 'text-gray-800'">{{ rate(s.zeroResults) | percent: '1.0-1' }}</div>
          <div class="text-xs text-gray-500">{{ s.zeroResults | number }} lượt</div></div>
        <div class="panel"><div class="text-xs text-gray-500">Bạn đọc mở kết quả</div>
          <div class="text-2xl font-semibold text-gray-800">{{ rate(s.withClicks) | percent: '1.0-1' }}</div>
          <div class="text-xs text-gray-500">{{ s.withClicks | number }} lượt có mở ít nhất 1 tài liệu</div></div>
        <div class="panel"><div class="text-xs text-gray-500">Vị trí kết quả được mở</div>
          <div class="text-2xl font-semibold text-gray-800">{{ s.avgClickPosition ?? '—' }}</div>
          <div class="text-xs text-gray-500">trung bình; càng gần 1 càng tốt</div></div>
      </div>

      @if (s.days.length) {
        <div class="panel mb-5">
          <div class="font-medium text-gray-800 mb-3">Lượt tìm theo ngày</div>
          <div class="flex items-end gap-px h-36 overflow-x-auto" role="img" [attr.aria-label]="'Biểu đồ lượt tìm từ ' + s.from + ' đến ' + s.to">
            @for (d of s.days; track d.date) {
              <div class="flex-1 min-w-[6px] flex flex-col justify-end h-full" [title]="(d.date | date: 'dd/MM/yyyy') + ': ' + d.searches + ' lượt, ' + d.zeroResults + ' không có kết quả'">
                <div class="bg-red-300" [style.height.%]="(d.zeroResults / maxDay()) * 100"></div>
                <div class="bg-blue-500" [style.height.%]="((d.searches - d.zeroResults) / maxDay()) * 100"></div>
              </div>
            }
          </div>
          <div class="flex justify-between text-xs text-gray-500 mt-1">
            <span>{{ s.days[0].date | date: 'dd/MM' }}</span>
            <span class="flex gap-3"><span><span class="inline-block w-2 h-2 bg-blue-500"></span> có kết quả</span><span><span class="inline-block w-2 h-2 bg-red-300"></span> không có kết quả</span></span>
            <span>{{ s.days[s.days.length - 1].date | date: 'dd/MM' }}</span>
          </div>
        </div>
      }

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-x-5">
        <div class="panel">
          <div class="font-medium text-gray-800 mb-2">Câu tìm nhiều nhất</div>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead><tr><th class="th">Câu tìm</th><th class="th !text-right">Lượt</th><th class="th !text-right">Kết quả TB</th><th class="th !text-right">Mở kết quả</th></tr></thead>
              <tbody>
                @for (q of s.topQueries; track q.text) {
                  <tr><td class="td max-w-[260px] truncate" [title]="q.text">{{ q.text }}</td><td class="td text-right">{{ q.searches | number }}</td>
                    <td class="td text-right">{{ q.avgTotal | number }}</td><td class="td text-right">{{ q.withClicks / q.searches | percent: '1.0-0' }}</td></tr>
                } @empty { <tr><td colspan="4" class="td text-center text-gray-500">Không có</td></tr> }
              </tbody>
            </table>
          </div>
        </div>
        <div class="panel">
          <div class="font-medium text-gray-800 mb-1">Tìm không ra kết quả</div>
          <div class="text-xs text-gray-500 mb-2">Tài liệu bạn đọc cần mà thư viện chưa có, hoặc biểu ghi thiếu từ khoá / viết khác.</div>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead><tr><th class="th">Câu tìm</th><th class="th !text-right">Lượt</th></tr></thead>
              <tbody>
                @for (q of s.topZeroResults; track q.text) {
                  <tr><td class="td max-w-[320px] truncate" [title]="q.text">{{ q.text }}</td><td class="td text-right">{{ q.searches | number }}</td></tr>
                } @empty { <tr><td colspan="2" class="td text-center text-gray-500">Không có</td></tr> }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    } @else if (!loading()) {
      <div class="panel text-gray-500">Chưa có số liệu.</div>
    }
  `,
})
export class SearchStats implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly stats = signal<SearchStatsSummary | null>(null);
  protected readonly loading = signal(false);
  protected readonly maxDay = computed(() => Math.max(1, ...(this.stats()?.days ?? []).map((d) => d.searches)));
  protected from = '';
  protected to = '';

  ngOnInit(): void {
    void this.load();
  }

  protected rate(count: number): number {
    const total = this.stats()?.searches ?? 0;
    return total ? count / total : 0;
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = await this.api.searchStats(this.from || null, this.to || null);
      this.stats.set(s);
      this.from = s.from;
      this.to = s.to;
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
