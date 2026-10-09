using System.Text.Json;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>Lịch sử thay đổi theo hồ sơ (Đợt 16 — port từ ELIB-LRC "Nhật ký thay đổi chi tiết", bản đầu
/// chỉ phủ Reader + LoanTransaction). Đọc <c>dbo.UserLog</c> với <c>ActionType="EntityChange"</c> do
/// <see cref="ELIBAPI.Infrastructure.Services.EntityAuditService"/> ghi — không có bảng/cột mới.</summary>
[Route("api/Dbo/EntityHistory")]
public sealed class EntityHistoryController(ELIBAPIDbContext db, IPermissionService permSvc) : BaseApiController
{
    private static readonly Dictionary<string, string> SourceModuleByType = new()
    {
        ["Reader"] = "READERS",
        ["LoanTransaction"] = "BORROW",
        // Đợt 17
        ["Barcode"] = "AB_RECEIPTS",
        ["DigitalDocument"] = "DIGITAL_DOC",
        ["PrintBib"] = "CATALOG_BIBS",
    };

    /// <summary>Loại thao tác được phép lọc (Đợt 21) — đúng các giá trị <c>action</c> mà EntityAuditService ghi.
    /// Danh sách đóng để tham số không bao giờ chèn ký tự đặc biệt vào mẫu LIKE.</summary>
    private static readonly HashSet<string> FilterableActions =
        ["Add", "Update", "Delete", "Checkout", "Renew", "Return", "NoteEdit", "Shelve", "SignConfirm", "Place", "ReRegister"];

    [HttpGet("{type}/{publicId:guid}")]
    public async Task<IActionResult> GetHistory(string type, Guid publicId, long? before, int pageSize = 25,
        DateTime? from = null, DateTime? to = null, long? actorId = null, string? action = null)
    {
        if (!string.IsNullOrEmpty(action) && !FilterableActions.Contains(action))
            return BadRequest(ApiResponse<object>.Fail("Loại thao tác không hợp lệ."));

        if (!SourceModuleByType.TryGetValue(type, out var sourceModule))
            return BadRequest(ApiResponse<object>.Fail("Loại hồ sơ không hợp lệ."));

        var uid = GetCurrentUserId();
        // Cần đồng thời quyền xem Nhật ký hệ thống VÀ quyền xem nguồn tương ứng, đúng LRC.
        if (!await permSvc.HasPermissionAsync(uid, "SYSTEM_LOG", "view"))
            return Forbidden();
        if (!await permSvc.HasPermissionAsync(uid, sourceModule, "view"))
            return Forbidden();

        var tenantId = GetTenantId();
        var sourceExists = type switch
        {
            "Reader" => await db.Readers.AnyAsync(r => r.PublicId == publicId && (tenantId == null || r.TenantId == tenantId)),
            "LoanTransaction" => await db.BookOuts.AnyAsync(b => b.PublicId == publicId && (tenantId == null || b.TenantId == tenantId)),
            "Barcode" => await db.Barcodes.AnyAsync(x => x.PublicId == publicId && (tenantId == null || x.TenantId == tenantId)),
            "DigitalDocument" => await db.EbookItems.AnyAsync(x => x.PublicId == publicId && (tenantId == null || x.TenantId == tenantId)),
            "PrintBib" => await db.Bibs.AnyAsync(x => x.PublicId == publicId && (tenantId == null || x.TenantId == tenantId)),
            _ => false,
        };
        // Không phân biệt "không tồn tại" với "khác tenant" — tránh rò rỉ tồn tại của hồ sơ tenant khác.
        if (!sourceExists) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));

        pageSize = Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100);
        var objectKey = $"{type}:{publicId}";
        var all = db.UserLogs.Where(l => l.ActionType == "EntityChange" && l.Object == objectKey);
        var q = all;
        if (before.HasValue) q = q.Where(l => l.Id < before.Value);
        // Đợt 21 — lọc ngày (theo ngày, gồm trọn ngày "đến"), người thao tác, loại thao tác. Payload JSON do
        // EntityAuditService tự ghi (camelCase, không thụt lề) nên "action" luôn ở dạng "action":"<X>".
        if (from.HasValue) q = q.Where(l => l.Submited >= from.Value.Date);
        if (to.HasValue) { var end = to.Value.Date.AddDays(1); q = q.Where(l => l.Submited < end); }
        if (actorId.HasValue) q = q.Where(l => l.UserId == actorId.Value);
        if (!string.IsNullOrEmpty(action)) { var pattern = $"\"action\":\"{action}\""; q = q.Where(l => l.Action != null && l.Action.Contains(pattern)); }

        var rows = await q.OrderByDescending(l => l.Id).Take(pageSize + 1)
            .Select(l => new { l.Id, l.Action, l.Submited }).ToListAsync();
        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        var items = new List<EntityHistoryEvent>();
        foreach (var row in page)
        {
            var parsed = TryParse(row.Action);
            if (parsed == null) continue; // phòng thủ — chỉ nơi này ghi ActionType=EntityChange, không nên xảy ra
            parsed.Id = row.Id;
            parsed.Timestamp = row.Submited;
            items.Add(parsed);
        }

        // Người thao tác lấy từ lịch sử của chính hồ sơ (đã kiểm tra đúng đơn vị ở trên) — không lộ tài khoản đơn vị
        // khác và không cần API danh sách người dùng. Chỉ tính ở trang đầu.
        List<EntityHistoryActor>? actors = null;
        if (!before.HasValue)
        {
            var actorIds = await all.Where(l => l.UserId != null).Select(l => l.UserId!.Value).Distinct().ToListAsync();
            var names = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = u.FullName ?? u.LoginName }).ToListAsync();
            actors = actorIds
                .Select(id => new EntityHistoryActor { Id = id, Name = names.FirstOrDefault(n => n.Id == id)?.Name })
                .OrderBy(a => a.Name ?? "").ToList();
        }

        return Ok(ApiResponse<EntityHistoryResponse>.Ok(new EntityHistoryResponse
        {
            Items = items,
            Next = hasMore && page.Count > 0 ? page[^1].Id : null,
            Actors = actors,
        }));
    }

    private static EntityHistoryEvent? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var evt = new EntityHistoryEvent
            {
                ActorId   = root.TryGetProperty("actorId", out var a) ? a.GetInt64() : 0,
                ActorName = root.TryGetProperty("actorName", out var an) && an.ValueKind != JsonValueKind.Null ? an.GetString() : null,
                Action    = root.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "",
                Reason    = root.TryGetProperty("reason", out var rs) && rs.ValueKind != JsonValueKind.Null ? rs.GetString() : null,
            };
            if (root.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in changes.EnumerateArray())
                {
                    evt.Changes.Add(new EntityHistoryFieldChange
                    {
                        Field    = c.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "",
                        OldValue = c.TryGetProperty("oldValue", out var ov) && ov.ValueKind != JsonValueKind.Null ? ov.GetString() : null,
                        NewValue = c.TryGetProperty("newValue", out var nv) && nv.ValueKind != JsonValueKind.Null ? nv.GetString() : null,
                    });
                }
            }
            return evt;
        }
        catch { return null; }
    }

    private static IActionResult Forbidden(string msg = "Bạn không có quyền xem lịch sử của nguồn này.")
        => new ObjectResult(ApiResponse<object>.Fail(msg, 403)) { StatusCode = 403 };
}
