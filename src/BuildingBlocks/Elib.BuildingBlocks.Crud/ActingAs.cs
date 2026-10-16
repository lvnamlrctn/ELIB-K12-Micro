using System.Globalization;
using System.Net;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Người thao tác thật của lời gọi nội bộ giữa service (service token chỉ cho biết "service nào gọi"). Service gọi đính kèm người dùng
/// của request gốc bằng <see cref="Apply"/>; service nhận gắn <see cref="ActingAsFilter"/> vào nhóm endpoint /internal (đã
/// <c>RequireServiceCaller</c> — chỉ service tin cậy mới tới được filter) để cột audit, event và nhật ký ghi đúng người.
/// </summary>
public static class ActingAsHeaders
{
    public const string Actor = "X-Acting-Actor";
    public const string Name = "X-Acting-Name";
    public const string Ip = "X-Acting-Ip";

    private const int MaxName = 250;

    /// <summary>Gắn người thao tác hiện tại vào request đi. Không có người dùng (job hệ thống, service) thì không gắn gì.</summary>
    public static void Apply(HttpRequestMessage request, ICurrentActor? actor, HttpContext? context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (actor?.Id is not { } id || actor.Kind is not (ElibSubjectTypes.Staff or ElibSubjectTypes.Reader or ElibSubjectTypes.Device)) return;
        request.Headers.TryAddWithoutValidation(Actor, string.Create(CultureInfo.InvariantCulture, $"{actor.Kind}:{id}"));
        if (AuditActor.Name(context) is { Length: > 0 } name)
            request.Headers.TryAddWithoutValidation(Name, Uri.EscapeDataString(name.Length > MaxName ? name[..MaxName] : name));
        if (context?.Connection.RemoteIpAddress?.ToString() is { } ip) request.Headers.TryAddWithoutValidation(Ip, ip);
    }

    /// <summary>Đọc người thao tác từ request vào; header thiếu / sai dạng / loại không hợp lệ → null (giữ tài khoản service).</summary>
    public static CrudAuditOrigin? Read(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var parts = headers[Actor].ToString().Split(':', 2);
        if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return null;
        if (parts[0] is not (ElibSubjectTypes.Staff or ElibSubjectTypes.Reader or ElibSubjectTypes.Device)) return null;
        var name = headers[Name].ToString() is { Length: > 0 } raw ? Uri.UnescapeDataString(raw) : null; // chuỗi mã hoá hỏng thì giữ nguyên chữ — vẫn đúng người theo id
        var ip = IPAddress.TryParse(headers[Ip].ToString(), out var address) ? address.ToString() : null;
        return new CrudAuditOrigin(id, parts[0], name is { Length: > MaxName } ? name[..MaxName] : name, null, ip);
    }
}

/// <summary>Endpoint filter đặt người thao tác thật của lời gọi nội bộ (xem <see cref="ActingAsHeaders"/>). Chỉ gắn vào nhóm endpoint yêu cầu service token.</summary>
public sealed class ActingAsFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (ActingAsHeaders.Read(http.Request.Headers) is { } origin)
            http.RequestServices.GetRequiredService<CrudAuditContext>().ActAs(origin);
        return next(context);
    }
}
