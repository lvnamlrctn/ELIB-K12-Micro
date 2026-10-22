import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, CrudPage, FineTicket, FineTicketSearch, FineTicketTotals, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { clampPage, rememberSearch, rememberedSearch } from '../../core/list-state';
import { ToastrService } from '../../shared/toastr';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Quản lý phạt (monolith: /admin/fines + /admin/fine-tickets, quyền FINES). Danh sách phiếu phạt kèm tổng phải thu / đã thu / còn lại
 * trên toàn bộ kết quả lọc; lập phiếu gom tài liệu quá hạn của bạn đọc hoặc phiếu thủ công.
 */
@Component({
  selector: 'app-fines',
  imports: [FormsModule, LDatePipe, LNumberPipe, RouterLink, Loading, Paginator, Modal, ReasonDialog, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'FINES_PAGE.TITLE' | translate }}</h4>
      @if (can('add')) {
        <div class="ml-auto flex gap-2">
          <button class="btn-primary" (click)="openCreate('build')"><span class="material-icons text-[18px]">playlist_add_check</span> {{ 'FINES_PAGE.BUILD' | translate }}</button>
          <button class="btn-secondary" (click)="openCreate('manual')"><span class="material-icons text-[18px]">add</span> {{ 'FINES_PAGE.MANUAL' | translate }}</button>
        </div>
      }
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="fn-kw" class="field-label">{{ 'FINES_PAGE.SEARCH_LABEL' | translate }}</label>
          <input id="fn-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" placeholder="VD: PT001, HS001..." />
        </div>
        <div>
          <label for="fn-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="fn-status" class="input" [(ngModel)]="search.ticketStatus" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option [ngValue]="1">{{ 'FINES_PAGE.OPEN' | translate }}</option>
            <option [ngValue]="2">{{ 'FINES_PAGE.DONE' | translate }}</option>
          </select>
        </div>
        <div class="flex items-end pb-2">
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="search.unpaid" (change)="find()" /> {{ 'FINES_PAGE.UNPAID_ONLY' | translate }}
          </label>
        </div>
        <div>
          <label for="fn-from" class="field-label">{{ 'FINES_PAGE.FROM' | translate }}</label>
          <input id="fn-from" type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
        <div>
          <label for="fn-to" class="field-label">{{ 'FINES_PAGE.TO' | translate }}</label>
          <input id="fn-to" type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
      <div class="flex justify-end mt-4">
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    @if (totals(); as t) {
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-5">
        <div class="panel !mb-0"><div class="text-xs text-gray-500">{{ 'FINES_PAGE.RECEIVABLE' | translate }}</div><div class="text-xl font-semibold text-gray-800">{{ t.receivable | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
        <div class="panel !mb-0"><div class="text-xs text-gray-500">{{ 'FINES_PAGE.RECEIVED' | translate }}</div><div class="text-xl font-semibold text-green-700">{{ t.received | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
        <div class="panel !mb-0"><div class="text-xs text-gray-500">{{ 'FINES_PAGE.REMAINING' | translate }}</div><div class="text-xl font-semibold" [class.text-red-600]="t.remaining > 0">{{ t.remaining | lnumber: '1.0-0' }} {{ 'COMMON.VND' | translate }}</div></div>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-24">{{ 'FINES_PAGE.CODE' | translate }}</th><th class="th">{{ 'FINES_PAGE.READER' | translate }}</th><th class="th w-28">{{ 'FINES_PAGE.DATE' | translate }}</th><th class="th w-20 !text-center">{{ 'FINES_PAGE.ROUND' | translate }}</th>
            <th class="th w-32 !text-right">{{ 'FINES_PAGE.TOTAL' | translate }}</th><th class="th w-32 !text-right">{{ 'FINES_PAGE.PAID' | translate }}</th><th class="th w-32 !text-right">{{ 'FINES_PAGE.REMAINING' | translate }}</th>
            <th class="th w-32">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-24 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (t of page()?.items ?? []; track t.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]"><a class="text-blue-600 hover:underline" [routerLink]="['/fine-ticket', t.publicId]">{{ t.code }}</a></td>
                <td class="td">{{ t.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ t.cardNo }}</div>
                  @if (t.note) { <div class="text-[11px] text-gray-500">{{ t.note }}</div> }</td>
                <td class="td text-[13px]">{{ t.fineDate | ldate: 'dd/MM/yyyy' }}</td>
                <td class="td text-center">{{ t.round }}</td>
                <td class="td text-right">{{ t.total | lnumber: '1.0-0' }}</td>
                <td class="td text-right">{{ t.paid | lnumber: '1.0-0' }}</td>
                <td class="td text-right" [class.text-red-600]="t.remaining > 0" [class.font-semibold]="t.remaining > 0">{{ t.remaining | lnumber: '1.0-0' }}</td>
                <td class="td"><span [class]="t.status === 2 ? 'badge-on' : 'badge-info'">{{ (t.status === 2 ? 'FINES_PAGE.DONE' : 'FINES_PAGE.OPEN') | translate }}</span></td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1">
                    <a class="icon-btn text-blue-600 hover:bg-blue-50" [title]="'FINES_PAGE.DETAIL' | translate" [routerLink]="['/fine-ticket', t.publicId]"><span class="material-icons text-[18px]">visibility</span></a>
                    @if (can('delete') && t.status === 1 && t.paid === 0) {
                      <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(t)"><span class="material-icons text-[18px]">delete</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="9" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('FINES_PAGE.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>

    @if (creating(); as mode) {
      <app-modal [title]="mode === 'build' ? 'FINES_PAGE.BUILD_TITLE' : 'FINES_PAGE.MANUAL_TITLE'" widthClass="max-w-md" (closed)="creating.set(null)">
        <div class="space-y-4">
          <div>
            <label for="fn-card" class="form-label">{{ 'FINES_PAGE.CARD' | translate }}</label>
            <input id="fn-card" class="input font-mono" [(ngModel)]="cardNo" [placeholder]="'FINES_PAGE.CARD_PH' | translate" autocomplete="off" />
          </div>
          @if (mode === 'build') {
            <p class="text-sm text-gray-600">{{ 'FINES_PAGE.BUILD_HINT' | translate }}</p>
          } @else {
            <div>
              <label for="fn-amount" class="form-label">{{ 'FINES_PAGE.AMOUNT' | translate }}</label>
              <input id="fn-amount" type="number" min="0" class="input" [(ngModel)]="amount" />
            </div>
            <div>
              <label for="fn-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label>
              <input id="fn-note" class="input" maxlength="1000" [(ngModel)]="note" [placeholder]="'FINES_PAGE.NOTE_EG' | translate" />
            </div>
          }
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="creating.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !cardNo.trim()" (click)="create(mode)">{{ (mode === 'build' ? 'FINES_PAGE.CREATE_BUILD' : 'FINES_PAGE.CREATE_MANUAL') | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-reason-dialog title="FINES_PAGE.DELETE_TITLE" message="FINES_PAGE.DELETE_CONFIRM" [params]="{ code: d.code, card: d.cardNo }" confirmText="COMMON.DELETE"
                         (confirmed)="remove(d, $event)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Fines implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected search: FineTicketSearch = rememberedSearch<FineTicketSearch>('fines', { keyword: '', ticketStatus: null, unpaid: false, from: null, to: null, pageIndex: 1, pageSize: 10 });
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

  protected async remove(t: FineTicket, result: ReasonResult): Promise<void> {
    try {
      await this.api.deleteFineTicket(t.publicId, result.reason);
      this.toastr.success('FINES_PAGE.DELETED', { code: t.code });
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
      if (clampPage(this.search, page)) return void (await this.load());
      this.page.set(page);
      rememberSearch('fines', this.search);
      this.totals.set(totals);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
