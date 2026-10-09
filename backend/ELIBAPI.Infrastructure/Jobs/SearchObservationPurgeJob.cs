using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>Đợt 9 — dọn SearchObservation quá 90 ngày (giới hạn lưu trữ cho báo cáo Search Quality, không
/// chỉ giới hạn ở tầng truy vấn). Chạy hàng ngày — 90 ngày là cửa sổ rộng, không cần mịn tới 30 phút như
/// ELIB-LRC (họ gộp chung vòng lặp với job kiểm tra tìm kiếm đã lưu).</summary>
public class SearchObservationPurgeJob(SearchQualityService service, ILogger<SearchObservationPurgeJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var deleted = await service.PurgeOldAsync();
        logger.LogInformation("SearchObservationPurgeJob: đã xoá {Count} bản ghi quá 90 ngày.", deleted);
    }
}
