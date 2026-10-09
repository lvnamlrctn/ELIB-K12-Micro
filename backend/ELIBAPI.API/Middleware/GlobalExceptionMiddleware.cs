using System.Text.Json;
using ELIBAPI.API;
using ELIBAPI.Core.Common;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _next      = next;
        _logger    = logger;
        _localizer = localizer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (KeyNotFoundException ex)
        {
            var msg = ex.Message == "NotFound" ? _localizer["NotFound"].Value : ex.Message;
            await WriteErrorAsync(context, 404, msg);
        }
        catch (UnauthorizedAccessException ex)
        {
            var msg = ex.Message == "ForbiddenDepartment"
                ? _localizer["ForbiddenDepartment"].Value
                : _localizer["Forbidden"].Value;
            await WriteErrorAsync(context, 403, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, _localizer["InternalError"].Value);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(ApiResponse<object>.Fail(message, statusCode));
        await context.Response.WriteAsync(body);
    }
}
