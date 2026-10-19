import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CircPlace, CrudPage, Visit, VisitSearch, errorMessage, saveFile } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { clampPage, rememberSearch, rememberedSearch } from '../../core/list-state';
import { ToastrService } from '../../shared/toastr';
import { Loading, Paginator } from '../../shared/ui';

/**
 * Lịch sử vào/ra thư viện (monolith: Receiption/Check/SearchHistory, quyền CHECK_IN_OUT) — gồm cả lượt đang ở trong
 * (checkedOutAt null, cột Ra hiện "Đang ở trong") như ELIB-LRC.
 */
@Component({
  selector: 'app-checkin-history',
  imports: [FormsModule, LDatePipe, Loading, Paginator, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'CHECKIN_HISTORY_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
        <div class="md:col-span-2">
          <label for="ch-card" class="field-label">{{ 'CHECKIN_HISTORY_PAGE.CARD' | translate }}</label>
          <input id="ch-card" class="input font-mono" [(ngModel)]="search.cardNo" (keydown.enter)="find()" />
        </div>
        <div>
          <label for="ch-state" class="field-label">{{ 'CHECKIN_HISTORY_PAGE.STATE' | translate }}</label>
          <select id="ch-state" class="input" [(ngModel)]="search.state" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            <option value="open">{{ 'CHECK_IN_OUT_PAGE.INSIDE' | translate }}</option>
            <option value="closed">{{ 'CHECK_IN_OUT_PAGE.OUTSIDE' | translate }}</option>
          </select>
        </div>
        <div>
          <label for="ch-place" class="field-label">{{ 'CHECK_IN_OUT_PAGE.PLACE' | translate }}</label>
          <select id="ch-place" class="input" [(ngModel)]="search.circPlaceId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (p of places(); track p.id) { <option [ngValue]="p.id">{{ p.code }} — {{ p.name }}</option> }
          </select>
        </div>
        <div>
          <label for="ch-from" class="field-label">{{ 'LOAN_HISTORY_PAGE.FROM' | translate }}</label>
          <input id="ch-from" type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
      </div>
      <div class="grid grid-cols-1 md:grid-cols-5 gap-4 mt-3">
        <div>
          <label for="ch-to" class="field-label">{{ 'LOAN_HISTORY_PAGE.TO' | translate }}</label>
          <input id="ch-to" type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
      <div class="flex justify-end gap-2 mt-4">
        <button (click)="export()" class="btn-secondary" [disabled]="exporting()"><span class="material-icons text-[18px]">download</span> {{ 'COMMON.EXPORT_EXCEL' | translate }}</button>
        <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
      </div>
    </div>

    <div class="panel flex justify-end items-center gap-2">
      <span class="text-sm text-gray-500">{{ 'LOAN_HISTORY_PAGE.COUNT' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-44">{{ 'CHECKIN_HISTORY_PAGE.CARD' | translate }}</th>
            <th class="th w-36">{{ 'BORROW_PAGE.READER_TYPE' | translate }}</th>
            <th class="th w-40">{{ 'CHECKIN_HISTORY_PAGE.PLACE_IN' | translate }}</th>
            <th class="th w-40">{{ 'CHECKIN_HISTORY_PAGE.CHECKED_IN_AT' | translate }}</th>
            <th class="th w-40">{{ 'CHECKIN_HISTORY_PAGE.PLACE_OUT' | translate }}</th>
            <th class="th w-40">{{ 'CHECKIN_HISTORY_PAGE.CHECKED_OUT_AT' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (v of page()?.items ?? []; track v.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ v.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ v.cardNo }}</div></td>
                <td class="td text-[13px]">{{ v.readerTypeName ?? '—' }}</td>
                <td class="td text-[13px]">{{ v.circPlaceName ?? '—' }}</td>
                <td class="td text-[13px]">{{ v.checkedInAt | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                <td class="td text-[13px]">{{ v.circPlaceOutName ?? '—' }}</td>
                <td class="td text-[13px]">
                  @if (v.checkedOutAt) { {{ v.checkedOutAt | ldate: 'dd/MM/yyyy HH:mm' }} }
                  @else { <span class="badge-on">{{ 'CHECK_IN_OUT_PAGE.INSIDE' | translate }}</span> }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('LOAN_HISTORY_PAGE.EMPTY' | translate) }}</td></tr>
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
export class CheckinHistory implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected search: VisitSearch = rememberedSearch<VisitSearch>('checkin-history',
    { cardNo: '', state: null, circPlaceId: null, from: null, to: null, pageIndex: 1, pageSize: 10 });
  protected readonly places = signal<CircPlace[]>([]);
  protected readonly page = signal<CrudPage<Visit> | null>(null);
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
      saveFile(await this.api.exportVisits({ ...s, cardNo: s.cardNo?.trim() || undefined, from: s.from || null, to: s.to || null }), 'lich-su-vao-ra.xlsx');
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
      const page = await this.api.searchVisits({ ...s, cardNo: s.cardNo?.trim() || undefined, from: s.from || null, to: s.to || null });
      if (clampPage(s, page)) return void (await this.load());
      this.page.set(page);
      rememberSearch('checkin-history', s);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
