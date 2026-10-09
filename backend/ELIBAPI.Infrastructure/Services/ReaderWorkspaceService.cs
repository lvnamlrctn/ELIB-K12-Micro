using System.Text;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Không gian nghiên cứu + Tìm kiếm đã lưu của bạn đọc (Đợt 9, port từ ELIB-LRC
/// ReaderWorkspaceService). Gộp 2 tính năng trong 1 service như bản gốc — cùng domain "công cụ bạn đọc",
/// cùng job nền kiểm tra định kỳ.</summary>
public class ReaderWorkspaceService(ELIBAPIDbContext db, IElasticsearchService elastic, ILogger<ReaderWorkspaceService> logger)
{
    // ── Không gian nghiên cứu (ReaderWorkspace) ─────────────────────────────────

    public async Task<object> GetWorkspaceAsync(long readerId)
    {
        var row = await db.ReaderWorkspaces.FirstOrDefaultAsync(x => x.ReaderId == readerId);
        return new
        {
            version = row?.Version ?? Guid.Empty,
            updatedAt = row?.UpdatedAt,
            snapshot = JsonSerializer.Deserialize<JsonElement>(row?.SnapshotJson ?? "{\"projects\":[],\"highlights\":[]}")
        };
    }

    /// <summary>null = xung đột phiên bản (409) hoặc dữ liệu không hợp lệ (400 — Error khác null).</summary>
    public async Task<(object? Accepted, string? Error)> SaveWorkspaceAsync(long readerId, long? tenantId, SaveWorkspaceRequest request)
    {
        var validationError = ValidateSnapshot(request.Projects, request.Highlights);
        if (validationError != null) return (null, validationError);

        var json = JsonSerializer.Serialize(new { projects = request.Projects, highlights = request.Highlights });
        if (Encoding.UTF8.GetByteCount(json) > 1_000_000)
            return (null, "Dữ liệu không gian nghiên cứu vượt quá giới hạn cho phép.");

        var version = Guid.NewGuid();
        var now = DateTime.UtcNow;

        if (request.Version == Guid.Empty)
        {
            if (await db.ReaderWorkspaces.AnyAsync(x => x.ReaderId == readerId))
                return (null, null); // đã có bản ghi — client phải GET bản mới trước, không phải tạo mới

            db.ReaderWorkspaces.Add(new ReaderWorkspace
            {
                ReaderId = readerId, Version = version, UpdatedAt = now, SnapshotJson = json, TenantId = tenantId
            });
            try
            {
                await db.SaveChangesAsync();
                return (new { version, updatedAt = now }, null);
            }
            catch (DbUpdateException)
            {
                return (null, null); // race hiếm gặp lúc tạo lần đầu — coi như xung đột
            }
        }

        var affected = await db.ReaderWorkspaces
            .Where(x => x.ReaderId == readerId && x.Version == request.Version)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SnapshotJson, json)
                .SetProperty(x => x.Version, version)
                .SetProperty(x => x.UpdatedAt, now));

        return affected == 1 ? (new { version, updatedAt = now }, null) : (null, null);
    }

    private static string? ValidateSnapshot(JsonElement projects, JsonElement highlights)
    {
        if (projects.ValueKind != JsonValueKind.Array) return "projects phải là mảng.";
        if (highlights.ValueKind != JsonValueKind.Array) return "highlights phải là mảng.";
        if (projects.GetArrayLength() > 500) return "Tối đa 500 đề tài nghiên cứu.";
        if (highlights.GetArrayLength() > 3000) return "Tối đa 3000 đoạn đánh dấu.";

        var seenProjectIds = new HashSet<string>();
        foreach (var p in projects.EnumerateArray())
        {
            foreach (var key in new[] { "id", "title", "description", "category", "createdAt", "color" })
                if (!p.TryGetProperty(key, out _)) return $"Đề tài thiếu trường '{key}'.";
            var id = p.GetProperty("id").GetString() ?? "";
            if (id.Length == 0 || id.Length > 120) return "id đề tài không hợp lệ.";
            if (!seenProjectIds.Add(id)) return "id đề tài bị trùng.";
            foreach (var key in new[] { "title", "description", "category", "createdAt", "color" })
                if ((p.GetProperty(key).GetString() ?? "").Length > 20000) return $"Trường '{key}' vượt quá giới hạn ký tự.";
            if (p.TryGetProperty("documentIds", out var docIds) && docIds.ValueKind == JsonValueKind.Array && docIds.GetArrayLength() > 1000)
                return "Tối đa 1000 tài liệu mỗi đề tài.";
        }

        var seenHighlightIds = new HashSet<string>();
        foreach (var h in highlights.EnumerateArray())
        {
            foreach (var key in new[] { "id", "documentId", "selectedText", "note", "createdAt", "color" })
                if (!h.TryGetProperty(key, out _)) return $"Đoạn đánh dấu thiếu trường '{key}'.";
            var id = h.GetProperty("id").GetString() ?? "";
            if (id.Length == 0 || id.Length > 120) return "id đoạn đánh dấu không hợp lệ.";
            if (!seenHighlightIds.Add(id)) return "id đoạn đánh dấu bị trùng.";
            foreach (var key in new[] { "selectedText", "note", "createdAt", "color" })
                if ((h.GetProperty(key).GetString() ?? "").Length > 20000) return $"Trường '{key}' vượt quá giới hạn ký tự.";
            if (h.TryGetProperty("pageIndex", out var pageIndex) &&
                (pageIndex.ValueKind != JsonValueKind.Number || !pageIndex.TryGetInt32(out var pi) || pi < 0))
                return "pageIndex không hợp lệ.";
        }

        return null;
    }

    // ── Tìm kiếm đã lưu (ReaderSavedSearch) ─────────────────────────────────────

    public async Task<List<object>> ListSearchesAsync(long readerId)
    {
        var rows = await db.ReaderSavedSearches
            .Where(x => x.ReaderId == readerId && x.IsDelete != 2)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<(Guid? Id, string? Error)> AddSearchAsync(long readerId, long? tenantId, SaveReaderSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            return (null, "Tên tìm kiếm không hợp lệ.");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var count = await db.ReaderSavedSearches.CountAsync(x => x.ReaderId == readerId && x.IsDelete != 2);
        if (count >= 20) { await tx.RollbackAsync(); return (null, "Bạn chỉ có thể lưu tối đa 20 tìm kiếm."); }

        var query = request.Query; query.Page = 1; query.PageSize = 50;
        var baseline = await elastic.SearchUnifiedAsync(query);

        var row = new ReaderSavedSearch
        {
            ReaderId = readerId,
            Name = request.Name.Trim(),
            RequestJson = JsonSerializer.Serialize(request.Query),
            AlertsEnabled = request.AlertsEnabled,
            KnownIdsJson = JsonSerializer.Serialize(baseline.Items.Select(x => x.GroupId).Distinct()),
            MatchesJson = "[]",
            HasUnread = false,
            LastCheckedAt = DateTime.UtcNow,
            Total = baseline.Total,
            Version = Guid.NewGuid(),
            IsDelete = 1,
            TenantId = tenantId,
            PublicId = Guid.NewGuid()
        };
        db.ReaderSavedSearches.Add(row);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (row.PublicId, null);
    }

    public async Task<bool> DeleteSearchAsync(long readerId, Guid searchId)
    {
        var row = await db.ReaderSavedSearches.FirstOrDefaultAsync(x => x.PublicId == searchId && x.ReaderId == readerId && x.IsDelete != 2);
        if (row == null) return false;
        row.IsDelete = 2;
        row.Version = Guid.NewGuid();
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleAlertsAsync(long readerId, Guid searchId, bool enabled)
    {
        var affected = await db.ReaderSavedSearches
            .Where(x => x.PublicId == searchId && x.ReaderId == readerId && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AlertsEnabled, enabled).SetProperty(x => x.Version, Guid.NewGuid()));
        return affected == 1;
    }

    public async Task<bool> MarkReadAsync(long readerId, Guid searchId)
    {
        var affected = await db.ReaderSavedSearches
            .Where(x => x.PublicId == searchId && x.ReaderId == readerId && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HasUnread, false).SetProperty(x => x.Version, Guid.NewGuid()));
        return affected == 1;
    }

    private static object ToDto(ReaderSavedSearch x) => new
    {
        id = x.PublicId,
        name = x.Name,
        query = JsonSerializer.Deserialize<JsonElement>(x.RequestJson),
        alertsEnabled = x.AlertsEnabled,
        hasUnread = x.HasUnread,
        total = x.Total,
        lastCheckedAt = x.LastCheckedAt,
        matches = JsonSerializer.Deserialize<JsonElement>(x.MatchesJson)
    };

    // ── Job nền: kiểm tra tìm kiếm đã lưu có kết quả mới (Hangfire "saved-search-alert-check") ───────

    public async Task CheckAlertsAsync()
    {
        var rows = await db.ReaderSavedSearches.Where(x => x.IsDelete != 2 && x.AlertsEnabled).ToListAsync();
        foreach (var row in rows)
        {
            try
            {
                var query = JsonSerializer.Deserialize<Core.DTOs.Request.UnifiedSearchRequest>(row.RequestJson) ?? new();
                query.Page = 1; query.PageSize = 50;
                var result = await elastic.SearchUnifiedAsync(query);

                var known = JsonSerializer.Deserialize<HashSet<string>>(row.KnownIdsJson) ?? [];
                var added = result.Items.Where(x => !known.Contains(x.GroupId)).ToList();
                if (added.Count == 0)
                {
                    row.LastCheckedAt = DateTime.UtcNow;
                    row.Total = result.Total;
                    await SaveRowIfVersionUnchangedAsync(row);
                    continue;
                }

                var existingMatches = JsonSerializer.Deserialize<List<SavedMatch>>(row.MatchesJson) ?? [];
                var merged = existingMatches
                    .Concat(added.Select(a => new SavedMatch(a.GroupId, a.Title ?? "", a.DocType)))
                    .GroupBy(m => (m.Id, m.Source)).Select(g => g.First())
                    .TakeLast(200).ToList();

                var knownIds = known.Concat(added.Select(a => a.GroupId)).Distinct().TakeLast(2000).ToList();

                row.MatchesJson = JsonSerializer.Serialize(merged);
                row.KnownIdsJson = JsonSerializer.Serialize(knownIds);
                row.HasUnread = true;
                row.LastCheckedAt = DateTime.UtcNow;
                row.Total = result.Total;
                await SaveRowIfVersionUnchangedAsync(row);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kiểm tra tìm kiếm đã lưu #{Id} thất bại — bỏ qua, thử lại lượt sau.", row.Id);
            }
        }
    }

    // Ghi có điều kiện Version — nếu bạn đọc vừa sửa/xoá/đánh dấu đã đọc entry này ở nơi khác giữa lúc
    // đọc và ghi của job, bỏ qua lượt ghi này (không ghi đè), để lượt chạy sau xử lý lại từ đầu.
    private async Task SaveRowIfVersionUnchangedAsync(ReaderSavedSearch row)
    {
        var newVersion = Guid.NewGuid();
        await db.ReaderSavedSearches
            .Where(x => x.Id == row.Id && x.Version == row.Version && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.MatchesJson, row.MatchesJson)
                .SetProperty(x => x.KnownIdsJson, row.KnownIdsJson)
                .SetProperty(x => x.HasUnread, row.HasUnread)
                .SetProperty(x => x.LastCheckedAt, row.LastCheckedAt)
                .SetProperty(x => x.Total, row.Total)
                .SetProperty(x => x.Version, newVersion));
    }

    private record SavedMatch(string Id, string Title, string Source);
}
