using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ELIBAPI.Core.DTOs.Chat;
using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Hỏi đáp theo NỘI DUNG tài liệu (RAG). Hai chế độ:
/// <list type="bullet">
/// <item><b>Trong 1 tài liệu</b> (<see cref="ChatRequest.EbookId"/> có giá trị — khung chat ở trang đọc): luôn cần
/// tra nội dung nên KHÔNG hỏi Gemini "có cần tra không" nữa — nhận diện tóm tắt/câu hỏi cụ thể bằng quy tắc cục
/// bộ, tìm lai (kNN + từ khóa) rồi gọi Gemini đúng 1 lần để viết câu trả lời.</item>
/// <item><b>Toàn kho</b> (không có EbookId): lượt 1 để Gemini quyết định có tra tài liệu không (chào hỏi thì trả
/// lời luôn), lượt 2 viết câu trả lời từ ngữ cảnh (không kèm tools nên luôn ra văn bản).</item>
/// </list>
/// Có cache (embedding câu hỏi, câu trả lời lặp lại, bản tóm tắt theo tài liệu) và bản trả dần (streaming).
/// Port ELIB-LRC 09-29. Tenant (K12): mọi truy xuất (kNN, BM25, tóm tắt) chỉ lấy chunk của đơn vị hoặc chunk dùng chung, và
/// khoá cache câu trả lời/tóm tắt có đơn vị — câu trả lời của đơn vị này không được trả cho đơn vị khác.
/// </summary>
public class ChatService(
    IElasticsearchService elastic,
    IEmbeddingService embedding,
    IGeminiClient gemini,
    IConfiguration config,
    ELIBAPIDbContext db,
    IMemoryCache cache,
    ILogger<ChatService> logger) : IChatService
{
    private const string SearchDocumentsTool = "search_documents";
    private const string ErrorAnswer = "Lỗi hệ thống khi tạo câu trả lời. Vui lòng thử lại.";
    private const string NotIndexedAnswer =
        "Tài liệu này chưa được lập chỉ mục nội dung nên chưa thể hỏi đáp. Vui lòng thử lại sau hoặc liên hệ thủ thư.";
    private const string NothingFoundAnswer = "Không tìm thấy tài liệu liên quan đến câu hỏi của bạn.";

    private static readonly TimeSpan AnswerCacheTtl    = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SummaryCacheTtl   = TimeSpan.FromHours(24);
    private static readonly TimeSpan EmbeddingCacheTtl = TimeSpan.FromHours(1);
    /// <summary>Giới hạn thời gian cho 1 lượt gọi Gemini trong chat — hết giờ thì báo lỗi thay vì treo 2 phút.</summary>
    private static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(45);

    /// <summary>Lượt 1 (chỉ ở chế độ toàn kho): quyết định có cần tra tài liệu hay không.</summary>
    private const string RouterInstruction =
        "Bạn là trợ lý thư viện điện tử. Nếu câu hỏi liên quan đến nội dung, kiến thức, thông tin có trong " +
        "sách/tài liệu, hãy gọi công cụ search_documents để tra cứu trước khi trả lời. Với câu chào hỏi/trò " +
        "chuyện thông thường thì trả lời ngắn gọn bằng tiếng Việt, không gọi công cụ. Nếu có các lượt hội " +
        "thoại trước, câu hiện tại có thể là câu nối tiếp — viết lại 'query' đầy đủ ý (vd 'giải thích ý 2' → " +
        "nêu rõ ý 2 là gì).";

    /// <summary>Lượt viết câu trả lời từ ngữ cảnh đã truy xuất.</summary>
    private const string AnswerInstruction =
        "Bạn là trợ lý thư viện điện tử. Trả lời câu hỏi CHỈ dựa trên các đoạn ngữ cảnh trích từ tài liệu được " +
        "cung cấp trong tin nhắn (mỗi đoạn có nhãn [Tên tài liệu, trang N]). Trả lời chính xác, rõ ràng, bằng " +
        "tiếng Việt; nếu ngữ cảnh không đủ để trả lời thì nói rõ điều đó — tuyệt đối không bịa đặt thông tin. " +
        "Khi trình bày, liệt kê, tóm tắt hoặc trích dẫn nội dung cụ thể nào, LUÔN ghi số trang ngay sau ý đó " +
        "(ví dụ: \"...nội dung X (trang 12).\"). Trình bày bằng Markdown gọn gàng: tiêu đề (##/###) khi câu trả " +
        "lời có nhiều phần, in đậm thuật ngữ/ý chính, gạch đầu dòng hoặc đánh số khi liệt kê từ 2 ý, đoạn văn ngắn.";

    private const string DocumentScopedInstruction =
        " Người dùng đang mở đọc 1 tài liệu cụ thể — mọi cụm 'tài liệu', 'tài liệu này', 'sách', 'nội dung' đều " +
        "ám chỉ chính tài liệu đó; KHÔNG hỏi lại tên tài liệu.";

    private const string SummaryInstruction =
        " Đây là yêu cầu tóm tắt/tổng quan: ngữ cảnh gồm các đoạn đại diện rải đều từ đầu đến cuối tài liệu. " +
        "Trình bày lần lượt theo trình tự trang, mỗi ý chính kèm số trang tương ứng.";

    private static readonly Regex SummaryPattern = new(
        @"tóm tắt|tổng quan|khái quát|nội dung chính|ý chính|tổng hợp nội dung|đại ý|" +
        @"(trình bày|giới thiệu) (tổng quát|khái quát|nội dung|về (tài liệu|cuốn sách|sách))|" +
        @"(tài liệu|sách|cuốn sách)( này)? (nói|viết) về (gì|cái gì|điều gì)|summar|overview",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Yêu cầu tóm tắt/tổng quan toàn tài liệu (khác câu hỏi 1 chi tiết cụ thể).</summary>
    public static bool IsSummaryRequest(string question) => SummaryPattern.IsMatch(question);

    // ── Chuẩn bị: truy xuất + dựng prompt, dùng chung cho AskAsync và AskStreamAsync ─────────────

    private sealed record Prepared(
        string? ImmediateAnswer,
        object? AnswerPayload,
        List<ChatSource> Sources,
        string? CacheKey,
        TimeSpan CacheTtl);

    private static Prepared Immediate(string answer, List<ChatSource>? sources = null) =>
        new(answer, null, sources ?? [], null, TimeSpan.Zero);

    private async Task<Prepared> PrepareAsync(ChatRequest request, long? tenantId, CancellationToken ct)
    {
        var question = (request.Question ?? "").Trim();
        var history  = ChatHistory.Normalize(request.History);
        var topK     = int.TryParse(config["RagSettings:TopK"], out var tk) ? tk : 5;

        if (request.EbookId is Guid ebookId)
        {
            var summary = IsSummaryRequest(question);
            // Tóm tắt không phụ thuộc cách hỏi → 1 bản cache cho mỗi tài liệu; câu hỏi cụ thể cache theo câu.
            var cacheKey = history.Count > 0 ? null
                : summary ? $"rag:summary:{tenantId}:{ebookId}"
                : $"rag:answer:{tenantId}:{ebookId}:{ChatHistory.NormalizeQuestion(question)}";
            if (cacheKey != null && cache.TryGetValue(cacheKey, out ChatResponse? hit) && hit != null)
                return Immediate(hit.Answer, hit.Sources);

            List<RagChunk> chunks;
            if (summary)
            {
                var maxPages = int.TryParse(config["RagSettings:SummaryMaxPages"], out var mp) ? mp : 300;
                var maxChars = int.TryParse(config["RagSettings:SummaryMaxChars"], out var mc) ? mc : 60000;
                chunks = FitSummaryBudget(await elastic.RetrieveDocumentOverviewChunksAsync(ebookId, tenantId, maxPages), maxChars);
            }
            else
            {
                // Câu nối tiếp rất ngắn ("giải thích thêm", "còn ý 2?") không đủ nghĩa để tìm — ghép câu hỏi trước.
                var query = question;
                if (history.Count > 0 && CountWords(question) < 6 && ChatHistory.LastUserQuestion(history) is { } prev)
                    query = prev + " " + question;
                chunks = await HybridRetrieveAsync(ebookId, query, topK, tenantId);
            }

            chunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Content)).ToList();
            if (chunks.Count == 0) return Immediate(NotIndexedAnswer);

            var instruction = AnswerInstruction + DocumentScopedInstruction + (summary ? SummaryInstruction : "");
            var payload = BuildAnswerPayload(instruction, history, question, chunks, summary ? 2048 : 1024);
            var sources = summary ? [] : await PageSourcesAsync(chunks, 5);
            return new Prepared(null, payload, sources, cacheKey, summary ? SummaryCacheTtl : AnswerCacheTtl);
        }

        // ── Toàn kho: lượt 1 để Gemini quyết định có tra tài liệu không ──
        var globalKey = history.Count > 0 ? null
            : $"rag:global:{tenantId}|{request.CollectionId}|{request.TopicId}|{request.SubjectId}|{request.Language}|{request.Free}|{ChatHistory.NormalizeQuestion(question)}";
        if (globalKey != null && cache.TryGetValue(globalKey, out ChatResponse? globalHit) && globalHit != null)
            return Immediate(globalHit.Answer, globalHit.Sources);

        JsonElement turn1;
        using (var cts = LinkedTimeout(ct))
        {
            turn1 = await gemini.GenerateAsync(new
            {
                system_instruction = new { parts = new[] { new { text = RouterInstruction } } },
                contents = ChatHistory.ToGeminiContents(history, question),
                generationConfig = GeminiGeneration.Config(0, 256),
                tools = BuildToolsPayload()
            }, cts.Token);
        }

        if (!turn1.TryGetProperty("functionCall", out var functionCall) ||
            functionCall.GetProperty("name").GetString() != SearchDocumentsTool)
            return Immediate(turn1.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "");

        var query2 = functionCall.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object
                     && args.TryGetProperty("query", out var q) && q.GetString() is { Length: > 0 } rewritten
            ? rewritten : question;

        var minScore = double.TryParse(config["RagSettings:MinScore"], System.Globalization.CultureInfo.InvariantCulture, out var ms) ? ms : 0.65;
        var candMult = int.TryParse(config["RagSettings:NumCandidatesMultiplier"], out var cm) ? cm : 10;
        var vector = await EmbedCachedAsync(query2);
        List<RagChunk> found;
        if (vector.Length > 0)
        {
            found = (await elastic.RetrieveRagChunksAsync(vector, tenantId, topK,
                    request.CollectionId, request.TopicId, request.SubjectId, request.Language, request.Free,
                    minScore, candMult, null))
                .Where(c => !string.IsNullOrWhiteSpace(c.Content) && c.Score >= minScore)
                .ToList();
        }
        else if (request.CollectionId == null && request.TopicId == null && request.SubjectId == null && request.Language == null && request.Free == null)
        {
            // Máy chủ embedding không phản hồi: vẫn trả lời được bằng tìm theo từ khóa trên toàn kho.
            found = await SafeAsync(() => elastic.RetrieveKeywordChunksAsync(null, query2, tenantId, topK), "BM25");
        }
        else return Immediate("Lỗi hệ thống khi xử lý câu hỏi. Vui lòng thử lại.");
        if (found.Count == 0) return Immediate(NothingFoundAnswer);

        return new Prepared(null,
            BuildAnswerPayload(AnswerInstruction, history, question, found, 1024),
            await DocumentSourcesAsync(found), globalKey, AnswerCacheTtl);
    }

    // ── Trả lời 1 lần ─────────────────────────────────────────────────────────

    public async Task<ChatResponse> AskAsync(ChatRequest request, long? tenantId, CancellationToken ct = default)
    {
        Prepared p;
        try { p = await PrepareAsync(request, tenantId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Chuẩn bị câu trả lời RAG thất bại");
            return new ChatResponse { Answer = ErrorAnswer };
        }
        if (p.ImmediateAnswer != null) return new ChatResponse { Answer = p.ImmediateAnswer, Sources = p.Sources };

        string answer;
        try
        {
            using var cts = LinkedTimeout(ct);
            var part = await gemini.GenerateAsync(p.AnswerPayload!, cts.Token);
            answer = part.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gọi Gemini viết câu trả lời thất bại");
            return new ChatResponse { Answer = ErrorAnswer };
        }

        var response = new ChatResponse { Answer = answer, Sources = p.Sources };
        if (p.CacheKey != null && !string.IsNullOrWhiteSpace(answer)) cache.Set(p.CacheKey, response, p.CacheTtl);
        return response;
    }

    // ── Trả dần (SSE) ─────────────────────────────────────────────────────────

    public async IAsyncEnumerable<ChatStreamEvent> AskStreamAsync(ChatRequest request, long? tenantId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Prepared? p = null;
        try { p = await PrepareAsync(request, tenantId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Chuẩn bị câu trả lời RAG (stream) thất bại");
        }
        if (p == null) { yield return ChatStreamEvent.Error(ErrorAnswer); yield break; }

        if (p.Sources.Count > 0) yield return ChatStreamEvent.Sources(p.Sources);
        if (p.ImmediateAnswer != null)
        {
            yield return ChatStreamEvent.Delta(p.ImmediateAnswer);
            yield return ChatStreamEvent.Done();
            yield break;
        }

        var full = new StringBuilder();
        string? error = null;
        await using (var e = gemini.StreamTextAsync(p.AnswerPayload!, ct).GetAsyncEnumerator(ct))
        {
            while (true)
            {
                bool has;
                try { has = await e.MoveNextAsync(); }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Gemini stream thất bại");
                    error = full.Length > 0 ? "Kết nối tới máy chủ AI bị gián đoạn — câu trả lời có thể chưa đầy đủ." : ErrorAnswer;
                    break;
                }
                if (!has) break;
                full.Append(e.Current);
                yield return ChatStreamEvent.Delta(e.Current);
            }
        }

        if (error != null) { yield return ChatStreamEvent.Error(error); yield break; }
        if (p.CacheKey != null && full.Length > 0)
            cache.Set(p.CacheKey, new ChatResponse { Answer = full.ToString(), Sources = p.Sources }, p.CacheTtl);
        yield return ChatStreamEvent.Done();
    }

    // ── Truy xuất ────────────────────────────────────────────────────────────

    /// <summary>Tìm lai trong 1 tài liệu: kNN (ngữ nghĩa) + BM25 (đúng từ ngữ), gộp bằng Reciprocal Rank Fusion.
    /// Embedding lỗi thì vẫn còn kết quả từ khóa, và ngược lại.</summary>
    private async Task<List<RagChunk>> HybridRetrieveAsync(Guid ebookId, string query, int topK, long? tenantId)
    {
        var candidates = Math.Max(topK * 2, 10);
        var keywordTask = SafeAsync(() => elastic.RetrieveKeywordChunksAsync(ebookId, query, tenantId, candidates), "BM25");
        var vector = await EmbedCachedAsync(query);
        var knn = vector.Length == 0 ? []
            : await SafeAsync(() => elastic.RetrieveRagChunksAsync(vector, tenantId, candidates, minScore: 0, ebookPublicId: ebookId), "kNN");
        return FuseRrf(knn, await keywordTask, topK);
    }

    private async Task<List<RagChunk>> SafeAsync(Func<Task<List<RagChunk>>> run, string what)
    {
        try { return await run(); }
        catch (Exception ex) { logger.LogWarning(ex, "Truy xuất {What} thất bại", what); return []; }
    }

    /// <summary>Reciprocal Rank Fusion (k=60): cộng 1/(60+hạng) của mỗi danh sách — không cần chuẩn hoá thang điểm
    /// giữa kNN (0..1) và BM25 (không giới hạn).</summary>
    public static List<RagChunk> FuseRrf(List<RagChunk> knn, List<RagChunk> keyword, int topK)
    {
        var scores = new Dictionary<(string, int, int), (RagChunk Chunk, double Score)>();
        void Add(List<RagChunk> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var key = (c.PublicId, c.PageNumber, c.ChunkIndex);
                var s = 1.0 / (60 + i + 1);
                scores[key] = scores.TryGetValue(key, out var cur) ? (cur.Chunk, cur.Score + s) : (c, s);
            }
        }
        Add(knn);
        Add(keyword);
        return scores.Values.OrderByDescending(x => x.Score).Take(topK)
            .Select(x => { x.Chunk.Score = x.Score; return x.Chunk; })
            .ToList();
    }

    /// <summary>Cắt ngữ cảnh tóm tắt theo ngân sách ký tự: đủ chỗ thì lấy mọi trang (cắt bớt mỗi đoạn), không đủ
    /// thì lấy các trang rải đều từ đầu đến cuối — thay vì gửi nguyên ~300 trang (~200 nghìn token) mỗi lần.</summary>
    public static List<RagChunk> FitSummaryBudget(List<RagChunk> pages, int maxChars, int minCharsPerPage = 400)
    {
        if (pages.Count == 0 || maxChars <= 0) return pages;
        var total = pages.Sum(p => p.Content.Length);
        if (total <= maxChars) return pages;

        var perPage = maxChars / pages.Count;
        List<RagChunk> picked;
        if (perPage >= minCharsPerPage) picked = pages;
        else
        {
            var n = Math.Max(1, maxChars / minCharsPerPage);
            picked = Enumerable.Range(0, n).Select(i => pages[(int)((long)i * pages.Count / n)]).Distinct().ToList();
            perPage = maxChars / picked.Count;
        }
        return picked.Select(p => new RagChunk
        {
            EbookId = p.EbookId, EbookFileId = p.EbookFileId, PublicId = p.PublicId, Title = p.Title, Author = p.Author,
            PageNumber = p.PageNumber, ChunkIndex = p.ChunkIndex, Score = p.Score,
            Content = p.Content.Length > perPage ? p.Content[..perPage] + "…" : p.Content
        }).ToList();
    }

    /// <summary>Embedding câu hỏi thường mất vài trăm ms; quá mức này coi như máy chủ embedding không phản hồi.</summary>
    private static readonly TimeSpan EmbeddingTimeout = TimeSpan.FromSeconds(5);
    private const string EmbeddingDownKey = "rag:emb:down";

    private async Task<float[]> EmbedCachedAsync(string query)
    {
        var key = "rag:emb:" + ChatHistory.NormalizeQuestion(query);
        if (cache.TryGetValue(key, out float[]? v) && v is { Length: > 0 }) return v;
        // Máy chủ embedding vừa lỗi → bỏ qua 1 phút (dùng tìm theo từ khóa) thay vì bắt mọi câu hỏi chờ timeout.
        if (cache.TryGetValue(EmbeddingDownKey, out _)) return [];
        try
        {
            v = await embedding.EmbedQueryAsync(query).WaitAsync(EmbeddingTimeout);
            if (v.Length > 0) cache.Set(key, v, EmbeddingCacheTtl);
            return v;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embedding câu hỏi thất bại — tạm dùng tìm theo từ khóa trong 1 phút");
            cache.Set(EmbeddingDownKey, true, TimeSpan.FromMinutes(1));
            return [];
        }
    }

    // ── Dựng prompt / nguồn ──────────────────────────────────────────────────

    /// <summary>1 lượt gọi viết câu trả lời: lịch sử hội thoại + tin nhắn cuối gồm ngữ cảnh và câu hỏi. Không kèm
    /// tools nên Gemini luôn trả văn bản.</summary>
    public static object BuildAnswerPayload(string instruction, List<ChatTurn> history, string question,
        List<RagChunk> chunks, int maxOutputTokens)
    {
        var context = string.Join("\n\n", chunks.Select(c => $"[{c.Title ?? "Tài liệu"}, trang {c.PageNumber}]\n{c.Content}"));
        var message = $"NGỮ CẢNH TRÍCH TỪ TÀI LIỆU:\n{context}\n\nCÂU HỎI: {question}";
        return new
        {
            system_instruction = new { parts = new[] { new { text = instruction } } },
            contents = ChatHistory.ToGeminiContents(history, message),
            generationConfig = GeminiGeneration.Config(0.3, maxOutputTokens)
        };
    }

    /// <summary>Nguồn trong 1 tài liệu: tối đa <paramref name="max"/> TRANG khác nhau (trước đây gộp còn 1 nguồn
    /// dù câu trả lời trích nhiều trang), sắp theo số trang.</summary>
    private async Task<List<ChatSource>> PageSourcesAsync(List<RagChunk> chunks, int max)
    {
        var best = chunks.GroupBy(c => c.PageNumber)
            .Select(g => g.OrderByDescending(c => c.Score).First())
            .OrderByDescending(c => c.Score).Take(max)
            .OrderBy(c => c.PageNumber).ToList();
        return await ToSourcesAsync(best);
    }

    /// <summary>Nguồn toàn kho: 1 nguồn mỗi tài liệu (đoạn điểm cao nhất).</summary>
    private async Task<List<ChatSource>> DocumentSourcesAsync(List<RagChunk> chunks) =>
        await ToSourcesAsync(chunks.GroupBy(c => c.EbookId).Select(g => g.OrderByDescending(c => c.Score).First()).ToList());

    private async Task<List<ChatSource>> ToSourcesAsync(List<RagChunk> chunks)
    {
        var fileIds = chunks.Where(c => c.EbookFileId.HasValue).Select(c => c.EbookFileId!.Value).Distinct().ToList();
        var filePublicIds = fileIds.Count > 0
            ? await db.EbookFiles.Where(f => fileIds.Contains(f.Id) && f.IsDelete != 2).ToDictionaryAsync(f => f.Id, f => f.PublicId)
            : new Dictionary<long, Guid>();

        return chunks.Select(c => new ChatSource
        {
            EbookId     = c.PublicId,
            EbookFileId = c.EbookFileId.HasValue && filePublicIds.TryGetValue(c.EbookFileId.Value, out var pid) ? pid : null,
            Title       = c.Title,
            Author      = c.Author,
            PageNumber  = c.PageNumber,
            Excerpt     = c.Content.Length > 200 ? c.Content[..200] + "…" : c.Content,
            Score       = c.Score
        }).ToList();
    }

    private static int CountWords(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static CancellationTokenSource LinkedTimeout(CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(LlmTimeout);
        return cts;
    }

    private static object[] BuildToolsPayload() =>
    [
        new
        {
            functionDeclarations = new object[]
            {
                new
                {
                    name = SearchDocumentsTool,
                    description = "Tìm nội dung trong kho tài liệu điện tử (ebook) của thư viện. Dùng khi câu hỏi liên quan đến " +
                                  "kiến thức/thông tin có trong sách. KHÔNG dùng cho chào hỏi hay câu ngoài phạm vi tài liệu.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            query = new { type = "STRING", description = "Câu/từ khóa cốt lõi để tìm, viết lại đầy đủ ý nếu là câu nối tiếp" }
                        },
                        required = new[] { "query" }
                    }
                }
            }
        }
    ];
}
