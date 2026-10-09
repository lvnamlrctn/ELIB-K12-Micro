using Elib.BuildingBlocks.Crud;
using Elib.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Notification.Application;

public sealed record NotificationLogDto(
    long Id,
    Guid PublicId,
    DateTimeOffset CreatedAt,
    string Channel,
    string TemplateCode,
    string? Recipient,
    string? Subject,
    NotificationStatus Status,
    string? Error,
    bool ViaPlatform)
{
    public static NotificationLogDto From(NotificationLog l) =>
        new(l.Id, l.PublicId, l.CreatedAt, l.Channel, l.TemplateCode, l.Recipient, l.Subject, l.Status, l.Error, l.ViaPlatform);
}

/// <summary>Status: 1 đã gửi, 2 lỗi, 3 bỏ qua (theo <see cref="NotificationStatus"/>).</summary>
public sealed class NotificationLogSearch : CrudSearch
{
    public string? TemplateCode { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

/// <summary>Nhật ký gửi tin — chỉ đọc.</summary>
public sealed class NotificationLogQueries(INotificationDb db)
{
    public async Task<CrudPage<NotificationLogDto>> SearchAsync(NotificationLogSearch search, CancellationToken ct)
    {
        IQueryable<NotificationLog> q = db.NotificationLogs.AsNoTracking();
        if (search.Status is > 0)
        {
            var status = (NotificationStatus)search.Status.Value;
            q = q.Where(l => l.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(search.TemplateCode))
        {
            var code = search.TemplateCode.Trim().ToUpperInvariant();
            q = q.Where(l => l.TemplateCode == code);
        }
        if (search.From is { } from) q = q.Where(l => l.CreatedAt >= from);
        if (search.To is { } to) q = q.Where(l => l.CreatedAt <= to);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            q = q.Where(l => (l.Recipient != null && l.Recipient.ToLower().Contains(term)) || (l.Subject != null && l.Subject.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<NotificationLogDto>(rows.Select(NotificationLogDto.From).ToList(), total, page, size);
    }
}
