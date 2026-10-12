import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CircPlace, CrudPage, Loan, LoanSearch, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/** Lịch sử lưu thông (monolith: /admin/loan-history, quyền LOAN_HISTORY): đang mượn, quá hạn, đã trả. */
@Component({
  selector: 'app-loan-history',
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'LOAN_HISTORY_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-6 gap-4">
        <div class="md:col-span-2">
          <label for="lh-kw" class="field-label">{{ 'LOAN_HISTORY_PAGE.SEARCH_LABEL' | translate }}</label>
          <input id="lh-kw" class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="lh-state" class="field-label">{{ 'LOAN_HISTORY_PAGE.STATE' | translate }}</label>
          <select id="lh-state" class="input" [(ngModel)]="search.state" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option value="open">{{ 'LOAN_HISTORY_PAGE.OPEN' | translate }}</option>
            <option value="overdue">{{ 'LOAN_HISTORY_PAGE.OVERDUE' | translate }}</option>
            <option value="returned">{{ 'LOAN_HISTORY_PAGE.RETURNED' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="lh-place" class="field-label">{{ 'LOAN_HISTORY_PAGE.PLACE' | translate }}</label>
          <select id="lh-place" class="input" [(ngModel)]="search.circPlaceId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div>
          <label for="lh-from" class="field-label">{{ 'LOAN_HISTORY_PAGE.FROM' | translate }}</label>
          <input id="lh-from" type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
        <div>
          <label for="lh-to" class="field-label">{{ 'LOAN_HISTORY_PAGE.TO' | translate }}</label>
          <input id="lh-to" type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
      <div class="flex justify-end gap-2 mt-4">
        <button (click)="export()" class="btn-secondary" [disabled]="exporting()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="panel flex justify-end"><span class="text-sm text-gray-500">{{ 'LOAN_HISTORY_PAGE.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span></div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-44">{{ 'LOAN_HISTORY_PAGE.READER' | translate }}</th><th class="th w-32">{{ 'LOAN_HISTORY_PAGE.BARCODE' | translate }}</th><th class="th">{{ 'LOAN_HISTORY_PAGE.TITLE_COL' | translate }}</th><th class="th w-28">{{ 'LOAN_HISTORY_PAGE.LOANED' | translate }}</th>
            <th class="th w-28">{{ 'LOAN_HISTORY_PAGE.DUE' | translate }}</th><th class="th w-28">{{ 'LOAN_HISTORY_PAGE.RETURNED_AT' | translate }}</th><th class="th w-20 !text-center">{{ 'LOAN_HISTORY_PAGE.RENEWALS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (l of page()?.items ?? []; track l.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ l.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ l.cardNo }}</div></td>
                <td class="td font-mono text-[13px]">{{ l.barcode }}</td>
                <td class="td">{{ l.title ?? '—' }}@if (l.note) { <div class="text-[11px] text-amber-700">{{ l.note }}</div> }</td>
                <td class="td text-[13px]">{{ l.loanedAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                <td class="td text-[13px]" [class.text-red-600]="overdue(l)" [class.font-semibold]="overdue(l)">{{ l.dueAt | ldate: 'dd/MM/yyyy' }}</td>
                <td class="td text-[13px]">
                  @if (l.returnedAt) { {{ l.returnedAt | ldate: 'dd/MM/yyyy HH:mm' }} }
                  @else { <span [class]="overdue(l) ? 'badge-off !bg-red-50 !text-red-700' : 'badge-info'">{{ (overdue(l) ? 'LOAN_HISTORY_PAGE.OVERDUE' : 'LOAN_HISTORY_PAGE.OPEN') | translate }}</span> }
                </td>
                <td class="td text-center">{{ l.renewCount }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('LOAN_HISTORY_PAGE.EMPTY' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" />
      }
    </div>
  `,
})
export class LoanHistory implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected search: LoanSearch = { keyword: '', state: null, circPlaceId: null, from: null, to: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<CrudPage<Loan> | null>(null);
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly loading = signal(false);
  protected readonly exporting = signal(false);

  async ngOnInit(): Promise<void> {
    void this.load();
    try {
      this.places.set(await this.api.crud<CircPlace>('circ-places', 'circulation').searchAll());
    } catch {
      /* thiếu quyền xem điểm lưu thông — chỉ mất bộ lọc điểm */
    }
  }

  protected overdue(l: Loan): boolean {
    return !l.returnedAt && new Date(l.dueAt).getTime() < Date.now();
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

  protected async export(): Promise<void> {
    this.exporting.set(true);
    try {
      const s = this.search;
      saveFile(await this.api.exportLoans({ ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null }), 'lich-su-luu-thong.xlsx');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = this.search;
      this.page.set(await this.api.searchLoans({ ...s, keyword: s.keyword?.trim() || undefined, from: s.from || null, to: s.to || null }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
