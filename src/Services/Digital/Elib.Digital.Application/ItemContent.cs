using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elib.Digital.Application;

/// <summary>File PDF của tài liệu (đuôi không phân biệt hoa/thường) — một biểu thức để EF dịch sang SQL.</summary>
internal static class PdfFiles
{
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
    public static readonly System.Linq.Expressions.Expression<Func<EbookFile, bool>> Predicate = f => f.FileExt != null && f.FileExt.ToLower() == "pdf";
#pragma warning restore CA1862, CA1304, CA1311
}

public sealed record ContentSyncResult(int Queued, IReadOnlyList<ItemSkipped> Skipped);

public sealed record ContentStatusDto(Guid ItemPublicId, string Status, int PageCount, int ChunkCount, int EmbeddedCount, string? Error, DateTimeOffset? SyncedAt);

public sealed record ContentStatusRequest(IReadOnlyList<Guid>? PublicIds);

/// <summary>Yêu cầu đồng bộ nội dung (LRC: "Đồng bộ nội dung") và xem trạng thái — việc trích chữ/vector hóa chạy nền ở <see cref="EbookContentSync"/>.</summary>
public sealed partial class EbookItemResource
{
    /// <summary>Trần số tài liệu một lần yêu cầu đồng bộ nội dung.</summary>
    public const int MaxContentSync = 500;

    public async Task<ContentSyncResult> RequestContentSyncAsync(ItemSelection selection, CancellationToken ct)
    {
        var ids = await ResolveIdsAsync(selection, ct);
        if (ids.Count > MaxContentSync)
            throw new BusinessRuleException("SELECTION_TOO_MANY", $"Mỗi lần đồng bộ nội dung tối đa {MaxContentSync} tài liệu — hãy thu hẹp bộ lọc.");
        // Bản sao tài liệu dùng chung không lập chỉ mục nội dung ở đơn vị (file ở kho đơn vị chủ) — bỏ qua.
        var items = await Set.AsNoTracking().Where(x => ids.Contains(x.PublicId) && !x.IsReplica).Select(x => new { x.Id, x.PublicId, x.Title }).ToListAsync(ct);
        var internalIds = items.Select(x => x.Id).ToList();
        var withPdf = (await Db.Set<EbookFile>().AsNoTracking().Where(f => internalIds.Contains(f.EbookItemId)).Where(PdfFiles.Predicate)
            .Select(f => f.EbookItemId).Distinct().ToListAsync(ct)).ToHashSet();
        var states = await Db.Set<EbookContentState>().Where(s => internalIds.Contains(s.EbookItemId)).ToDictionaryAsync(s => s.EbookItemId, ct);

        var now = clock.GetUtcNow();
        var queued = 0;
        var skipped = new List<ItemSkipped>();
        foreach (var item in items)
        {
            if (!withPdf.Contains(item.Id))
            {
                skipped.Add(new ItemSkipped(item.PublicId, item.Title, "CONTENT_NO_PDF", "Tài liệu chưa có file PDF."));
                continue;
            }
            if (!states.TryGetValue(item.Id, out var state)) Db.Set<EbookContentState>().Add(EbookContentState.Request(item.Id, now));
            else if (!state.Requeue(now))
            {
                skipped.Add(new ItemSkipped(item.PublicId, item.Title, "CONTENT_RUNNING", "Đang đồng bộ nội dung, thử lại sau khi xong."));
                continue;
            }
            queued++;
        }
        await Db.SaveChangesAsync(ct);
        return new ContentSyncResult(queued, skipped);
    }

    public async Task<IReadOnlyList<ContentStatusDto>> ContentStatusAsync(IReadOnlyList<Guid>? publicIds, CancellationToken ct)
    {
        var ids = (publicIds ?? []).Distinct().Take(MaxSelection).ToList();
        if (ids.Count == 0) return [];
        return await (from s in Db.Set<EbookContentState>().AsNoTracking()
                      join i in Set.AsNoTracking() on s.EbookItemId equals i.Id
                      where ids.Contains(i.PublicId)
                      select new ContentStatusDto(i.PublicId, s.Status, s.PageCount, s.ChunkCount, s.EmbeddedCount, s.Error, s.SyncedAt)).ToListAsync(ct);
    }
}

/// <summary>
/// Việc nền đồng bộ nội dung cho MỘT đơn vị (đơn vị hiện tại trong ngữ cảnh): lấy tối đa vài tài liệu đang chờ, đọc các file PDF từ media qua luồng,
/// trích lớp văn bản, cắt đoạn theo trang, vector hóa (nếu bật Ollama) rồi thay toàn bộ đoạn cũ của tài liệu trong một giao dịch.
/// PDF quét (không có chữ) → trạng thái <see cref="EbookContentState.NoText"/>; chưa có OCR.
/// </summary>
public sealed partial class EbookContentSync(
    ICrudDbContext db, IMediaFileStore media, IPdfTextExtractor extractor, ITextEmbedder embedder, TimeProvider clock, ILogger<EbookContentSync> logger)
{
    public const int MaxFilesPerItem = 5;
    public const int MaxPagesPerItem = 3000;
    public const int MaxChunksPerItem = 6000;
    public const int ChunkChars = 1200;
    public const int ChunkOverlap = 150;

    /// <summary>Xử lý tối đa <paramref name="batch"/> tài liệu đang chờ của đơn vị hiện tại; trả số tài liệu đã xử lý.</summary>
    public async Task<int> ProcessPendingAsync(int batch, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Dòng kẹt ở Running (tiến trình chết giữa chừng) quay lại hàng đợi.
        var stale = now.AddMinutes(-EbookContentState.StaleMinutes);
        foreach (var s in await db.Set<EbookContentState>().Where(s => s.Status == EbookContentState.Running && s.StartedAt != null && s.StartedAt < stale).ToListAsync(ct))
            s.Release(now);
        await db.SaveChangesAsync(ct);

        var pending = await db.Set<EbookContentState>().Where(s => s.Status == EbookContentState.Pending).OrderBy(s => s.RequestedAt).Take(batch).ToListAsync(ct);
        foreach (var state in pending) await ProcessAsync(state, ct);
        return pending.Count;
    }

    private async Task ProcessAsync(EbookContentState state, CancellationToken ct)
    {
        state.Start(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var staged = new List<EbookChunk>();
        try
        {
            var files = await db.Set<EbookFile>().AsNoTracking()
                .Where(f => f.EbookItemId == state.EbookItemId).Where(PdfFiles.Predicate)
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Take(MaxFilesPerItem).Select(f => f.MediaFileId).ToListAsync(ct);
            if (files.Count == 0)
            {
                // File PDF đã bị gỡ sau khi xếp hàng: xoá đoạn cũ để không còn tìm thấy nội dung của file không còn.
                await ReplaceAsync(state, [], EbookContentState.Failed, 0, 0, "Tài liệu không còn file PDF.", ct);
                return;
            }

            var pages = new List<PdfPageText>();
            var pageCount = 0;
            foreach (var mediaFileId in files)
            {
                var remaining = MaxPagesPerItem - pageCount;
                if (remaining <= 0) break;
                foreach (var page in await ExtractAsync(mediaFileId, remaining, ct))
                {
                    pageCount++;
                    pages.Add(page with { Page = pageCount }); // nhiều file: đánh số trang liên tục
                }
            }

            var pieces = Split(pages).Take(MaxChunksPerItem).ToList();
            var chunks = staged;
            string? warning = null;
            var embedded = 0;
            var tryEmbed = embedder.Enabled;
            for (var i = 0; i < pieces.Count; i++)
            {
                float[]? vector = null;
                if (tryEmbed)
                {
                    try
                    {
                        vector = await embedder.EmbedAsync(pieces[i].Text, query: false, ct);
                        embedded++;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        // Ollama lỗi/quá tải: giữ phần chữ để vẫn tìm theo từ khóa, bỏ vector các đoạn còn lại.
                        LogEmbeddingStopped(logger, state.EbookItemId, i, ex);
                        warning = $"Vector hóa dừng ở đoạn {i + 1}/{pieces.Count}; phần chữ vẫn tìm được theo từ khóa.";
                        tryEmbed = false;
                    }
                }
                chunks.Add(EbookChunk.Create(state.EbookItemId, i, pieces[i].Page, pieces[i].Text, vector));
            }

            var status = chunks.Count == 0 ? EbookContentState.NoText : EbookContentState.Done;
            await ReplaceAsync(state, chunks, status, pageCount, embedded, chunks.Count == 0 ? "PDF không có lớp văn bản (PDF quét) — chưa hỗ trợ OCR." : warning, ct);
        }
        catch (OperationCanceledException)
        {
            state.Release(clock.GetUtcNow()); // dịch vụ tắt giữa chừng → làm lại sau
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException or InvalidDataException or HttpRequestException or IOException)
        {
            LogFailed(logger, state.EbookItemId, ex);
            state.Finish(EbookContentState.Failed, 0, 0, 0, ex.Message, clock.GetUtcNow());
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Lỗi không lường trước (vd. lưu CSDL hỏng): gỡ các đoạn đang chờ lưu rồi đánh dấu lỗi — không để dòng kẹt ở Running và bị thử lại mãi.
            LogFailed(logger, state.EbookItemId, ex);
            foreach (var chunk in staged) db.Set<EbookChunk>().Entry(chunk).State = EntityState.Detached;
            state.Finish(EbookContentState.Failed, 0, 0, 0, "Lỗi không lường trước khi đồng bộ nội dung — xem nhật ký dịch vụ.", clock.GetUtcNow());
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Tải file về tệp tạm (PdfPig cần luồng tìm kiếm được) rồi trích chữ; tệp tạm tự xóa khi đóng.</summary>
    private async Task<List<PdfPageText>> ExtractAsync(Guid mediaFileId, int maxPages, CancellationToken ct)
    {
        await using var source = await media.OpenReadAsync(mediaFileId, ct);
        await using var temp = new FileStream(Path.Combine(Path.GetTempPath(), "elib-digital-" + Guid.NewGuid().ToString("N")), FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await source.CopyToAsync(temp, ct);
        temp.Position = 0;
        return [.. extractor.Extract(temp, maxPages)];
    }

    private async Task ReplaceAsync(EbookContentState state, List<EbookChunk> chunks, string status, int pageCount, int embedded, string? note, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await db.Set<EbookChunk>().Where(c => c.EbookItemId == state.EbookItemId).ExecuteDeleteAsync(token);
            db.Set<EbookChunk>().AddRange(chunks);
            state.Finish(status, pageCount, chunks.Count, embedded, note, clock.GetUtcNow());
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }, ct);
    }

    /// <summary>Cắt văn bản từng trang thành đoạn ~<see cref="ChunkChars"/> ký tự, gối nhau <see cref="ChunkOverlap"/>, ngắt ở khoảng trắng; không đoạn nào vắt qua trang.</summary>
    public static IEnumerable<(int Page, string Text)> Split(IEnumerable<PdfPageText> pages)
    {
        foreach (var page in pages)
        {
            var text = CollapseWhitespace(page.Text);
            if (text.Length == 0) continue;
            var start = 0;
            while (start < text.Length)
            {
                var end = Math.Min(start + ChunkChars, text.Length);
                if (end < text.Length)
                {
                    var space = text.LastIndexOf(' ', end - 1, Math.Min(200, end - start));
                    if (space > start) end = space;
                }
                var piece = text[start..end].Trim();
                if (piece.Length > 0) yield return (page.Page, piece);
                if (end >= text.Length) break;
                start = Math.Max(end - ChunkOverlap, start + 1);
                var next = text.IndexOf(' ', start);
                if (next >= 0 && next < end) start = next + 1; // bắt đầu ở đầu từ, không cắt giữa chữ
            }
        }
    }

    private static string CollapseWhitespace(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        var space = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (space) sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đồng bộ nội dung tài liệu {ItemId}: vector hóa dừng ở đoạn {Index}")]
    private static partial void LogEmbeddingStopped(ILogger logger, long itemId, int index, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đồng bộ nội dung tài liệu {ItemId} lỗi")]
    private static partial void LogFailed(ILogger logger, long itemId, Exception exception);
}

public sealed record ContentSearchRequest(string? Q = null, int Take = 20, bool Semantic = false);

/// <summary>Một kết quả tìm trong nội dung. <see cref="Snippet"/> chỉ có với tài liệu công khai (Free, không thuộc chủ đề yêu cầu đăng nhập) — tài liệu cần mượn chỉ lộ nhan đề và số trang.</summary>
public sealed record ContentHit(Guid PublicId, string Title, string? Author, string? CoverUrl, bool Free, int Page, string? Snippet, double Score);

/// <summary>
/// Tìm trong nội dung tài liệu số. Từ khóa: mọi từ của câu tìm phải có trong đoạn (không phân biệt hoa/thường). Ngữ nghĩa
/// (cần Ollama, CHỈ cán bộ — mỗi lần quét tới <see cref="MaxSemanticScan"/> đoạn nên không mở cho OPAC ẩn danh): cosine giữa vector câu hỏi và vector các đoạn,
/// chưa có chỉ mục vector (pgvector) nên chưa dùng cho kho lớn. OPAC chỉ thấy tài liệu đang xuất bản và đoạn trích của tài liệu công khai; cán bộ thấy cả tài liệu ẩn và mọi đoạn trích.
/// </summary>
public sealed class OpacContentSearch(ICrudDbContext db, ITextEmbedder embedder)
{
    public const int MaxTake = 50;
    public const int MaxCandidates = 1000;
    public const int MaxSemanticScan = 20_000;
    private const int SnippetChars = 240;

    public async Task<IReadOnlyList<ContentHit>> SearchAsync(ContentSearchRequest request, bool staff, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = (request.Q ?? "").Trim();
        if (q.Length < 2 || q.Length > 200) throw new BusinessRuleException("CONTENT_QUERY_INVALID", "Câu tìm phải từ 2 đến 200 ký tự.");
        var take = Math.Clamp(request.Take, 1, MaxTake);
        var items = db.Set<EbookItem>().AsNoTracking();
        if (!staff) items = items.Where(i => i.Status == IHasStatus.Active && !i.HiddenByTenant);
        var visible = db.Set<EbookChunk>().AsNoTracking().Join(items, c => c.EbookItemId, i => i.Id, (c, i) => new { Chunk = c, Item = i });

        List<(long ItemId, int Page, string Text, double Score)> best;
        if (request.Semantic && !staff) request = request with { Semantic = false }; // quét vector tốn kém — không cho ẩn danh
        if (request.Semantic)
        {
            if (!embedder.Enabled) throw new BusinessRuleException("EMBEDDING_DISABLED", "Chưa bật tìm theo ngữ nghĩa (chưa cấu hình dịch vụ vector hóa).", 503);
            var query = await embedder.EmbedAsync(q, query: true, ct);
            var rows = await visible.Where(x => x.Chunk.Embedding != null).OrderByDescending(x => x.Chunk.Id).Take(MaxSemanticScan)
                .Select(x => new { x.Chunk.EbookItemId, x.Chunk.Page, x.Chunk.Text, x.Chunk.Embedding }).ToListAsync(ct);
            best = [.. rows.Where(r => r.Embedding is { Length: > 0 } e && e.Length == query.Length)
                .Select(r => (r.EbookItemId, r.Page, r.Text, Score: Dot(query, r.Embedding!)))
                .GroupBy(r => r.EbookItemId).Select(g => g.MaxBy(r => r.Score)).OrderByDescending(r => r.Score).Take(take * 3)];
        }
        else
        {
            var words = q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Take(6).ToList();
            var filtered = visible;
            foreach (var word in words)
            {
                var w = word;
                filtered = filtered.Where(x => x.Chunk.SearchText.Contains(w));
            }
            var rows = await filtered.OrderByDescending(x => x.Chunk.Id).Take(MaxCandidates).Select(x => new { x.Chunk.EbookItemId, x.Chunk.Page, x.Chunk.Text, x.Chunk.SearchText }).ToListAsync(ct);
            var phrase = q.ToLowerInvariant();
            best = [.. rows.Select(r => (r.EbookItemId, r.Page, r.Text, Score: (double)Occurrences(r.SearchText, phrase) * 3 + words.Sum(w => Math.Min(Occurrences(r.SearchText, w), 5))))
                .GroupBy(r => r.EbookItemId).Select(g => g.MaxBy(r => r.Score)).OrderByDescending(r => r.Score).Take(take * 3)];
        }

        var itemIds = best.Select(b => b.ItemId).ToList();
        var found = await db.Set<EbookItem>().AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.PublicId, i.Title, i.Author, i.CoverUrl, i.Free, i.TopicId }).ToDictionaryAsync(i => i.Id, ct);
        var loginTopics = (await db.Set<EbookTopic>().AsNoTracking().Where(t => t.RequiresLogin).Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var first = q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var hits = new List<ContentHit>();
        foreach (var b in best)
        {
            if (!found.TryGetValue(b.ItemId, out var item)) continue;
            var open = staff || (item.Free && !(item.TopicId is { } t && loginTopics.Contains(t)));
            hits.Add(new ContentHit(item.PublicId, item.Title, item.Author, item.CoverUrl, item.Free, b.Page, open ? Snippet(b.Text, request.Semantic ? null : first) : null,
                Math.Round(b.Score, 4)));
            if (hits.Count == take) break;
        }
        return hits;
    }

    private static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++) sum += (double)a[i] * b[i];
        return sum;
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static string Snippet(string text, string? word)
    {
        var at = word is null ? 0 : text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, (at < 0 ? 0 : at) - SnippetChars / 4);
        var end = Math.Min(text.Length, start + SnippetChars);
        return (start > 0 ? "…" : "") + text[start..end].Trim() + (end < text.Length ? "…" : "");
    }
}
