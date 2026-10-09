using System.Linq.Expressions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xếp job Hangfire qua <see cref="IBackgroundJobClient"/> lấy từ DI (port ELIB-LRC 09-30). Máy chủ không cấu hình
/// Hangfire (không có chuỗi kết nối) thì client không được đăng ký: trước đây lớp tĩnh <c>BackgroundJob.Enqueue</c> ném lỗi
/// SAU KHI đã lưu biểu ghi, nên lưu thành công mà vẫn trả 500. Nay chỉ bỏ qua việc xếp job và ghi cảnh báo.</summary>
public static class BackgroundJobs
{
    /// <returns>Id job, hoặc null khi Hangfire không bật / xếp job lỗi.</returns>
    public static string? TryEnqueue<T>(IServiceProvider services, Expression<Func<T, Task>> job, ILogger? logger = null)
    {
        var client = services.GetService<IBackgroundJobClient>();
        if (client == null)
        {
            logger?.LogWarning("Hangfire chưa bật — bỏ qua job {Job}", job.Body);
            return null;
        }
        try
        {
            return client.Enqueue(job);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không xếp được job {Job}", job.Body);
            return null;
        }
    }
}
