namespace ELIBAPI.Core.DTOs.Request;

// ==================== NOTIFICATION_CHANNEL_CONFIG ====================
public class NotificationChannelConfigRequest
{
    // Để trống khi Update = giữ nguyên giá trị cũ (không ghi đè bằng chuỗi rỗng/mask "***").
    public string? SmsAccessToken  { get; set; }
    public string? SmsSender       { get; set; }
    public string? SmsApiUrl       { get; set; }
    public string? ZaloAccessToken { get; set; }
    public string? ZaloApiUrl      { get; set; }
}
public class NotificationChannelConfigSearchRequest : SearchRequest { }

// ==================== NOTIFICATION_LOG ====================
// Chỉ đọc — insert duy nhất từ NotificationDispatcher, không có UI Add/Edit; giữ TRequest cho khớp
// IGenericRepository<TEntity,TSearch,TRequest>, không dùng trong thực tế.
public class NotificationLogRequest { }
public class NotificationLogSearchRequest : SearchRequest
{
    public string?   Channel   { get; set; }
    public string?   EventCode { get; set; }
    public bool?     Success   { get; set; }
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
}
