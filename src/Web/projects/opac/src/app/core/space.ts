import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SpaceBuildingSummary { id: number; publicId: string; code: string; name: string; description: string | null; }
export interface SpaceFloorSummary { id: number; publicId: string; buildingId: number; name: string; floorNumber: number | null; layoutImageUrl: string | null; canvasWidth: number; canvasHeight: number; }
export interface SpaceRoomSummary {
  id: number; publicId: string; floorId: number; floorName: string | null; buildingId: number | null; buildingName: string | null;
  code: string; name: string; kind: number; category: number; capacity: number; minAdvanceMinutes: number; maxAdvanceDays: number;
  maxAdvanceHours: number | null; minGroupSize: number | null; colorHex: string | null; iconName: string | null;
  positionX: number | null; positionY: number | null; width: number | null; height: number | null; maintenance: boolean; maintenanceNote: string | null;
}
export interface SpaceBusySlot { startAt: string; endAt: string; }
export interface SpaceAvailability {
  closed: boolean; openTime: string | null; closeTime: string | null; closedReason: string | null; maintenance: boolean;
  maintenanceNote: string | null; busy: SpaceBusySlot[];
}

export interface SpaceMemberSummary { readerPublicId: string; cardNo: string; name: string | null; }
/** status: 1 Chờ duyệt, 2 Đã duyệt, 3 Đang sử dụng, 4 Hoàn tất, 5 Đã huỷ, 6 Từ chối, 7 Không tới. */
export interface SpaceBookingSummary {
  publicId: string; spaceId: number; spaceName: string | null; floorName: string | null; buildingName: string | null;
  startAt: string; endAt: string; partySize: number; status: number; rejectReason: string | null; note: string | null;
  members: SpaceMemberSummary[];
}
export interface SpaceMyBooking { booking: SpaceBookingSummary; isOwner: boolean; }
export const SpaceBookingStatus = { Pending: 1, Approved: 2, CheckedIn: 3, Completed: 4, Cancelled: 5, Rejected: 6, NoShow: 7 } as const;

export interface SpaceBookRequest { spaceId: number; startAt: string; endAt: string; partySize?: number | null; note?: string | null; memberCards?: string[] | null; }

/**
 * API không gian & đặt phòng của OPAC — ẩn danh dưới /api/opac/space/browse/** (gateway → space's /api/opac/**), cần đăng nhập
 * bạn đọc dưới /api/opac/space/** còn lại (gateway → space's /api/reader/**), đúng khuôn DigitalApi.
 */
@Injectable({ providedIn: 'root' })
export class SpaceApi {
  private readonly http = inject(HttpClient);

  buildings() { return firstValueFrom(this.http.get<SpaceBuildingSummary[]>('/api/opac/space/browse/buildings')); }
  floors(buildingId?: number | null) {
    return firstValueFrom(this.http.get<SpaceFloorSummary[]>('/api/opac/space/browse/floors', { params: buildingId ? { buildingId } : {} }));
  }
  rooms(floorId?: number | null) {
    return firstValueFrom(this.http.get<SpaceRoomSummary[]>('/api/opac/space/browse/spaces', { params: floorId ? { floorId } : {} }));
  }
  availability(spaceId: number, date: string) {
    return firstValueFrom(this.http.get<SpaceAvailability>(`/api/opac/space/browse/spaces/${spaceId}/availability`, { params: { date } }));
  }

  myBookings() { return firstValueFrom(this.http.get<SpaceMyBooking[]>('/api/opac/space/bookings')); }
  book(request: SpaceBookRequest) { return firstValueFrom(this.http.post<SpaceBookingSummary>('/api/opac/space/bookings', request)); }
  checkIn(publicId: string) { return firstValueFrom(this.http.post<SpaceBookingSummary>(`/api/opac/space/bookings/${publicId}/CheckIn`, {})); }
  checkOut(publicId: string) { return firstValueFrom(this.http.post<SpaceBookingSummary>(`/api/opac/space/bookings/${publicId}/CheckOut`, {})); }
  cancel(publicId: string) { return firstValueFrom(this.http.delete<void>(`/api/opac/space/bookings/${publicId}`)); }
  lookupMember(cardNo: string) { return firstValueFrom(this.http.get<SpaceMemberSummary | null>(`/api/opac/space/members/${encodeURIComponent(cardNo)}`)); }
}
