using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Application.Sip2;
using Elib.Circulation.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Circulation.Infrastructure;

/// <summary>Cổng SIP2 (TCP) của circulation — tắt mặc định; DEV/production bật bằng Sip2__Enabled=true.</summary>
public sealed class Sip2Options
{
    public const string SectionName = "Sip2";

    public bool Enabled { get; set; }
    public string Address { get; set; } = "0.0.0.0";

    /// <summary>0 = cổng ngẫu nhiên (test).</summary>
    public int Port { get; set; } = 6001;

    public int MaxConnections { get; set; } = 500;

    /// <summary>Không nhận lệnh nào quá thời gian này thì đóng kết nối.</summary>
    public int IdleMinutes { get; set; } = 30;
}

/// <summary>
/// Nhận kết nối TCP của trạm tự phục vụ: mỗi dòng (kết thúc CR/LF) là một thông điệp SIP2. Checksum sai → 96 (yêu cầu gửi lại).
/// Trước khi đăng nhập (93) chỉ trả lời 99; sai mật khẩu trạm 5 lần thì đóng kết nối. Mỗi lệnh một scope trong ngữ cảnh đơn vị của trạm,
/// nhật ký/event ghi "Trạm SIP2 …" làm người thao tác; mỗi giao dịch ghi một dòng <see cref="Sip2Log"/> (đã che mật khẩu).
/// </summary>
public sealed partial class Sip2Listener(IServiceScopeFactory scopes, IOptions<Sip2Options> options, TimeProvider clock, ILogger<Sip2Listener> logger)
    : BackgroundService
{
    private const int MaxLineBytes = 8 * 1024;
    private readonly TaskCompletionSource<int> _bound = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _connections;

    /// <summary>Cổng thực đang nghe (test dùng cổng 0).</summary>
    public Task<int> BoundPort => _bound.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled)
        {
            _bound.TrySetResult(0);
            return;
        }
        var listener = new TcpListener(IPAddress.Parse(o.Address), o.Port);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _bound.TrySetResult(port);
        LogListening(logger, o.Address, port);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                if (Interlocked.Increment(ref _connections) > o.MaxConnections)
                {
                    Interlocked.Decrement(ref _connections);
                    client.Dispose();
                    continue;
                }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ServeAsync(client, stoppingToken);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _connections);
                        client.Dispose();
                    }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // dừng service
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken stoppingToken)
    {
        var remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
        var session = new Sip2Session(remote);
        var stream = client.GetStream();
        var buffer = new byte[4096];
        var line = new List<byte>(256);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                idle.CancelAfter(TimeSpan.FromMinutes(options.Value.IdleMinutes));
                var read = await stream.ReadAsync(buffer, idle.Token);
                if (read == 0) return;
                for (var i = 0; i < read; i++)
                {
                    var b = buffer[i];
                    if (b is (byte)'\r' or (byte)'\n')
                    {
                        if (line.Count == 0) continue;
                        var keep = await ReplyAsync(session, [.. line], stream, stoppingToken);
                        line.Clear();
                        if (!keep) return;
                        continue;
                    }
                    line.Add(b);
                    if (line.Count > MaxLineBytes) return; // không phải SIP2
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException)
        {
            // trạm ngắt kết nối / quá thời gian chờ / dừng service
        }
    }

    /// <summary>Trả lời một dòng; false = đóng kết nối.</summary>
    private async Task<bool> ReplyAsync(Sip2Session session, byte[] raw, NetworkStream stream, CancellationToken ct)
    {
        byte[] reply;
        var keep = true;
        if (!Sip2Codec.ChecksumValid(raw))
        {
            reply = Sip2Codec.Encode(new Sip2Message("96"), asciiOnly: true);
        }
        else
        {
            Sip2Message request;
            try
            {
                request = Sip2Codec.Parse(Encoding.UTF8.GetString(raw));
            }
            catch (FormatException)
            {
                return true;
            }
            (reply, keep) = await ProcessAsync(session, request, ct);
        }
        await stream.WriteAsync(reply, ct);
        await stream.FlushAsync(ct);
        return keep;
    }

    public async Task<(byte[] Reply, bool Keep)> ProcessAsync(Sip2Session session, Sip2Message request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        if (request.Command == Sip2Commands.RequestAcsResend)
            return (session.LastResponse ?? Sip2Codec.Encode(Sip2Service.Status(session.Station, request, now), true), true);

        if (request.Command == Sip2Commands.Login)
        {
            Sip2StationInfo? station;
            using (var scope = scopes.CreateScope())
                station = await scope.ServiceProvider.GetRequiredService<Sip2Authenticator>()
                    .LoginAsync(request.Field(Sip2Tags.LoginUserId), request.Field(Sip2Tags.LoginPassword), ct);
            var login = new Sip2Message("94", station is null ? "0" : "1") { Sequence = request.Sequence };
            if (station is null)
            {
                session.FailedLogins++;
                LogLoginFailed(logger, request.Field(Sip2Tags.LoginUserId) ?? "", session.RemoteIp ?? "");
                await Task.Delay(TimeSpan.FromSeconds(1), clock, ct);
                return (Sip2Codec.Encode(login, true), session.FailedLogins < Sip2Session.MaxFailedLogins);
            }
            session.Station = station;
            session.FailedLogins = 0;
            var bytes = Sip2Codec.Encode(login, station.AsciiOnly);
            await LogAsync(station, request, bytes, true, 0, session.RemoteIp, ct);
            return (session.LastResponse = bytes, true);
        }

        if (session.Station is not { } current)
            return (Sip2Codec.Encode(Sip2Service.Status(null, request, now), true), true);

        var watch = Stopwatch.StartNew();
        Sip2Message response;
        var ok = true;
        try
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<ITenantContext>().Use(current.TenantId);
            sp.GetRequiredService<CrudAuditContext>().RunAs(new CrudAuditOrigin(null, ElibSubjectTypes.Device, $"Trạm SIP2 {current.Code} — {current.Name}", null, session.RemoteIp));
            response = await sp.GetRequiredService<Sip2Service>().HandleAsync(current, request, ct);
            ok = response.Command is not ("12" or "10" or "30" or "66") || response.FixedChar(0) == '1';
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCommandFailed(logger, ex, request.Command, current.Code, current.TenantId);
            response = Sip2Service.Status(current, request, now).Add(Sip2Tags.ScreenMessage, "Lỗi hệ thống — vui lòng đến quầy thủ thư.");
            ok = false;
        }
        var reply = Sip2Codec.Encode(response, current.AsciiOnly);
        await LogAsync(current, request, reply, ok, (int)watch.ElapsedMilliseconds, session.RemoteIp, ct);
        return (session.LastResponse = reply, true);
    }

    private async Task LogAsync(Sip2StationInfo station, Sip2Message request, byte[] reply, bool ok, int elapsedMs, string? remoteIp, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            using var _ = sp.GetRequiredService<ITenantContext>().Use(station.TenantId);
            var db = sp.GetRequiredService<CirculationDbContext>();
            db.Set<Sip2Log>().Add(new Sip2Log
            {
                StationId = station.Id, Command = request.Command, Request = Cut(Sip2Codec.Mask(request)),
                Response = Cut(Encoding.UTF8.GetString(reply).TrimEnd('\r')), Ok = ok, ElapsedMs = elapsedMs, RemoteIp = remoteIp, At = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWriteFailed(logger, ex, station.Code); // nhật ký lỗi không làm hỏng giao dịch đã xong
        }
    }

    private static string Cut(string value) => value.Length <= Sip2Log.MaxMessageLength ? value : value[..Sip2Log.MaxMessageLength];

    [LoggerMessage(Level = LogLevel.Information, Message = "Cổng SIP2 nghe tại {Address}:{Port}")]
    private static partial void LogListening(ILogger logger, string address, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trạm SIP2 đăng nhập sai: '{LoginId}' từ {RemoteIp}")]
    private static partial void LogLoginFailed(ILogger logger, string loginId, string remoteIp);

    [LoggerMessage(Level = LogLevel.Error, Message = "Lệnh SIP2 {Command} của trạm {Station} (đơn vị {TenantId}) lỗi")]
    private static partial void LogCommandFailed(ILogger logger, Exception exception, string command, string station, long tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không ghi được nhật ký SIP2 của trạm {Station}")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception, string station);
}

/// <summary>Kiểm tra mật khẩu bạn đọc qua identity (/internal/tenants/{id}/reader-accounts/check, service token).</summary>
public sealed class HttpReaderPasswordChecker(IHttpClientFactory clients) : IReaderPasswordChecker
{
    public async Task<string> CheckAsync(long tenantId, string cardNo, string? password, CancellationToken ct)
    {
        using var response = await clients.CreateClient(HttpPermissionSource.HttpClientName).PostAsJsonAsync(
            new Uri(string.Create(CultureInfo.InvariantCulture, $"internal/tenants/{tenantId}/reader-accounts/check"), UriKind.Relative),
            new { cardNo, password }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckResult>(ct))?.Result ?? "invalid";
    }

    private sealed record CheckResult(string Result);
}
