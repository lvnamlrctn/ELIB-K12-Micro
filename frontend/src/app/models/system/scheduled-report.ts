// Cấu hình báo cáo định kỳ tự động gửi email — xem backend ScheduledReport entity
export interface ScheduledReport {
  id?:               string;
  publicId?:         string;
  name:              string;
  reportType:        'CIRCULATION' | 'STORE_BOOK';
  reportParamsJson?: string;
  frequencyType:     number; // 1 = Daily, 2 = Weekly, 3 = Monthly
  dayOfWeek?:        number | null;
  dayOfMonth?:       number | null;
  timeOfDay:         string; // "HH:mm"
  recipientEmails:   string; // phân tách bằng dấu phẩy
  status:            number; // 2 = Hoạt động, 1 = Tạm dừng
  lastRunAt?:        string | null;
  lastRunStatus?:    string | null;
  tenantName?:       string | null;
}
