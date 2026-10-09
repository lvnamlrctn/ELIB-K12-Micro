using System.Text.Json;
using ELIBAPI.Core.DTOs.Chat;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Trợ lý TÌM TÀI LIỆU — nhánh song song với <see cref="ChatService"/> (RAG nội dung trang sách).
///
/// Ranh giới giữa hai nhánh, và lý do phải là hai nhánh:
/// <list type="bullet">
/// <item><see cref="ChatService"/> trả lời câu hỏi <i>bên trong</i> tài liệu — embedding + kNN trên
/// index chunk nội dung, câu trả lời do LLM tổng hợp từ văn bản trang sách.</item>
/// <item>Lớp này chỉ dẫn người dùng <i>đến</i> tài liệu — LLM làm đúng một việc là dịch câu nói tự
/// nhiên thành bộ tiêu chí thư mục, phần tìm do Elasticsearch trên index metadata đảm nhiệm, và
/// câu trả lời được ghép bằng mã nguồn chứ không phải do LLM viết.</item>
/// </list>
///
/// Ba chốt chặn để nội dung sách không lọt vào đường này:
/// <list type="number">
/// <item>Dùng <see cref="UnifiedSearchRequest.MetaQ"/> chứ tuyệt đối không dùng
/// <see cref="UnifiedSearchRequest.Q"/> — <c>Q</c> có <c>content</c> trong danh sách trường.</item>
/// <item>Không bao giờ gán <see cref="UnifiedSearchRequest.Content"/>.</item>
/// <item>Câu dẫn trả về do mã nguồn sinh từ số liệu tìm được, nên LLM không có cơ hội chèn nội dung
/// tự bịa hay trích đoạn sách vào câu trả lời.</item>
/// </list>
/// </summary>
public class DocumentFinderService(
    IElasticsearchService elastic,
    IGeminiClient gemini,
    ELIBAPIDbContext db,
    IMemoryCache cache,
    ILogger<DocumentFinderService> logger) : IDocumentFinderService
{
    private const string ExtractTool = "find_documents";

    /// <summary>Tiêu chí bóc được cho cùng 1 câu hỏi (cùng lịch sử, cùng đơn vị) được dùng lại trong khoảng này — câu hỏi
    /// lặp lại (gợi ý mẫu, nhiều bạn đọc hỏi giống nhau) không phải gọi lại Gemini (port ELIB-LRC 09-29).</summary>
    private static readonly TimeSpan CriteriaCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CatalogCacheTtl  = TimeSpan.FromMinutes(10);
    /// <summary>Chat tìm tài liệu chỉ cần hiểu câu nối tiếp gần nhất — 4 lượt là đủ, gửi nhiều hơn chỉ tốn token.</summary>
    private const int HistoryTurns = 4;

    private const string SystemInstruction =
        "Bạn là bộ phân tích truy vấn của một mục lục thư viện. Nhiệm vụ DUY NHẤT của bạn là chuyển " +
        "câu nói của người dùng thành bộ tiêu chí tra cứu THƯ MỤC, bằng cách gọi công cụ find_documents. " +
        "Bạn KHÔNG trả lời câu hỏi, KHÔNG tóm tắt, KHÔNG suy đoán nội dung bên trong sách. " +
        "Quy tắc bóc tách: " +
        "(1) 'keyword' là chủ đề/lĩnh vực cốt lõi, viết lại thật ngắn bằng danh từ — bỏ hết các cụm " +
        "rào đón như 'cho tôi hỏi', 'tìm giúp', 'có sách nào về', 'không'. " +
        "(2) Chỉ điền 'title' khi người dùng nêu rõ một nhan đề cụ thể; chỉ điền 'author' khi nêu rõ " +
        "tên người viết — đừng nhét chủ đề vào hai trường này. " +
        "(3) 'docType' chỉ điền khi người dùng nói rõ muốn sách in ('print') hay tài liệu số/ebook " +
        "('digital'). " +
        "(4) Năm: 'từ 2020' → publishYearFrom=2020; 'trước 2010' → publishYearTo=2010; 'năm 2015' → " +
        "cả hai bằng 2015; 'mới nhất/gần đây' → sortBy='newest'. " +
        "(5) 'availableOnly'=true khi người dùng hỏi sách còn bản để mượn. " +
        "(6) Loại hình ('luận án', 'giáo trình', 'từ điển') và ngôn ngữ ('tiếng Anh') thì gộp thẳng " +
        "vào 'keyword', đừng tạo trường riêng. " +
        "(7) 'topic' chỉ điền khi người dùng muốn tài liệu về một chủ đề/lĩnh vực/môn học cụ thể để " +
        "gợi ý (không phải tìm đúng 1 cuốn theo tên) -- ví dụ 'lập trình web', 'kinh tế vĩ mô'. " +
        "(8) 'wantsPersonalRecommendation'=true CHỈ khi người dùng xin gợi ý PHÙ HỢP VỚI HỌ theo hồ " +
        "sơ/môn học/ngành học của chính họ mà KHÔNG nêu chủ đề cụ thể nào (vd: 'gợi ý tài liệu cho " +
        "tôi', 'có gì hợp với ngành tôi học không', 'gợi ý theo môn học của tôi'). Nếu họ đã nêu rõ " +
        "chủ đề thì dùng 'topic', đừng bật cả hai. " +
        "Trường nào không chắc chắn thì BỎ TRỐNG, đừng đoán bừa. " +
        "(9) Nếu có các lượt hội thoại trước, câu hiện tại có thể là câu nối tiếp (vd 'chỉ bản từ 2020', " +
        "'còn sách in không', 'của tác giả khác') — giữ lại chủ đề/tiêu chí của lượt trước và áp thêm yêu cầu mới.";

    public async Task<DocumentFinderResponse> FindAsync(DocumentFinderRequest request)
    {
        var question = (request.Question ?? "").Trim();
        if (question.Length == 0)
            return new DocumentFinderResponse { Answer = "Bạn muốn tìm tài liệu về chủ đề gì?" };

        var pageSize = Math.Clamp(request.PageSize, 1, 20);

        // Đơn vị do controller phân giải (GUID client gửi, không có thì theo tên miền).
        var tenantId = request.ResolvedTenantId;

        var criteria = await ExtractCriteriaCachedAsync(question, request.History, tenantId);

        // Ràng buộc của portal thắng mọi thứ LLM bóc ra: widget nhúng ở trang bộ sưu tập thì phải
        // ở trong bộ sưu tập đó, người dùng không "nói" cho mình ra ngoài được.
        if (!string.IsNullOrWhiteSpace(request.DocType))
            criteria.DocType = request.DocType;

        await ResolveTopicSubjectAsync(criteria, request.ReaderPublicId, tenantId);

        // Xin gợi ý theo hồ sơ nhưng bạn đọc chưa khai chủ đề/môn học nào -- trả lời sớm, không tìm
        // mò theo nguyên câu hỏi (sẽ ra kết quả không liên quan gì tới "phù hợp với tôi").
        if (criteria.WantsPersonalRecommendation && criteria.TopicId is null && criteria.SubjectId is null)
        {
            return new DocumentFinderResponse
            {
                Answer = "Bạn chưa khai chủ đề/môn học quan tâm trong hồ sơ. Vào trang Hồ sơ để " +
                         "chọn nhé, hoặc cứ nói rõ chủ đề bạn cần mình tìm giúp.",
                Criteria = criteria,
                Total = 0,
                Documents = []
            };
        }

        var result = await SearchAsync(criteria, request.CollectionId, pageSize, tenantId);

        // Nới lỏng 1 lần khi bộ lọc quá chặt: giữ lại từ khóa chủ đề, bỏ các ràng buộc phụ mà LLM
        // suy ra (năm, ngôn ngữ, loại hình, còn bản). Không kết quả vì đoán sai bộ lọc thì tệ hơn
        // nhiều so với trả về đúng chủ đề nhưng rộng hơn một chút.
        var relaxed = false;
        if (result.Total == 0 && HasNarrowingFilter(criteria))
        {
            // Giữ nguyên Keyword kể cả khi nó null: khi LLM chỉ bóc được tên tác giả thì truy vấn
            // "mọi tài liệu của tác giả này" là đúng ý người dùng. Lấy nguyên câu hỏi thô làm từ
            // khóa thay thế sẽ ném cả "cho tôi hỏi", "có… không" vào truy vấn — vừa nhiễu kết quả
            // vừa lòi ra trong câu dẫn ("…về "có luận án nào của X không"").
            var loose = new DocumentFinderCriteria
            {
                Keyword     = criteria.Keyword,
                Author      = criteria.Author,
                Title       = criteria.Title,
                DocType     = request.DocType,          // ràng buộc portal thì vẫn giữ
                // Giữ nguyên chủ đề/môn học đã resolve -- nới các ràng buộc phụ (năm, ngôn ngữ...)
                // chứ không nới luôn tiêu chí "đúng chủ đề" mà gợi ý theo hồ sơ dựa vào.
                TopicId     = criteria.TopicId,
                SubjectId   = criteria.SubjectId,
                TopicName   = criteria.TopicName,
                SubjectName = criteria.SubjectName,
            };
            var second = await SearchAsync(loose, request.CollectionId, pageSize, tenantId);
            if (second.Total > 0)
            {
                result   = second;
                criteria = loose;
                relaxed  = true;
            }
        }

        var items = result.Items.Select(ToItem).ToList();

        return new DocumentFinderResponse
        {
            Answer    = BuildAnswer(criteria, result.Total, items.Count, relaxed),
            Criteria  = criteria,
            Total     = result.Total,
            Documents = items
        };
    }

    /// <summary>
    /// Nhờ Gemini bóc câu hỏi thành tiêu chí. Mọi trục trặc của LLM đều suy giảm về việc dùng nguyên
    /// câu hỏi làm từ khóa — chatbot tra cứu không được phép chết chỉ vì nhà cung cấp LLM bận.
    /// </summary>
    private async Task<DocumentFinderCriteria> ExtractCriteriaCachedAsync(string question, List<ChatTurn>? history, long? tenantId)
    {
        // Tiêu chí có TopicId/SubjectId đã khớp theo danh mục của đơn vị → khoá cache phải có đơn vị.
        var key = $"finder:criteria:{tenantId}:" + ChatHistory.NormalizeQuestion(question) + "|" + ChatHistory.Fingerprint(ChatHistory.Normalize(history, HistoryTurns));
        if (cache.TryGetValue(key, out DocumentFinderCriteria? cached) && cached != null)
            return Copy(cached);

        var (criteria, fromLlm) = await ExtractCriteriaAsync(question, history, tenantId);
        // Chỉ cache kết quả Gemini thật — lượt suy giảm (Gemini lỗi) phải được thử lại ở câu hỏi sau.
        if (fromLlm) cache.Set(key, Copy(criteria), CriteriaCacheTtl);
        return criteria;
    }

    private static DocumentFinderCriteria Copy(DocumentFinderCriteria c) =>
        JsonSerializer.Deserialize<DocumentFinderCriteria>(JsonSerializer.Serialize(c))!;

    /// <summary>Cụm rào đón/từ đệm bỏ khỏi câu hỏi khi phải dùng chính câu hỏi làm từ khóa (Gemini lỗi).</summary>
    private static readonly string[] FillerPhrases =
    [
        "cho tôi hỏi", "cho mình hỏi", "cho em hỏi", "tôi muốn tìm", "mình muốn tìm", "em muốn tìm", "tìm giúp tôi",
        "tìm giúp mình", "tìm giúp", "tìm cho tôi", "tìm cho mình", "tìm kiếm", "có sách nào về", "có tài liệu nào về",
        "có sách nào", "có tài liệu nào", "sách về", "tài liệu về", "giáo trình về", "thư viện có", "không ạ", "không vậy",
        "được không", "ạ", "nhé", "làm ơn", "xin", "tìm", "gợi ý"
    ];

    /// <summary>Bỏ cụm rào đón để còn lại chủ đề cốt lõi ("cho tôi hỏi có sách nào về kinh tế vĩ mô không"
    /// → "kinh tế vĩ mô"). Bỏ hết mà rỗng thì giữ nguyên câu hỏi.</summary>
    public static string StripFillers(string question)
    {
        var s = " " + System.Text.RegularExpressions.Regex.Replace(question.ToLowerInvariant(), @"[?!.,;:""“”]", " ") + " ";
        foreach (var phrase in FillerPhrases)
            s = s.Replace(" " + phrase + " ", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        // "không" ở CUỐI câu là từ hỏi ("có sách X không") — ở giữa có thể là nội dung ("hình học không gian").
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+(không|chưa)$", "").Trim();
        return s.Length > 0 ? s : question;
    }

    private async Task<(DocumentFinderCriteria Criteria, bool FromLlm)> ExtractCriteriaAsync(string question, List<ChatTurn>? history, long? tenantId)
    {
        var fallback = new DocumentFinderCriteria { Keyword = StripFillers(question) };

        JsonElement part;
        try
        {
            part = await gemini.GenerateAsync(new
            {
                system_instruction = new { parts = new[] { new { text = SystemInstruction } } },
                contents = ChatHistory.ToGeminiContents(history, question, HistoryTurns),
                // Bóc tiêu chí là việc tất định: temperature 0, output rất ngắn, không cần "suy nghĩ".
                generationConfig = GeminiGeneration.Config(0, 300),
                tools = BuildToolsPayload(),
                // mode = ANY buộc Gemini phải gọi hàm thay vì trả lời bằng văn xuôi. Đây là điểm
                // then chốt khiến nhánh này không thể biến thành chatbot trả lời tự do.
                tool_config = new
                {
                    function_calling_config = new
                    {
                        mode = "ANY",
                        allowed_function_names = new[] { ExtractTool }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bóc tiêu chí tìm tài liệu bằng Gemini thất bại — dùng câu hỏi (đã bỏ từ đệm) làm từ khóa");
            return (fallback, false);
        }

        if (!part.TryGetProperty("functionCall", out var call) ||
            call.GetProperty("name").GetString() != ExtractTool ||
            !call.TryGetProperty("args", out var args) ||
            args.ValueKind != JsonValueKind.Object)
            return (fallback, false);

        var criteria = new DocumentFinderCriteria
        {
            Keyword         = Str(args, "keyword"),
            Title           = Str(args, "title"),
            Author          = Str(args, "author"),
            Publisher       = Str(args, "publisher"),
            DocType         = Enum(args, "docType",  ["print", "digital"]),
            // Language/MaterialType cố ý KHÔNG nhờ LLM bóc, dù DTO có chỗ chứa. Hai trường này
            // trong index là `keyword` (lọc khớp tuyệt đối) trên một bộ từ vựng vừa riêng của kho
            // vừa không sạch — ngôn ngữ có cả "vie", "Vie", "en_US", "Eng"; loại hình có
            // "Luận Án- Luận Văn", "Thesis", "Sách". LLM không thể đoán trúng chuỗi chính xác, nên
            // mọi giá trị nó sinh ra gần như chắc chắn cho ra 0 kết quả. Thay vào đó system prompt
            // yêu cầu gộp các ý này vào `keyword` để chúng đi qua MetaQ — đường đó khớp trên
            // title/keyword/summary/allMarcText đã phân tích, nơi biểu ghi luận án thực sự có chữ
            // "luận án". Hai trường vẫn nằm trong DTO cho bên gọi tự đặt khi biết chắc giá trị.
            PublishYearFrom = Year(args, "publishYearFrom"),
            PublishYearTo   = Year(args, "publishYearTo"),
            AvailableOnly   = Bool(args, "availableOnly"),
            SortBy          = Enum(args, "sortBy", ["relevance", "newest", "oldest", "title"]),
            WantsPersonalRecommendation = Bool(args, "wantsPersonalRecommendation") == true,
        };

        var topic = Str(args, "topic");

        // Chủ đề/môn học tự do LLM bóc được — resolve gần đúng theo tên thật trong danh mục
        // EbookTopic/EbookSubject TRƯỚC, để biết chắc có nên nhét topic vào Keyword hay không.
        var topicResolved = topic != null && await ResolveFreeTextTopicAsync(criteria, topic, tenantId);

        // LLM có thể gọi hàm với args rỗng khi câu hỏi mơ hồ, hoặc bóc được `topic` nhưng topic đó
        // không khớp danh mục thật nào (topicResolved=false) — cả 2 trường hợp vẫn phải có gì đó để
        // tìm. Không áp dụng khi người dùng xin gợi ý theo hồ sơ — nhánh đó tự có tiêu chí riêng
        // (topicId/subjectId), nhét văn bản vào keyword sẽ chỉ gây nhiễu.
        if (criteria.Keyword is null && criteria.Title is null && criteria.Author is null
            && !topicResolved && !criteria.WantsPersonalRecommendation)
            criteria.Keyword = topic ?? StripFillers(question);

        return (criteria, true);
    }

    /// <summary>Khớp gần đúng 1 cụm chủ đề tự do (LLM bóc từ câu nói) với danh mục thật —
    /// EbookTopic trước (chủ đề), EbookSubject sau (môn học/lĩnh vực). So khớp 2 chiều (chứa nhau)
    /// vì tên danh mục trong DB thường NGẮN hơn cụm từ tự nhiên của LLM (vd DB "Tin học" còn LLM bóc
    /// "tin học cơ bản") — chỉ so 1 chiều `Name.Contains(needle)` sẽ gần như luôn thất bại với chủ đề
    /// nhiều từ. Trả về true nếu resolve được, để nơi gọi biết còn phải fallback Keyword hay không.
    /// Chỉ khớp trong đúng tenant (hoặc danh mục dùng chung TenantId=null) -- danh mục của tenant
    /// khác không được lẫn vào gợi ý.</summary>
    private async Task<bool> ResolveFreeTextTopicAsync(DocumentFinderCriteria criteria, string topic, long? tenantId)
    {
        var needle = topic.Trim().ToLower();
        if (needle.Length == 0) return false;

        // So khớp 2 chiều (contains-nhau) không dịch được gọn sang SQL LIKE khi vế cần "chứa" lại là
        // cột (không phải hằng số) — nên tải danh mục (bảng nhỏ, vài chục-vài trăm dòng) về rồi so
        // khớp phía C#, thay vì cố ép EF dịch biểu thức ngược `needle.Contains(x.Name)`.
        // Danh mục ít thay đổi → cache vài phút theo đơn vị thay vì tải lại cả 2 bảng ở mọi câu hỏi.
        var topics = await cache.GetOrCreateAsync($"finder:topics:{tenantId}", e =>
        {
            e.AbsoluteExpirationRelativeToNow = CatalogCacheTtl;
            return db.EbookTopics.AsNoTracking()
                .Where(x => x.IsDelete != 2 && x.Status == 2 && x.Name != null
                    && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
                .Select(x => new CatalogName(x.Id, x.Name!))
                .ToListAsync();
        }) ?? [];
        var t = BestOverlap(needle, topics);
        if (t != null)
        {
            criteria.TopicId   = t.Id;
            criteria.TopicName = t.Name;
            return true;
        }

        var subjects = await cache.GetOrCreateAsync($"finder:subjects:{tenantId}", e =>
        {
            e.AbsoluteExpirationRelativeToNow = CatalogCacheTtl;
            return db.EbookSubjects.AsNoTracking()
                .Where(x => x.IsDelete != 2 && x.Status == 2 && x.Name != null
                    && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
                .Select(x => new CatalogName(x.Id, x.Name!))
                .ToListAsync();
        }) ?? [];
        var s = BestOverlap(needle, subjects);
        if (s != null)
        {
            criteria.SubjectId   = s.Id;
            criteria.SubjectName = s.Name;
            return true;
        }

        return false;
    }

    private static bool Overlaps(string needle, string name) => name.Contains(needle) || needle.Contains(name);

    private sealed record CatalogName(long Id, string Name);

    /// <summary>Tên danh mục khớp gần đúng nhất: ưu tiên trùng khớp hoàn toàn, rồi tên dài nhất (cụ thể nhất) —
    /// tránh tên rất ngắn (vd "Tin") chiếm chỗ của "Tin học ứng dụng" chỉ vì đứng trước trong danh sách.</summary>
    private static CatalogName? BestOverlap(string needle, List<CatalogName> names) =>
        names.Where(x => Overlaps(needle, x.Name.ToLower()))
             .OrderByDescending(x => x.Name.Equals(needle, StringComparison.OrdinalIgnoreCase))
             .ThenByDescending(x => x.Name.Length)
             .FirstOrDefault();

    /// <summary>Gợi ý theo hồ sơ: nếu người dùng xin gợi ý cho chính họ và có ReaderPublicId, tra
    /// dbo.ReaderPreference, ưu tiên hơn giá trị suy từ 'topic' tự do (ý định "theo hồ sơ của tôi"
    /// tường minh hơn). Không làm gì nếu không xin gợi ý cá nhân hoặc bạn đọc chưa đăng nhập.</summary>
    private async Task ResolveTopicSubjectAsync(DocumentFinderCriteria criteria, Guid? readerPublicId, long? tenantId)
    {
        if (!criteria.WantsPersonalRecommendation || readerPublicId is null) return;

        var pref = await (
            from p in db.ReaderPreferences
            join r in db.Readers on p.ReaderId equals r.Id
            where r.PublicId == readerPublicId.Value && r.IsDelete != 2
            select new { p.SubjectId, p.TopicId })
            .FirstOrDefaultAsync();
        if (pref == null) return;

        if (pref.TopicId != null)
        {
            criteria.TopicId   = pref.TopicId;
            criteria.TopicName = await db.EbookTopics
                .Where(x => x.Id == pref.TopicId && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
                .Select(x => x.Name).FirstOrDefaultAsync();
        }
        if (pref.SubjectId != null)
        {
            criteria.SubjectId   = pref.SubjectId;
            criteria.SubjectName = await db.EbookSubjects
                .Where(x => x.Id == pref.SubjectId && (tenantId == null || x.TenantId == tenantId || x.TenantId == null))
                .Select(x => x.Name).FirstOrDefaultAsync();
        }
    }

    private Task<UnifiedSearchResponse> SearchAsync(
        DocumentFinderCriteria c, string? collectionId, int pageSize, long? tenantId) =>
        elastic.SearchUnifiedAsync(new UnifiedSearchRequest
        {
            // MetaQ chứ KHÔNG phải Q: xem chú thích đầu lớp, chốt chặn số 1.
            MetaQ           = c.Keyword,
            Title           = c.Title,
            Author          = c.Author,
            Publisher       = c.Publisher,
            DocType         = c.DocType,
            Language        = c.Language,
            MaterialType    = c.MaterialType,
            PublishYearFrom = c.PublishYearFrom,
            PublishYearTo   = c.PublishYearTo,
            AvailableOnly   = c.AvailableOnly,
            CollectionId    = collectionId,
            ResolvedTenantId = tenantId,
            // Trước đây TopicId/SubjectId (gợi ý theo hồ sơ/chủ đề) không được đưa xuống tìm kiếm → kết quả không lọc.
            TopicId         = c.TopicId?.ToString(),
            SubjectId       = c.SubjectId?.ToString(),
            SortBy          = c.SortBy is "newest" or "oldest" or "title" ? c.SortBy : null,
            Page            = 1,
            PageSize        = pageSize,
            // Chat chỉ hiện danh sách + tổng số: bỏ facet/highlight/dfs cho nhanh.
            LightMode       = true,
            // Content cố ý bỏ trống — chốt chặn số 2.
        });

    private static bool HasNarrowingFilter(DocumentFinderCriteria c) =>
        c.PublishYearFrom.HasValue || c.PublishYearTo.HasValue ||
        c.AvailableOnly == true ||
        !string.IsNullOrWhiteSpace(c.Language) ||
        !string.IsNullOrWhiteSpace(c.MaterialType) ||
        !string.IsNullOrWhiteSpace(c.Publisher);

    private static DocumentFinderItem ToItem(UnifiedSearchItem i) => new()
    {
        DocType     = i.DocType,
        PublicId    = i.PublicId,
        Title       = i.Title,
        Author      = i.Author,
        Publisher   = i.Publisher,
        PublishYear = i.PublishYear,
        Images      = i.Images,
        Language    = i.Language,
        Keyword     = i.Keyword,
        EbookId     = i.EbookId,
        Free        = i.Free,
        BibId       = i.BibId,
        CopyCount   = i.CopyCount,
        AvailableCount = i.AvailableCount,
    };

    /// <summary>
    /// Câu dẫn do mã nguồn ghép từ số liệu thật (chốt chặn số 3). Cố ý không nhờ LLM viết: một câu
    /// dẫn do LLM sinh ra có thể mô tả sai số lượng, hoặc tệ hơn là "trả lời" luôn câu hỏi bằng kiến
    /// thức nền của nó — đúng thứ nhánh này sinh ra để tránh.
    /// </summary>
    private static string BuildAnswer(DocumentFinderCriteria c, long total, int shown, bool relaxed)
    {
        var what = Describe(c);

        if (total == 0)
            return $"Không tìm thấy tài liệu nào{what} trong kho. Bạn thử nêu từ khóa khác, "
                 + "hoặc cho tôi biết tên tác giả / nhan đề cụ thể nhé.";

        var head = relaxed
            ? $"Không có tài liệu nào khớp đủ mọi điều kiện, nên tôi nới bớt bộ lọc. Tìm thấy {total} tài liệu{what}"
            : $"Tìm thấy {total} tài liệu{what}";

        return total > shown
            ? $"{head}. Dưới đây là {shown} tài liệu phù hợp nhất:"
            : $"{head}:";
    }

    private static string Describe(DocumentFinderCriteria c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Title))   parts.Add($"có nhan đề “{c.Title}”");
        if (!string.IsNullOrWhiteSpace(c.Author))  parts.Add($"của {c.Author}");
        if (!string.IsNullOrWhiteSpace(c.Keyword)) parts.Add($"về “{c.Keyword}”");
        if (!string.IsNullOrWhiteSpace(c.TopicName))   parts.Add($"về chủ đề “{c.TopicName}”");
        if (!string.IsNullOrWhiteSpace(c.SubjectName)) parts.Add($"thuộc môn học “{c.SubjectName}”");

        if (c.PublishYearFrom.HasValue && c.PublishYearFrom == c.PublishYearTo)
            parts.Add($"xuất bản năm {c.PublishYearFrom}");
        else if (c.PublishYearFrom.HasValue && c.PublishYearTo.HasValue)
            parts.Add($"xuất bản {c.PublishYearFrom}–{c.PublishYearTo}");
        else if (c.PublishYearFrom.HasValue) parts.Add($"xuất bản từ năm {c.PublishYearFrom}");
        else if (c.PublishYearTo.HasValue)   parts.Add($"xuất bản trước năm {c.PublishYearTo}");

        if (c.DocType == "print")   parts.Add("dạng sách in");
        if (c.DocType == "digital") parts.Add("dạng tài liệu số");
        if (c.AvailableOnly == true) parts.Add("còn bản cho mượn");

        // Nói rõ đang xếp theo gì: khi sắp xếp theo năm, thứ tự KHÔNG còn theo độ liên quan nữa,
        // nên tài liệu đầu danh sách có thể không phải tài liệu sát chủ đề nhất.
        if (c.SortBy == "newest") parts.Add("xếp theo năm mới nhất");
        if (c.SortBy == "oldest") parts.Add("xếp theo năm cũ nhất");

        return parts.Count == 0 ? "" : " " + string.Join(", ", parts);
    }

    private static object[] BuildToolsPayload() =>
    [
        new
        {
            functionDeclarations = new object[]
            {
                new
                {
                    name = ExtractTool,
                    description = "Tra cứu MỤC LỤC thư viện theo tiêu chí thư mục (nhan đề, tác giả, chủ đề, " +
                                  "năm xuất bản…). Công cụ này KHÔNG đọc nội dung bên trong sách — nó chỉ trả về " +
                                  "danh sách tài liệu khớp mô tả.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            keyword         = new { type = "STRING", description = "Chủ đề/lĩnh vực cốt lõi, viết lại ngắn gọn bằng danh từ" },
                            title           = new { type = "STRING", description = "Nhan đề cụ thể, chỉ điền khi người dùng nêu rõ" },
                            author          = new { type = "STRING", description = "Tên tác giả, chỉ điền khi người dùng nêu rõ" },
                            publisher       = new { type = "STRING", description = "Nhà xuất bản, chỉ điền khi người dùng nêu rõ" },
                            docType         = new { type = "STRING", @enum = new[] { "print", "digital" }, description = "'print' = sách in, 'digital' = tài liệu số/ebook" },
                            publishYearFrom = new { type = "INTEGER", description = "Năm xuất bản sớm nhất" },
                            publishYearTo   = new { type = "INTEGER", description = "Năm xuất bản muộn nhất" },
                            availableOnly   = new { type = "BOOLEAN", description = "true khi người dùng chỉ muốn tài liệu in còn bản cho mượn" },
                            sortBy          = new { type = "STRING", @enum = new[] { "relevance", "newest", "oldest", "title" }, description = "'newest' khi người dùng hỏi tài liệu mới nhất" },
                            topic           = new { type = "STRING", description = "Chủ đề/môn học người dùng nói tới khi họ muốn tài liệu về một lĩnh vực cụ thể (vd 'lập trình web', 'kinh tế vĩ mô')" },
                            wantsPersonalRecommendation = new { type = "BOOLEAN", description = "true CHỈ khi người dùng xin gợi ý PHÙ HỢP VỚI HỌ theo hồ sơ/môn học/ngành học của chính họ mà KHÔNG nêu chủ đề cụ thể (vd 'gợi ý tài liệu cho tôi', 'có gì hợp với ngành tôi học không')" }
                        },
                        required = Array.Empty<string>()
                    }
                }
            }
        }
    ];

    // ── Bóc giá trị từ args của functionCall ─────────────────────────────────
    // Gemini không đảm bảo kiểu: một trường khai INTEGER vẫn có thể về dạng chuỗi "2020".
    // Các hàm dưới nhận cả hai dạng và trả null thay vì ném khi gặp thứ không hiểu được.

    private static string? Str(JsonElement args, string name) =>
        args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static string? Enum(JsonElement args, string name, string[] allowed)
    {
        var value = Str(args, name)?.ToLowerInvariant();
        return value is not null && allowed.Contains(value) ? value : null;
    }

    private static int? Year(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var v)) return null;
        var year = v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var n) => n,
            _ => 0
        };
        return year is >= 1000 and <= 2200 ? year : null;
    }

    private static bool? Bool(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
            _ => null
        };
    }
}
