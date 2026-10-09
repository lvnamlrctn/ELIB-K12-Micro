import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { RoomBookingAdminService, AccessControlAdminService, RoomBookingTenantScope } from '../../../services/map/room-booking.service';
import { RoomBookingConfigService } from '../../../services/map/room-booking-config.service';
import { MapObjectService } from '../../../services/map/map-object.service';
import { RoomBookingAdmin, RoomBookingConfig, ROOM_BOOKING_STATUS_LABELS, MAP_OBJECT_CATEGORIES_FOR_BOOKING } from '../../../models/map/room-booking';
import { MapObject } from '../../../models/map/map-object';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { RoomCheckinScannerComponent } from './room-checkin-scanner';
import { RoomBookingSettingsTab } from './room-booking/room-booking-settings-tab';
import { RoomBookingHoursTab } from './room-booking/room-booking-hours-tab';
import { RoomBookingBansTab } from './room-booking/room-booking-bans-tab';
import { RoomBookingMapTab } from './room-booking/room-booking-map-tab';
import { RoomBookingReportTab } from './room-booking/room-booking-report-tab';
import { RoomBookingTemplatesTab } from './room-booking/room-booking-templates-tab';
import { RoomBookingAccessTab } from './room-booking/room-booking-access-tab';

/**
 * Đặt phòng học nhóm (admin) — port ELIB-LRC 09-28..10-04: danh sách có bộ lọc/phân trang/xuất Excel, trả phòng, mở cửa, quét QR
 * check-in, và các tab Sơ đồ, Báo cáo, Tham số, Giờ mở cửa, Danh sách chặn, Mẫu thông báo, Kiểm soát cửa. Cấu hình phòng giữ ở trang
 * riêng /admin/room-booking-config như trước.
 * Tenant: tài khoản đặc quyền chọn đơn vị ở đầu trang (RoomBookingTenantScope) — áp cho danh sách và mọi tab (đổi đơn vị thì tab tải
 * lại); không chọn = xem tất cả, các tab cấu hình sửa cấu hình dùng chung. User thường luôn theo đơn vị của mình (backend ép).
 */
@Component({
  selector: 'app-study-room-bookings',
  standalone: true,
  imports: [CanDirective, CommonModule, RouterLink, FormsModule, TranslateModule, MatIconModule, TenantFilterSelectComponent, DateInputComponent,
    RoomCheckinScannerComponent, RoomBookingSettingsTab, RoomBookingHoursTab, RoomBookingBansTab, RoomBookingMapTab, RoomBookingReportTab,
    RoomBookingTemplatesTab, RoomBookingAccessTab],
  templateUrl: './study-room-bookings.html'
})
export class StudyRoomBookingsPage implements OnInit, OnDestroy {
  private adminSvc     = inject(RoomBookingAdminService);
  private configSvc    = inject(RoomBookingConfigService);
  private accessSvc    = inject(AccessControlAdminService);
  private mapObjectSvc = inject(MapObjectService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private auth         = inject(Auth);
  readonly scope       = inject(RoomBookingTenantScope);
  private destroy$     = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  readonly statusLabels = ROOM_BOOKING_STATUS_LABELS;
  readonly categories = MAP_OBJECT_CATEGORIES_FOR_BOOKING;

  activeTab = signal<'requests' | 'settings' | 'hours' | 'bans' | 'map' | 'report' | 'templates' | 'access'>('requests');
  readonly tabs = [
    { key: 'requests', label: 'STUDY_ROOM_BOOKING.TAB_REQUESTS' },
    { key: 'map', label: 'STUDY_ROOM_BOOKING.TAB_MAP' },
    { key: 'report', label: 'STUDY_ROOM_BOOKING.TAB_REPORT' },
    { key: 'settings', label: 'STUDY_ROOM_BOOKING.TAB_SETTINGS' },
    { key: 'hours', label: 'STUDY_ROOM_BOOKING.TAB_HOURS' },
    { key: 'bans', label: 'STUDY_ROOM_BOOKING.TAB_BANS' },
    { key: 'templates', label: 'STUDY_ROOM_BOOKING.TAB_TEMPLATES' },
    { key: 'access', label: 'STUDY_ROOM_BOOKING.TAB_ACCESS' },
  ] as const;

  // Bộ lọc danh sách lượt đặt
  filterKeyword = '';
  filterFrom = '';
  filterTo = '';
  filterRoom: number | null = null;
  filterCategory: number | null = null;
  exporting = signal(false);

  configs = signal<RoomBookingConfig[]>([]);
  mapObjects = signal<MapObject[]>([]);

  requests = signal<RoomBookingAdmin[]>([]);
  requestsLoading = signal(false);
  statusFilter = signal<number | null>(1);
  totalRecords = 0;
  pageIndex = 0;
  pageSize = 10;

  showRejectModal = signal(false);
  rejectTargetId = signal<string | null>(null);
  rejectReason = '';

  // ── Quét QR check-in ──────────────────────────────────────────────────────
  showScanner = signal(false);
  scannerRooms = computed(() => this.configs().map(c => ({ id: c.mapObjectId, name: c.roomName || `#${c.mapObjectId}` })));

  ngOnInit(): void {
    this.scope.tenantId.set(null);
    this.loadMapObjects();
    this.loadConfigs();
    this.loadRequests();
  }

  ngOnDestroy(): void {
    this.scope.tenantId.set(null);
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTenantChange(): void {
    this.scope.tenantId.set(this.tenantId);
    this.filterRoom = null;
    this.pageIndex = 0;
    this.loadConfigs();
    this.loadRequests();
  }

  loadMapObjects(): void {
    this.mapObjectSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.mapObjects.set(res.data),
      error: () => {}
    });
  }

  loadConfigs(): void {
    this.configSvc.search({ tenantId: this.tenantId, pageIndex: 1, pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.configs.set(res.data),
      error: () => {}
    });
  }

  roomName(mapObjectId: number): string {
    return this.mapObjects().find(o => o.id === mapObjectId)?.name || `#${mapObjectId}`;
  }

  // ── Danh sách lượt đặt ─────────────────────────────────────────────────────
  loadRequests(): void {
    this.requestsLoading.set(true);
    this.adminSvc.search({ ...this.filter(), pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.requests.set(res.data); this.totalRecords = res.recordsTotal; this.requestsLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.requestsLoading.set(false); }
      });
  }

  private filter() {
    return {
      status: this.statusFilter(), keyword: this.filterKeyword.trim() || null, dateFrom: this.filterFrom || null, dateTo: this.filterTo || null,
      mapObjectId: this.filterRoom, category: this.filterCategory, tenantId: this.tenantId,
    };
  }

  applyFilters(): void { this.pageIndex = 0; this.loadRequests(); }

  clearFilters(): void {
    this.filterKeyword = ''; this.filterFrom = ''; this.filterTo = ''; this.filterRoom = null; this.filterCategory = null;
    this.applyFilters();
  }

  get lastPage(): number { return Math.max(1, Math.ceil(this.totalRecords / this.pageSize)); }

  movePage(delta: number): void {
    this.pageIndex = Math.min(this.lastPage - 1, Math.max(0, this.pageIndex + delta));
    this.loadRequests();
  }

  setStatusFilter(status: number | null): void {
    this.statusFilter.set(status);
    this.pageIndex = 0;
    this.loadRequests();
  }

  exportExcel(): void {
    this.exporting.set(true);
    this.adminSvc.exportExcel(this.filter()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'dat-phong.xlsx'; a.click();
        URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: () => this.exporting.set(false)
    });
  }

  approve(b: RoomBookingAdmin): void {
    this.adminSvc.approve(b.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.loadRequests(); },
      error: (err) => this.toastr.error(err?.error?.message || this.translate.instant('COMMON.UPDATE_ERROR'))
    });
  }

  checkOut(b: RoomBookingAdmin): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.CHECK_OUT_CONFIRM', { room: b.roomName ?? '', reader: b.readerName ?? '' }))) return;
    this.adminSvc.checkOut(b.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.CHECKED_OUT')); this.loadRequests(); },
      error: () => {}
    });
  }

  unlock(b: RoomBookingAdmin): void {
    const note = prompt(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_NOTE_PROMPT'));
    if (note === null) return;
    this.accessSvc.unlock({ bookingPublicId: b.publicId, note }).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_SENT', { count: r.devices })),
      error: () => {}
    });
  }

  openReject(b: RoomBookingAdmin): void {
    this.rejectTargetId.set(b.publicId);
    this.rejectReason = '';
    this.showRejectModal.set(true);
  }

  closeReject(): void {
    this.showRejectModal.set(false);
    this.rejectTargetId.set(null);
  }

  confirmReject(): void {
    const id = this.rejectTargetId();
    if (!id) return;
    this.adminSvc.reject(id, this.rejectReason).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        this.closeReject();
        this.loadRequests();
      },
      error: (err) => {
        this.toastr.error(err?.error?.message || this.translate.instant('COMMON.UPDATE_ERROR'));
        this.closeReject();
      }
    });
  }
}
