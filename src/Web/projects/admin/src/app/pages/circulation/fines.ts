import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, CrudPage, FineTicket, FineTicketSearch, FineTicketTotals, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Quản lý phạt (monolith: /admin/fines + /admin/fine-tickets, quyền FINES). Danh sách phiếu phạt kèm tổng phải thu / đã thu / còn lại
 * trên toàn bộ kết quả lọc; lập phiếu gom tài liệu quá hạn của bạn đọc hoặc phiếu thủ công.
 */
@Component({
  selector: 'app-fines',
  imports: [FormsModule, DatePipe, DecimalPipe, RouterLink, Loading, Paginator, Modal, ConfirmDelete],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">Quản lý phạt</h4>
      @if (can('add')) {
        <div class="ml-auto flex gap-2">
          <button class="btn-primary" (click)="openCreate('build')"><span class="material-icons text-[18px]">playlist_add_check</span> Lập phiếu cho bạn đọc</button>
          <button class="btn-secondary" (click)="openCreate('manual')"><span class="material-icons text-[18px]">add</span> Phiếu thủ công</button>
        </div>
      }
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="fn-kw" class="field-label">Số phiếu / số thẻ / họ tên</label>
          <input id="fn-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" placeholder="VD: PT001, HS001..." />
        </div>
        <div>
          <label for="fn-status" class="field-label">Trạng thái</label>
          <select id="fn-status" class="input" [(ngModel)]="search.ticketStatus" (change)="find()">
            <option [ngValue]="null">Tất cả</option>
            <option [ngValue]="1">Đang xử lý</option>
            <option [ngValue]="2">Đã hoàn thành</option>
          </select>
        </div>
        <div class="flex items-end pb-2">
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="search.unpaid" (change)="find()" /> Còn nợ tiền phạt
          </label>
        </div>
        <div>
          <label for="fn-from" class="field-label">Ngày phạt từ</label>
          <input id="fn-from" type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
        <div>
          <label for="fn-to" class="field-label">đến ngày</label>
          <input id="fn-to" type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
      </div>
    </div>

    @if (totals(); as t) {
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-5">
        <div class="panel !mb-0"><div class="text-xs text-gray-500">Phải thu</div><div class="text-xl font-semibold text-gray-800">{{ t.receivable | number: '1.0-0' }} đ</div></div>
        <div class="panel !mb-0"><div class="text-xs text-gray-500">Đã thu</div><div class="text-xl font-semibold text-green-700">{{ t.received | number: '1.0-0' }} đ</div></div>
        <div class="panel !mb-0"><div class="text-xs text-gray-500">Còn lại</div><div class="text-xl font-semibold" [class.text-red-600]="t.remaining > 0">{{ t.remaining | number: '1.0-0' }} đ</div></div>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-24">Số phiếu</th><th class="th">Bạn đọc</th><th class="th w-28">Ngày phạt</th><th class="th w-20 !text-center">Lần</th>
            <th class="th w-32 !text-right">Tổng</th><th class="th w-32 !text-right">Đã nộp</th><th class="th w-32 !text-right">Còn lại</th>
            <th class="th w-32">Trạng thái</th><th class="th w-24 !text-center">Thao tác</th>
          </tr></thead>
          <tbody>
            @for (t of page()?.items ?? []; track t.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]"><a class="text-blue-600 hover:underline" [routerLink]="['/fine-ticket', t.publicId]">{{ t.code }}</a></td>
                <td class="td">{{ t.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ t.cardNo }}</div>
                  @if (t.note) { <div class="text-[11px] text-gray-500">{{ t.note }}</div> }</td>
                <td class="td text-[13px]">{{ t.fineDate | date: 'dd/MM/yyyy' }}</td>
                <td class="td text-center">{{ t.round }}</td>
                <td class="td text-right">{{ t.total | number: '1.0-0' }}</td>
                <td class="td text-right">{{ t.paid | number: '1.0-0' }}</td>
                <td class="td text-right" [class.text-red-600]="t.remaining > 0" [class.font-semibold]="t.remaining > 0">{{ t.remaining | number: '1.0-0' }}</td>
                <td class="td"><span [class]="t.status === 2 ? 'badge-on' : 'badge-info'">{{ t.status === 2 ? 'Đã hoàn thành' : 'Đang xử lý' }}</span></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1">
                    <a class="icon-btn text-blue-600 hover:bg-blue-50" title="Chi tiết" [routerLink]="['/fine-ticket', t.publicId]"><span class="material-icons text-[18px]">visibility</span></a>
                    @if (can('delete') && t.status === 1 && t.paid === 0) {
                      <button class="icon-btn text-red-600 hover:bg-red-50" title="Xoá" (click)="deleting.set(t)"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="9" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không có phiếu phạt nào' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (creating(); as mode) {
      <app-modal [title]="mode === 'build' ? 'Lập phiếu phạt cho bạn đọc' : 'Phiếu phạt thủ công'" widthClass="max-w-md" (closed)="creating.set(null)">
        <div class="space-y-4">
          <div>
            <label for="fn-card" class="form-label">Số thẻ bạn đọc</label>
            <input id="fn-card" class="input font-mono" [(ngModel)]="cardNo" placeholder="Quét hoặc nhập số thẻ" autocomplete="off" />
          </div>
          @if (mode === 'build') {
            <p class="text-sm text-gray-600">Gom mọi tài liệu bạn đọc đang mượn quá hạn vào phiếu phạt đang mở (chưa có thì lập phiếu mới). Tiền phạt = số ngày quá hạn × tiền phạt mỗi ngày của chính sách.</p>
          } @else {
            <div>
              <label for="fn-amount" class="form-label">Số tiền phạt (đ)</label>
              <input id="fn-amount" type="number" min="0" class="input" [(ngModel)]="amount" />
            </div>
            <div>
              <label for="fn-note" class="form-label">Ghi chú</label>
              <input id="fn-note" class="input" maxlength="1000" [(ngModel)]="note" placeholder="VD: Làm hỏng thẻ" />
            </div>
          }
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="creating.set(null)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !cardNo.trim()" (click)="create(mode)">{{ mode === 'build' ? 'Lập phiếu' : 'Thêm phiếu' }}</button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [message]="'Xoá phiếu phạt ' + d.code + ' của thẻ ' + d.cardNo + '?'" (confirmed)="remove(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Fines implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected search: FineTicketSearch = { keyword: '', ticketStatus: null, unpaid: false, from: null, to: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<FineTicket> | null>(null);
  protected readonly totals = signal<FineTicketTotals | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly creating = signal<'build' | 'manual' | null>(null);
  protected readonly deleting = signal<FineTicket | null>(null);
  protected cardNo = '';
  protected amount = 0;
  protected note = '';

  ngOnInit(): void {
    const card = this.route.snapshot.queryParamMap.get('cardNo');
    if (card) this.search.keyword = card;
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`FINES:${action}`);
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

  protected openCreate(mode: 'build' | 'manual'): void {
    this.cardNo = '';
    this.amount = 0;
    this.note = '';
    this.creating.set(mode);
  }

  protected async create(mode: 'build' | 'manual'): Promise<void> {
    this.busy.set(true);
    try {
      const publicId = mode === 'build'
        ? (await this.api.buildFineTicket(this.cardNo.trim())).ticket.publicId
        : (await this.api.addFineTicket({ cardNo: this.cardNo.trim(), amount: Number(this.amount) || 0, note: this.note.trim() || null })).publicId;
      this.creating.set(null);
      await this.router.navigate(['/fine-ticket', publicId]);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(t: FineTicket): Promise<void> {
    try {
      await this.api.deleteFineTicket(t.publicId);
      this.toastr.success(`Đã xoá phiếu ${t.code}.`);
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
      this.deleting.set(null);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = { ...this.search, keyword: this.search.keyword?.trim() || undefined, from: this.search.from || null, to: this.search.to || null, unpaid: this.search.unpaid || null };
      const [page, totals] = await Promise.all([this.api.searchFineTickets(s), this.api.fineTicketTotals(s)]);
      this.page.set(page);
      this.totals.set(totals);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
