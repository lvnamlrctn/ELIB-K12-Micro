using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Chat;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ELIBAPI.API.Controllers.Admin;

[Authorize]
[ApiController]
[Route("api/Cms/AdminStatChat")]
public class AdminStatChatController(
    IAdminStatChatService statChatService,
    ILogger<AdminStatChatController> logger) : ControllerBase
{
    private const string GenericError = "Hệ thống tạm thời không xử lý được câu hỏi thống kê. Vui lòng thử lại.";

    /// <summary>
    /// Tra cứu số liệu thống kê thư viện bằng AI Chatbot dành riêng cho cán bộ quản trị Admin.
    /// Kiểm tra quyền truy cập module của tài khoản đăng nhập trước khi thu thập số liệu.
    /// </summary>
    [HttpPost("ask")]
    [Permission("STAT_CHAT", "view")]
    [EnableRateLimiting(RateLimitingExtensions.ChatAdminPolicy)]
    public async Task<IActionResult> Ask([FromBody] AdminStatChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<string>.Fail("Question is required"));

        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<string>.Fail("Invalid user claims"));

        try
        {
            var result = await statChatService.AskStatsAsync(userId, GetTenantId(), request.Question, request.History, ct);
            return Ok(ApiResponse<AdminStatChatResponse>.Ok(result));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Không trả ex.Message ra client (lộ chi tiết nội bộ) — chỉ ghi log.
            logger.LogError(ex, "Error processing AdminStatChat for userId {UserId}", userId);
            return StatusCode(500, ApiResponse<string>.Fail(GenericError));
        }
    }

    /// <summary>Như <see cref="Ask"/> nhưng trả dần qua Server-Sent Events: nhiều <c>delta</c> → <c>done</c> (hoặc <c>error</c>).</summary>
    [HttpPost("ask-stream")]
    [Permission("STAT_CHAT", "view")]
    [EnableRateLimiting(RateLimitingExtensions.ChatAdminPolicy)]
    public async Task AskStream([FromBody] AdminStatChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(ApiResponse<string>.Fail("Question is required"), ct);
            return;
        }
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(ApiResponse<string>.Fail("Invalid user claims"), ct);
            return;
        }
        await ServerSentEvents.WriteAsync(Response, Guard(statChatService.AskStatsStreamAsync(userId, GetTenantId(), request.Question, request.History, ct), userId, ct), ct);
    }

    /// <summary>Đơn vị của người hỏi (JWT) — số liệu chỉ trong đơn vị này; tài khoản hệ thống (không đơn vị) xem toàn bộ.</summary>
    private long? GetTenantId() => long.TryParse(User.FindFirst("TenantId")?.Value, out var id) ? id : null;

    /// <summary>Lỗi phát sinh khi đang chuẩn bị (quyền, số liệu) → sự kiện "error" chung, không lộ chi tiết.</summary>
    private async IAsyncEnumerable<ChatStreamEvent> Guard(IAsyncEnumerable<ChatStreamEvent> source, long userId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var e = source.GetAsyncEnumerator(ct);
        var failed = false;
        while (true)
        {
            bool has;
            try { has = await e.MoveNextAsync(); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error streaming AdminStatChat for userId {UserId}", userId);
                failed = true;
                break;
            }
            if (!has) break;
            yield return e.Current;
        }
        if (failed) yield return ChatStreamEvent.Error(GenericError);
    }
}
