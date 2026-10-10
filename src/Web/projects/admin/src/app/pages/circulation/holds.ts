import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, CrudPage, HOLD_STATUSES, Hold, HoldSearch, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Yêu cầu mượn / đặt mượn (monolith: /admin/request-books, quyền REQUEST_BOOKS). Cán bộ đặt hộ bạn đọc theo MFN hoặc ĐKCB: còn bản
 * sẵn sàng thì giữ ngay, hết bản thì xếp hàng — bản được trả sẽ giữ cho người đặt sớm nhất và báo bạn đọc qua email.
 */
@Component({
  selector: 'app-holds',
  imports: [FormsModule, DatePipe, Loading, Modal, Paginator],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">Yêu cầu mượn</h4>
      @if (can('add')) {
        <button class="btn-primary ml-auto" (click)="openPlace()"><span class="material-icons text-[18px]">bookmark_add</span> Đặt mượn hộ bạn đọc</button>
      }
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="md:col-span-2">
          <label for="hd-kw" class="field-label">Số thẻ / ĐKCB / nhan đề / họ tên</label>
          <input id="hd-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="hd-status" class="field-label">Trạng thái</label>
          <select id="hd-status" class="input" [(ngModel)]="search.holdStatus" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name }}</option> }
          </select>
        </div>
        <div class="flex items-end">
          <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-44">Bạn đọc</th><th class="th">Tài liệu</th><th class="th w-32">Ngày đặt</th><th class="th w-36">Trạng thái</th>
            <th class="th w-40">Bản giữ / hạn lấy</th><th class="th w-20 !text-center">Thao tác</th>
          </tr></thead>
          <tbody>
            @for (h of page()?.items ?? []; track h.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ h.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ h.cardNo }}</div></td>
                <td class="td">{{ h.title ?? '—' }}<div class="text-[11px] text-gray-400">MFN {{ h.mfn }}@if (h.author) { · {{ h.author }} }</div>
                  @if (h.note) { <div class="text-[11px] text-gray-500">{{ h.note }}</div> }</td>
                <td class="td text-[13px]">{{ h.requestedAt | date: 'dd/MM/yyyy HH:mm' }}</td>
                <td class="td"><span [class]="status(h.status).badge">{{ status(h.status).name }}</span>
                  @if (h.queuePosition) { <div class="text-[11px] text-gray-500">Thứ tự chờ: {{ h.queuePosition }}</div> }</td>
                <td class="td text-[13px]">
                  @if (h.barcode) { <span class="font-mono">{{ h.barcode }}</span> }
                  @if (h.status === 2 && h.expiresAt) { <div class="text-[11px] text-amber-700">lấy trước hết {{ h.expiresAt | date: 'dd/MM/yyyy' }}</div> }
                  @if (h.closedAt && h.status !== 2) { <div class="text-[11px] text-gray-400">{{ h.closedAt | date: 'dd/MM/yyyy HH:mm' }}</div> }
                </td>
                <td class="td text-center">
                  @if (can('edit') && (h.status === 1 || h.status === 2)) {
                    <button class="icon-btn text-red-600 hover:bg-red-50" title="Huỷ đặt mượn" (click)="cancelling.set(h)"><span class="material-icons text-[18px]">event_busy</span></button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không có yêu cầu mượn nào' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (placing()) {
      <app-modal title="Đặt mượn hộ bạn đọc" widthClass="max-w-md" (closed)="placing.set(false)">
        <div class="space-y-4">
          <div>
            <label for="hd-card" class="form-label">Số thẻ bạn đọc</label>
            <input id="hd-card" class="input font-mono" [(ngModel)]="form.cardNo" autocomplete="off" />
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label for="hd-mfn" class="form-label">MFN biểu ghi</label>
              <input id="hd-mfn" type="number" class="input" [(ngModel)]="form.mfn" />
            </div>
            <div>
              <label for="hd-barcode" class="form-label">hoặc số ĐKCB</label>
              <input id="hd-barcode" class="input font-mono" [(ngModel)]="form.barcode" autocomplete="off" />
            </div>
          </div>
          <div>
            <label for="hd-note" class="form-label">Ghi chú</label>
            <input id="hd-note" class="input" maxlength="500" [(ngModel)]="form.note" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="placing.set(false)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !form.cardNo.trim() || (!form.mfn && !form.barcode.trim())" (click)="place()">Đặt mượn</button>
        </ng-container>
      </app-modal>
    }

    @if (cancelling(); as h) {
      <app-modal [title]="'Huỷ đặt mượn — thẻ ' + h.cardNo" widthClass="max-w-md" (closed)="cancelling.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ h.title ?? 'MFN ' + h.mfn }}@if (h.barcode) { — bản đang giữ {{ h.barcode }} sẽ chuyển cho người đặt kế tiếp. }</p>
        <label for="hd-reason" class="form-label">Lý do</label>
        <input id="hd-reason" class="input" maxlength="300" [(ngModel)]="reason" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="cancelling.set(null)">Đóng</button>
          <button type="button" class="btn-danger" [disabled]="busy()" (click)="cancel(h)">Huỷ đặt mượn</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class Holds implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly statuses = HOLD_STATUSES;
  protected search: HoldSearch = { keyword: '', holdStatus: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Hold> | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly placing = signal(false);
  protected readonly cancelling = signal<Hold | null>(null);
  protected form = { cardNo: '', mfn: null as number | null, barcode: '', note: '' };
  protected reason = '';

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`REQUEST_BOOKS:${action}`);
  }

  protected status(code: number) {
    return HOLD_STATUSES.find((s) => s.code === code) ?? { name: String(code), badge: 'badge-off' };
  }

  protected find(): void {
    this.search.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.search.pageIndex = e.pageIndex;
    this.search.pageSize = e.pageSize;
    void this.load();
  }

  protected openPlace(): void {
    this.form = { cardNo: '', mfn: null, barcode: '', note: '' };
    this.placing.set(true);
  }

  protected async place(): Promise<void> {
    await this.run(async () => {
      const f = this.form;
      const hold = await this.api.placeHold({ cardNo: f.cardNo.trim(), mfn: f.mfn || null, barcode: f.barcode.trim() || null, note: f.note.trim() || null });
      this.toastr.success(hold.status === 2 ? `Đã giữ bản ${hold.barcode} cho thẻ ${hold.cardNo}.` : `Đã xếp hàng chờ, thứ tự ${hold.queuePosition}.`);
      this.placing.set(false);
      await this.load();
    });
  }

  protected async cancel(h: Hold): Promise<void> {
    await this.run(async () => {
      await this.api.cancelHold(h.publicId, this.reason.trim() || null);
      this.toastr.success('Đã huỷ đặt mượn.');
      this.cancelling.set(null);
      this.reason = '';
      await this.load();
    });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.api.searchHolds({ ...this.search, keyword: this.search.keyword?.trim() || undefined }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
