using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class NotificationChannelConfigRepository
    : BaseRepository<NotificationChannelConfig, NotificationChannelConfigSearchRequest, NotificationChannelConfigRequest>
{
    public NotificationChannelConfigRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<NotificationChannelConfig> BuildQuery(NotificationChannelConfigSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        return q.OrderByDescending(x => x.Id);
    }

    // Để trống field khi Update = giữ nguyên giá trị cũ; không bao giờ ghi đè bằng rỗng/mask "***".
    protected override void MapRequestToEntity(NotificationChannelConfigRequest r, NotificationChannelConfig e, long userId, bool isNew)
    {
        if (!string.IsNullOrWhiteSpace(r.SmsAccessToken) && r.SmsAccessToken != "***")
            e.SmsAccessToken = "ENC:" + AesEncryptionHelper.Encrypt(r.SmsAccessToken);
        if (!string.IsNullOrWhiteSpace(r.ZaloAccessToken) && r.ZaloAccessToken != "***")
            e.ZaloAccessToken = "ENC:" + AesEncryptionHelper.Encrypt(r.ZaloAccessToken);
        e.SmsSender  = r.SmsSender;
        e.SmsApiUrl  = r.SmsApiUrl;
        e.ZaloApiUrl = r.ZaloApiUrl;

        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(NotificationChannelConfig e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(NotificationChannelConfig e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
