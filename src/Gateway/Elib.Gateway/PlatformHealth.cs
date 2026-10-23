using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace Elib.Gateway;

/// <summary>Tình trạng một service: Healthy (/ready trả 2xx), Unhealthy (trả lỗi, ví dụ DB không kết nối được), Unreachable (không phản hồi kịp).</summary>
public sealed record ServiceHealth(string Name, string Status, long LatencyMs, string? Detail);

/// <summary>Độ sâu hàng đợi lỗi (queue có đuôi _error) của RabbitMQ — thông điệp đã hết lần thử lại, chờ người xử lý.</summary>
public sealed record QueueDepth(string Name, long Messages);

/// <summary><see cref="Queues"/> = null khi gateway không có cách gọi RabbitMQ management (xem <see cref="QueuesDetail"/>).</summary>
public sealed record PlatformHealth(DateTimeOffset CheckedAt, IReadOnlyList<ServiceHealth> Services, IReadOnlyList<QueueDepth>? Queues, string? QueuesDetail);

/// <summary>
/// Gom tình trạng nền tảng cho tab DevOps (chỉ host hệ thống): ping /ready của mọi cluster YARP song song + hàng đợi lỗi RabbitMQ.
/// Cluster không phải service .NET (minio, admin-web, opac-web) không có /ready nên bỏ qua.
/// </summary>
public sealed class PlatformHealthService(IProxyConfigProvider proxy, IHttpClientFactory clients, IConfiguration configuration, IOptions<GatewayOptions> gateway, TimeProvider clock)
{
    public const string HttpClientName = "platform-health";
    public const string ErrorQueueSuffix = "_error";
    private static readonly string[] NotDotNetServices = ["minio", "admin-web", "opac-web"];
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(3);

    public async Task<PlatformHealth> CheckAsync(CancellationToken ct)
    {
        var clusters = proxy.GetConfig().Clusters
            .Where(c => !NotDotNetServices.Contains(c.ClusterId, StringComparer.OrdinalIgnoreCase))
            .Select(c => (Name: c.ClusterId, Address: c.Destinations?.Values.FirstOrDefault()?.Address))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
        var pings = await Task.WhenAll(clusters.Select(c => PingAsync(c.Name, c.Address, ct)));
        var (queues, detail) = await ErrorQueuesAsync(ct);
        return new PlatformHealth(clock.GetUtcNow(), pings, queues, detail);
    }

    private async Task<ServiceHealth> PingAsync(string name, string? address, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(address)) return new ServiceHealth(name, "Unreachable", 0, "Cluster chưa cấu hình địa chỉ.");
        var watch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PingTimeout);
        try
        {
            using var response = await clients.CreateClient(HttpClientName).GetAsync(new Uri(new Uri(address.TrimEnd('/') + "/"), "ready"), timeout.Token);
            return response.IsSuccessStatusCode
                ? new ServiceHealth(name, "Healthy", watch.ElapsedMilliseconds, null)
                : new ServiceHealth(name, "Unhealthy", watch.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException) && !ct.IsCancellationRequested)
        {
            return new ServiceHealth(name, "Unreachable", watch.ElapsedMilliseconds, ex is HttpRequestException ? "Không kết nối được." : "Quá thời gian chờ.");
        }
    }

    /// <summary>
    /// RabbitMQ management API: cấu hình Gateway:RabbitManagementUrl, hoặc suy từ Messaging:Host (cổng 15672) khi có tài khoản Messaging — cùng tài khoản
    /// service đang dùng, không thêm credential. Không có cả hai → bỏ phần này.
    /// </summary>
    private async Task<(IReadOnlyList<QueueDepth>?, string?)> ErrorQueuesAsync(CancellationToken ct)
    {
        // Gateway không tham chiếu thư viện Messaging (MassTransit) — đọc thẳng các khoá cấu hình mà compose đã đặt cho mọi service.
        var messaging = configuration.GetSection("Messaging");
        var user = messaging["Username"];
        var password = messaging["Password"] ?? "";
        var vhost = messaging["VirtualHost"] is { Length: > 0 } v ? v : "/";
        var url = gateway.Value.RabbitManagementUrl;
        if (string.IsNullOrWhiteSpace(url) && messaging["Transport"] is null or "RabbitMq" && !string.IsNullOrEmpty(user) && messaging["Host"] is { Length: > 0 } host)
            url = $"http://{host}:15672/";
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrEmpty(user)) return (null, "Gateway chưa có địa chỉ/tài khoản RabbitMQ management.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PingTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(new Uri(url.TrimEnd('/') + "/"), $"api/queues/{Uri.EscapeDataString(vhost)}?columns=name,messages"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return (null, $"RabbitMQ management trả HTTP {(int)response.StatusCode}.");
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            return (ParseErrorQueues(json.RootElement), null);
        }
        catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException or JsonException) && !ct.IsCancellationRequested)
        {
            return (null, "Không đọc được RabbitMQ management.");
        }
    }

    /// <summary>Các hàng đợi _error (cả rỗng — để thấy là đã sạch), nhiều thông điệp nhất trước.</summary>
    public static IReadOnlyList<QueueDepth> ParseErrorQueues(JsonElement queues)
    {
        if (queues.ValueKind != JsonValueKind.Array) return [];
        return [.. queues.EnumerateArray()
            .Where(q => q.TryGetProperty("name", out var n) && n.GetString() is { } s && s.EndsWith(ErrorQueueSuffix, StringComparison.Ordinal))
            .Select(q => new QueueDepth(q.GetProperty("name").GetString()!, q.TryGetProperty("messages", out var c) && c.TryGetInt64(out var v) ? v : 0))
            .OrderByDescending(q => q.Messages).ThenBy(q => q.Name, StringComparer.Ordinal)];
    }
}
