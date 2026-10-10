import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, CircPlace, CirculationReport, CirculationReportRequest, errorMessage, saveFile } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

const REPORT_TYPES: { value: number; label: string; hint: string }[] = [
  { value: 1, label: 'Hoạt động phục vụ tại thư viện', hint: 'Theo ngày; mặc định 30 ngày gần nhất' },
  { value: 2, label: 'Danh sách tài liệu đang mượn', hint: 'Khoảng ngày theo ngày mượn' },
  { value: 3, label: 'Tài liệu đang mượn theo ngày trả', hint: 'Khoảng ngày theo hạn trả' },
  { value: 4, label: 'Danh sách tài liệu đang mượn quá hạn', hint: 'Khoảng ngày theo ngày mượn' },
  { value: 5, label: 'Danh sách tài liệu trả quá hạn', hint: 'Khoảng ngày theo ngày trả' },
  { value: 6, label: 'Bạn đọc hết hạn thẻ chưa trả sách', hint: '' },
  { value: 7, label: 'Bạn đọc quá hạn sách', hint: '' },
  { value: 8, label: 'Thống kê tài liệu mượn nhiều', hint: '50 biểu ghi đầu, theo ngày mượn' },
  { value: 9, label: 'Thống kê tài liệu không được mượn', hint: 'Bản sách chưa ai mượn trong khoảng ngày' },
  { value: 10, label: 'Danh sách tài liệu đã trả', hint: 'Khoảng ngày theo ngày trả' },
  { value: 11, label: 'Danh sách tài liệu mất', hint: 'Báo mất qua phiếu phạt, theo ngày mất' },
];

type Named = { id: number; name: string; publicId: string };

/** Báo cáo lưu thông (monolith: /admin/circulation-report, quyền CIRC_REPORT) — 11 loại, xem theo trang hoặc xuất Excel. */
@Component({
  selector: 'app-circulation-reports',
  imports: [FormsModule, Loading, Paginator],
  template: `
    <div class="mb-5"><h4 class="page-title">Báo cáo lưu thông</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="rp-type" class="field-label">Loại báo cáo</label>
          <select id="rp-type" class="input" [(ngModel)]="request.reportType" (change)="run()">
            @for (t of types; track t.value) { <option [ngValue]="t.value">{{ t.value }}. {{ t.label }}</option> }
          </select>
          <div class="text-[11px] text-gray-500 mt-1">{{ hint() }}</div>
        </div>
        <div>
          <label for="rp-from" class="field-label">Từ ngày</label>
          <input id="rp-from" type="date" class="input" [(ngModel)]="request.from" />
        </div>
        <div>
          <label for="rp-to" class="field-label">Đến ngày</label>
          <input id="rp-to" type="date" class="input" [(ngModel)]="request.to" />
        </div>
        <div>
          <label for="rp-place" class="field-label">Điểm lưu thông</label>
          <select id="rp-place" class="input" [(ngModel)]="request.circPlaceId">
            <option [ngValue]="null">Tất cả</option>
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div>
          <label for="rp-type-reader" class="field-label">Loại bạn đọc</label>
          <select id="rp-type-reader" class="input" [(ngModel)]="request.readerTypeId">
            <option [ngValue]="null">Tất cả</option>
            @for (t of readerTypes(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }
          </select>
        </div>
      </div>
      <div class="flex justify-end gap-2 mt-4">
        <button class="btn-secondary" [disabled]="busy()" (click)="export()"><span class="material-icons text-[18px]">download</span> Xuất Excel</button>
        <button class="btn-primary" [disabled]="busy()" (click)="run()"><span class="material-icons text-[18px]">summarize</span> Xem báo cáo</button>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      @if (report(); as r) {
        <div class="px-5 py-4 border-b border-gray-100 text-center">
          @if (r.libraryName) { <div class="text-xs text-gray-500 uppercase">{{ r.libraryName }}</div> }
          <div class="font-semibold text-gray-800">{{ r.title }}</div>
          <div class="text-xs text-gray-500">{{ r.totalCount }} dòng@if (r.truncated) { — chỉ hiện 5.000 dòng đầu, thu hẹp khoảng ngày để xem đủ }</div>
        </div>
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>@for (h of r.headers; track $index) { <th class="th">{{ h }}</th> }</tr></thead>
            <tbody>
              @for (row of r.rows; track $index) {
                <tr class="hover:bg-gray-50/50">@for (c of row; track $index) { <td class="td text-[13px]">{{ c }}</td> }</tr>
              } @empty {
                <tr><td [attr.colspan]="r.headers.length || 1" class="py-8 text-center text-gray-500">Không có dữ liệu</td></tr>
              }
              @if (r.totalRow; as t) {
                <tr class="bg-gray-50 font-semibold">@for (c of t; track $index) { <td class="td text-[13px]">{{ c }}</td> }</tr>
              }
            </tbody>
          </table>
        </div>
        <app-paginator [total]="r.totalCount" [pageIndex]="request.pageIndex" [pageSize]="request.pageSize" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class CirculationReports implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly types = REPORT_TYPES;
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly readerTypes = signal<Named[]>([]);
  protected readonly report = signal<CirculationReport | null>(null);
  protected readonly busy = signal(false);
  protected request: CirculationReportRequest = {
    reportType: 1, from: null, to: null, circPlaceId: null, readerTypeId: null, className: null, pageIndex: 1, pageSize: 20,
  };

  async ngOnInit(): Promise<void> {
    void this.load();
    this.api.crud<CircPlace>('circ-places', 'circulation').searchAll().then((p) => this.places.set(p), () => undefined);
    this.api.crud<Named>('reader-types', 'patron').searchAll().then((t) => this.readerTypes.set(t), () => undefined);
  }

  protected hint(): string {
    return REPORT_TYPES.find((t) => t.value === this.request.reportType)?.hint ?? '';
  }

  protected run(): void {
    this.request.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.request.pageIndex = e.pageIndex;
    this.request.pageSize = e.pageSize;
    void this.load();
  }

  protected async export(): Promise<void> {
    this.busy.set(true);
    try {
      saveFile(await this.api.exportCirculationReport(this.body()), `bao-cao-luu-thong-${this.request.reportType}.xlsx`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private body(): CirculationReportRequest {
    return { ...this.request, from: this.request.from || null, to: this.request.to || null };
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.report.set(await this.api.circulationReport(this.body()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
