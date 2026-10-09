import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '../error.interceptor';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface RoomOpac {
  mapObjectId: number;
  name: string;
  iconName?: string | null;
  capacity: number;
  slotMinutes: number;
  /** Phòng tự động duyệt: đặt xong là lượt đặt được duyệt ngay. */
  autoApprove?: boolean;
  /** Nội quy phòng. */
  rules?: string | null;
  /** Tên loại bạn đọc được đặt (rỗng = mọi đối tượng). */
  allowedReaderTypes?: string[];
  /** Bạn đọc đang đăng nhập có thuộc đối tượng được đặt không. */
  readerAllowed?: boolean | null;
  /** Số người tối thiểu (kể cả người đặt) — > 1 thì phải nhập thẻ thành viên. */
  minGroupSize?: number;
  /** Phòng tạm ngưng phục vụ (bảo trì). */
  maintenance?: boolean;
  maintenanceNote?: string | null;
}

export interface RoomOpacList {
  enabled: boolean;
  rooms: RoomOpac[];
}

export interface AvailabilitySlot {
  startAt: string;
  endAt: string;
  /** 1=Pending,2=Approved,3=CheckedIn — bất kỳ giá trị nào trong 3 trạng thái này đều coi là "đã có người giữ chỗ". */
  status: number;
}

/** Lượt bận + giờ mở cửa của phòng trong ngày (giờ thư viện "HH:mm"). */
export interface RoomDayAvailability {
  slots: AvailabilitySlot[];
  openTime: string;
  closeTime: string;
  closed: boolean;
  closedReason?: string | null;
  stepMinutes: number;
}

/** Lịch tầng (GET FloorBoard, xem công khai) — port ELIB-LRC 09-28/10-04. Mọi mốc giờ là UTC. */
export interface FloorBoardBooking {
  startAt: string;
  endAt: string;
  /** 1 chờ duyệt, 2 đã duyệt, 3 đang dùng. */
  status: number;
  isMine: boolean;
  /** Tên đã che (trừ lượt của chính mình). */
  readerName: string;
  readerCardNo?: string | null;
  partySize: number;
  note?: string | null;
}

export interface FloorBoardRoom {
  mapObjectId: number;
  name?: string | null;
  iconName?: string | null;
  capacity: number;
  minAdvanceMinutes: number;
  maxAdvanceDays: number;
  maxAdvanceHours?: number | null;
  autoApprove: boolean;
  equipment: { name?: string | null; quantity?: number | null }[];
  busy: FloorBoardBooking[];
  rules?: string | null;
  allowedReaderTypes?: string[];
  readerAllowed?: boolean | null;
  /** Giờ mở cửa của riêng phòng trong ngày ("HH:mm"). */
  openTime?: string;
  closeTime?: string;
  closed?: boolean;
  closedReason?: string | null;
  earliestFreeAt?: string | null;
  minGroupSize?: number;
  maintenance?: boolean;
  maintenanceNote?: string | null;
}

export interface RoomFloorBoard {
  enabled: boolean;
  openTime: string;
  closeTime: string;
  stepMinutes: number;
  rooms: FloorBoardRoom[];
}

export interface MyRoomBooking {
  publicId: string;
  roomName?: string | null;
  startAt: string;
  endAt: string;
  partySize: number;
  /** 1=Pending,2=Approved,3=CheckedIn,4=Completed,5=Cancelled,6=Rejected,7=NoShow */
  status: number;
  note?: string | null;
  /** Hết ân hạn check-in (UTC) — quá mốc này lượt đã duyệt bị tính vắng mặt. */
  checkInUntil?: string | null;
  /** false = bạn là thành viên nhóm (không huỷ được). */
  isOwner?: boolean;
  ownerName?: string | null;
  members?: string[];
}

/** Đặt phòng học nhóm (RoomBookingController, api/public/RoomBooking), gọi kèm Bearer token bạn đọc
 * (xem opac/auth.interceptor.ts) — cùng khuôn MyLibraryApiService nhưng thuộc domain Map, tách file riêng. */
@Injectable({ providedIn: 'root' })
export class RoomBookingOpacService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private get backendRoot(): string {
    return isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : '';
  }

  private get baseUrl(): string {
    return `${this.backendRoot}/api/public/RoomBooking`;
  }

  getRooms(): Observable<RoomOpacList> {
    return this.http.get<any>(`${this.baseUrl}/Rooms`, { params: { tenantId: APP_CONFIG.TenantId } }).pipe(
      map(res => (res?.success && res.data ? (res.data as RoomOpacList) : { enabled: false, rooms: [] })),
      catchError(() => of({ enabled: false, rooms: [] }))
    );
  }

  getAvailability(mapObjectId: number, date: string): Observable<RoomDayAvailability> {
    const empty: RoomDayAvailability = { slots: [], openTime: '07:00', closeTime: '21:00', closed: false, stepMinutes: 30 };
    return this.http.get<any>(`${this.baseUrl}/Availability`, { params: { mapObjectId, date, tenantId: APP_CONFIG.TenantId } }).pipe(
      map(res => (res?.success && res.data ? { ...empty, ...res.data, slots: res.data.slots ?? [] } as RoomDayAvailability : empty)),
      catchError(() => of(empty))
    );
  }

  book(request: { mapObjectId: number; startAt: string; endAt: string; partySize: number; note?: string; memberCards?: string[] }): Observable<{ ok: boolean; message?: string; publicId?: string; status?: number }> {
    return this.http.post<any>(`${this.baseUrl}/Book`, request).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message, publicId: res?.data?.publicId, status: res?.data?.status })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  /** Lịch tầng: phòng khả đặt + thiết bị + lượt bận trong ngày (tên người khác đã che). Xem được khi chưa đăng nhập. */
  getFloorBoard(floorId: number, date: string): Observable<RoomFloorBoard | null> {
    return this.http.get<any>(`${this.baseUrl}/FloorBoard`, { params: { floorId, date, tenantId: APP_CONFIG.TenantId } }).pipe(
      map(res => (res?.success && res.data ? (res.data as RoomFloorBoard) : null)),
      catchError(() => of(null))
    );
  }

  /** Tra thẻ thành viên nhóm: tên đã che, null = không tìm thấy / khác đơn vị. */
  lookupMember(card: string): Observable<string | null> {
    return this.http.get<any>(`${this.baseUrl}/Member`, { params: { card }, context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(
      map(res => res?.data?.maskedName ?? null),
      catchError(() => of(null))
    );
  }

  getMyBookings(): Observable<MyRoomBooking[]> {
    return this.http.get<any>(`${this.baseUrl}/MyBookings`).pipe(
      map(res => (res?.success && Array.isArray(res.data) ? (res.data as MyRoomBooking[]) : [])),
      catchError(() => of([]))
    );
  }

  checkIn(publicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/CheckIn/${publicId}`, {}).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  /** Tự check-in, xác minh bằng khuôn mặt — ảnh chỉ so với ảnh đã đăng ký của chính bạn đọc (port ELIB-LRC 09-30). */
  checkInByFace(publicId: string, imageBase64: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/CheckInByFace/${publicId}`, { imageBase64 }, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  /** Trả phòng (kết thúc sớm) lượt đang sử dụng. */
  checkOut(publicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.post<any>(`${this.baseUrl}/CheckOut/${publicId}`, {}).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }

  cancel(publicId: string): Observable<{ ok: boolean; message?: string }> {
    return this.http.delete<any>(`${this.baseUrl}/Cancel/${publicId}`).pipe(
      map(res => ({ ok: !!res?.success, message: res?.message })),
      catchError(err => of({ ok: false, message: err?.error?.message }))
    );
  }
}
