using System.Data;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Phần claim/chunk/worker của <see cref="AdminTaskService"/> — tách file cho dễ đọc, đúng cách
/// ELIB-LRC tổ chức (AdminTaskService.cs + AdminTaskChunks.cs cùng 1 partial class).</summary>
public partial class AdminTaskService
{
    /// <summary>Xử lý đúng 1 chunk của 1 tác vụ — gọi lặp lại bởi <see cref="AdminTaskWorker"/> (poll 3
    /// giây/lần). Claim bằng compare-and-swap trên (Attempts, State), tái khẳng định trong transaction
    /// Serializable — xung đột thật (2 worker cùng lỡ vượt qua bước claim đầu) do chính engine DB phát
    /// hiện khi commit, không dùng lock DB tường minh. 10 phút không cập nhật ("Running" treo do worker
    /// crash) sẽ tự bị claim lại. Trả về false nếu không có tác vụ nào cần xử lý.</summary>
    public async Task<bool> RunNext(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var candidate = await db.AdminTasks.AsNoTracking()
            .Where(x => x.State == "Queued" || (x.State == "Running" && x.UpdatedAt < cutoff))
            .OrderBy(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (candidate == null) return false;

        var attempt = candidate.Attempts + 1;
        var claimed = await db.AdminTasks
            .Where(x => x.Id == candidate.Id && x.Attempts == candidate.Attempts &&
                (x.State == "Queued" || (x.State == "Running" && x.UpdatedAt < cutoff)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, "Running")
                .SetProperty(x => x.Attempts, attempt)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        if (claimed == 0) return true; // đã bị worker khác claim trước — thử lại lượt sau

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var reaffirmed = await db.AdminTasks
                .Where(x => x.Id == candidate.Id && x.State == "Running" && x.Attempts == attempt)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Running"), ct);
            if (reaffirmed == 0) return true;

            db.ChangeTracker.Clear();
            var task = await db.AdminTasks.SingleAsync(x => x.Id == candidate.Id, ct);
            var request = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(task.Payload!), Json)!;
            await Authorize(task.ActorId, request);

            db.BackgroundActorId = task.ActorId;
            db.BackgroundTenantId = task.TenantId;
            db.AcceptedAdminTask = true;
            db.AdminTaskId = task.Id;

            if (task.StopRequested)
            {
                task.State = "Paused";
                task.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }

            if (task.TotalChunks == 0) await InitializeChunks(task, request, ct);

            var chunk = await db.AdminTaskChunks.SingleAsync(x => x.TaskId == task.Id && x.Position == task.CompletedChunks, ct);
            var chunkRequest = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(chunk.Payload!), Json)!;
            var output = await ExecuteChunk(task, chunkRequest, ct);

            chunk.Completed = true;
            chunk.Result = JsonSerializer.Serialize(output, Json);
            task.CompletedChunks++;
            task.CompletedItems += chunk.ItemCount;
            task.ConsecutiveFailures = 0;
            task.Error = null;
            task.UpdatedAt = DateTime.UtcNow;

            if (task.CompletedChunks == task.TotalChunks)
            {
                // Lưu Result của chunk vừa xong TRƯỚC — Finish() đọc lại toàn bộ chunk từ DB (AsNoTracking)
                // để tổng hợp, nếu chưa flush sẽ đọc phải Result cũ (null) của chính chunk này.
                await db.SaveChangesAsync(ct);
                await Finish(task, request, ct);
            }
            else
            {
                task.State = "Queued";
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failures = candidate.ConsecutiveFailures + 1;
            // Lỗi nghiệp vụ (quyền bị thu hồi, dữ liệu không hợp lệ) → NeedsReview ngay, không thử lại mù
            // quáng. Lỗi hạ tầng (DB tạm gián đoạn...) → thử lại tối đa 3 lần rồi mới Failed.
            var state = ex is InvalidOperationException or UnauthorizedAccessException ? "NeedsReview"
                : failures >= 3 ? "Failed" : "Queued";
            await db.AdminTasks.Where(x => x.Id == candidate.Id && x.State == "Running" && x.Attempts == attempt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.State, state)
                    .SetProperty(x => x.Error, ex.Message)
                    .SetProperty(x => x.ConsecutiveFailures, failures)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        }
        finally
        {
            db.BackgroundActorId = null;
            db.BackgroundTenantId = null;
            db.AcceptedAdminTask = false;
            db.AdminTaskId = null;
            db.ChangeTracker.Clear();
        }
        return true;
    }

    private async Task Finish(AdminTask task, AdminTaskRequest request, CancellationToken ct)
    {
        task.State = "Completed";
        task.FinishedAt = DateTime.UtcNow;
        var chunks = await db.AdminTaskChunks.AsNoTracking().Where(x => x.TaskId == task.Id)
            .OrderBy(x => x.Position).ToListAsync(ct);
        var results = chunks.Select(c => JsonSerializer.Deserialize<ReaderImportResult>(c.Result!, Json)!).ToList();

        if (task.Preview)
        {
            var changes = results.SelectMany(r => r.Review?.Changes ?? new List<ReaderChangePreview>()).ToList();
            var expires = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
            task.ReviewToken = AdminMutationGuard.Sign(RequestSummary(request), changes, expires, task.Id);
            // Đợt 20: kèm danh mục "sẽ tự tạo" + lỗi từng dòng để màn xem trước hiển thị và tải file dòng lỗi.
            var createdRefs = results.SelectMany(r => r.CreatedRefs).Distinct().ToList();
            var errors = results.SelectMany(r => r.Errors).ToList();
            task.Result = JsonSerializer.Serialize(new { changes, reviewToken = task.ReviewToken, createdRefs, errors }, Json);
        }
        else
        {
            var aggregate = new ReaderImportResult();
            foreach (var r in results)
            {
                aggregate.TotalRows += r.TotalRows;
                aggregate.SuccessCount += r.SuccessCount;
                aggregate.FailedCount += r.FailedCount;
                aggregate.SkippedCount += r.SkippedCount;
                aggregate.Errors.AddRange(r.Errors);
                aggregate.CreatedRefs.AddRange(r.CreatedRefs);
            }
            aggregate.CreatedRefs = aggregate.CreatedRefs.Distinct().ToList();
            task.Result = JsonSerializer.Serialize(aggregate, Json);
        }
    }

    private async Task InitializeChunks(AdminTask task, AdminTaskRequest request, CancellationToken ct)
    {
        switch (task.Kind)
        {
            case "reader-import":
            {
                var rows = request.Rows ?? new List<ReaderImportRow>();
                await SaveChunksAsync(task, rows.Count, rows.Count == 0
                    ? new List<ReaderImportRow[]> { Array.Empty<ReaderImportRow>() }
                    : rows.Chunk(ChunkSize).ToList(),
                    g => new AdminTaskRequest
                    {
                        Kind = request.Kind, Preview = task.Preview, Rows = g.ToList(),
                        PortalId = request.PortalId, Language = request.Language, Overwrite = request.Overwrite,
                        ReaderTypeId = request.ReaderTypeId, ClassByCode = request.ClassByCode,
                        CourseByCode = request.CourseByCode, OrgByCode = request.OrgByCode, AutoCreateRefs = request.AutoCreateRefs,
                    }, ct);
                break;
            }
            case "barcode-reregister-import":
            {
                var rows = request.BarcodeRows ?? new List<BarcodeReRegisterRow>();
                await SaveChunksAsync(task, rows.Count, rows.Count == 0
                    ? new List<BarcodeReRegisterRow[]> { Array.Empty<BarcodeReRegisterRow>() }
                    : rows.Chunk(ChunkSize).ToList(),
                    g => new AdminTaskRequest { Kind = request.Kind, Preview = task.Preview, BarcodeRows = g.ToList() }, ct);
                break;
            }
            case "inventory-import":
            {
                var rows = request.InventoryRows ?? new List<InventoryImportRow>();
                await SaveChunksAsync(task, rows.Count, rows.Count == 0
                    ? new List<InventoryImportRow[]> { Array.Empty<InventoryImportRow>() }
                    : rows.Chunk(ChunkSize).ToList(),
                    g => new AdminTaskRequest { Kind = request.Kind, Preview = task.Preview, InventoryId = request.InventoryId, InventoryRows = g.ToList() }, ct);
                break;
            }
            default:
                throw new InvalidOperationException($"Loại tác vụ '{task.Kind}' chưa hỗ trợ.");
        }
    }

    private async Task SaveChunksAsync<TRow>(AdminTask task, int totalRows, List<TRow[]> groups,
        Func<TRow[], AdminTaskRequest> buildRequest, CancellationToken ct)
    {
        if (totalRows > MaxRows) throw new InvalidOperationException($"Vượt giới hạn {MaxRows} dòng cho 1 tác vụ.");

        var chunks = groups.Select((g, i) => new AdminTaskChunk
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Position = i,
            ItemCount = g.Length,
            Payload = crypto.Protect(JsonSerializer.Serialize(buildRequest(g), Json)),
            Completed = false,
        }).ToList();

        db.AdminTaskChunks.AddRange(chunks);
        task.TotalChunks = chunks.Count;
        task.TotalItems = totalRows;
        await db.SaveChangesAsync(ct);
    }

    private async Task<object> ExecuteChunk(AdminTask task, AdminTaskRequest request, CancellationToken ct) => request.Kind switch
    {
        "reader-import" => await readers.ImportAsync(request.Rows ?? new List<ReaderImportRow>(), request.PortalId, request.Language,
            request.Overwrite, request.ReaderTypeId, request.ClassByCode, request.CourseByCode, request.OrgByCode,
            previewOnly: task.Preview, autoCreateRefs: request.AutoCreateRefs),
        "barcode-reregister-import" => await ExecuteBarcodeRows(request.BarcodeRows ?? new List<BarcodeReRegisterRow>(), task.TenantId, task.ActorId, task.Preview),
        "inventory-import" => await ExecuteInventoryRows(request, task.TenantId, task.ActorId, task.Preview),
        _ => throw new InvalidOperationException($"Loại tác vụ '{request.Kind}' chưa hỗ trợ."),
    };

    /// <summary>Chạy 1 lô dòng đánh lại mã ĐKCB qua <see cref="BarcodeReRegisterService"/> — dùng chung hình dạng
    /// kết quả <see cref="ReaderImportResult"/> (TotalRows/SuccessCount/FailedCount/Errors/Review) với reader-import
    /// để <c>Finish</c>/màn xem trước generic đọc được, dù tên field "Cardno"/"Card" chỉ mượn làm định danh dòng
    /// hiển thị (ở đây là mã vạch cũ), không có ý nghĩa "thẻ bạn đọc".</summary>
    private async Task<ReaderImportResult> ExecuteBarcodeRows(List<BarcodeReRegisterRow> rows, long? tenantId, long actorId, bool previewOnly)
    {
        var actorName = await ActorNameAsync(actorId);
        var result = new ReaderImportResult { TotalRows = rows.Count };
        var changes = new List<ReaderChangePreview>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.OldBarcode) || string.IsNullOrWhiteSpace(row.NewBarcode))
            {
                result.FailedCount++;
                result.Errors.Add("Dòng thiếu mã vạch cũ/mã vạch mới");
                continue;
            }
            var outcome = await barcodeReRegister.ExecuteAsync(tenantId, row.OldBarcode!, row.NewBarcode!, row.StoreId,
                actorId, actorName, ip: null, previewOnly: previewOnly);
            if (!outcome.Success)
            {
                result.FailedCount++;
                result.Errors.Add($"{row.OldBarcode} → {row.NewBarcode}: {outcome.Error}");
                continue;
            }
            result.SuccessCount++;
            changes.Add(new ReaderChangePreview
            {
                Cardno = row.OldBarcode,
                IsNew = false,
                Fields = new List<ReaderFieldChange> { new() { Field = "Mã vạch mới", Before = row.OldBarcode, After = row.NewBarcode } },
            });
        }
        if (previewOnly) result.Review = new ReaderMutationReview { Changes = changes };
        return result;
    }

    /// <summary>Chạy 1 lô dòng nhập kiểm kê qua <see cref="InventoryImportService"/> — hình dạng kết quả như trên.
    /// Mã đã quét trước đó (trùng lặp giữa các lần nhập, hoặc đã quét tay qua UI) tính là SkippedCount, không phải
    /// lỗi.</summary>
    private async Task<ReaderImportResult> ExecuteInventoryRows(AdminTaskRequest request, long? callerTenantId, long actorId, bool previewOnly)
    {
        var rows = request.InventoryRows ?? new List<InventoryImportRow>();
        var result = new ReaderImportResult { TotalRows = rows.Count };
        var (tenantId, found) = await inventoryImport.ResolveTenantAsync(request.InventoryId ?? 0, callerTenantId);
        if (!found)
        {
            result.FailedCount = rows.Count;
            result.Errors.Add("Không tìm thấy phiên kiểm kê");
            return result;
        }

        var changes = new List<ReaderChangePreview>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Barcode))
            {
                result.FailedCount++;
                result.Errors.Add("Dòng thiếu mã vạch");
                continue;
            }
            var outcome = await inventoryImport.ScanAsync(request.InventoryId ?? 0, tenantId, row.Barcode!, row.StoreId, actorId, previewOnly);
            if (!outcome.Success)
            {
                result.FailedCount++;
                result.Errors.Add($"{row.Barcode}: {outcome.Error}");
                continue;
            }
            if (outcome.AlreadyScanned) result.SkippedCount++; else result.SuccessCount++;
            changes.Add(new ReaderChangePreview
            {
                Cardno = row.Barcode,
                IsNew = !outcome.AlreadyScanned,
                Fields = new List<ReaderFieldChange>(),
            });
        }
        if (previewOnly) result.Review = new ReaderMutationReview { Changes = changes };
        return result;
    }

    public async Task<AdminTask?> Pause(long actorId, Guid id)
    {
        await db.AdminTasks.Where(x => x.Id == id && x.ActorId == actorId && (x.State == "Queued" || x.State == "Running"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StopRequested, true).SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        return await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actorId);
    }

    public async Task<AdminTask?> Resume(long actorId, Guid id)
    {
        await db.AdminTasks.Where(x => x.Id == id && x.ActorId == actorId && (x.State == "Paused" || x.State == "Failed"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, "Queued")
                .SetProperty(x => x.StopRequested, false)
                .SetProperty(x => x.ConsecutiveFailures, 0)
                .SetProperty(x => x.Error, (string?)null)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        return await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actorId);
    }

    public async Task<AdminTask?> Cancel(long actorId, Guid id)
    {
        await db.AdminTasks.Where(x => x.Id == id && x.ActorId == actorId &&
                (x.State == "Queued" || x.State == "Running" || x.State == "Paused" ||
                 x.State == "Failed" || x.State == "NeedsReview"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, "Cancelled")
                .SetProperty(x => x.StopRequested, false)
                .SetProperty(x => x.FinishedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        return await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actorId);
    }

    /// <summary>Gộp lại các chunk CHƯA hoàn tất của 1 tác vụ (Paused/Failed/NeedsReview) thành 1 tác vụ
    /// xem trước mới — chunk đã hoàn tất không bao giờ được đưa lại vào đây, nên xem-trước-lại rồi xác
    /// nhận lại không thể ghi trùng phần đã xong.</summary>
    public async Task<AdminTask> PreviewRemaining(long actorId, long? tenantId, Guid id)
    {
        var task = await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actorId)
            ?? throw new InvalidOperationException("Không tìm thấy tác vụ.");

        var remaining = await db.AdminTaskChunks.AsNoTracking()
            .Where(x => x.TaskId == id && !x.Completed).OrderBy(x => x.Position).ToListAsync();
        if (remaining.Count == 0) throw new InvalidOperationException("Không còn phần nào chưa xử lý.");

        AdminTaskRequest? baseRequest = null;
        var readerRows = new List<ReaderImportRow>();
        var barcodeRows = new List<BarcodeReRegisterRow>();
        var inventoryRows = new List<InventoryImportRow>();
        foreach (var c in remaining)
        {
            var r = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(c.Payload!), Json)!;
            baseRequest ??= r;
            readerRows.AddRange(r.Rows ?? []);
            barcodeRows.AddRange(r.BarcodeRows ?? []);
            inventoryRows.AddRange(r.InventoryRows ?? []);
        }

        baseRequest!.Rows = readerRows.Count > 0 ? readerRows : null;
        baseRequest.BarcodeRows = barcodeRows.Count > 0 ? barcodeRows : null;
        baseRequest.InventoryRows = inventoryRows.Count > 0 ? inventoryRows : null;
        baseRequest.Preview = true;
        baseRequest.Token = null;
        return await Enqueue(actorId, tenantId, baseRequest);
    }
}
