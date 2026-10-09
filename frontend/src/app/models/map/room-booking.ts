import { MAP_OBJECT_CATEGORIES } from './map-object';
export type { RoomBookingConfig } from './room-booking-config';
/** 1=Pending,2=Approved,3=CheckedIn,4=Completed,5=Cancelled,6=Rejected,7=NoShow */
export type RoomBookingStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export interface RoomBookingAdmin {
  id: number;
  publicId: string;
  mapObjectId: number;
  roomName?: string | null;
  readerId: number;
  readerName?: string | null;
  readerCardNo?: string | null;
  startAt: string;
  endAt: string;
  partySize: number;
  status: RoomBookingStatus;
  note?: string | null;
  members?: string[] | null;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  tenantId?: number | null;
  tenantName?: string | null;
}

export const ROOM_BOOKING_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ duyệt',
  2: 'Đã duyệt',
  3: 'Đã check-in',
  4: 'Hoàn tất',
  5: 'Đã hủy',
  6: 'Bị từ chối',
  7: 'Không tới (NoShow)',
};

/** PUT api/Map/RoomBookingAdmin/CheckIn/{publicId} — kết quả quét mã QR check-in (giờ là UTC có "Z"). */
export interface RoomCheckInBooking {
  publicId: string;
  mapObjectId: number;
  roomName?: string | null;
  readerName?: string | null;
  readerCardNo?: string | null;
  startAt: string;
  endAt: string;
  partySize: number;
  status: RoomBookingStatus;
}

export interface RoomCheckInResult {
  ok: boolean;
  message: string;
  booking: RoomCheckInBooking | null;
}

/** GET/PUT api/Map/RoomBookingAdmin/Settings — tham số chung (0 = không giới hạn / tắt). */
export interface RoomBookingSettings {
  openTime: string;
  closeTime: string;
  maxPerDay: number;
  maxActive: number;
  maxSessionMinutes: number;
  noShowLimit: number;
  noShowWindowDays: number;
  banDays: number;
}

/** Giờ theo thứ (weekday 0 = CN … 6 = T7) của 1 loại cơ sở (category null = mọi loại). */
export interface RoomOpeningHour {
  id?: number;
  category: number | null;
  weekday: number;
  openTime: string | null;
  closeTime: string | null;
  isClosed: boolean;
}

export interface RoomSpecialDay {
  id: number;
  publicId: string;
  date: string;
  category: number | null;
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
  reason: string | null;
}

export interface SpecialDayInput {
  date: string;
  category: number | null;
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
  reason: string | null;
  cancelAffected: boolean;
}

export interface AffectedBooking {
  publicId: string;
  roomName: string | null;
  startAt: string;
  endAt: string;
  status: number;
  readerName: string;
  readerCardNo: string | null;
}

export interface SpecialDayResult { day: RoomSpecialDay | null; affected: AffectedBooking[]; cancelled: number; }

export interface RoomBookingBan {
  id: number;
  publicId: string;
  readerId: number;
  readerName?: string | null;
  readerCardNo?: string | null;
  reason: string | null;
  /** 1 = tự động (vắng mặt), 2 = thủ thư. */
  source: number;
  bannedUntil: string;
  liftedAt: string | null;
  createdRowDate: string | null;
  tenantName?: string | null;
}

export interface RoomBookingFilter {
  tenantId?: string | null;
  status?: number | null;
  mapObjectId?: number | null;
  category?: number | null;
  keyword?: string | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  pageIndex?: number;
  pageSize?: number;
}

export interface MaintenanceResult { affected: AffectedBooking[]; cancelled: number; }

export interface RoomUsageRow { mapObjectId: number; roomName: string | null; bookings: number; used: number; noShows: number; cancelled: number; usedHours: number; openHours: number; utilization: number; }
export interface TopReaderRow { readerId: number; name: string; cardNo: string | null; bookings: number; usedHours: number; noShows: number; }
export interface RoomBookingReport {
  from: string; to: string; total: number; byStatus: Record<string, number>; noShowRate: number; cancelRate: number;
  usedHours: number; utilization: number; visitors: number; byRoom: RoomUsageRow[]; byHour: number[]; byWeekday: number[]; topReaders: TopReaderRow[];
}
export interface RoomBookingReportRequest { from: string; to: string; mapObjectId?: number | null; category?: number | null; tenantId?: string | null; }

export interface NotificationTemplate { event: string; key: string; subject: string; body: string; isDefault: boolean; defaultSubject: string; defaultBody: string; }

/** Lịch tầng cho thủ thư (không che tên, có thành viên + mã lượt đặt). */
export interface StaffBoardBusy { startAt: string; endAt: string; status: number; readerName: string; readerCardNo: string | null; partySize: number; note: string | null; publicId: string | null; members: string[] | null; }
export interface StaffBoardRoom { mapObjectId: number; name: string | null; capacity: number; openTime: string; closeTime: string; closed: boolean; closedReason: string | null; maintenance: boolean; busy: StaffBoardBusy[]; }
export interface StaffBoard { enabled: boolean; openTime: string; closeTime: string; stepMinutes: number; rooms: StaffBoardRoom[]; }

export interface AccessDevice { id: number; publicId: string; code: string; name: string | null; mapObjectId: number | null; roomName: string | null; isActive: boolean; lastSeenAt: string | null; tenantName?: string | null; }
export interface AccessStaffCard { id: number; publicId: string; cardUid: string; userId: number | null; label: string | null; isActive: boolean; }
export interface AccessScanLog {
  id: number; deviceCode: string | null; mapObjectId: number | null; roomName: string | null; cardUid: string | null; readerName: string | null; readerCardNo: string | null;
  staffUserId: number | null; source: number; allowed: boolean; reason: string | null; createdAt: string;
}

/** Loại cơ sở dùng cho bộ lọc đặt phòng (MapObject.Category). */
export const MAP_OBJECT_CATEGORIES_FOR_BOOKING = MAP_OBJECT_CATEGORIES;
