using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ELIBAPI.Infrastructure.RateLimiting;

/// <summary>Đợt 22 — port từ ELIB-LRC. Chỉ 3 kiểm tra: Database, Elasticsearch, MinIO — bỏ Keycloak (ELIB
/// không dùng SSO ngoài) và SMS/Zalo gateway (ELIB gọi ZNS với accessToken/apiUrl theo TỪNG đơn vị, lấy từ
/// SystemParameter lúc gửi — không có 1 endpoint chung để probe như LRC, probe generic sẽ không phản ánh
/// đúng tình trạng; bỏ qua thay vì kiểm tra sai). Toàn cục, không theo tenant — health check trả lời "server
/// còn sống/sẵn sàng phục vụ mọi đơn vị hay không", tenant nào cũng dùng chung 3 dịch vụ nền này.</summary>
public class DatabaseHealthCheck(ELIBAPIDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Kết nối cơ sở dữ liệu hoạt động bình thường.")
                : HealthCheckResult.Unhealthy("Không thể kết nối cơ sở dữ liệu.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Lỗi khi kiểm tra kết nối cơ sở dữ liệu.", ex);
        }
    }
}

public class ElasticsearchHealthCheck(IElasticsearchService elastic) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAlive = await elastic.PingAsync();
            return isAlive
                ? HealthCheckResult.Healthy("Elasticsearch cluster hoạt động bình thường.")
                : HealthCheckResult.Unhealthy("Không thể kết nối cụm Elasticsearch.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Lỗi khi kiểm tra kết nối Elasticsearch.", ex);
        }
    }
}

public class MinioHealthCheck(IMinioService minio) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAlive = await minio.PingAsync();
            return isAlive
                ? HealthCheckResult.Healthy("MinIO Object Storage hoạt động bình thường.")
                : HealthCheckResult.Unhealthy("Không thể kết nối MinIO Object Storage.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Lỗi khi kiểm tra kết nối MinIO.", ex);
        }
    }
}
