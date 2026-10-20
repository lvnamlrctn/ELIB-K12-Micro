import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SpaceSettings, errorMessage } from '../../core/api';
import { Loading } from '../../shared/ui';
import { ToastrService } from '../../shared/toastr';

/** Tham số đặt phòng của đơn vị (monolith: SystemParameter ROOM_BOOKING_*, quyền SPACE_SETTINGS) — tự seed khi đọc lần đầu. */
@Component({
  selector: 'app-space-settings',
  imports: [FormsModule, Loading, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'SPACE_SETTINGS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel relative max-w-2xl">
      @if (loading()) { <app-loading /> }
      @if (settings(); as s) {
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.DEFAULT_OPEN' | translate }}</label>
            <input type="time" class="input" [(ngModel)]="s.defaultOpenTime" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.DEFAULT_CLOSE' | translate }}</label>
            <input type="time" class="input" [(ngModel)]="s.defaultCloseTime" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.MAX_PER_DAY' | translate }}</label>
            <input type="number" class="input" min="0" [(ngModel)]="s.maxPerDay" />
            <p class="text-xs text-gray-500 mt-1">{{ 'SPACE_SETTINGS_PAGE.MAX_PER_DAY_HINT' | translate }}</p>
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.MAX_ACTIVE' | translate }}</label>
            <input type="number" class="input" min="0" [(ngModel)]="s.maxActive" />
            <p class="text-xs text-gray-500 mt-1">{{ 'SPACE_SETTINGS_PAGE.MAX_ACTIVE_HINT' | translate }}</p>
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.MAX_SESSION_MINUTES' | translate }}</label>
            <input type="number" class="input" min="0" [(ngModel)]="s.maxSessionMinutes" />
          </div>
          <div></div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.NO_SHOW_LIMIT' | translate }}</label>
            <input type="number" class="input" min="0" [(ngModel)]="s.noShowLimit" />
            <p class="text-xs text-gray-500 mt-1">{{ 'SPACE_SETTINGS_PAGE.NO_SHOW_LIMIT_HINT' | translate }}</p>
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.NO_SHOW_WINDOW_DAYS' | translate }}</label>
            <input type="number" class="input" min="1" [(ngModel)]="s.noShowWindowDays" />
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_SETTINGS_PAGE.BAN_DAYS' | translate }}</label>
            <input type="number" class="input" min="1" [(ngModel)]="s.banDays" />
          </div>
        </div>
        <button type="button" class="btn-primary mt-5" [disabled]="saving()" (click)="save(s)">{{ 'COMMON.SAVE' | translate }}</button>
      }
    </div>
  `,
})
export class SpaceSettingsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly settings = signal<SpaceSettings | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      this.settings.set(await this.api.spaceSettings());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async save(s: SpaceSettings): Promise<void> {
    this.saving.set(true);
    try {
      this.settings.set(await this.api.updateSpaceSettings(s));
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
