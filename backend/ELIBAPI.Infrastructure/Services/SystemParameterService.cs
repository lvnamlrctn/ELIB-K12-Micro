using System.Text.RegularExpressions;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

public partial class SystemParameterService(ELIBAPIDbContext db, IHttpContextAccessor http) : ISystemParameterService
{
    public Task<string?> GetValueAsync(string code) => GetValueAsync(code, GetTenantId());

    public async Task<string?> GetValueAsync(string code, long? tenantId)
    {
        var codeLower = code.ToLower();
        var q = db.SystemParameters.Where(x => x.Code!.ToLower() == codeLower && x.IsDelete != 2);
        if (tenantId.HasValue) q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);
        return await q.OrderByDescending(x => x.TenantId != null).Select(x => x.Value).FirstOrDefaultAsync();
    }

    public Task<bool> IsEnabledAsync(string code) => IsEnabledAsync(code, GetTenantId());

    public async Task<bool> IsEnabledAsync(string code, long? tenantId)
    {
        var raw = await GetValueAsync(code, tenantId);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var plain = HtmlTagRegex().Replace(raw, "").Trim();
        return plain == "1";
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    private long? GetTenantId()
    {
        var value = http.HttpContext?.User.FindFirst("TenantId")?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }
}
