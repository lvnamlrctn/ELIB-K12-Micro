using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Elib.BuildingBlocks.Storage;

/// <summary>
/// Cấu hình MinIO/S3 (section "Storage"). Service nói chuyện với MinIO qua <see cref="Endpoint"/> trong mạng nội bộ;
/// trình duyệt đi qua gateway ở <see cref="PublicPathPrefix"/> (/s3/** → MinIO), cùng origin với app nên không cần CORS.
/// URL ký sẵn được ký cho host <see cref="Endpoint"/>; gateway chuyển tiếp với Host = địa chỉ MinIO nên chữ ký vẫn khớp.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>host:port của MinIO, ví dụ minio:9000. Phải trùng địa chỉ cluster MinIO khai báo ở gateway.</summary>
    public string Endpoint { get; set; } = "minio:9000";

    public bool UseSsl { get; set; }
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";

    /// <summary>Đặt cố định để ký URL không phải hỏi MinIO region của bucket.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Tiền tố đường dẫn của MinIO phía trình duyệt (route /s3/** ở gateway).</summary>
    public string PublicPathPrefix { get; set; } = "/s3";
}

/// <summary>Thông tin object đã có trên kho.</summary>
public sealed record StoredObject(long Size, string? ContentType);

/// <summary>
/// Kho file (MinIO/S3). Object key có tiền tố <c>{tenantId}/</c> (docs 04 §6) — do service gọi tự ghép.
/// URL trả về là đường dẫn tương đối (<c>/s3/{bucket}/{key}…</c>) để dùng trên mọi host của đơn vị.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Tạo bucket nếu chưa có. <paramref name="publicRead"/>: ai cũng đọc được từng object (không liệt kê được bucket).</summary>
    Task EnsureBucketAsync(string bucket, bool publicRead, CancellationToken cancellationToken);

    /// <summary>URL để trình duyệt PUT thẳng file lên kho (không qua service), hết hạn sau <paramref name="expiry"/>.</summary>
    Task<string> PresignPutAsync(string bucket, string key, TimeSpan expiry);

    /// <summary>URL tải file riêng tư, hết hạn sau <paramref name="expiry"/>.</summary>
    Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry);

    /// <summary>URL cố định của object trong bucket công khai.</summary>
    string PublicUrl(string bucket, string key);

    /// <summary>null nếu object không tồn tại.</summary>
    Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken);

    /// <summary>Đọc tối đa <paramref name="maxBytes"/> byte đầu của object.</summary>
    Task<byte[]> ReadAsync(string bucket, string key, long maxBytes, CancellationToken cancellationToken);

    Task PutAsync(string bucket, string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    /// <summary>Xoá object; không lỗi nếu object không còn.</summary>
    Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken);
}

public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly StorageOptions _options;

    public MinioObjectStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSsl)
            .WithRegion(_options.Region)
            .Build();
    }

    public async Task EnsureBucketAsync(string bucket, bool publicRead, CancellationToken cancellationToken)
    {
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken))
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket).WithLocation(_options.Region), cancellationToken);
        if (publicRead)
        {
            // Chỉ s3:GetObject — không cấp s3:ListBucket, người ngoài không liệt kê được file của các đơn vị.
            var policy = $$"""
                {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucket}}/*"]}]}
                """;
            await _client.SetPolicyAsync(new SetPolicyArgs().WithBucket(bucket).WithPolicy(policy), cancellationToken);
        }
    }

    public async Task<string> PresignPutAsync(string bucket, string key, TimeSpan expiry) =>
        ToPublicPath(await _client.PresignedPutObjectAsync(
            new PresignedPutObjectArgs().WithBucket(bucket).WithObject(key).WithExpiry(Seconds(expiry))));

    public async Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry) =>
        ToPublicPath(await _client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs().WithBucket(bucket).WithObject(key).WithExpiry(Seconds(expiry))));

    public string PublicUrl(string bucket, string key) => StoragePaths.Public(_options.PublicPathPrefix, bucket, key);

    public async Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken)
    {
        try
        {
            var stat = await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key), cancellationToken);
            return new StoredObject(stat.Size, stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadAsync(string bucket, string key, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket).WithObject(key).WithOffsetAndLength(0, maxBytes)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(buffer, ct)), cancellationToken);
        return buffer.ToArray();
    }

    public async Task PutAsync(string bucket, string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await _client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(key)
            .WithStreamData(stream).WithObjectSize(stream.Length).WithContentType(contentType), cancellationToken);
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken cancellationToken) =>
        _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), cancellationToken);

    private string ToPublicPath(string absoluteUrl) => StoragePaths.FromSigned(_options.PublicPathPrefix, absoluteUrl);

    private static int Seconds(TimeSpan expiry) => (int)Math.Clamp(expiry.TotalSeconds, 1, 7 * 24 * 3600);
}

/// <summary>Ghép/đổi đường dẫn phía trình duyệt — tách riêng để test không cần MinIO.</summary>
public static class StoragePaths
{
    public static string Public(string prefix, string bucket, string key) => $"{prefix.TrimEnd('/')}/{bucket}/{key}";

    /// <summary>http://minio:9000/bucket/key?X-Amz-… → /s3/bucket/key?X-Amz-… (giữ nguyên path và query đã ký).</summary>
    public static string FromSigned(string prefix, string absoluteUrl) => prefix.TrimEnd('/') + new Uri(absoluteUrl).PathAndQuery;
}

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddElibObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        return services;
    }
}
