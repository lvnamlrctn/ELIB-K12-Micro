// Lịch sử gửi SMS/Zalo ZNS — nguồn: Entities/Dbo/NotificationLog.cs. Chỉ đọc.
export interface NotificationLog {
  id?:            number;
  channel?:       string | null;
  recipient?:     string | null;
  eventCode?:     string | null;
  success?:       boolean | null;
  errorMessage?:  string | null;
  sentAt?:        string | null;
  tenantId?:      number | null;
  tenantName?:    string | null;
  publicId?:      string;
}
