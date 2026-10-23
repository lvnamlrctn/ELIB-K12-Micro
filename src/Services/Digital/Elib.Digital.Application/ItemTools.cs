using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Elib.BuildingBlocks.Crud;

namespace Elib.Digital.Application;

public sealed record SuggestClassificationRequest(string? Title, string? Description = null);

/// <summary>Công cụ hỗ trợ nhập tài liệu: tra ISBN, gợi ý phân loại bằng AI, khả năng tích hợp đang bật (LRC: tra ISBN, "Gợi ý AI" trên form tài liệu số).</summary>
public sealed class EbookItemTools(ICrudDbContext db, IIsbnLookup isbn, IClassificationAi ai, ITextEmbedder embedder)
{
    public ItemCapabilities Capabilities() => new(Isbn: true, Ai: ai.Enabled, Content: true, Embedding: embedder.Enabled);

    public async Task<IsbnMetadata> LookupIsbnAsync(string? value, CancellationToken ct) =>
        await isbn.FindAsync(value ?? "", ct) ?? throw new NotFoundException("ISBN", value ?? "");

    public async Task<ClassificationSuggestion> SuggestAsync(SuggestClassificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ai.Enabled) throw new BusinessRuleException("AI_DISABLED", "Chưa bật gợi ý AI (chưa cấu hình khóa dịch vụ).", 503);
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("TITLE_INVALID", "Nhập nhan đề trước khi nhờ AI gợi ý.");
        var subjects = await db.Set<EbookSubject>().AsNoTracking().Where(x => x.Status == IHasStatus.Active).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new ClassificationOption(x.Id, x.Name)).ToListAsync(ct);
        var topics = await db.Set<EbookTopic>().AsNoTracking().Where(x => x.Status == IHasStatus.Active).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new ClassificationOption(x.Id, x.Name)).ToListAsync(ct);
        return await ai.SuggestAsync(request.Title, request.Description, subjects, topics, ct)
            ?? throw new BusinessRuleException("AI_UNAVAILABLE", "AI chưa trả được gợi ý lúc này — thử lại sau hoặc nhập tay.", 503);
    }
}
