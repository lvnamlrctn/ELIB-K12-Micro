import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage as Page, SpaceBan, SpaceBanSearch, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/** Cấm đặt phòng (monolith: RoomBookingBanService, quyền SPACE_BANS) — thủ công hoặc tự động (job vắng mặt nhiều lần). */
@Component({
  selector: 'app-space-bans',
  imports: [FormsModule, LDatePipe, Loading, Modal, Paginator, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'SPACE_BANS_PAGE.TITLE' | translate }}</h4>
      @if (can('edit')) {
        <button type="button" class="btn-primary ml-auto" (click)="adding.set(true)">
          <span class="material-icons text-[18px]">block</span> {{ 'SPACE_BANS_PAGE.ADD' | translate }}
        </button>
      }
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
        <div>
          <label class="field-label">{{ 'COMMON.SEARCH' | translate }}</label>
          <input class="input" [(ngModel)]="search.keyword" (keydown.enter)="find()" [placeholder]="'SPACE_BANS_PAGE.SEARCH_PH' | translate" />
        </div>
        <div class="flex items-center gap-2">
          <input id="active-only" type="checkbox" [(ngModel)]="activeOnly" (change)="find()" />
          <label for="active-only" class="field-label !mb-0">{{ 'SPACE_BANS_PAGE.ACTIVE_ONLY' | translate }}</label>
        </div>
        <div class="flex items-end">
          <button (click)="find()" class="btn-primary"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mt-4">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th w-44">{{ 'SPACE_BANS_PAGE.READER' | translate }}</th>
            <th class="th">{{ 'SPACE_BANS_PAGE.REASON' | translate }}</th>
            <th class="th w-28">{{ 'SPACE_BANS_PAGE.SOURCE' | translate }}</th>
            <th class="th w-36">{{ 'SPACE_BANS_PAGE.BANNED_UNTIL' | translate }}</th>
            <th class="th w-28">{{ 'COMMON.STATUS' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (b of page()?.items ?? []; track b.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ b.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ b.cardNo }}</div></td>
                <td class="td">{{ b.reason }}</td>
                <td class="td text-[13px]">{{ b.source === 1 ? ('SPACE_BANS_PAGE.SOURCE_AUTO' | translate) : ('SPACE_BANS_PAGE.SOURCE_STAFF' | translate) }}</td>
                <td class="td text-[13px]">{{ b.bannedUntil | ldate: 'dd/MM/yyyy HH:mm' }}</td>
                <td class="td"><span [class]="b.isActive ? 'badge-warn' : 'badge-off'">{{ (b.isActive ? 'SPACE_BANS_PAGE.ACTIVE' : 'SPACE_BANS_PAGE.LIFTED') | translate }}</span></td>
                <td class="td text-center">
                  @if (can('edit') && b.isActive) {
                    <button class="icon-btn text-green-600 hover:bg-green-50" [title]="'SPACE_BANS_PAGE.LIFT' | translate" [disabled]="busy()" (click)="lift(b)">
                      <span class="material-icons text-[18px]">lock_open</span></button>
                  }
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (adding()) {
      <app-modal [title]="'SPACE_BANS_PAGE.ADD' | translate" widthClass="max-w-md" (closed)="adding.set(false)">
        <div class="space-y-3">
          <div>
            <label class="field-label">{{ 'SPACE_BANS_PAGE.CARD_NO' | translate }} <span class="text-red-500">*</span></label>
            <input class="input" [(ngModel)]="newCardNo" [ngModelOptions]="{ standalone: true }" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_BANS_PAGE.DAYS' | translate }} <span class="text-red-500">*</span></label>
            <input type="number" class="input" min="1" max="365" [(ngModel)]="newDays" [ngModelOptions]="{ standalone: true }" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_BANS_PAGE.REASON' | translate }} <span class="text-red-500">*</span></label>
            <input class="input" maxlength="500" [(ngModel)]="newReason" [ngModelOptions]="{ standalone: true }" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="adding.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !newCardNo.trim() || !newReason.trim()" (click)="add()">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class SpaceBans implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected search: SpaceBanSearch = { keyword: '', pageIndex: 1, pageSize: 10 };
  protected activeOnly = false;
  protected readonly page = signal<Page<SpaceBan> | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly adding = signal(false);
  protected newCardNo = '';
  protected newDays = 7;
  protected newReason = '';

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`SPACE_BANS:${action}`);
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

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.api.searchSpaceBans({ ...this.search, activeOnly: this.activeOnly || null }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async add(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.addSpaceBan(this.newCardNo.trim(), this.newDays, this.newReason.trim());
      this.toastr.success('SPACE_BANS_PAGE.ADD_SUCCESS');
      this.adding.set(false);
      this.newCardNo = '';
      this.newDays = 7;
      this.newReason = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async lift(b: SpaceBan): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.liftSpaceBan(b.publicId);
      this.toastr.success('SPACE_BANS_PAGE.LIFT_SUCCESS');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
