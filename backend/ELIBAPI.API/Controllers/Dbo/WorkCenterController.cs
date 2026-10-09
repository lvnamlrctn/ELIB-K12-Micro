using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>
/// Trung tâm công việc (Đợt 15 — port từ ELIB-LRC, bản đầu). Tổng hợp 3 nguồn hồ sơ chờ duyệt (đặt phòng
/// học nhóm, tài liệu nộp, đánh giá) để cán bộ nhận việc/phân công/đặt hạn thay vì vào từng trang quản lý
/// riêng. Không thêm quyền mới ngoài <c>DOCUMENT_SUBMISSION</c> (nguồn duy nhất chưa có quyền admin nào) —
/// dùng lại <c>STUDY_ROOM_BOOKING</c>/<c>EBOOKREVIEW</c> sẵn có. Đặt phòng KHÔNG có endpoint quyết định ở
/// đây — FE gọi thẳng <see cref="Map.RoomBookingAdminController"/> Approve/Reject sẵn có, giữ nguyên cơ chế
/// thông báo hiện tại. Metadata phân công (<see cref="AdminWorkAssignment"/>) LEFT JOIN trực tiếp với 3
/// nguồn ở phía đọc — không backfill, không worker/outbox, đúng quy mô thư viện.
/// </summary>
[Route("api/Dbo/WorkCenter")]
public sealed class WorkCenterController(
    ELIBAPIDbContext db,
    IPermissionService permSvc,
    IEbookReviewRepository reviewRepo,
    IMinioService minio) : BaseApiController
{
    private static readonly string[] ValidTypes = ["room-booking", "submission", "review"];

    private static readonly Dictionary<string, string> ModuleByType = new()
    {
        ["room-booking"] = "STUDY_ROOM_BOOKING",
        ["submission"]   = "DOCUMENT_SUBMISSION",
        ["review"]       = "EBOOKREVIEW",
    };

    private static readonly Dictionary<string, string> LabelByType = new()
    {
        ["room-booking"] = "Yêu cầu đặt phòng",
        ["submission"]   = "Tài liệu nộp",
        ["review"]       = "Đánh giá",
    };

    // ── Helper: quyền theo nguồn ─────────────────────────────────────────────
    private async Task<(bool CanView, bool CanEdit)> GetSourcePermAsync(string type)
    {
        if (!ModuleByType.TryGetValue(type, out var module)) return (false, false);
        var uid = GetCurrentUserId();
        var view = await permSvc.HasPermissionAsync(uid, module, "view");
        if (!view) return (false, false);
        var edit = await permSvc.HasPermissionAsync(uid, module, "edit");
        return (true, edit);
    }

    private static IActionResult Forbidden(string msg = "Bạn không có quyền trên nguồn này.")
        => new ObjectResult(ApiResponse<object>.Fail(msg, 403)) { StatusCode = 403 };

    private static IActionResult ConflictResult(string msg)
        => new ObjectResult(ApiResponse<object>.Fail(msg, 409)) { StatusCode = 409 };

    // ── Helper: trạng thái nguồn (tồn tại/còn chờ/tenant) — dùng cho claim/assignment/decision ─────────
    private async Task<(bool Exists, bool Pending, long? TenantId)> GetSourceStatusAsync(string type, Guid publicId)
    {
        switch (type)
        {
            case "room-booking":
            {
                var b = await db.RoomBookings.Where(x => x.PublicId == publicId && x.IsDelete != 2)
                    .Select(x => new { x.Status, x.TenantId }).FirstOrDefaultAsync();
                return b == null ? (false, false, null) : (true, b.Status == 1, b.TenantId);
            }
            case "submission":
            {
                var s = await db.DocumentSubmissions.Where(x => x.PublicId == publicId && x.IsDelete != 2)
                    .Select(x => new { x.Status, x.TenantId }).FirstOrDefaultAsync();
                return s == null ? (false, false, null) : (true, s.Status == "Chờ duyệt", s.TenantId);
            }
            case "review":
            {
                var r = await db.EbookReviews.Where(x => x.PublicId == publicId && x.IsDelete != 2)
                    .Select(x => new { x.Status, x.TenantId }).FirstOrDefaultAsync();
                return r == null ? (false, false, null) : (true, r.Status == 1, r.TenantId);
            }
            default: return (false, false, null);
        }
    }

    private static string RoomBookingStatusLabel(int s) => s switch
    {
        1 => "Chờ duyệt", 2 => "Đã duyệt", 3 => "Đã check-in", 4 => "Hoàn tất",
        5 => "Đã hủy", 6 => "Từ chối", 7 => "Không tới", _ => "?"
    };

    private static string ReviewStatusLabel(int? s) => s switch { 1 => "Chờ duyệt", 2 => "Đã duyệt", _ => "?" };

    // ── Xây dòng hiển thị theo từng nguồn ────────────────────────────────────
    private async Task<List<WorkItemRow>> BuildRoomBookingRowsAsync(long? tenantId, Guid? onlyPublicId, bool pendingOnly)
    {
        var q = db.RoomBookings.Where(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId));
        if (pendingOnly) q = q.Where(x => x.Status == 1);
        if (onlyPublicId.HasValue) q = q.Where(x => x.PublicId == onlyPublicId.Value);
        var bookings = await q.ToListAsync();
        if (bookings.Count == 0) return [];

        var roomIds = bookings.Select(b => b.MapObjectId).Distinct().ToList();
        var readerIds = bookings.Select(b => b.ReaderId).Distinct().ToList();
        var rooms = await db.MapObjects.Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "");
        var readers = await db.Readers.Where(r => readerIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        return bookings.Select(b =>
        {
            readers.TryGetValue(b.ReaderId, out var reader);
            rooms.TryGetValue(b.MapObjectId, out var roomName);
            var readerName = reader == null ? "" : $"{reader.LastName} {reader.FirstName}".Trim();
            return new WorkItemRow
            {
                Type = "room-booking", PublicId = b.PublicId,
                Title = $"Phòng {roomName} · {b.StartAt:dd/MM HH:mm}–{b.EndAt:HH:mm}",
                Detail = b.Note, SubmittedByName = readerName, SubmittedByCardNo = reader?.Cardno,
                SubmittedAt = b.CreatedRowDate, StatusLabel = RoomBookingStatusLabel(b.Status),
                RoomStartAt = b.StartAt, RoomEndAt = b.EndAt,
            };
        }).ToList();
    }

    private async Task<List<WorkItemRow>> BuildSubmissionRowsAsync(long? tenantId, Guid? onlyPublicId, bool pendingOnly)
    {
        var q = db.DocumentSubmissions.Where(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId));
        if (pendingOnly) q = q.Where(x => x.Status == "Chờ duyệt");
        if (onlyPublicId.HasValue) q = q.Where(x => x.PublicId == onlyPublicId.Value);
        var subs = await q.ToListAsync();
        if (subs.Count == 0) return [];

        var readerIds = subs.Select(s => s.ReaderId).Distinct().ToList();
        var readers = await db.Readers.Where(r => readerIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        return subs.Select(s =>
        {
            readers.TryGetValue(s.ReaderId, out var reader);
            var readerName = reader == null ? "" : $"{reader.LastName} {reader.FirstName}".Trim();
            return new WorkItemRow
            {
                Type = "submission", PublicId = s.PublicId, Title = s.Title ?? "", Detail = s.Abstract,
                SubmittedByName = readerName, SubmittedByCardNo = reader?.Cardno,
                SubmittedAt = s.CreatedDate, StatusLabel = s.Status ?? "",
                DocType = s.DocType, FileName = s.FileName,
            };
        }).ToList();
    }

    private async Task<List<WorkItemRow>> BuildReviewRowsAsync(Guid? onlyPublicId, bool pendingOnly)
    {
        // Title tài liệu ebook nằm ở ItemXml (biên mục MARC), đã có sẵn logic ghép trong
        // IEbookReviewRepository.SearchAllWithTitleAsync (dùng cho trang admin/ebook-review) — tái dùng
        // thay vì tự parse XML. Repo tự lọc TenantId theo ambient context, đúng quy ước toàn dự án.
        var req = new EbookReviewSearchRequest { Status = pendingOnly ? 1 : null, PageSize = 2000, PageIndex = 1 };
        var reviews = await reviewRepo.SearchAllWithTitleAsync(req);
        if (onlyPublicId.HasValue) reviews = reviews.Where(r => r.PublicId == onlyPublicId.Value).ToList();

        return reviews.Select(r => new WorkItemRow
        {
            Type = "review", PublicId = r.PublicId, Title = r.ItemTitle ?? "", Detail = r.Content,
            SubmittedByName = r.DisplayName, SubmittedAt = r.CreatedRowDate,
            StatusLabel = ReviewStatusLabel(r.Status), Rating = r.Rating,
        }).ToList();
    }

    private Task<List<WorkItemRow>> BuildRowsAsync(string type, long? tenantId, Guid? onlyPublicId, bool pendingOnly) => type switch
    {
        "room-booking" => BuildRoomBookingRowsAsync(tenantId, onlyPublicId, pendingOnly),
        "submission"   => BuildSubmissionRowsAsync(tenantId, onlyPublicId, pendingOnly),
        "review"       => BuildReviewRowsAsync(onlyPublicId, pendingOnly),
        _              => Task.FromResult(new List<WorkItemRow>()),
    };

    // ── Helper: tên/trạng thái quyền của người được giao (cho cờ "mất quyền xử lý") ─────────────────────
    private async Task<Dictionary<long, (string Name, bool Lost)>> ResolveAssigneesAsync(string type, IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        var result = new Dictionary<long, (string, bool)>();
        if (idList.Count == 0) return result;

        var moduleCode = ModuleByType.GetValueOrDefault(type, "");
        var moduleId = await db.Modules.Where(m => m.ModuleCode == moduleCode && m.IsDelete != 2)
            .Select(m => (long?)m.Id).FirstOrDefaultAsync();
        var users = await db.Users.Where(u => idList.Contains(u.Id)).ToListAsync();
        var editableIds = moduleId == null
            ? []
            : (await db.Permissions.Where(p => p.ModuleId == moduleId && p.UserId != null && idList.Contains(p.UserId.Value)
                    && p.Can_Edit == 2 && p.IsDelete != 2)
                .Select(p => p.UserId!.Value).ToListAsync()).ToHashSet();

        foreach (var id in idList)
        {
            var u = users.FirstOrDefault(x => x.Id == id);
            var name = u?.FullName ?? u?.LoginName ?? $"#{id}";
            var lost = u == null || u.IsDelete == 2 || u.Status != 2 || !editableIds.Contains(id);
            result[id] = (name, lost);
        }
        return result;
    }

    private async Task WriteWorkLogAsync(string actionType, string action)
    {
        try
        {
            db.UserLogs.Add(new UserLog
            {
                UserId = GetCurrentUserId(), ActionType = actionType, Object = "AdminWorkAssignment",
                Action = action, Submited = DateTime.Now,
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Application = "ELIBAPI", TenantId = GetTenantId(),
            });
            await db.SaveChangesAsync();
        }
        catch { /* không để lỗi UserLog làm hỏng luồng chính */ }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // API
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        var result = new List<WorkSourceInfo>();
        foreach (var t in ValidTypes)
        {
            var (view, edit) = await GetSourcePermAsync(t);
            result.Add(new WorkSourceInfo { Type = t, Label = LabelByType[t], CanView = view, CanEdit = edit });
        }
        return Ok(ApiResponse<List<WorkSourceInfo>>.Ok(result));
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(string type, string view = "all", int page = 1, int pageSize = 20)
    {
        if (!ValidTypes.Contains(type)) return BadRequest(ApiResponse<object>.Fail("Loại nguồn không hợp lệ."));
        var (canView, _) = await GetSourcePermAsync(type);
        if (!canView) return Forbidden();

        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        page = page < 1 ? 1 : page;
        var tenantId = GetTenantId();
        var uid = GetCurrentUserId();
        var now = DateTime.UtcNow;

        var rows = await BuildRowsAsync(type, tenantId, null, pendingOnly: true);
        var ids = rows.Select(r => r.PublicId).ToList();
        var assignments = ids.Count == 0
            ? []
            : await db.AdminWorkAssignments.Where(x => x.SourceType == type && ids.Contains(x.SourcePublicId)).ToListAsync();
        var byId = assignments.ToDictionary(a => a.SourcePublicId);

        foreach (var row in rows)
        {
            if (byId.TryGetValue(row.PublicId, out var a))
            {
                row.AssigneeId = a.AssigneeId;
                row.DueAtUtc = a.DueAtUtc;
                row.Version = a.Version;
                row.Overdue = a.DueAtUtc.HasValue && a.DueAtUtc.Value < now;
            }
        }

        var response = new WorkItemsResponse
        {
            TotalCount = rows.Count,
            MineCount = rows.Count(r => r.AssigneeId == uid),
            UnassignedCount = rows.Count(r => r.AssigneeId == null),
            OverdueCount = rows.Count(r => r.Overdue),
            CheckedAtUtc = now,
        };

        IEnumerable<WorkItemRow> filtered = view switch
        {
            "mine"       => rows.Where(r => r.AssigneeId == uid),
            "unassigned" => rows.Where(r => r.AssigneeId == null),
            "overdue"    => rows.Where(r => r.Overdue),
            _            => rows,
        };
        var page1 = filtered.OrderByDescending(r => r.SubmittedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToList();

        var assigneeIds = page1.Where(r => r.AssigneeId.HasValue).Select(r => r.AssigneeId!.Value).Distinct().ToList();
        var resolved = await ResolveAssigneesAsync(type, assigneeIds);
        foreach (var row in page1)
        {
            if (row.AssigneeId.HasValue && resolved.TryGetValue(row.AssigneeId.Value, out var info))
            {
                row.AssigneeName = info.Name;
                row.AssigneePermissionLost = info.Lost;
            }
        }
        response.Items = page1;
        return Ok(ApiResponse<WorkItemsResponse>.Ok(response));
    }

    [HttpGet("{type}/assignees")]
    public async Task<IActionResult> GetAssignees(string type, string? keyword, long after = 0)
    {
        if (!ValidTypes.Contains(type)) return BadRequest(ApiResponse<object>.Fail("Loại nguồn không hợp lệ."));
        var (_, canEdit) = await GetSourcePermAsync(type);
        if (!canEdit) return Forbidden();

        var moduleCode = ModuleByType[type];
        var tenantId = GetTenantId();
        var moduleId = await db.Modules.Where(m => m.ModuleCode == moduleCode && m.IsDelete != 2)
            .Select(m => (long?)m.Id).FirstOrDefaultAsync();
        if (moduleId == null) return Ok(ApiResponse<object>.Ok(new { items = new List<WorkItemAssigneeOption>(), next = (long?)null }));

        var query =
            from u in db.Users
            join p in db.Permissions on u.Id equals p.UserId
            where p.ModuleId == moduleId && p.Can_Edit == 2 && p.IsDelete != 2
               && u.IsDelete != 2 && u.Status == 2 && u.Id > after
               && (tenantId == null || u.TenantId == tenantId)
            select u;

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(u => (u.FullName != null && u.FullName.Contains(keyword))
                                   || (u.LoginName != null && u.LoginName.Contains(keyword)));

        var users = await query.OrderBy(u => u.Id).Take(100).ToListAsync();
        var options = users.Select(u => new WorkItemAssigneeOption { Id = u.Id, Name = u.FullName ?? u.LoginName ?? $"#{u.Id}" }).ToList();
        var next = users.Count == 100 ? users[^1].Id : (long?)null;
        return Ok(ApiResponse<object>.Ok(new { items = options, next }));
    }

    [HttpGet("{type}/{publicId:guid}")]
    public async Task<IActionResult> GetDetail(string type, Guid publicId)
    {
        if (!ValidTypes.Contains(type)) return BadRequest(ApiResponse<object>.Fail("Loại nguồn không hợp lệ."));
        var (canView, _) = await GetSourcePermAsync(type);
        if (!canView) return Forbidden();

        var rows = await BuildRowsAsync(type, GetTenantId(), publicId, pendingOnly: false);
        var row = rows.FirstOrDefault();
        if (row == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));

        var a = await db.AdminWorkAssignments.FirstOrDefaultAsync(x => x.SourceType == type && x.SourcePublicId == publicId);
        if (a != null)
        {
            row.AssigneeId = a.AssigneeId; row.DueAtUtc = a.DueAtUtc; row.Version = a.Version;
            row.Overdue = a.DueAtUtc.HasValue && a.DueAtUtc.Value < DateTime.UtcNow;
        }
        if (row.AssigneeId.HasValue)
        {
            var resolved = await ResolveAssigneesAsync(type, [row.AssigneeId.Value]);
            if (resolved.TryGetValue(row.AssigneeId.Value, out var info)) { row.AssigneeName = info.Name; row.AssigneePermissionLost = info.Lost; }
        }
        return Ok(ApiResponse<WorkItemRow>.Ok(row));
    }

    [HttpGet("submission/{publicId:guid}/file")]
    public async Task<IActionResult> DownloadSubmissionFile(Guid publicId)
    {
        var (canView, _) = await GetSourcePermAsync("submission");
        if (!canView) return Forbidden();

        var tenantId = GetTenantId();
        var sub = await db.DocumentSubmissions.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
            && (tenantId == null || x.TenantId == tenantId));
        if (sub == null || string.IsNullOrEmpty(sub.FileUrl))
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tệp.", 404));

        var (stream, contentType) = await minio.GetObjectStreamAsync(sub.FileUrl);
        return File(stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sub.FileName ?? "document");
    }

    [HttpPost("{type}/{publicId:guid}/claim")]
    public async Task<IActionResult> Claim(string type, Guid publicId, [FromBody] WorkItemClaimRequest? _)
    {
        if (!ValidTypes.Contains(type)) return BadRequest(ApiResponse<object>.Fail("Loại nguồn không hợp lệ."));
        var (_, canEdit) = await GetSourcePermAsync(type);
        if (!canEdit) return Forbidden();

        var (exists, pending, sourceTenantId) = await GetSourceStatusAsync(type, publicId);
        if (!exists) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));
        var actingTenantId = GetTenantId();
        if (actingTenantId != null && sourceTenantId != actingTenantId) return Forbidden();
        if (!pending) return ConflictResult("Hồ sơ đã được xử lý hoặc đóng, vui lòng tải lại.");

        var uid = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var existing = await db.AdminWorkAssignments.FirstOrDefaultAsync(x => x.SourceType == type && x.SourcePublicId == publicId);

        if (existing == null)
        {
            db.AdminWorkAssignments.Add(new AdminWorkAssignment
            {
                SourceType = type, SourcePublicId = publicId, AssigneeId = uid, DueAtUtc = null,
                Version = 1, UpdatedBy = uid, UpdatedAtUtc = now, TenantId = sourceTenantId,
            });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return ConflictResult("Đã có người nhận việc này, vui lòng tải lại."); }
        }
        else
        {
            if (existing.AssigneeId != null) return ConflictResult("Đã có người nhận việc này, vui lòng tải lại.");
            var affected = await db.AdminWorkAssignments
                .Where(x => x.SourceType == type && x.SourcePublicId == publicId && x.AssigneeId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AssigneeId, uid)
                    .SetProperty(x => x.UpdatedBy, uid)
                    .SetProperty(x => x.UpdatedAtUtc, now)
                    .SetProperty(x => x.Version, existing.Version + 1));
            if (affected == 0) return ConflictResult("Đã có người nhận việc này, vui lòng tải lại.");
        }

        await WriteWorkLogAsync("Claim", $"Nhận việc {LabelByType[type]} #{publicId}");
        return Ok(ApiResponse<object>.Ok(null!, "Đã nhận việc."));
    }

    [HttpPut("{type}/{publicId:guid}/assignment")]
    public async Task<IActionResult> SetAssignment(string type, Guid publicId, [FromBody] WorkItemAssignmentRequest request)
    {
        if (!ValidTypes.Contains(type)) return BadRequest(ApiResponse<object>.Fail("Loại nguồn không hợp lệ."));
        var (_, canEdit) = await GetSourcePermAsync(type);
        if (!canEdit) return Forbidden();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập lý do (tối đa 500 ký tự)."));

        var (exists, pending, sourceTenantId) = await GetSourceStatusAsync(type, publicId);
        if (!exists) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));
        var actingTenantId = GetTenantId();
        if (actingTenantId != null && sourceTenantId != actingTenantId) return Forbidden();
        if (!pending) return ConflictResult("Hồ sơ đã được xử lý hoặc đóng, vui lòng tải lại.");

        if (request.AssigneeId.HasValue)
        {
            var resolved = await ResolveAssigneesAsync(type, [request.AssigneeId.Value]);
            if (!resolved.TryGetValue(request.AssigneeId.Value, out var info) || info.Lost)
                return BadRequest(ApiResponse<object>.Fail("Cán bộ được chọn không còn quyền xử lý nguồn này."));
        }

        var uid = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var existing = await db.AdminWorkAssignments.FirstOrDefaultAsync(x => x.SourceType == type && x.SourcePublicId == publicId);
        var currentVersion = existing?.Version ?? 0;
        if (request.Version != currentVersion) return ConflictResult("Đã bị người khác cập nhật, vui lòng tải lại.");

        var oldAssignee = existing?.AssigneeId;
        var oldDue = existing?.DueAtUtc;

        if (existing == null)
        {
            db.AdminWorkAssignments.Add(new AdminWorkAssignment
            {
                SourceType = type, SourcePublicId = publicId, AssigneeId = request.AssigneeId,
                DueAtUtc = request.DueAtUtc, Version = 1, UpdatedBy = uid, UpdatedAtUtc = now, TenantId = sourceTenantId,
            });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return ConflictResult("Đã bị người khác cập nhật, vui lòng tải lại."); }
        }
        else
        {
            var affected = await db.AdminWorkAssignments
                .Where(x => x.SourceType == type && x.SourcePublicId == publicId && x.Version == request.Version)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AssigneeId, request.AssigneeId)
                    .SetProperty(x => x.DueAtUtc, request.DueAtUtc)
                    .SetProperty(x => x.UpdatedBy, uid)
                    .SetProperty(x => x.UpdatedAtUtc, now)
                    .SetProperty(x => x.Version, request.Version + 1));
            if (affected == 0) return ConflictResult("Đã bị người khác cập nhật, vui lòng tải lại.");
        }

        await WriteWorkLogAsync("Assign",
            $"Phân công {LabelByType[type]} #{publicId}: người xử lý {oldAssignee?.ToString() ?? "chưa có"} → " +
            $"{request.AssigneeId?.ToString() ?? "chưa phân công"}, hạn {oldDue?.ToString("dd/MM/yyyy HH:mm") ?? "chưa đặt"} → " +
            $"{request.DueAtUtc?.ToString("dd/MM/yyyy HH:mm") ?? "chưa đặt"}. Lý do: {request.Reason}");
        return Ok(ApiResponse<object>.Ok(null!, "Đã lưu phân công."));
    }

    [HttpPost("{type}/{publicId:guid}/decision")]
    public async Task<IActionResult> Decide(string type, Guid publicId, [FromBody] WorkItemDecisionRequest request)
    {
        if (type != "submission" && type != "review")
            return BadRequest(ApiResponse<object>.Fail("Đặt phòng xử lý qua RoomBookingAdmin/Approve hoặc Reject."));
        var (_, canEdit) = await GetSourcePermAsync(type);
        if (!canEdit) return Forbidden();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập lý do (tối đa 500 ký tự)."));

        var tenantId = GetTenantId();
        var uid = GetCurrentUserId();

        if (type == "submission")
        {
            var sub = await db.DocumentSubmissions.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
                && (tenantId == null || x.TenantId == tenantId));
            if (sub == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));
            if (sub.Status != "Chờ duyệt") return ConflictResult("Hồ sơ đã được xử lý trước đó, vui lòng tải lại.");

            var oldStatus = sub.Status;
            sub.Status = request.Approve ? "Đã duyệt" : "Từ chối";
            await db.SaveChangesAsync();
            await WriteWorkLogAsync("Decision",
                $"{(request.Approve ? "Duyệt" : "Từ chối")} tài liệu nộp #{sub.Id} \"{sub.Title}\": {oldStatus} → {sub.Status}. Lý do: {request.Reason}");
            return Ok(ApiResponse<object>.Ok(null!, "Đã lưu quyết định."));
        }
        else // review
        {
            if (!request.Approve)
                return BadRequest(ApiResponse<object>.Fail("Đánh giá ở bản này chỉ hỗ trợ duyệt — từ chối/xóa vẫn ở trang Quản lý bình luận."));

            var review = await db.EbookReviews.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
                && (tenantId == null || x.TenantId == tenantId));
            if (review == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ.", 404));
            if (review.Status != 1) return ConflictResult("Hồ sơ đã được xử lý trước đó, vui lòng tải lại.");

            review.Status = 2;
            review.UpdateRowBy = uid;
            review.UpdatedRowDate = DateTime.Now;
            await db.SaveChangesAsync();
            await WriteWorkLogAsync("Decision", $"Duyệt đánh giá #{review.Id}: Chờ duyệt → Đã duyệt. Lý do: {request.Reason}");
            return Ok(ApiResponse<object>.Ok(null!, "Đã lưu quyết định."));
        }
    }
}
