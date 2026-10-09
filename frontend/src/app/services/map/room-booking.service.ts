import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '../../opac/error.interceptor';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { RoomBookingAdmin, RoomCheckInResult, RoomBookingSettings, RoomOpeningHour, RoomSpecialDay, SpecialDayInput,
  SpecialDayResult, RoomBookingBan, RoomBookingFilter, MaintenanceResult, RoomBookingReport, RoomBookingReportRequest, NotificationTemplate,
  StaffBoard, AccessDevice, AccessStaffCard, AccessScanLog } from '../../models/map/room-booking';

export interface RoomBookingAdminSearchResult { data: RoomBookingAdmin[]; recordsTotal: number; }

/**
 * Đơn vị đang xem trên màn Đặt phòng (K12). Chỉ tài khoản đặc quyền chọn được (ô "Đơn vị" đầu trang); user thường để null — backend
 * ép theo đơn vị JWT. Các service bên dưới tự gửi kèm tenantId (query hoặc body), nên các tab không phải tự truyền.
 * null với tài khoản đặc quyền = xem tất cả / sửa cấu hình dùng chung.
 */
@Injectable({ providedIn: 'root' })
export class RoomBookingTenantScope {
  readonly tenantId = signal<string | null>(null);

  /** "?tenantId=…" / "&tenantId=…" hoặc chuỗi rỗng. */
  query(prefix: '?' | '&' = '?'): string {
    const t = this.tenantId();
    return t ? `${prefix}tenantId=${encodeURIComponent(t)}` : '';
  }
}

/** Đặt phòng phía admin (api/Map/RoomBookingAdmin) — danh sách, duyệt, QR check-in, trả phòng, cấu hình, báo cáo (port ELIB-LRC 10-04). */
@Injectable({ providedIn: 'root' })
export class RoomBookingAdminService {
  private http = inject(HttpClient);
  private scope = inject(RoomBookingTenantScope);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/RoomBookingAdmin`; }
  private q(prefix: '?' | '&' = '?'): string { return this.scope.query(prefix); }

  private filterPayload(params: RoomBookingFilter) {
    return {
      tenantId: params.tenantId ?? this.scope.tenantId(),
      status: params.status ?? null,
      mapObjectId: params.mapObjectId ?? null,
      category: params.category ?? null,
      keyword: params.keyword || null,
      // Ngày theo giờ trình duyệt → mốc UTC; "đến ngày" lấy hết ngày đó.
      dateFrom: params.dateFrom ? new Date(params.dateFrom + 'T00:00:00').toISOString() : null,
      dateTo: params.dateTo ? new Date(params.dateTo + 'T23:59:59').toISOString() : null,
      pageIndex: params.pageIndex ?? 1,
      pageSize: params.pageSize ?? 10,
    };
  }

  search(params: RoomBookingFilter): Observable<RoomBookingAdminSearchResult> {
    const payload = this.filterPayload(params);
    return this.http.post<any>(`${this.baseUrl}/Search`, payload).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  approve(publicId: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Approve/${publicId}`, {}).pipe(map(res => res.data ?? res));
  }

  reject(publicId: string, reason?: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Reject/${publicId}`, { reason }).pipe(map(res => res.data ?? res));
  }

  /** Check-in bằng mã QR bạn đọc xuất trình. Lỗi nghiệp vụ (400) vẫn trả về kèm lượt đặt tìm được để hiển thị. */
  checkIn(publicId: string, mapObjectId: number | null): Observable<RoomCheckInResult> {
    return this.http.put<any>(`${this.baseUrl}/CheckIn/${publicId}`, { mapObjectId }, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(
      map(res => ({ ok: true, message: res?.message ?? '', booking: res?.data ?? null })),
      catchError(err => of({ ok: false, message: err?.error?.message ?? 'Không thể check-in.', booking: err?.error?.data ?? null }))
    );
  }

  /** Kiosk check-in bằng khuôn mặt (port ELIB-LRC 09-30). null = chưa nhận ra ai (camera quét tiếp); còn lại là kết quả để hiện
   *  như khi quét QR (kể cả lỗi kèm thông tin lượt đặt, vd sai phòng kiosk). */
  checkInByFace(imageBase64: string, mapObjectId: number | null): Observable<RoomCheckInResult | null> {
    return this.http.put<any>(`${this.baseUrl}/CheckInByFace`, { imageBase64, mapObjectId }, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(
      map(res => res?.data?.recognized ? { ok: true, message: res?.message ?? '', booking: res.data.booking ?? null } : null),
      catchError(err => of({ ok: false, message: err?.error?.message ?? 'Không thể check-in.', booking: err?.error?.data?.booking ?? null }))
    );
  }

  exportExcel(params: RoomBookingFilter): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Export`, this.filterPayload(params), { responseType: 'blob' });
  }

  report(req: RoomBookingReportRequest): Observable<RoomBookingReport> {
    return this.http.post<any>(`${this.baseUrl}/Report`, { ...req, tenantId: this.scope.tenantId() }).pipe(map(res => res.data));
  }

  reportExport(req: RoomBookingReportRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/Report/Export`, { ...req, tenantId: this.scope.tenantId() }, { responseType: 'blob' });
  }

  floorBoard(floorId: number, date: string): Observable<StaffBoard> {
    return this.http.get<any>(`${this.baseUrl}/FloorBoard?floorId=${floorId}&date=${date}`).pipe(map(res => res.data));
  }

  maintenance(configPublicId: string, body: { on: boolean; note?: string | null; cancelAffected?: boolean }, preview: boolean): Observable<MaintenanceResult> {
    return this.http.put<any>(`${this.baseUrl}/Maintenance/${configPublicId}?preview=${preview}`, body).pipe(map(res => res.data));
  }

  templates(): Observable<{ templates: NotificationTemplate[]; tokens: string[] }> {
    return this.http.get<any>(`${this.baseUrl}/Templates${this.q()}`).pipe(map(res => res.data));
  }

  saveTemplate(event: string, subject: string, body: string): Observable<NotificationTemplate[]> {
    return this.http.put<any>(`${this.baseUrl}/Templates/${event}${this.q()}`, { subject, body }).pipe(map(res => res.data));
  }

  resetTemplate(event: string): Observable<NotificationTemplate[]> {
    return this.http.delete<any>(`${this.baseUrl}/Templates/${event}${this.q()}`).pipe(map(res => res.data));
  }

  testTemplate(event: string, to: string | null): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/Templates/${event}/Test${this.q()}`, { to }).pipe(map(res => res.data));
  }

  /** Thủ thư trả phòng hộ (lượt đang sử dụng → hoàn tất), phòng được mở lại ngay. */
  checkOut(publicId: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/CheckOut/${publicId}`, {}).pipe(map(res => res.data ?? res));
  }

  getSettings(): Observable<RoomBookingSettings> {
    return this.http.get<any>(`${this.baseUrl}/Settings${this.q()}`).pipe(map(res => res.data));
  }

  saveSettings(settings: RoomBookingSettings): Observable<RoomBookingSettings> {
    return this.http.put<any>(`${this.baseUrl}/Settings${this.q()}`, settings).pipe(map(res => res.data));
  }

  openingHours(): Observable<RoomOpeningHour[]> {
    return this.http.get<any>(`${this.baseUrl}/OpeningHours${this.q()}`).pipe(map(res => res.data ?? []));
  }

  saveOpeningHours(category: number | null, rows: { weekday: number; openTime: string | null; closeTime: string | null; isClosed: boolean }[]): Observable<RoomOpeningHour[]> {
    const q = category == null ? this.q() : `?category=${category}${this.q('&')}`;
    return this.http.put<any>(`${this.baseUrl}/OpeningHours${q}`, rows).pipe(map(res => res.data ?? []));
  }

  specialDays(from?: string): Observable<RoomSpecialDay[]> {
    const q = from ? `?from=${from}${this.q('&')}` : this.q();
    return this.http.get<any>(`${this.baseUrl}/SpecialDays${q}`).pipe(map(res => res.data ?? []));
  }

  /** Xem trước lượt đặt bị ảnh hưởng — không lưu. */
  previewSpecialDay(input: SpecialDayInput, publicId?: string | null): Observable<SpecialDayResult> {
    const q = publicId ? `?publicId=${publicId}${this.q('&')}` : this.q();
    return this.http.post<any>(`${this.baseUrl}/SpecialDays/Preview${q}`, input).pipe(map(res => res.data));
  }

  saveSpecialDay(input: SpecialDayInput, publicId?: string | null): Observable<SpecialDayResult> {
    return (publicId
      ? this.http.put<any>(`${this.baseUrl}/SpecialDays/${publicId}${this.q()}`, input)
      : this.http.post<any>(`${this.baseUrl}/SpecialDays${this.q()}`, input)).pipe(map(res => res.data));
  }

  deleteSpecialDay(publicId: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/SpecialDays/${publicId}${this.q()}`).pipe(map(res => res.data ?? res));
  }

  bans(activeOnly: boolean, keyword: string, pageIndex: number, pageSize: number): Observable<{ data: RoomBookingBan[]; recordsTotal: number }> {
    const q = `activeOnly=${activeOnly}&keyword=${encodeURIComponent(keyword || '')}&pageIndex=${pageIndex}&pageSize=${pageSize}${this.q('&')}`;
    return this.http.get<any>(`${this.baseUrl}/Bans?${q}`).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }

  addBan(readerId: number, days: number, reason: string): Observable<RoomBookingBan> {
    return this.http.post<any>(`${this.baseUrl}/Bans`, { readerId, days, reason }).pipe(map(res => res.data));
  }

  liftBan(publicId: string): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/Bans/Lift/${publicId}`, {}).pipe(map(res => res.data ?? res));
  }
}

/** Màn "Kiểm soát cửa": thiết bị, thẻ quản trị, mở cửa từ web, nhật ký quẹt thẻ. */
@Injectable({ providedIn: 'root' })
export class AccessControlAdminService {
  private http = inject(HttpClient);
  private scope = inject(RoomBookingTenantScope);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Map/AccessControl`; }
  private q(): string { return this.scope.query(); }

  devices(): Observable<AccessDevice[]> {
    return this.http.get<any>(`${this.baseUrl}/Devices${this.q()}`).pipe(map(res => res.data ?? []));
  }

  /** Thêm/sửa — thêm mới (hoặc cấp lại khoá) trả apiKey gốc, chỉ hiện 1 lần. */
  saveDevice(publicId: string | null, body: { code: string; name: string | null; mapObjectId: number | null; isActive: boolean }): Observable<{ device: AccessDevice; apiKey: string | null }> {
    const req = publicId ? this.http.put<any>(`${this.baseUrl}/Devices/${publicId}${this.q()}`, body) : this.http.post<any>(`${this.baseUrl}/Devices${this.q()}`, body);
    return req.pipe(map(res => res.data));
  }

  regenerateKey(publicId: string): Observable<{ device: AccessDevice; apiKey: string | null }> {
    return this.http.put<any>(`${this.baseUrl}/Devices/${publicId}/RegenerateKey${this.q()}`, {}).pipe(map(res => res.data));
  }

  deleteDevice(publicId: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/Devices/${publicId}${this.q()}`).pipe(map(res => res.data ?? res));
  }

  staffCards(): Observable<AccessStaffCard[]> {
    return this.http.get<any>(`${this.baseUrl}/StaffCards${this.q()}`).pipe(map(res => res.data ?? []));
  }

  saveStaffCard(publicId: string | null, body: { cardUid: string; label: string | null; isActive: boolean }): Observable<AccessStaffCard> {
    const req = publicId ? this.http.put<any>(`${this.baseUrl}/StaffCards/${publicId}${this.q()}`, body) : this.http.post<any>(`${this.baseUrl}/StaffCards${this.q()}`, body);
    return req.pipe(map(res => res.data));
  }

  deleteStaffCard(publicId: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/StaffCards/${publicId}${this.q()}`).pipe(map(res => res.data ?? res));
  }

  unlock(body: { devicePublicId?: string | null; mapObjectId?: number | null; bookingPublicId?: string | null; note?: string | null }): Observable<{ devices: number }> {
    return this.http.post<any>(`${this.baseUrl}/Unlock${this.q()}`, body).pipe(map(res => res.data));
  }

  logs(filter: { from?: string | null; to?: string | null; mapObjectId?: number | null; allowed?: boolean | null; keyword?: string | null; pageIndex: number; pageSize: number }): Observable<{ data: AccessScanLog[]; recordsTotal: number }> {
    return this.http.post<any>(`${this.baseUrl}/Logs`, { ...filter, tenantId: this.scope.tenantId() }).pipe(
      map(res => ({ data: res?.data?.items ?? [], recordsTotal: res?.data?.totalCount ?? 0 })),
      catchError(() => of({ data: [], recordsTotal: 0 }))
    );
  }
}
