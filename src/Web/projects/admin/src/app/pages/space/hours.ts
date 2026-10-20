import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SPACE_CATEGORIES, SpaceSpecialDay, SpaceWeeklyHourRow, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

const WEEKDAYS = ['SPACE_HOURS_PAGE.SUN', 'SPACE_HOURS_PAGE.MON', 'SPACE_HOURS_PAGE.TUE', 'SPACE_HOURS_PAGE.WED',
  'SPACE_HOURS_PAGE.THU', 'SPACE_HOURS_PAGE.FRI', 'SPACE_HOURS_PAGE.SAT'];

/** Giờ mở cửa hàng tuần + ngày đặc biệt (monolith: RoomOpeningHour/RoomSpecialDay, quyền SPACE_HOURS). */
@Component({
  selector: 'app-space-hours',
  imports: [FormsModule, LDatePipe, Loading, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'SPACE_HOURS_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="panel mb-4 relative">
      @if (loadingWeekly()) { <app-loading /> }
      <div class="flex items-center gap-3 mb-3">
        <label class="field-label !mb-0">{{ 'SPACE_CAT.CATEGORY' | translate }}</label>
        <select class="input max-w-xs" [ngModel]="category()" (ngModelChange)="selectCategory($event)">
          <option [ngValue]="null">{{ 'SPACE_HOURS_PAGE.ALL_CATEGORIES' | translate }}</option>
          @for (c of categories; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
        </select>
      </div>
      <table class="w-full text-left border-collapse max-w-xl">
        <thead><tr>
          <th class="th">{{ 'SPACE_HOURS_PAGE.WEEKDAY' | translate }}</th>
          <th class="th w-32">{{ 'SPACE_HOURS_PAGE.OPEN' | translate }}</th>
          <th class="th w-32">{{ 'SPACE_HOURS_PAGE.CLOSE' | translate }}</th>
          <th class="th w-24">{{ 'SPACE_HOURS_PAGE.CLOSED' | translate }}</th>
        </tr></thead>
        <tbody>
          @for (row of rows(); track row.weekday) {
            <tr>
              <td class="td">{{ WEEKDAYS[row.weekday] | translate }}</td>
              <td class="td"><input type="time" class="input !py-1" [(ngModel)]="row.openTime" [disabled]="row.isClosed" /></td>
              <td class="td"><input type="time" class="input !py-1" [(ngModel)]="row.closeTime" [disabled]="row.isClosed" /></td>
              <td class="td"><input type="checkbox" [(ngModel)]="row.isClosed" /></td>
            </tr>
          }
        </tbody>
      </table>
      <button type="button" class="btn-primary mt-3" [disabled]="savingWeekly()" (click)="saveWeekly()">{{ 'COMMON.SAVE' | translate }}</button>
    </div>

    <div class="panel relative">
      @if (loadingSpecial()) { <app-loading /> }
      <div class="flex items-center justify-between mb-3">
        <div class="font-medium text-gray-800">{{ 'SPACE_HOURS_PAGE.SPECIAL_DAYS' | translate }}</div>
        <button type="button" class="btn-secondary" (click)="openAdd()"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD' | translate }}</button>
      </div>
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th w-32">{{ 'SPACE_HOURS_PAGE.DATE' | translate }}</th>
          <th class="th w-40">{{ 'SPACE_CAT.CATEGORY' | translate }}</th>
          <th class="th w-24">{{ 'SPACE_HOURS_PAGE.CLOSED' | translate }}</th>
          <th class="th w-28">{{ 'SPACE_HOURS_PAGE.OPEN' | translate }}</th>
          <th class="th w-28">{{ 'SPACE_HOURS_PAGE.CLOSE' | translate }}</th>
          <th class="th">{{ 'SPACE_HOURS_PAGE.REASON' | translate }}</th>
          <th class="th w-16 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
        </tr></thead>
        <tbody>
          @for (d of specialDays(); track d.publicId) {
            <tr>
              <td class="td">{{ d.date | ldate: 'dd/MM/yyyy' }}</td>
              <td class="td">{{ d.category ? (categoryLabel(d.category) | translate) : ('SPACE_HOURS_PAGE.ALL_CATEGORIES' | translate) }}</td>
              <td class="td">{{ d.isClosed ? ('COMMON.YES' | translate) : ('COMMON.NO' | translate) }}</td>
              <td class="td">{{ d.openTime ?? '—' }}</td>
              <td class="td">{{ d.closeTime ?? '—' }}</td>
              <td class="td">{{ d.reason ?? '—' }}</td>
              <td class="td text-center">
                <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(d)">
                  <span class="material-icons text-[18px]">delete</span></button>
              </td>
            </tr>
          } @empty { <tr><td colspan="7" class="py-6 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
        </tbody>
      </table>
    </div>

    @if (adding()) {
      <app-modal [title]="'SPACE_HOURS_PAGE.SPECIAL_DAYS' | translate" widthClass="max-w-lg" (closed)="adding.set(false)">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="field-label">{{ 'SPACE_HOURS_PAGE.DATE' | translate }} <span class="text-red-500">*</span></label>
            <input type="date" class="input" [(ngModel)]="newDay.date" [ngModelOptions]="{ standalone: true }" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_CAT.CATEGORY' | translate }}</label>
            <select class="input" [(ngModel)]="newDay.category" [ngModelOptions]="{ standalone: true }">
              <option [ngValue]="null">{{ 'SPACE_HOURS_PAGE.ALL_CATEGORIES' | translate }}</option>
              @for (c of categories; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
            </select>
          </div>
          <div class="flex items-center gap-2 col-span-2">
            <input id="sd-closed" type="checkbox" [(ngModel)]="newDay.isClosed" [ngModelOptions]="{ standalone: true }" />
            <label for="sd-closed" class="field-label !mb-0">{{ 'SPACE_HOURS_PAGE.CLOSED' | translate }}</label>
          </div>
          @if (!newDay.isClosed) {
            <div>
              <label class="field-label">{{ 'SPACE_HOURS_PAGE.OPEN' | translate }}</label>
              <input type="time" class="input" [(ngModel)]="newDay.openTime" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="field-label">{{ 'SPACE_HOURS_PAGE.CLOSE' | translate }}</label>
              <input type="time" class="input" [(ngModel)]="newDay.closeTime" [ngModelOptions]="{ standalone: true }" />
            </div>
          }
          <div class="col-span-2">
            <label class="field-label">{{ 'SPACE_HOURS_PAGE.REASON' | translate }}</label>
            <input class="input" maxlength="250" [(ngModel)]="newDay.reason" [ngModelOptions]="{ standalone: true }" />
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="adding.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="savingSpecial() || !newDay.date" (click)="addSpecialDay()">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="savingSpecial()" (confirmed)="removeSpecialDay(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class SpaceHours implements OnInit {
  protected readonly WEEKDAYS = WEEKDAYS;
  protected readonly categories = SPACE_CATEGORIES;

  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly specialDaysClient = this.api.crud<SpaceSpecialDay>('special-days', 'space');

  protected readonly category = signal<number | null>(null);
  protected readonly rows = signal<SpaceWeeklyHourRow[]>([]);
  protected readonly loadingWeekly = signal(false);
  protected readonly savingWeekly = signal(false);

  protected readonly specialDays = signal<SpaceSpecialDay[]>([]);
  protected readonly loadingSpecial = signal(false);
  protected readonly savingSpecial = signal(false);
  protected readonly adding = signal(false);
  protected readonly deleting = signal<SpaceSpecialDay | null>(null);
  protected newDay: { date: string; category: number | null; isClosed: boolean; openTime: string | null; closeTime: string | null; reason: string | null } = {
    date: '', category: null, isClosed: false, openTime: '07:00', closeTime: '21:00', reason: null,
  };

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadWeekly(), this.loadSpecialDays()]);
  }

  protected categoryLabel(value: number): string {
    return this.categories.find((c) => c.value === value)?.label ?? String(value);
  }

  protected selectCategory(value: number | null): void {
    this.category.set(value);
    void this.loadWeekly();
  }

  private async loadWeekly(): Promise<void> {
    this.loadingWeekly.set(true);
    try {
      this.rows.set(await this.api.spaceWeeklyHours(this.category()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loadingWeekly.set(false);
    }
  }

  protected async saveWeekly(): Promise<void> {
    this.savingWeekly.set(true);
    try {
      this.rows.set(await this.api.saveSpaceWeeklyHours(this.category(), this.rows()));
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.savingWeekly.set(false);
    }
  }

  private async loadSpecialDays(): Promise<void> {
    this.loadingSpecial.set(true);
    try {
      this.specialDays.set(await this.specialDaysClient.searchAll());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loadingSpecial.set(false);
    }
  }

  protected openAdd(): void {
    this.newDay = { date: '', category: null, isClosed: false, openTime: '07:00', closeTime: '21:00', reason: null };
    this.adding.set(true);
  }

  protected async addSpecialDay(): Promise<void> {
    this.savingSpecial.set(true);
    try {
      await this.specialDaysClient.add({
        date: this.newDay.date, category: this.newDay.category, isClosed: this.newDay.isClosed,
        openTime: this.newDay.isClosed ? null : this.newDay.openTime, closeTime: this.newDay.isClosed ? null : this.newDay.closeTime,
        reason: this.newDay.reason,
      });
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.adding.set(false);
      await this.loadSpecialDays();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.savingSpecial.set(false);
    }
  }

  protected async removeSpecialDay(d: SpaceSpecialDay): Promise<void> {
    this.savingSpecial.set(true);
    try {
      await this.specialDaysClient.delete(d.publicId);
      this.deleting.set(null);
      await this.loadSpecialDays();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.savingSpecial.set(false);
    }
  }
}
