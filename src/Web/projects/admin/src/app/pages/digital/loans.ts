import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, CrudPage as Page, EBOOK_LOAN_STATUSES, EBOOK_RESERVATION_STATUSES, EbookItem, EbookLoan, EbookLoanSearch, EbookReservation,
  EbookReservationSearch, errorMessage,
} from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/**
 * Mượn/trả/đặt trước tài liệu số — tra cứu toàn hệ thống (LRC: EbookItemLoanRepository/EbookItemReservationRepository, quyền
 * DIGITAL_LOANS, chỉ View/Edit). Hai tab: Mượn/trả (có Thu hồi/Thu hồi tất cả) và Đặt trước (chỉ tra cứu — huỷ/thăng hạng chỉ
 * chạy từ luồng bạn đọc qua OPAC).
 */
@Component({
  selector: 'app-digital-loans',
  imports: [FormsModule, LDatePipe, Loading, Modal, Paginator, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'DIGITAL_LOANS_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="mb-4 flex gap-2 border-b border-gray-200">
      <button (click)="tab.set('loan')" class="px-4 py-2 text-sm font-medium border-b-2 -mb-px" [class]="tab() === 'loan' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'">
        {{ 'DIGITAL_LOANS_PAGE.TAB_LOANS' | translate }}
      </button>
      <button (click)="tab.set('reservation')" class="px-4 py-2 text-sm font-medium border-b-2 -mb-px" [class]="tab() === 'reservation' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'">
        {{ 'DIGITAL_LOANS_PAGE.TAB_RESERVATIONS' | translate }}
      </button>
    </div>

    @if (tab() === 'loan') {
      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
          <div>
            <label for="dl-item" class="field-label">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }}</label>
            <select id="dl-item" class="input" [(ngModel)]="loanSearch.ebookItemPublicId" (change)="findLoans()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (i of items(); track i.publicId) { <option [ngValue]="i.publicId">{{ i.title }}</option> }
            </select>
          </div>
          <div>
            <label for="dl-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="dl-status" class="input" [(ngModel)]="loanSearch.loanStatus" (change)="findLoans()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (s of loanStatuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
            </select>
          </div>
          <div class="flex items-end gap-2">
            <button (click)="findLoans()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
            @if (can('edit') && loanSearch.ebookItemPublicId) {
              <button (click)="openRecallAll()" class="btn-outline-danger !py-2"><span class="material-icons text-[18px]">block</span> {{ 'DIGITAL_LOANS_PAGE.RECALL_ALL' | translate }}</button>
            }
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
        @if (loansLoading()) { <app-loading /> }
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[1000px]">
            <thead><tr>
              <th class="th w-44">{{ 'DIGITAL_LOANS_PAGE.READER' | translate }}</th><th class="th">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-36">{{ 'DIGITAL_LOANS_PAGE.CHECKED_OUT' | translate }}</th><th class="th w-36">{{ 'DIGITAL_LOANS_PAGE.EXPIRES' | translate }}</th>
              <th class="th w-32">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-20 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (l of loanPage()?.items ?? []; track l.publicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td">{{ l.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ l.cardNo }}</div></td>
                  <td class="td">{{ l.title ?? '—' }}</td>
                  <td class="td text-[13px]">{{ l.checkedOutAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                  <td class="td text-[13px]">{{ l.expiresAt ? (l.expiresAt | ldate: 'dd/MM/yyyy') : ('DIGITAL_CAT.NO_LIMIT' | translate) }}</td>
                  <td class="td"><span [class]="loanStatus(l.status).badge">{{ loanStatus(l.status).name | translate }}</span></td>
                  <td class="td text-center">
                    @if (can('edit') && l.status === 1) {
                      <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'DIGITAL_LOANS_PAGE.RECALL' | translate" (click)="recalling.set(l)">
                        <span class="material-icons text-[18px]">block</span></button>
                    }
                  </td>
                </tr>
              } @empty { <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loansLoading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
            </tbody>
          </table>
        </div>
        @if (loanPage(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="loanSearch.pageIndex!" [pageSize]="loanSearch.pageSize!" (pageChange)="onLoanPage($event)" /> }
      </div>
    } @else {
      <div class="panel">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
          <div>
            <label for="dr-item" class="field-label">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }}</label>
            <select id="dr-item" class="input" [(ngModel)]="reservationSearch.ebookItemPublicId" (change)="findReservations()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (i of items(); track i.publicId) { <option [ngValue]="i.publicId">{{ i.title }}</option> }
            </select>
          </div>
          <div>
            <label for="dr-status" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="dr-status" class="input" [(ngModel)]="reservationSearch.reservationStatus" (change)="findReservations()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              @for (s of reservationStatuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
            </select>
          </div>
          <div class="flex items-end">
            <button (click)="findReservations()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
          </div>
        </div>
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
        @if (reservationsLoading()) { <app-loading /> }
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>
              <th class="th w-44">{{ 'DIGITAL_LOANS_PAGE.READER' | translate }}</th><th class="th">{{ 'DIGITAL_ITEMS_PAGE.TITLE_COL' | translate }}</th>
              <th class="th w-36">{{ 'DIGITAL_LOANS_PAGE.REQUESTED' | translate }}</th><th class="th w-28 !text-center">{{ 'DIGITAL_LOANS_PAGE.QUEUE' | translate }}</th>
              <th class="th w-36">{{ 'COMMON.STATUS' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (r of reservationPage()?.items ?? []; track r.publicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td">{{ r.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ r.cardNo }}</div></td>
                  <td class="td">{{ r.title ?? '—' }}</td>
                  <td class="td text-[13px]">{{ r.requestedAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                  <td class="td text-center">{{ r.queuePosition ?? '—' }}</td>
                  <td class="td"><span [class]="reservationStatus(r.status).badge">{{ reservationStatus(r.status).name | translate }}</span>
                    @if (r.status === 2 && r.readyExpiresAt) { <div class="text-[11px] text-amber-700">{{ 'DIGITAL_LOANS_PAGE.READY_UNTIL' | translate: { date: (r.readyExpiresAt | ldate: 'dd/MM/yyyy HH:mm') } }}</div> }
                  </td>
                </tr>
              } @empty { <tr><td colspan="5" class="py-8 text-center text-gray-500">{{ reservationsLoading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
            </tbody>
          </table>
        </div>
        @if (reservationPage(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="reservationSearch.pageIndex!" [pageSize]="reservationSearch.pageSize!" (pageChange)="onReservationPage($event)" /> }
      </div>
    }

    @if (recalling(); as l) {
      <app-modal [title]="'DIGITAL_LOANS_PAGE.RECALL_TITLE' | translate: { card: l.cardNo }" widthClass="max-w-md" (closed)="recalling.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ l.title }}</p>
        <label for="dl-reason" class="form-label">{{ 'DIGITAL_LOANS_PAGE.REASON' | translate }} <span class="text-red-500">*</span></label>
        <input id="dl-reason" class="input" maxlength="500" [(ngModel)]="reason" [ngModelOptions]="{ standalone: true }" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="recalling.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="busy() || !reason.trim()" (click)="recall(l)">{{ 'DIGITAL_LOANS_PAGE.RECALL' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (recallingAll()) {
      <app-modal title="DIGITAL_LOANS_PAGE.RECALL_ALL" widthClass="max-w-md" (closed)="recallingAll.set(false)">
        <label for="dl-reason-all" class="form-label">{{ 'DIGITAL_LOANS_PAGE.REASON' | translate }} <span class="text-red-500">*</span></label>
        <input id="dl-reason-all" class="input" maxlength="500" [(ngModel)]="reason" [ngModelOptions]="{ standalone: true }" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="recallingAll.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="busy() || !reason.trim()" (click)="recallAll()">{{ 'DIGITAL_LOANS_PAGE.RECALL_ALL' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class DigitalLoans implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly route = inject(ActivatedRoute);
  private readonly loanClient = this.api.crud<EbookLoan>('loans', 'digital');
  private readonly reservationClient = this.api.crud<EbookReservation>('reservations', 'digital');

  protected readonly loanStatuses = EBOOK_LOAN_STATUSES;
  protected readonly reservationStatuses = EBOOK_RESERVATION_STATUSES;
  protected readonly tab = signal<'loan' | 'reservation'>('loan');
  protected readonly items = signal<EbookItem[]>([]);

  protected loanSearch: EbookLoanSearch = { ebookItemPublicId: null, loanStatus: null, pageIndex: 1, pageSize: 10 };
  protected readonly loanPage = signal<Page<EbookLoan> | null>(null);
  protected readonly loansLoading = signal(false);

  protected reservationSearch: EbookReservationSearch = { ebookItemPublicId: null, reservationStatus: null, pageIndex: 1, pageSize: 10 };
  protected readonly reservationPage = signal<Page<EbookReservation> | null>(null);
  protected readonly reservationsLoading = signal(false);

  protected readonly busy = signal(false);
  protected readonly recalling = signal<EbookLoan | null>(null);
  protected readonly recallingAll = signal(false);
  protected reason = '';

  ngOnInit(): void {
    // Từ màn Tài liệu số ("Mượn/thu hồi"): mở sẵn với bộ lọc theo tài liệu đó.
    const item = this.route.snapshot.queryParamMap.get('item');
    if (item) {
      this.loanSearch.ebookItemPublicId = item;
      this.reservationSearch.ebookItemPublicId = item;
    }
    void this.loadLoans();
    void this.loadReservations();
    void this.loadItems();
  }

  protected can(action: string): boolean {
    return this.session.can(`DIGITAL_LOANS:${action}`);
  }

  protected loanStatus(code: number) {
    return this.loanStatuses.find((s) => s.code === code) ?? { name: String(code), badge: 'badge-off' };
  }

  protected reservationStatus(code: number) {
    return this.reservationStatuses.find((s) => s.code === code) ?? { name: String(code), badge: 'badge-off' };
  }

  private async loadItems(): Promise<void> {
    try {
      this.items.set(await this.api.crud<EbookItem>('items', 'digital').searchAll());
    } catch {
      /* thiếu quyền xem danh mục liên quan — ô chọn để trống */
    }
  }

  protected findLoans(): void {
    this.loanSearch.pageIndex = 1;
    void this.loadLoans();
  }

  protected onLoanPage(e: { pageIndex: number; pageSize: number }): void {
    this.loanSearch.pageIndex = e.pageIndex;
    this.loanSearch.pageSize = e.pageSize;
    void this.loadLoans();
  }

  private async loadLoans(): Promise<void> {
    this.loansLoading.set(true);
    try {
      this.loanPage.set(await this.loanClient.search(this.loanSearch));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loansLoading.set(false);
    }
  }

  protected findReservations(): void {
    this.reservationSearch.pageIndex = 1;
    void this.loadReservations();
  }

  protected onReservationPage(e: { pageIndex: number; pageSize: number }): void {
    this.reservationSearch.pageIndex = e.pageIndex;
    this.reservationSearch.pageSize = e.pageSize;
    void this.loadReservations();
  }

  private async loadReservations(): Promise<void> {
    this.reservationsLoading.set(true);
    try {
      this.reservationPage.set(await this.reservationClient.search(this.reservationSearch));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.reservationsLoading.set(false);
    }
  }

  protected openRecallAll(): void {
    this.reason = '';
    this.recallingAll.set(true);
  }

  protected async recall(loan: EbookLoan): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.recallEbookLoan(loan.publicId, this.reason.trim());
      this.toastr.success('DIGITAL_LOANS_PAGE.RECALL_SUCCESS');
      this.recalling.set(null);
      this.reason = '';
      await this.loadLoans();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async recallAll(): Promise<void> {
    const itemPublicId = this.loanSearch.ebookItemPublicId;
    if (!itemPublicId) return;
    this.busy.set(true);
    try {
      const count = await this.api.recallAllEbookLoans(itemPublicId, this.reason.trim());
      this.toastr.success('DIGITAL_LOANS_PAGE.RECALL_ALL_SUCCESS', { count });
      this.recallingAll.set(false);
      this.reason = '';
      await this.loadLoans();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
