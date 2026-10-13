using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application.Sip2;

/// <summary><see cref="Password"/>: bắt buộc khi thêm; để trống khi sửa = giữ mật khẩu cũ.</summary>
public sealed record Sip2StationRequest(string Code, string Name, string LoginId, string? Password, long CircPlaceId,
    bool RequirePatronPassword = false, bool AsciiOnly = false, bool Enabled = true);

public sealed record Sip2StationDto(
    long Id, Guid PublicId, string Code, string Name, string LoginId, bool HasPassword, long CircPlaceId, bool RequirePatronPassword, bool AsciiOnly,
    bool Enabled, DateTimeOffset? LastSeenAt, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Trạm tự phục vụ SIP2 (quyền SIP2LOG). Tài khoản đăng nhập duy nhất trên toàn nền tảng (kiểm tra chéo đơn vị trong ngữ cảnh hệ thống,
/// có thêm unique index); mật khẩu chỉ lưu dạng băm, không bao giờ trả về.
/// </summary>
public sealed class Sip2StationResource(ICrudDbContext db, ITenantContext tenant)
    : CrudResource<Sip2StationResource, Sip2Station, CrudSearch, Sip2StationRequest, Sip2StationDto>(db)
{
    public const int MinPasswordLength = 8;

    protected override string EntityName => "Trạm SIP2";

    protected override string? Describe(Sip2Station entity) => $"{entity.Code} — {entity.Name} (đăng nhập {entity.LoginId})";

    protected override Expression<Func<Sip2Station, Sip2StationDto>> Projection => x => new Sip2StationDto(
        x.Id, x.PublicId, x.Code, x.Name, x.LoginId, x.PasswordHash != "", x.CircPlaceId, x.RequirePatronPassword, x.AsciiOnly, x.Enabled,
        x.LastSeenAt, x.CreatedAt, x.UpdatedAt);

    protected override Sip2Station Create(Sip2StationRequest r)
    {
        var station = Sip2Station.Create(r.Code, r.Name, r.LoginId, r.CircPlaceId, r.RequirePatronPassword, r.AsciiOnly, r.Enabled);
        station.SetPasswordHash(Sip2Passwords.Hash(RequirePassword(r.Password)));
        return station;
    }

    protected override void Update(Sip2Station entity, Sip2StationRequest r)
    {
        entity.Update(r.Code, r.Name, r.LoginId, r.CircPlaceId, r.RequirePatronPassword, r.AsciiOnly, r.Enabled);
        if (!string.IsNullOrEmpty(r.Password)) entity.SetPasswordHash(Sip2Passwords.Hash(RequirePassword(r.Password)));
    }

    protected override IQueryable<Sip2Station> Filter(IQueryable<Sip2Station> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term) || x.LoginId.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Sip2Station> Order(IQueryable<Sip2Station> query) => query.OrderBy(x => x.Code).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(Sip2Station entity, CancellationToken ct)
    {
        if (!await Db.Set<CircPlace>().AnyAsync(p => p.Id == entity.CircPlaceId, ct))
            throw new BusinessRuleException("CIRC_PLACE_NOT_FOUND", "Điểm lưu thông đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("SIP2_STATION_CODE_EXISTS", $"Mã trạm '{entity.Code}' đã có.");
        using (tenant.UseSystem())
        {
            // "Tenant" = bộ lọc đơn vị của ElibDbContext; ngữ cảnh hệ thống bỏ qua RLS.
            if (await Set.IgnoreQueryFilters(["Tenant"]).AnyAsync(x => x.LoginKey == entity.LoginKey && x.Id != entity.Id, ct))
                throw new ConflictException("SIP2_LOGIN_EXISTS", $"Tài khoản đăng nhập '{entity.LoginId}' đã được trạm khác dùng — chọn tên khác (nên thêm mã đơn vị).");
        }
    }

    private static string RequirePassword(string? password) => password is { Length: >= MinPasswordLength and <= 64 } && password.All(ch => ch is > ' ' and < (char)127 and not '|')
        ? password
        : throw new BusinessRuleException("SIP2_PASSWORD_INVALID", $"Mật khẩu trạm từ {MinPasswordLength} đến 64 ký tự ASCII, không dấu cách, không có '|'.");
}

/// <summary>Mật khẩu trạm: PBKDF2-SHA256, 100.000 vòng, salt 16 byte — "pbkdf2$vòng$salt$hash" (base64).</summary>
public static class Sip2Passwords
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return string.Create(CultureInfo.InvariantCulture, $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public static bool Verify(string? password, string stored)
    {
        var parts = (stored ?? "").Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)) return false;
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password ?? "", Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class Sip2LogSearch : CrudSearch
{
    public long? StationId { get; set; }
    public string? Command { get; set; }
    public bool? Failed { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record Sip2LogDto(long Id, long StationId, string? StationCode, string Command, string Request, string Response, bool Ok, int ElapsedMs,
    string? RemoteIp, DateTimeOffset At);

/// <summary>Nhật ký giao dịch SIP2 (monolith: Sip2Log/Search, quyền SIP2LOG:view). Giữ <see cref="RetentionDays"/> ngày.</summary>
public sealed class Sip2LogQueries(ICrudDbContext db)
{
    public const int RetentionDays = 90;

    public async Task<CrudPage<Sip2LogDto>> SearchAsync(Sip2LogSearch s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var query = db.Set<Sip2Log>().AsNoTracking();
        if (s.StationId is { } station) query = query.Where(x => x.StationId == station);
        if (!string.IsNullOrWhiteSpace(s.Command)) query = query.Where(x => x.Command == s.Command.Trim());
        if (s.Failed is { } failed) query = query.Where(x => x.Ok != failed);
        if (s.From is { } from)
        {
            var start = Loan.VietnamStart(from);
            query = query.Where(x => x.At >= start);
        }
        if (s.To is { } to)
        {
            var end = Loan.VietnamStart(to.AddDays(1));
            query = query.Where(x => x.At < end);
        }
        if (s.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Request.ToLower().Contains(term) || x.Response.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        var size = Math.Clamp(s.PageSize, 1, 200);
        var index = Math.Max(1, s.PageIndex);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Skip((index - 1) * size).Take(size)
            .Select(x => new Sip2LogDto(x.Id, x.StationId, null, x.Command, x.Request, x.Response, x.Ok, x.ElapsedMs, x.RemoteIp, x.At))
            .ToListAsync(ct);
        // Mã trạm (kể cả trạm đã xoá — nhật ký còn giữ 90 ngày).
        var stationIds = items.Select(x => x.StationId).Distinct().ToList();
        var codes = await db.Set<Sip2Station>().IgnoreQueryFilters(["SoftDelete"]).AsNoTracking().Where(st => stationIds.Contains(st.Id))
            .ToDictionaryAsync(st => st.Id, st => st.Code, ct);
        return new CrudPage<Sip2LogDto>([.. items.Select(x => x with { StationCode = codes.GetValueOrDefault(x.StationId) })], total, index, size);
    }

    /// <summary>Xoá nhật ký cũ hơn <see cref="RetentionDays"/> ngày (job định kỳ của đơn vị).</summary>
    public Task<int> PurgeAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-RetentionDays);
        return db.Set<Sip2Log>().Where(x => x.At < cutoff).ExecuteDeleteAsync(ct);
    }
}
