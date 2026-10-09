using System.Text.Json;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>Cấu hình bảng theo tài khoản (Đợt 21 — port từ ELIB-LRC <c>AdminPreferenceController</c>). Chỉ đọc/ghi
/// bản ghi của chính tài khoản đang đăng nhập (ActorId lấy từ JWT, payload không nhận userId); cần quyền xem
/// module của trang. Ghi dùng Revision so-và-đổi để 2 phiên cùng sửa không âm thầm ghi đè nhau (409).</summary>
[Route("api/Dbo/AdminPreference")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPreferenceController(ELIBAPIDbContext db, IPermissionService permissions) : BaseApiController
{
    public sealed record SaveRequest(int Revision, JsonElement Settings);

    private long Actor => User.FindFirst("Type")?.Value == "Reader" ? 0 : GetCurrentUserId();

    private async Task<bool> AllowedAsync(string page)
        => Actor > 0 && await permissions.HasPermissionAsync(Actor, AdminPreferencePolicy.Module(page), "view");

    [HttpGet("{page}")]
    public async Task<IActionResult> Get(string page, CancellationToken ct)
    {
        if (!AdminPreferencePolicy.Supports(page)) return NotFound(ApiResponse<object>.Fail("Trang không hỗ trợ lưu cấu hình.", 404));
        if (!await AllowedAsync(page)) return Forbidden();
        var actor = Actor;
        var row = await db.AdminUiPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.PageKey == page, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            revision = row?.Revision ?? 0,
            settings = row == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(row.SettingsJson),
        }));
    }

    [HttpPut("{page}"), RequestSizeLimit(20000)]
    public async Task<IActionResult> Put(string page, [FromBody] SaveRequest request, CancellationToken ct)
    {
        if (!AdminPreferencePolicy.Supports(page)) return NotFound(ApiResponse<object>.Fail("Trang không hỗ trợ lưu cấu hình.", 404));
        if (!await AllowedAsync(page)) return Forbidden();
        try { AdminPreferencePolicy.Validate(page, request.Settings); }
        catch (ArgumentException e) { return BadRequest(ApiResponse<object>.Fail(e.Message)); }
        if (request.Revision < 0 || request.Revision == int.MaxValue) return BadRequest(ApiResponse<object>.Fail("Revision không hợp lệ."));

        var actor = Actor;
        var json = request.Settings.GetRawText();
        if (request.Revision == 0)
        {
            db.AdminUiPreferences.Add(new AdminUiPreference
            {
                ActorId = actor, PageKey = page, SettingsJson = json, Revision = 1, UpdatedAt = DateTime.UtcNow, TenantId = GetTenantId(),
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.AdminUiPreferences.AnyAsync(x => x.ActorId == actor && x.PageKey == page, ct)) throw;
                return Conflicted();
            }
        }
        else
        {
            var changed = await db.AdminUiPreferences
                .Where(x => x.ActorId == actor && x.PageKey == page && x.Revision == request.Revision)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.SettingsJson, json)
                    .SetProperty(x => x.Revision, x => x.Revision + 1)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            if (changed != 1) return Conflicted();
        }
        return Ok(ApiResponse<object>.Ok(new { revision = request.Revision + 1 }));
    }

    private static IActionResult Forbidden()
        => new ObjectResult(ApiResponse<object>.Fail("Bạn không có quyền xem trang này.", 403)) { StatusCode = 403 };

    private static IActionResult Conflicted()
        => new ObjectResult(ApiResponse<object>.Fail("Cấu hình đã thay đổi ở phiên khác. Hãy tải lại trước khi lưu.", 409)) { StatusCode = 409 };
}
