using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

// 1 dòng cấu hình SMS/Zalo ZNS cho mỗi tenant (TenantId = null = cấu hình fallback dùng chung khi tenant
// chưa tự cấu hình riêng) — khác ELIB-LRC (đơn-tenant, key đặt phẳng trong appsettings.json).
[Table("NotificationChannelConfig", Schema = "dbo")]
public class NotificationChannelConfig
{
    [Key] public long Id { get; set; }
    public string? SmsAccessToken  { get; set; } // lưu "ENC:<base64>" qua AesEncryptionHelper.Encrypt
    public string? SmsSender       { get; set; }
    public string? SmsApiUrl       { get; set; } // null = dùng mặc định https://api.speedsms.vn/index.php/sms/send
    public string? ZaloAccessToken { get; set; } // "ENC:<base64>"
    public string? ZaloApiUrl      { get; set; } // null = dùng mặc định https://business.openapi.zalo.me/message/template
    // Audit Trail
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?      TenantId      { get; set; }
    public Guid       PublicId      { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
