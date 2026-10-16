using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Application;
using Elib.Patron.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Patron.Tests.Api;

/// <summary>Chạy nguyên service patron trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class PatronApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ConcurrentQueue<object> Published { get; } = new();
    public FakePermissionSource Permissions { get; } = new();

    /// <summary>Danh mục của service tenant (đơn vị, dân tộc…) theo (đơn vị, loại) — thay cho gọi /internal của tenant.</summary>
    public ConcurrentDictionary<(long TenantId, string Kind), List<ReferenceItem>> References { get; } = new();

    /// <summary>Loại bạn đọc lưu thông đang dùng theo (đơn vị, loại) — thay cho gọi /internal của circulation; null = circulation không trả lời.</summary>
    public ConcurrentDictionary<(long TenantId, long ReaderTypeId), CirculationReaderTypeUsage?> CirculationUsage { get; } = new();

    /// <summary>Ảnh bạn đọc theo id file (thay cho media) và các lô gửi sang bộ so khớp (thay cho Gemini).</summary>
    public ConcurrentDictionary<Guid, byte[]> PhotoFiles { get; } = new();

    public ConcurrentQueue<Guid> PhotoReads { get; } = new();
    public FakeFaceMatcher Matcher { get; } = new();

    /// <summary>true = media không đọc được (token sai, dịch vụ sập) — mọi lần đọc ảnh ném lỗi.</summary>
    public bool PhotoSourceDown { get; set; }

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PatronDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");
        builder.UseSetting("FaceRecognition:ApiKey", "test-key");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<PatronDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PatronDbContext>>();
            services.AddDbContext<PatronDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IPermissionSource>();
            services.AddSingleton<IPermissionSource>(Permissions);
            services.RemoveAll<ITenantReferences>();
            services.AddScoped<ITenantReferences>(sp => new FakeTenantReferences(References, sp.GetRequiredService<ITenantContext>()));
            services.RemoveAll<IFaceMatcher>();
            services.AddSingleton<IFaceMatcher>(Matcher);
            services.RemoveAll<IReaderPhotoSource>();
            services.AddScoped<IReaderPhotoSource>(_ => new FakePhotoSource(PhotoFiles, PhotoReads, () => PhotoSourceDown));
            services.RemoveAll<IReaderTypeUsage>();
            services.AddScoped<IReaderTypeUsage>(sp => new FakeReaderTypeUsage(CirculationUsage, sp.GetRequiredService<ITenantContext>()));
        });
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using (var scope = Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<PatronDbContext>().Database.EnsureCreatedAsync();
            }
            Services.GetRequiredService<IBus>().ConnectPublishObserver(new Recorder(Published));
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task PublishAsync<T>(T message) where T : class => Services.GetRequiredService<IBus>().Publish(message);

    /// <summary>Chờ consumer xử lý xong (bus in-memory chạy nền).</summary>
    public static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, string what) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } found) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Hết thời gian chờ: {what}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database.Dispose();
            _initLock.Dispose();
        }
    }

    private sealed class Recorder(ConcurrentQueue<object> sink) : IPublishObserver
    {
        public Task PrePublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            sink.Enqueue(context.Message);
            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}

public sealed class FakeTenantReferences(ConcurrentDictionary<(long TenantId, string Kind), List<ReferenceItem>> all, ITenantContext tenant) : ITenantReferences
{
    public Task<IReadOnlyList<ReferenceItem>> ListAsync(string kind, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReferenceItem>>(all.GetValueOrDefault((tenant.RequireTenantId(), kind)) ?? []);
}

public sealed class FakeReaderTypeUsage(ConcurrentDictionary<(long TenantId, long ReaderTypeId), CirculationReaderTypeUsage?> all, ITenantContext tenant)
    : IReaderTypeUsage
{
    public Task<CirculationReaderTypeUsage> CirculationAsync(long readerTypeId, CancellationToken ct) =>
        all.TryGetValue((tenant.RequireTenantId(), readerTypeId), out var usage)
            ? Task.FromResult(usage ?? throw new Elib.BuildingBlocks.Domain.BusinessRuleException("CIRCULATION_UNAVAILABLE", "Không kiểm tra được.", 503))
            : Task.FromResult(new CirculationReaderTypeUsage([], 0));
}

/// <summary>Bộ so khớp giả: khớp ảnh nào có nội dung bằng <see cref="Target"/> với độ tin cậy <see cref="Confidence"/>; ghi lại các lô nhận được.</summary>
public sealed class FakeFaceMatcher : IFaceMatcher
{
    public byte[]? Target { get; set; }
    public double Confidence { get; set; } = 0.9;
    public bool Fail { get; set; }
    public ConcurrentQueue<IReadOnlyList<byte[]>> Batches { get; } = new();

    public Task<FaceBatchMatch> MatchAsync(byte[] camera, IReadOnlyList<byte[]> candidates, CancellationToken ct)
    {
        if (Fail) throw new FaceServiceException("giả lập lỗi");
        Batches.Enqueue(candidates);
        var index = Target is null ? -1 : candidates.ToList().FindIndex(c => c.SequenceEqual(Target));
        return Task.FromResult(index < 0 ? new FaceBatchMatch(null, 0) : new FaceBatchMatch(index + 1, Confidence));
    }
}

public sealed class FakePhotoSource(ConcurrentDictionary<Guid, byte[]> files, ConcurrentQueue<Guid> reads, Func<bool> down) : IReaderPhotoSource
{
    public Task<byte[]?> ReadAsync(Guid fileId, CancellationToken ct)
    {
        reads.Enqueue(fileId);
        if (down()) throw new FaceServiceException("giả lập media sập");
        return Task.FromResult(files.TryGetValue(fileId, out var bytes) ? bytes : null);
    }
}
