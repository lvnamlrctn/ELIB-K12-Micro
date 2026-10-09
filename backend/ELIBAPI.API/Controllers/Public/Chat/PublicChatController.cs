using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Chat;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

[Route("api/public/chat")]
public class PublicChatController(
    IChatService chat,
    IDocumentFinderService finder,
    IPublicPrintBookRepository printBookRepo,
    IMinioService minio,
    ELIBAPIDbContext db) : PublicBaseController
{
    /// <summary>
    /// Hỏi đáp theo NỘI DUNG bên trong tài liệu (RAG). Dùng cho khung chat trong trang đọc.
    /// Muốn tìm tài liệu thì dùng <see cref="FindDocuments"/> — đó là nhánh khác hẳn.
    /// </summary>
    [HttpPost("ask")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.ChatPublicPolicy)]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<string>.Fail("Question is required"));

        var result = await chat.AskAsync(request, await ResolveTenantAsync(request.TenantId), ct);
        return Ok(ApiResponse<ChatResponse>.Ok(result));
    }

    /// <summary>
    /// Như <see cref="Ask"/> nhưng trả dần qua Server-Sent Events (port ELIB-LRC 09-29): <c>sources</c> →
    /// nhiều <c>delta</c> (đoạn chữ) → <c>done</c>, hoặc <c>error</c>. Khung chat "Hỏi AI về tài liệu này" trong trang đọc dùng
    /// endpoint này để chữ hiện ra ngay khi AI bắt đầu viết.
    /// </summary>
    [HttpPost("ask-stream")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.ChatPublicPolicy)]
    public async Task AskStream([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(ApiResponse<string>.Fail("Question is required"), ct);
            return;
        }
        var tenantId = await ResolveTenantAsync(request.TenantId);
        await ELIBAPI.API.Infrastructure.ServerSentEvents.WriteAsync(Response, chat.AskStreamAsync(request, tenantId, ct), ct);
    }

    /// <summary>Đơn vị của câu hỏi: GUID client gửi (OPAC biết đơn vị đang chạy), không có thì đơn vị theo tên miền
    /// (TenantContextMiddleware). Trước đây thiếu tenantId là truy xuất nội dung của mọi đơn vị.</summary>
    private async Task<long?> ResolveTenantAsync(Guid? tenantPublicId)
    {
        if (tenantPublicId.HasValue && tenantPublicId != Guid.Empty)
        {
            var id = await db.Tenants.Where(t => t.PublicId == tenantPublicId.Value && t.IsDelete != 2)
                .Select(t => (long?)t.Id).FirstOrDefaultAsync();
            if (id != null) return id;
        }
        return HttpContext.Items.TryGetValue(ELIBAPI.Infrastructure.RateLimiting.TenantContextMiddleware.ItemKey, out var t) ? t as long? : null;
    }

    /// <summary>
    /// Trợ lý TÌM TÀI LIỆU cho nút chat nổi ngoài portal: hỏi bằng lời thường, nhận về câu dẫn ngắn
    /// kèm danh sách tài liệu bấm được.
    ///
    /// Chỉ tra mục lục thư mục, không đọc nội dung bên trong tài liệu — xem
    /// <c>DocumentFinderService</c> để biết ba chốt chặn giữ đúng ranh giới đó.
    /// </summary>
    [HttpPost("find-documents")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.ChatPublicPolicy)]
    public async Task<IActionResult> FindDocuments([FromBody] DocumentFinderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<string>.Fail("Question is required"));

        request.ResolvedTenantId = await ResolveTenantAsync(request.TenantId);

        // Bạn đọc chưa đăng nhập vẫn dùng chatbot bình thường -- chỉ là không có gợi ý theo hồ sơ.
        // Không nhận ReaderPublicId từ body: luôn ghi đè bằng claim JWT (hoặc null) để không thể giả mạo.
        request.ReaderPublicId = null;
        if (User.Identity!.IsAuthenticated && User.FindFirstValue("Type") == "Reader"
            && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId))
        {
            request.ReaderPublicId = readerPublicId;
        }

        var result = await finder.FindAsync(request);

        foreach (var doc in result.Documents)
            doc.Images = ResolveImageUrl(doc.Images);

        await ApplyRealtimeHoldingsAsync(result.Documents);

        return Ok(ApiResponse<DocumentFinderResponse>.Ok(result));
    }

    /// <summary>
    /// Ghi đè số bản của tài liệu in bằng số đếm thời gian thực — cùng nguồn với trang chi tiết OPAC
    /// (<see cref="IPublicPrintBookRepository.GetAvailabilityStatsAsync"/>): con số nằm trong index chỉ
    /// là ảnh chụp lúc đánh index, nên không dùng để hiển thị "còn/hết" được.
    /// </summary>
    private async Task ApplyRealtimeHoldingsAsync(List<DocumentFinderItem> docs)
    {
        var printDocs = docs.Where(d => d.DocType == "print" && d.BibId.HasValue).ToList();
        if (printDocs.Count == 0) return;

        var stats = await printBookRepo.GetAvailabilityStatsAsync(
            printDocs.Select(d => d.BibId!.Value).Distinct().ToList());

        foreach (var doc in printDocs)
        {
            var c = stats.TryGetValue(doc.BibId!.Value, out var s) ? s : default;
            doc.CopyCount      = c.CopyCount;
            doc.AvailableCount = c.AvailableCount;
        }
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{minio.PublicBaseUrl}/{value}";
}
