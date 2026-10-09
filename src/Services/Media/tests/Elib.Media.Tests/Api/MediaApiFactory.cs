using System.Collections.Concurrent;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Storage;
using Elib.BuildingBlocks.Testing;
using Elib.Media.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Media.Tests.Api;

/// <summary>Service media trong process: SQLite thay PostgreSQL, kho file trong bộ nhớ thay MinIO, xác thực bằng header.</summary>
public sealed class MediaApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public InMemoryObjectStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MediaDb", "Host=unused-in-tests");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MediaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MediaDbContext>>();
            services.AddDbContext<MediaDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(Storage);
        });
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Database.EnsureCreatedAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
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
}

/// <summary>Kho file giả: URL ký có dạng /s3/{bucket}/{key}?sig=… như MinIO thật qua gateway; test "PUT" bằng <see cref="Upload"/>.</summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    public ConcurrentDictionary<string, (byte[] Content, string? ContentType)> Objects { get; } = new();
    public ConcurrentDictionary<string, bool> Buckets { get; } = new();

    public static string Id(string bucket, string key) => $"{bucket}/{key}";

    /// <summary>Giả lập trình duyệt PUT vào URL upload (/s3/{bucket}/{key}?…).</summary>
    public void Upload(string uploadUrl, byte[] content)
    {
        var path = uploadUrl.Split('?')[0];
        Assert.StartsWith("/s3/", path, StringComparison.Ordinal);
        Objects[path["/s3/".Length..]] = (content, "application/octet-stream");
    }

    public Task EnsureBucketAsync(string bucket, bool publicRead, CancellationToken cancellationToken)
    {
        Buckets[bucket] = publicRead;
        return Task.CompletedTask;
    }

    public Task<string> PresignPutAsync(string bucket, string key, TimeSpan expiry) => Task.FromResult($"/s3/{bucket}/{key}?sig=put");

    public Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry) => Task.FromResult($"/s3/{bucket}/{key}?sig=get");

    public string PublicUrl(string bucket, string key) => StoragePaths.Public("/s3", bucket, key);

    public Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.TryGetValue(Id(bucket, key), out var o) ? new StoredObject(o.Content.Length, o.ContentType) : null);

    public Task<byte[]> ReadAsync(string bucket, string key, long maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(Objects[Id(bucket, key)].Content.Take((int)maxBytes).ToArray());

    public Task PutAsync(string bucket, string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        Objects[Id(bucket, key)] = (content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken)
    {
        Objects.TryRemove(Id(bucket, key), out _);
        return Task.CompletedTask;
    }
}
