export interface RoomBookingConfig {
  id: number;
  publicId?: string;
  mapObjectId: number;
  roomName?: string | null;
  capacity: number;
  slotMinutes: number;
  minAdvanceMinutes: number;
  maxAdvanceDays: number;
  maxBookingMinutesPerReader: number;
  checkInGraceMinutes: number;
  /** Loại phòng: true = tự động duyệt, false = chờ thủ thư duyệt. */
  autoApprove?: boolean;
  /** Đặt trước tối đa theo giờ; > 0 thì thay cho maxAdvanceDays. */
  maxAdvanceHours?: number | null;
  /** Nội quy phòng hiện cho bạn đọc. */
  rules?: string | null;
  /** Id loại bạn đọc được đặt, cách nhau dấu phẩy; trống = mọi đối tượng. */
  allowedReaderTypeIds?: string | null;
  /** Số người tối thiểu (kể cả người đặt) — phòng nhóm bắt buộc nhập thẻ thành viên. */
  minGroupSize?: number | null;
  /** Đang tạm ngưng (bảo trì) — bật/tắt qua RoomBookingAdmin/Maintenance, không qua form cấu hình. */
  maintenance?: boolean;
  maintenanceNote?: string | null;
  status?: number | null;
  tenantId?: number | null;
  tenantName?: string | null;
}
