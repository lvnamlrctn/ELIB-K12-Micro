import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { AccessControlAdminService, RoomBookingTenantScope } from '../../../../services/map/room-booking.service';
import { RoomBookingConfigService } from '../../../../services/map/room-booking-config.service';
import { AccessDevice, AccessScanLog, AccessStaffCard } from '../../../../models/map/room-booking';
import { CanDirective } from '../../../../directives/can.directive';
import { environment } from '../../../../../environments/environment';
import { DateInputComponent as DateInput } from '../../../../components/date-input/date-input';

/** Tab "Kiểm soát cửa": khai báo đầu đọc/cửa (mã cửa → phòng, khoá API), thẻ quản trị, mở cửa từ web, nhật ký quẹt thẻ. */
@Component({
  selector: 'app-room-booking-access-tab',
  standalone: true,
  imports: [DateInput, FormsModule, DatePipe, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-access-tab.html'
})
export class RoomBookingAccessTab implements OnInit {
  private svc = inject(AccessControlAdminService);
  private configSvc = inject(RoomBookingConfigService);
  private scope = inject(RoomBookingTenantScope);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  readonly apiBase = `${environment.baseApiUrl || location.origin}/api/access`;
  rooms = signal<{ id: number; name: string }[]>([]);
  devices = signal<AccessDevice[]>([]);
  cards = signal<AccessStaffCard[]>([]);

  // Thiết bị
  deviceModal = signal(false);
  editingDevice: string | null = null;
  device = { code: '', name: '' as string | null, mapObjectId: null as number | null, isActive: true };
  /** Khoá API gốc vừa cấp — chỉ hiện 1 lần. */
  issuedKey = signal<{ code: string; key: string } | null>(null);

  // Thẻ quản trị
  cardModal = signal(false);
  editingCard: string | null = null;
  card = { cardUid: '', label: '' as string | null, isActive: true };

  // Nhật ký
  logs = signal<AccessScanLog[]>([]);
  logTotal = signal(0);
  logPage = 1;
  readonly logSize = 20;
  logKeyword = '';
  logAllowed: boolean | null = null;
  logFrom = '';
  logTo = '';
  showGuide = signal(false);

  ngOnInit(): void {
    this.configSvc.search({ tenantId: this.scope.tenantId(), pageIndex: 1, pageSize: 200 }).subscribe(res =>
      this.rooms.set(res.data.map(c => ({ id: c.mapObjectId, name: c.roomName || `#${c.mapObjectId}` }))));
    this.loadDevices();
    this.loadCards();
    this.loadLogs();
  }

  loadDevices(): void { this.svc.devices().subscribe({ next: d => this.devices.set(d), error: () => {} }); }
  loadCards(): void { this.svc.staffCards().subscribe({ next: c => this.cards.set(c), error: () => {} }); }

  roomName(id: number | null): string { return id == null ? this.translate.instant('STUDY_ROOM_BOOKING.SHARED_DOOR') : (this.rooms().find(r => r.id === id)?.name ?? `#${id}`); }

  /** Thiết bị coi là đang kết nối nếu gọi API trong 5 phút gần nhất. */
  online(d: AccessDevice): boolean { return !!d.lastSeenAt && Date.now() - new Date(d.lastSeenAt).getTime() < 5 * 60_000; }

  openDevice(d?: AccessDevice): void {
    this.editingDevice = d?.publicId ?? null;
    this.device = { code: d?.code ?? '', name: d?.name ?? '', mapObjectId: d?.mapObjectId ?? null, isActive: d?.isActive ?? true };
    this.deviceModal.set(true);
  }

  saveDevice(): void {
    if (!this.device.code.trim()) return;
    this.svc.saveDevice(this.editingDevice, { ...this.device, code: this.device.code.trim(), name: this.device.name?.trim() || null }).subscribe({
      next: r => {
        this.deviceModal.set(false);
        if (r.apiKey) this.issuedKey.set({ code: r.device.code, key: r.apiKey });
        this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        this.loadDevices();
      },
      error: () => {}
    });
  }

  regenerate(d: AccessDevice): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.REGEN_CONFIRM', { code: d.code }))) return;
    this.svc.regenerateKey(d.publicId).subscribe({ next: r => r.apiKey && this.issuedKey.set({ code: r.device.code, key: r.apiKey }), error: () => {} });
  }

  deleteDevice(d: AccessDevice): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.DELETE_DEVICE_CONFIRM', { code: d.code }))) return;
    this.svc.deleteDevice(d.publicId).subscribe({ next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadDevices(); }, error: () => {} });
  }

  unlock(d: AccessDevice): void {
    const note = prompt(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_NOTE_PROMPT'));
    if (note === null) return;
    this.svc.unlock({ devicePublicId: d.publicId, note }).subscribe({
      next: r => { this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_SENT', { count: r.devices })); this.loadLogs(); },
      error: () => {}
    });
  }

  copyKey(): void {
    const k = this.issuedKey();
    if (k) navigator.clipboard?.writeText(k.key).then(() => this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.COPIED')));
  }

  openCard(c?: AccessStaffCard): void {
    this.editingCard = c?.publicId ?? null;
    this.card = { cardUid: c?.cardUid ?? '', label: c?.label ?? '', isActive: c?.isActive ?? true };
    this.cardModal.set(true);
  }

  saveCard(): void {
    if (!this.card.cardUid.trim()) return;
    this.svc.saveStaffCard(this.editingCard, { ...this.card, label: this.card.label?.trim() || null }).subscribe({
      next: () => { this.cardModal.set(false); this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.loadCards(); },
      error: () => {}
    });
  }

  deleteCard(c: AccessStaffCard): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.DELETE_CARD_CONFIRM', { uid: c.cardUid }))) return;
    this.svc.deleteStaffCard(c.publicId).subscribe({ next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadCards(); }, error: () => {} });
  }

  loadLogs(): void {
    this.svc.logs({
      keyword: this.logKeyword || null, allowed: this.logAllowed,
      from: this.logFrom ? new Date(this.logFrom + 'T00:00:00').toISOString() : null,
      to: this.logTo ? new Date(this.logTo + 'T23:59:59').toISOString() : null,
      pageIndex: this.logPage, pageSize: this.logSize,
    }).subscribe(r => { this.logs.set(r.data); this.logTotal.set(r.recordsTotal); });
  }

  searchLogs(): void { this.logPage = 1; this.loadLogs(); }
  lastLogPage(): number { return Math.max(1, Math.ceil(this.logTotal() / this.logSize)); }
  logPageMove(delta: number): void { this.logPage = Math.min(this.lastLogPage(), Math.max(1, this.logPage + delta)); this.loadLogs(); }
  sourceLabel(s: number): string { return this.translate.instant(`STUDY_ROOM_BOOKING.SOURCE_${s}`); }
}
