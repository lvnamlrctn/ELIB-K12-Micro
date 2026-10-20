import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, SpaceAccessDevice, SpaceAccessScanLog, SpaceAccessStaffCard, SpaceRoom, STATUS_ACTIVE, STATUS_INACTIVE, errorMessage,
} from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

/** Kiểm soát cửa RFID (monolith: AccessDeviceController, quyền SPACE_ACCESS) — thiết bị, thẻ cán bộ, nhật ký, hướng dẫn tích hợp. */
@Component({
  selector: 'app-space-access',
  imports: [FormsModule, LDatePipe, Loading, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'SPACE_ACCESS_PAGE.TITLE' | translate }}</h4></div>

    <div class="panel mb-4 relative">
      <div class="flex items-center justify-between mb-3">
        <div class="font-medium text-gray-800">{{ 'SPACE_ACCESS_PAGE.DEVICES' | translate }}</div>
        @if (can('add')) {
          <button type="button" class="btn-secondary" (click)="openAddDevice()"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD' | translate }}</button>
        }
      </div>
      @if (loadingDevices()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th">{{ 'SPACE_ACCESS_PAGE.CODE' | translate }}</th><th class="th">{{ 'COMMON.NAME' | translate }}</th>
          <th class="th">{{ 'SPACE_CAT.ROOMS_TITLE' | translate }}</th><th class="th w-36">{{ 'SPACE_ACCESS_PAGE.LAST_SEEN' | translate }}</th>
          <th class="th w-24">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-56 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
        </tr></thead>
        <tbody>
          @for (d of devices(); track d.publicId) {
            <tr>
              <td class="td font-mono">{{ d.code }}</td><td class="td">{{ d.name }}</td><td class="td">{{ d.spaceName ?? '—' }}</td>
              <td class="td text-[13px]">{{ d.lastSeenAt ? (d.lastSeenAt | ldate: 'dd/MM HH:mm') : '—' }}</td>
              <td class="td"><span [class]="d.status === STATUS_ACTIVE ? 'badge-on' : 'badge-off'">{{ (d.status === STATUS_ACTIVE ? 'COMMON.ACTIVE' : 'COMMON.INACTIVE') | translate }}</span></td>
              <td class="td text-center">
                <div class="flex items-center justify-center gap-1">
                  @if (can('edit')) {
                    <button class="icon-btn text-blue-600 hover:bg-blue-50" [title]="'SPACE_ACCESS_PAGE.UNLOCK' | translate" [disabled]="busy()" (click)="unlock(d)">
                      <span class="material-icons text-[18px]">lock_open</span></button>
                    <button class="icon-btn text-amber-600 hover:bg-amber-50" [title]="'SPACE_ACCESS_PAGE.REGENERATE_KEY' | translate" [disabled]="busy()" (click)="regenerateKey(d)">
                      <span class="material-icons text-[18px]">key</span></button>
                    <button class="icon-btn text-gray-600 hover:bg-gray-100" [title]="'COMMON.STATUS' | translate" [disabled]="busy()" (click)="toggleDeviceStatus(d)">
                      <span class="material-icons text-[18px]">{{ d.status === STATUS_ACTIVE ? 'toggle_on' : 'toggle_off' }}</span></button>
                  }
                  @if (can('delete')) {
                    <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'COMMON.DELETE' | translate" (click)="deletingDevice.set(d)">
                      <span class="material-icons text-[18px]">delete</span></button>
                  }
                </div>
              </td>
            </tr>
          } @empty { <tr><td colspan="6" class="py-6 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
        </tbody>
      </table>
    </div>

    <div class="panel mb-4 relative">
      <div class="flex items-center justify-between mb-3">
        <div class="font-medium text-gray-800">{{ 'SPACE_ACCESS_PAGE.STAFF_CARDS' | translate }}</div>
        @if (can('add')) {
          <button type="button" class="btn-secondary" (click)="openAddCard()"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD' | translate }}</button>
        }
      </div>
      @if (loadingCards()) { <app-loading /> }
      <table class="w-full text-left border-collapse max-w-lg">
        <thead><tr><th class="th">{{ 'SPACE_ACCESS_PAGE.CARD_UID' | translate }}</th><th class="th">{{ 'SPACE_ACCESS_PAGE.LABEL' | translate }}</th><th class="th w-16 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th></tr></thead>
        <tbody>
          @for (c of staffCards(); track c.publicId) {
            <tr><td class="td font-mono">{{ c.cardUid }}</td><td class="td">{{ c.label ?? '—' }}</td>
              <td class="td text-center">
                @if (can('delete')) {
                  <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'COMMON.DELETE' | translate" (click)="deletingCard.set(c)">
                    <span class="material-icons text-[18px]">delete</span></button>
                }
              </td></tr>
          } @empty { <tr><td colspan="3" class="py-6 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
        </tbody>
      </table>
    </div>

    <div class="panel mb-4 relative">
      <div class="font-medium text-gray-800 mb-3">{{ 'SPACE_ACCESS_PAGE.SCAN_LOGS' | translate }}</div>
      @if (loadingLogs()) { <app-loading /> }
      <table class="w-full text-left border-collapse">
        <thead><tr>
          <th class="th w-36">{{ 'SPACE_ACCESS_PAGE.TIME' | translate }}</th><th class="th">{{ 'SPACE_ACCESS_PAGE.DEVICE' | translate }}</th>
          <th class="th">{{ 'SPACE_ACCESS_PAGE.CARD_UID' | translate }}</th><th class="th">{{ 'SPACE_BANS_PAGE.READER' | translate }}</th>
          <th class="th w-24">{{ 'SPACE_ACCESS_PAGE.ALLOWED' | translate }}</th><th class="th">{{ 'SPACE_ACCESS_PAGE.REASON' | translate }}</th>
        </tr></thead>
        <tbody>
          @for (l of scanLogs(); track l.publicId) {
            <tr>
              <td class="td text-[13px]">{{ l.scannedAt | ldate: 'dd/MM HH:mm:ss' }}</td><td class="td font-mono">{{ l.deviceCode }}</td>
              <td class="td font-mono">{{ l.cardUid }}</td><td class="td">{{ l.readerName ?? '—' }}</td>
              <td class="td"><span [class]="l.allowed ? 'badge-on' : 'badge-off'">{{ (l.allowed ? 'COMMON.YES' : 'COMMON.NO') | translate }}</span></td>
              <td class="td text-[13px]">{{ l.reason }}</td>
            </tr>
          } @empty { <tr><td colspan="6" class="py-6 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</td></tr> }
        </tbody>
      </table>
    </div>

    <div class="panel text-sm text-gray-600 space-y-1">
      <div class="font-medium text-gray-800 mb-1">{{ 'SPACE_ACCESS_PAGE.GUIDE_TITLE' | translate }}</div>
      <p>{{ 'SPACE_ACCESS_PAGE.GUIDE_HOST' | translate }}</p>
      <p class="font-mono text-xs bg-gray-50 border border-gray-200 rounded p-2">
        POST /api/device/space/Verify — header X-Device-Code, X-Device-Key — body &#123; "uid": "&lt;card-uid&gt;" &#125;<br />
        GET /api/device/space/Commands<br />
        POST /api/device/space/Commands/Ack — body &#123; "id": "&lt;command-id&gt;" &#125;
      </p>
      <p>{{ 'SPACE_ACCESS_PAGE.GUIDE_KEY' | translate }}</p>
    </div>

    @if (addingDevice()) {
      <app-modal [title]="'SPACE_ACCESS_PAGE.ADD_DEVICE' | translate" widthClass="max-w-md" (closed)="addingDevice.set(false)">
        <div class="space-y-3">
          <div><label class="field-label">{{ 'SPACE_ACCESS_PAGE.CODE' | translate }} <span class="text-red-500">*</span></label>
            <input class="input" [(ngModel)]="newDevice.code" [ngModelOptions]="{ standalone: true }" /></div>
          <div><label class="field-label">{{ 'COMMON.NAME' | translate }} <span class="text-red-500">*</span></label>
            <input class="input" [(ngModel)]="newDevice.name" [ngModelOptions]="{ standalone: true }" /></div>
          <div><label class="field-label">{{ 'SPACE_CAT.ROOMS_TITLE' | translate }}</label>
            <select class="input" [(ngModel)]="newDevice.spaceId" [ngModelOptions]="{ standalone: true }">
              <option [ngValue]="null">{{ 'SPACE_ACCESS_PAGE.NO_ROOM' | translate }}</option>
              @for (r of rooms(); track r.id) { <option [ngValue]="r.id">{{ r.name }}</option> }
            </select></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="addingDevice.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !newDevice.code.trim() || !newDevice.name.trim()" (click)="addDevice()">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (addingCard()) {
      <app-modal [title]="'SPACE_ACCESS_PAGE.ADD_STAFF_CARD' | translate" widthClass="max-w-md" (closed)="addingCard.set(false)">
        <div class="space-y-3">
          <div><label class="field-label">{{ 'SPACE_ACCESS_PAGE.CARD_UID' | translate }} <span class="text-red-500">*</span></label>
            <input class="input" [(ngModel)]="newCard.cardUid" [ngModelOptions]="{ standalone: true }" /></div>
          <div><label class="field-label">{{ 'SPACE_ACCESS_PAGE.LABEL' | translate }}</label>
            <input class="input" [(ngModel)]="newCard.label" [ngModelOptions]="{ standalone: true }" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="addingCard.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || !newCard.cardUid.trim()" (click)="addCard()">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (newKey(); as key) {
      <app-modal [title]="'SPACE_ACCESS_PAGE.NEW_KEY_TITLE' | translate" widthClass="max-w-md" (closed)="newKey.set(null)">
        <p class="text-sm text-amber-700 mb-2">{{ 'SPACE_ACCESS_PAGE.NEW_KEY_WARNING' | translate }}</p>
        <p class="font-mono text-sm bg-gray-50 border border-gray-200 rounded p-2 break-all">{{ key }}</p>
        <ng-container footer>
          <button type="button" class="btn-primary" (click)="newKey.set(null)">{{ 'COMMON.CLOSE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (deletingDevice(); as d) { <app-confirm-delete [busy]="busy()" (confirmed)="removeDevice(d)" (cancelled)="deletingDevice.set(null)" /> }
    @if (deletingCard(); as c) { <app-confirm-delete [busy]="busy()" (confirmed)="removeCard(c)" (cancelled)="deletingCard.set(null)" /> }
  `,
})
export class SpaceAccess implements OnInit {
  protected readonly STATUS_ACTIVE = STATUS_ACTIVE;

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly cardsClient = this.api.crud<SpaceAccessStaffCard>('access/staff-cards', 'space');

  protected readonly devices = signal<SpaceAccessDevice[]>([]);
  protected readonly staffCards = signal<SpaceAccessStaffCard[]>([]);
  protected readonly scanLogs = signal<SpaceAccessScanLog[]>([]);
  protected readonly rooms = signal<SpaceRoom[]>([]);
  protected readonly loadingDevices = signal(false);
  protected readonly loadingCards = signal(false);
  protected readonly loadingLogs = signal(false);
  protected readonly busy = signal(false);

  protected readonly addingDevice = signal(false);
  protected readonly addingCard = signal(false);
  protected readonly deletingDevice = signal<SpaceAccessDevice | null>(null);
  protected readonly deletingCard = signal<SpaceAccessStaffCard | null>(null);
  protected readonly newKey = signal<string | null>(null);
  protected newDevice: { code: string; name: string; spaceId: number | null } = { code: '', name: '', spaceId: null };
  protected newCard: { cardUid: string; label: string | null } = { cardUid: '', label: null };

  async ngOnInit(): Promise<void> {
    this.rooms.set(await this.api.crud<SpaceRoom>('spaces', 'space').searchAll());
    await Promise.all([this.loadDevices(), this.loadCards(), this.loadLogs()]);
  }

  protected can(action: string): boolean {
    return this.session.can(`SPACE_ACCESS:${action}`);
  }

  private async loadDevices(): Promise<void> {
    this.loadingDevices.set(true);
    try {
      this.devices.set((await this.api.searchSpaceAccessDevices({ pageIndex: 1, pageSize: 100 })).items);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loadingDevices.set(false);
    }
  }

  private async loadCards(): Promise<void> {
    this.loadingCards.set(true);
    try {
      this.staffCards.set(await this.cardsClient.searchAll());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loadingCards.set(false);
    }
  }

  private async loadLogs(): Promise<void> {
    this.loadingLogs.set(true);
    try {
      this.scanLogs.set((await this.api.searchSpaceAccessScanLogs({ pageIndex: 1, pageSize: 50 })).items);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loadingLogs.set(false);
    }
  }

  protected openAddDevice(): void {
    this.newDevice = { code: '', name: '', spaceId: null };
    this.addingDevice.set(true);
  }

  protected async addDevice(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await this.api.addSpaceAccessDevice(this.newDevice.code.trim(), this.newDevice.name.trim(), this.newDevice.spaceId);
      this.addingDevice.set(false);
      this.newKey.set(result.key.rawKey);
      await this.loadDevices();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async regenerateKey(d: SpaceAccessDevice): Promise<void> {
    this.busy.set(true);
    try {
      const key = await this.api.regenerateSpaceAccessDeviceKey(d.publicId);
      this.newKey.set(key.rawKey);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async toggleDeviceStatus(d: SpaceAccessDevice): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.changeSpaceAccessDeviceStatus(d.publicId, d.status === STATUS_ACTIVE ? STATUS_INACTIVE : STATUS_ACTIVE);
      await this.loadDevices();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async unlock(d: SpaceAccessDevice): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.unlockSpaceAccessDevice({ deviceId: d.id });
      this.toastr.success('SPACE_ACCESS_PAGE.UNLOCK_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async removeDevice(d: SpaceAccessDevice): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.deleteSpaceAccessDevice(d.publicId);
      this.deletingDevice.set(null);
      await this.loadDevices();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected openAddCard(): void {
    this.newCard = { cardUid: '', label: null };
    this.addingCard.set(true);
  }

  protected async addCard(): Promise<void> {
    this.busy.set(true);
    try {
      await this.cardsClient.add({ cardUid: this.newCard.cardUid.trim(), label: this.newCard.label?.trim() || null });
      this.addingCard.set(false);
      await this.loadCards();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async removeCard(c: SpaceAccessStaffCard): Promise<void> {
    this.busy.set(true);
    try {
      await this.cardsClient.delete(c.publicId);
      this.deletingCard.set(null);
      await this.loadCards();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
