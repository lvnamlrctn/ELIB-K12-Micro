import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, CrudPage as Page, SPACE_BOOKING_STATUSES, SpaceBooking, SpaceBookingSearch, SpaceRoom, errorMessage,
} from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { CameraCapture, CapturedPhoto } from '../../shared/camera';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';
import jsQR from 'jsqr';

const STATUS_PENDING = 1;
const STATUS_APPROVED = 2;
const STATUS_CHECKED_IN = 3;

/**
 * Yêu cầu đặt phòng/không gian (monolith: /admin/dat-phong — quyền SPACE_BOOKINGS). Duyệt/từ chối lượt Chờ duyệt; check-in
 * (gõ/qu quét mã QR hoặc nhận diện khuôn mặt — đủ cả 3 cách như bản gốc, RFID vật lý nằm ở màn Kiểm soát cửa riêng) rồi check-out.
 */
@Component({
  selector: 'app-space-bookings',
  imports: [FormsModule, LDatePipe, Loading, Modal, Paginator, TranslatePipe, CameraCapture],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'SPACE_BOOKINGS_PAGE.TITLE' | translate }}</h4>
      <button type="button" class="btn-primary ml-auto" (click)="openCheckIn()">
        <span class="material-icons text-[18px]">qr_code_scanner</span> {{ 'SPACE_BOOKINGS_PAGE.CHECK_IN' | translate }}
      </button>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-5 gap-4 items-end">
        <div>
          <label class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select class="input" [(ngModel)]="search.bookingStatus" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }
          </select>
        </div>
        <div>
          <label class="field-label">{{ 'SPACE_BOOKINGS_PAGE.CARD_NO' | translate }}</label>
          <input class="input" [(ngModel)]="search.cardNo" (keydown.enter)="find()" />
        </div>
        <div>
          <label class="field-label">{{ 'SPACE_CAT.ROOMS_TITLE' | translate }}</label>
          <select class="input" [(ngModel)]="search.spaceId" (change)="find()">
            <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (r of rooms(); track r.id) { <option [ngValue]="r.id">{{ r.name }}</option> }
          </select>
        </div>
        <div>
          <label class="field-label">{{ 'COMMON.FROM' | translate }}</label>
          <input type="date" class="input" [(ngModel)]="search.from" (change)="find()" />
        </div>
        <div>
          <label class="field-label">{{ 'COMMON.TO' | translate }}</label>
          <input type="date" class="input" [(ngModel)]="search.to" (change)="find()" />
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative mt-4">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1200px]">
          <thead><tr>
            <th class="th w-44">{{ 'SPACE_BOOKINGS_PAGE.READER' | translate }}</th>
            <th class="th">{{ 'SPACE_CAT.ROOMS_TITLE' | translate }}</th>
            <th class="th w-72">{{ 'SPACE_BOOKINGS_PAGE.TIME_RANGE' | translate }}</th>
            <th class="th w-20">{{ 'SPACE_BOOKINGS_PAGE.PARTY_SIZE' | translate }}</th>
            <th class="th w-32">{{ 'COMMON.STATUS' | translate }}</th>
            <th class="th w-40 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (b of page()?.items ?? []; track b.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ b.readerName ?? '—' }}<div class="text-[11px] text-gray-400 font-mono">{{ b.cardNo }}</div>
                  @if (b.members.length) { <div class="text-[11px] text-gray-400">+{{ b.members.length }} {{ 'SPACE_BOOKINGS_PAGE.MEMBERS' | translate }}</div> }
                </td>
                <td class="td">{{ b.spaceName }}<div class="text-[11px] text-gray-400">{{ b.floorName }} — {{ b.buildingName }}</div></td>
                <td class="td text-[13px]">{{ b.startAt | ldate: 'dd/MM HH:mm' }} → {{ b.endAt | ldate: 'dd/MM HH:mm' }}</td>
                <td class="td text-center">{{ b.partySize }}</td>
                <td class="td"><span [class]="statusOf(b.status).badge">{{ statusOf(b.status).name | translate }}</span></td>
                <td class="td text-center">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('edit') && b.status === STATUS_PENDING) {
                      <button class="icon-btn text-green-600 hover:bg-green-50" [title]="'SPACE_BOOKINGS_PAGE.APPROVE' | translate" [disabled]="busy()" (click)="approve(b)">
                        <span class="material-icons text-[18px]">check_circle</span></button>
                      <button class="icon-btn text-red-600 hover:bg-red-50" [title]="'SPACE_BOOKINGS_PAGE.REJECT' | translate" (click)="rejecting.set(b)">
                        <span class="material-icons text-[18px]">cancel</span></button>
                    }
                    @if (can('edit') && b.status === STATUS_APPROVED) {
                      <button class="icon-btn text-blue-600 hover:bg-blue-50" [title]="'SPACE_BOOKINGS_PAGE.CHECK_IN' | translate" [disabled]="busy()" (click)="checkInRow(b)">
                        <span class="material-icons text-[18px]">login</span></button>
                    }
                    @if (can('edit') && b.status === STATUS_CHECKED_IN) {
                      <button class="icon-btn text-gray-600 hover:bg-gray-100" [title]="'SPACE_BOOKINGS_PAGE.CHECK_OUT' | translate" [disabled]="busy()" (click)="checkOut(b)">
                        <span class="material-icons text-[18px]">logout</span></button>
                    }
                  </div>
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr> }
          </tbody>
        </table>
      </div>
      @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="search.pageIndex!" [pageSize]="search.pageSize!" (pageChange)="onPage($event)" /> }
    </div>

    @if (rejecting(); as b) {
      <app-modal [title]="'SPACE_BOOKINGS_PAGE.REJECT_TITLE' | translate" widthClass="max-w-md" (closed)="rejecting.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ b.cardNo }} — {{ b.spaceName }}</p>
        <label class="form-label">{{ 'SPACE_BOOKINGS_PAGE.REASON' | translate }} <span class="text-red-500">*</span></label>
        <input class="input" maxlength="500" [(ngModel)]="reason" [ngModelOptions]="{ standalone: true }" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="rejecting.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="busy() || !reason.trim()" (click)="reject(b)">{{ 'SPACE_BOOKINGS_PAGE.REJECT' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (checkInOpen()) {
      <app-modal [title]="'SPACE_BOOKINGS_PAGE.CHECK_IN' | translate" widthClass="max-w-lg" (closed)="checkInOpen.set(false)">
        <div class="space-y-3">
          <div>
            <label class="field-label">{{ 'SPACE_BOOKINGS_PAGE.KIOSK_ROOM' | translate }}</label>
            <select class="input" [(ngModel)]="kioskSpaceId" [ngModelOptions]="{ standalone: true }">
              <option [ngValue]="null">{{ 'SPACE_BOOKINGS_PAGE.KIOSK_NONE' | translate }}</option>
              @for (r of rooms(); track r.id) { <option [ngValue]="r.id">{{ r.name }}</option> }
            </select>
          </div>
          <div>
            <label class="field-label">{{ 'SPACE_BOOKINGS_PAGE.SCAN_HINT' | translate }}</label>
            <input class="input font-mono" [(ngModel)]="code" [ngModelOptions]="{ standalone: true }" (keydown.enter)="submitCode()" autofocus />
          </div>
          <div class="flex gap-2">
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy()" (click)="camera.set('qr')">
              <span class="material-icons text-[18px]">photo_camera</span> {{ 'SPACE_BOOKINGS_PAGE.SCAN_CAMERA' | translate }}</button>
            <button type="button" class="btn-secondary !py-1.5" [disabled]="busy()" (click)="camera.set('face')">
              <span class="material-icons text-[18px]">face</span> {{ 'SPACE_BOOKINGS_PAGE.SCAN_FACE' | translate }}</button>
            <button type="button" class="btn-primary !py-1.5 ml-auto" [disabled]="busy() || !code.trim()" (click)="submitCode()">
              {{ 'SPACE_BOOKINGS_PAGE.CHECK_IN' | translate }}</button>
          </div>
        </div>
      </app-modal>
    }
    @if (camera(); as mode) {
      <app-camera-capture [title]="mode === 'face' ? 'SPACE_BOOKINGS_PAGE.SCAN_FACE' : 'SPACE_BOOKINGS_PAGE.SCAN_CAMERA'"
                           [captureLabel]="mode === 'face' ? 'CAMERA.CAPTURE' : 'SPACE_BOOKINGS_PAGE.SCAN_SHOT'"
                           (captured)="onCaptured(mode, $event)" (cancelled)="camera.set(null)" />
    }
  `,
})
export class SpaceBookings implements OnInit {
  protected readonly STATUS_PENDING = STATUS_PENDING;
  protected readonly STATUS_APPROVED = STATUS_APPROVED;
  protected readonly STATUS_CHECKED_IN = STATUS_CHECKED_IN;

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly statuses = SPACE_BOOKING_STATUSES;
  protected search: SpaceBookingSearch = { bookingStatus: null, cardNo: '', spaceId: null, from: null, to: null, pageIndex: 1, pageSize: 10 };
  protected readonly page = signal<Page<SpaceBooking> | null>(null);
  protected readonly rooms = signal<SpaceRoom[]>([]);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly rejecting = signal<SpaceBooking | null>(null);
  protected reason = '';

  protected readonly checkInOpen = signal(false);
  protected readonly camera = signal<'qr' | 'face' | null>(null);
  protected kioskSpaceId: number | null = null;
  protected code = '';

  async ngOnInit(): Promise<void> {
    this.rooms.set(await this.api.crud<SpaceRoom>('spaces', 'space').searchAll());
    await this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`SPACE_BOOKINGS:${action}`);
  }

  protected statusOf(code: number) {
    return this.statuses.find((s) => s.code === code) ?? { name: String(code), badge: 'badge-off' };
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
      this.page.set(await this.api.searchSpaceBookings(this.search));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async approve(b: SpaceBooking): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.approveSpaceBooking(b.publicId);
      this.toastr.success('SPACE_BOOKINGS_PAGE.APPROVE_SUCCESS');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async reject(b: SpaceBooking): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.rejectSpaceBooking(b.publicId, this.reason.trim());
      this.toastr.success('SPACE_BOOKINGS_PAGE.REJECT_SUCCESS');
      this.rejecting.set(null);
      this.reason = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async checkOut(b: SpaceBooking): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.checkOutSpaceBooking(b.publicId);
      this.toastr.success('SPACE_BOOKINGS_PAGE.CHECK_OUT_SUCCESS');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Nút check-in trên một dòng sẵn — điền mã sẵn, vẫn mở hộp thoại để chọn kiosk nếu cần. */
  protected checkInRow(b: SpaceBooking): void {
    this.code = b.publicId;
    this.checkInOpen.set(true);
  }

  protected openCheckIn(): void {
    this.code = '';
    this.checkInOpen.set(true);
  }

  protected async submitCode(): Promise<void> {
    const value = this.code.trim();
    if (!value) return;
    this.busy.set(true);
    try {
      await this.api.checkInSpaceBooking(value, this.kioskSpaceId);
      this.toastr.success('SPACE_BOOKINGS_PAGE.CHECK_IN_SUCCESS');
      this.checkInOpen.set(false);
      this.code = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async onCaptured(mode: 'qr' | 'face', photo: CapturedPhoto): Promise<void> {
    this.camera.set(null);
    this.busy.set(true);
    try {
      if (mode === 'face') {
        const match = await this.api.faceIdentify(photo.dataUrl);
        if (!match) { this.toastr.warning('SPACE_BOOKINGS_PAGE.FACE_NOT_FOUND'); return; }
        await this.api.checkInSpaceBookingByCard(match.cardNo, this.kioskSpaceId);
      } else {
        const text = await decodeQr(photo.file);
        if (!text) { this.toastr.warning('SPACE_BOOKINGS_PAGE.QR_NOT_FOUND'); return; }
        await this.api.checkInSpaceBooking(text.trim(), this.kioskSpaceId);
      }
      this.toastr.success('SPACE_BOOKINGS_PAGE.CHECK_IN_SUCCESS');
      this.checkInOpen.set(false);
      this.code = '';
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Giải mã QR từ một ảnh chụp tĩnh (đủ dùng cho kiosk: bạn đọc đưa mã, cán bộ bấm chụp) — không cần luồng video liên tục. */
async function decodeQr(file: File): Promise<string | null> {
  const bitmap = await createImageBitmap(file);
  const canvas = document.createElement('canvas');
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  ctx.drawImage(bitmap, 0, 0);
  bitmap.close();
  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
  return jsQR(imageData.data, imageData.width, imageData.height)?.data ?? null;
}
