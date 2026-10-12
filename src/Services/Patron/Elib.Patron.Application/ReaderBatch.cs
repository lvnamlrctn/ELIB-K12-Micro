using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>
/// Bạn đọc được chọn cho thao tác hàng loạt: danh sách đã tích (≤ 2.000) HOẶC toàn bộ kết quả của bộ lọc — khi đó phải kèm
/// <see cref="Token"/> lấy từ bước xem trước (ELIB-LRC: ReaderMutationGuard/previewToken), để người dùng đã thấy số bạn đọc bị ảnh hưởng.
/// </summary>
public sealed record ReaderSelection(IReadOnlyList<Guid>? PublicIds = null, ReaderSearch? Search = null, string? Token = null);

/// <summary>Kết quả xem trước theo bộ lọc: số bạn đọc khớp và mã xác nhận (hết hạn sau 15 phút, gắn với đúng bộ lọc này).</summary>
public sealed record ReaderSelectionPreview(int Count, string Token, DateTimeOffset ExpiresAt);

/// <summary>Sửa hàng loạt (ELIB-LRC: BatchUpdate) — chỉ trường được gửi; Status 1/2 = khoá/mở.</summary>
public sealed record ReaderBatchUpdateRequest(
    ReaderSelection Selection, long? ReaderTypeId = null, long? ClassId = null, long? CourseId = null, long? OrgId = null,
    DateOnly? IssueDate = null, DateOnly? ExpireDate = null, int? Status = null);

/// <summary>Thêm vào / bỏ khỏi một nhóm (ELIB-LRC: ReaderInGroup).</summary>
public sealed record ReaderGroupsRequest(ReaderSelection Selection, long GroupId, bool Add);

/// <summary>Đặt đúng các nhóm của một bạn đọc (form Thêm/Sửa).</summary>
public sealed record ReaderGroupSetRequest(IReadOnlyList<long> GroupIds);

public sealed record ReaderBulkDeleteRequest(IReadOnlyList<Guid> PublicIds);

public sealed record ReaderBatchResult(int Count);

public sealed partial class ReaderResource
{
    /// <summary>Theo bộ lọc: tối đa 10.000 bạn đọc mỗi lần (ghi theo lô 500, mỗi lô một transaction).</summary>
    public const int MaxBatch = 10_000;

    private const int BatchChunk = 500;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly byte[] TokenKey = "elib-patron-reader-selection-v1"u8.ToArray();
    private static readonly JsonSerializerOptions SearchJson = new(JsonSerializerDefaults.Web);

    // ── Xem trước theo bộ lọc ──

    public async Task<ReaderSelectionPreview> PreviewSelectionAsync(ReaderSearch search, CancellationToken ct)
    {
        var count = await Query(search).CountAsync(ct);
        var expires = clock.GetUtcNow() + TokenLifetime;
        return new ReaderSelectionPreview(count, $"{expires.ToUnixTimeSeconds()}.{Sign(search, expires.ToUnixTimeSeconds())}", expires);
    }

    /// <summary>
    /// Mã xác nhận = hạn + HMAC(đơn vị, người dùng, bộ lọc, hạn). Không phải ranh giới quyền (người gọi đã có quyền sửa) — chỉ chặn
    /// sửa nhầm khi bộ lọc trên màn hình đã đổi so với lúc xem trước.
    /// </summary>
    private string Sign(ReaderSearch search, long expires)
    {
        var node = JsonSerializer.SerializeToNode(search, SearchJson)!.AsObject();
        node.Remove("pageIndex");
        node.Remove("pageSize");
        var payload = $"{tenant.RequireTenantId()}|{actor.Id}|{expires}|{node.ToJsonString(SearchJson)}";
        return Convert.ToHexString(HMACSHA256.HashData(TokenKey, Encoding.UTF8.GetBytes(payload)));
    }

    private void VerifyToken(ReaderSearch search, string? token)
    {
        var parts = (token ?? "").Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], out var expires))
            throw new BusinessRuleException("READER_BATCH_TOKEN_REQUIRED", "Xem trước số bạn đọc bị ảnh hưởng trước khi áp dụng theo bộ lọc.");
        if (DateTimeOffset.FromUnixTimeSeconds(expires) < clock.GetUtcNow())
            throw new BusinessRuleException("READER_BATCH_TOKEN_EXPIRED", "Bản xem trước đã quá 15 phút — xem trước lại.");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(search, expires)), Encoding.ASCII.GetBytes(parts[1])))
            throw new BusinessRuleException("READER_BATCH_TOKEN_MISMATCH", "Bộ lọc đã đổi so với lúc xem trước — xem trước lại.");
    }

    /// <summary>Id nội bộ của bạn đọc được chọn.</summary>
    private async Task<List<long>> SelectIdsAsync(ReaderSelection selection, CancellationToken ct)
    {
        if (selection.PublicIds is { Count: > 0 } publicIds)
        {
            var ids = publicIds.Distinct().ToList();
            if (ids.Count > MaxBulk) throw new BusinessRuleException("READER_BULK_SIZE", $"Chọn từ 1 đến {MaxBulk} bạn đọc mỗi lần.");
            return await Set.Where(x => ids.Contains(x.PublicId)).Select(x => x.Id).ToListAsync(ct);
        }
        if (selection.Search is not { } search)
            throw new BusinessRuleException("READER_BULK_SIZE", $"Chọn từ 1 đến {MaxBulk} bạn đọc mỗi lần.");
        VerifyToken(search, selection.Token);
        var found = await Query(search).Select(x => x.Id).Take(MaxBatch + 1).ToListAsync(ct);
        if (found.Count > MaxBatch)
            throw new BusinessRuleException("READER_BATCH_TOO_MANY", $"Bộ lọc khớp hơn {MaxBatch:N0} bạn đọc — thu hẹp bộ lọc rồi làm từng phần.");
        return found;
    }

    // ── Sửa hàng loạt ──

    public async Task<ReaderBatchResult> BatchUpdateAsync(ReaderBatchUpdateRequest r, CancellationToken ct)
    {
        if (r is { ReaderTypeId: null, ClassId: null, CourseId: null, OrgId: null, IssueDate: null, ExpireDate: null, Status: null })
            throw new BusinessRuleException("READER_BULK_EMPTY", "Chưa chọn trường cần sửa.");
        await EnsureExistsAsync<ReaderType>(r.ReaderTypeId, "Loại bạn đọc", ct);
        await EnsureExistsAsync<SchoolClass>(r.ClassId, "Lớp", ct);
        await EnsureExistsAsync<Course>(r.CourseId, "Khoá", ct);
        if (r.OrgId is { } orgId) (await ReferencesAsync(ReferenceKinds.Orgs, ct)).Resolve(orgId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Đơn vị");
        var ids = await SelectIdsAsync(r.Selection, ct);
        return new ReaderBatchResult(await ForEachChunkAsync(ids, reader =>
        {
            reader.ApplyBulk(r.ReaderTypeId, r.ClassId, r.CourseId, r.IssueDate, r.ExpireDate, r.OrgId);
            if (r.Status is { } status && status != reader.Status) reader.ChangeStatus(status);
        }, CrudChange.Updated, ct));
    }

    /// <summary>Ghi theo lô: mỗi bạn đọc một event + một dòng nhật ký có diff; mỗi lô một SaveChanges rồi xả bộ theo dõi.</summary>
    private async Task<int> ForEachChunkAsync(List<long> ids, Action<Reader> change, CrudChange kind, CancellationToken ct)
    {
        var count = 0;
        foreach (var chunk in ids.Chunk(BatchChunk))
        {
            foreach (var reader in await Set.Where(x => chunk.Contains(x.Id)).ToListAsync(ct))
            {
                var before = ToDto(reader);
                change(reader);
                await PublishAsync(reader, deleted: kind == CrudChange.Deleted, ct);
                await AuditAsync(reader, kind, ct, kind == CrudChange.Deleted ? null : Diff(before, reader));
                count++;
            }
            await Db.SaveChangesAsync(ct);
            ((DbContext)Db).ChangeTracker.Clear();
        }
        return count;
    }

    // ── Xoá nhiều ──

    public async Task<ReaderBatchResult> BulkDeleteAsync(ReaderBulkDeleteRequest request, CancellationToken ct)
    {
        var ids = await SelectIdsAsync(new ReaderSelection(request.PublicIds), ct);
        if (ids.Count == 0) throw new BusinessRuleException("READER_BULK_SIZE", $"Chọn từ 1 đến {MaxBulk} bạn đọc mỗi lần.");
        return new ReaderBatchResult(await ForEachChunkAsync(ids, reader => Set.Remove(reader), CrudChange.Deleted, ct)); // interceptor: xoá mềm
    }

    // ── Nhóm ──

    public async Task<ReaderBatchResult> ChangeGroupsAsync(ReaderGroupsRequest request, CancellationToken ct)
    {
        var group = await Db.Set<ReaderGroup>().FirstOrDefaultAsync(g => g.Id == request.GroupId, ct)
            ?? throw new NotFoundException("Nhóm bạn đọc", request.GroupId);
        var ids = await SelectIdsAsync(request.Selection, ct);
        var members = Db.Set<ReaderGroupMember>();
        var count = 0;
        foreach (var chunk in ids.Chunk(BatchChunk))
        {
            var existing = await members.Where(m => m.GroupId == group.Id && chunk.Contains(m.ReaderId)).ToListAsync(ct);
            if (request.Add)
            {
                var have = existing.Select(m => m.ReaderId).ToHashSet();
                var added = chunk.Where(id => !have.Contains(id)).Select(id => ReaderGroupMember.Create(id, group.Id)).ToList();
                members.AddRange(added);
                count += added.Count;
            }
            else
            {
                members.RemoveRange(existing);
                count += existing.Count;
            }
            await Db.SaveChangesAsync(ct);
        }
        if (AuditSink is not null && count > 0)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(ReaderGroup), "Nhóm bạn đọc", group.PublicId, CrudChange.Updated,
                $"{group.Name}: {(request.Add ? "thêm" : "bỏ")} {count} bạn đọc"), ct);
        await Db.SaveChangesAsync(ct);
        return new ReaderBatchResult(count);
    }

    public async Task<IReadOnlyList<long>> GroupsOfAsync(Guid publicId, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        return await Db.Set<ReaderGroupMember>().AsNoTracking().Where(m => m.ReaderId == reader.Id).Select(m => m.GroupId).OrderBy(id => id)
            .ToListAsync(ct);
    }

    /// <summary>Đặt đúng các nhóm của một bạn đọc; ghi nhật ký của bạn đọc với diff "groupIds".</summary>
    public async Task<IReadOnlyList<long>> SetGroupsAsync(Guid publicId, ReaderGroupSetRequest request, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        var wanted = (request.GroupIds ?? []).Distinct().ToList();
        if (wanted.Count > 100) throw new BusinessRuleException("READER_GROUPS_TOO_MANY", "Một bạn đọc thuộc tối đa 100 nhóm.");
        var valid = await Db.Set<ReaderGroup>().Where(g => wanted.Contains(g.Id)).Select(g => g.Id).ToListAsync(ct);
        if (valid.Count != wanted.Count) throw new NotFoundException("Nhóm bạn đọc", wanted.Except(valid).First());
        var members = Db.Set<ReaderGroupMember>();
        var current = await members.Where(m => m.ReaderId == reader.Id).ToListAsync(ct);
        var before = current.Select(m => m.GroupId).Order().ToList();
        members.RemoveRange(current.Where(m => !wanted.Contains(m.GroupId)));
        members.AddRange(wanted.Except(before).Select(id => ReaderGroupMember.Create(reader.Id, id)));
        var after = wanted.Order().ToList();
        if (!before.SequenceEqual(after))
            await AuditAsync(reader, CrudChange.Updated, ct,
                new JsonObject { ["groupIds"] = new JsonArray(JsonSerializer.SerializeToNode(before), JsonSerializer.SerializeToNode(after)) });
        await Db.SaveChangesAsync(ct);
        return after;
    }
}
