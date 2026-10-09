// Cấu hình SMS/Zalo ZNS theo tenant — nguồn: Entities/Dbo/NotificationChannelConfig.cs.
// smsAccessToken/zaloAccessToken trả về "***" khi đã có giá trị (server mask) — để trống khi Update để
// giữ nguyên giá trị cũ.
export interface NotificationChannelConfig {
  id?:              number;
  smsAccessToken?:  string | null;
  smsSender?:       string | null;
  smsApiUrl?:       string | null;
  zaloAccessToken?: string | null;
  zaloApiUrl?:      string | null;
  status?:          number | null;
  tenantId?:        number | null;
  tenantName?:      string | null;
  publicId?:        string;
}
