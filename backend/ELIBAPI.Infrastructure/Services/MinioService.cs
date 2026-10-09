using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace ELIBAPI.Infrastructure.Services;

public class MinioService : IMinioService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly string _privateBucket;
    private readonly string _publicBase;

    public string PublicBaseUrl => _publicBase;

    public MinioService(IConfiguration config)
    {
        var s = config.GetSection("MinioSettings");

        var secretKey = s["SecretKey"] ?? "";
        if (secretKey.StartsWith("ENC:"))
            secretKey = AesEncryptionHelper.Decrypt(secretKey[4..]);

        _bucket        = s["BucketName"] ?? "files";
        _privateBucket = s["PrivateBucketName"] ?? (_bucket + "-private");
        _publicBase    = (s["PublicBaseUrl"] ?? "").TrimEnd('/');

        var endpoint  = s["Endpoint"] ?? "";
        var accessKey = s["AccessKey"] ?? "";
        var useSSL    = bool.Parse(s["UseSSL"] ?? "false");

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSSL)
            .Build();
    }

    public async Task<string> UploadAsync(Stream stream, string filename, string contentType)
    {
        var ext        = Path.GetExtension(filename).TrimStart('.');
        var objectName = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{ext}";

        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucket));

        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket));
            var policy = $$"""
                {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*","Action":"s3:GetObject","Resource":"arn:aws:s3:::{{_bucket}}/*"}]}
                """;
            await _client.SetPolicyAsync(
                new SetPolicyArgs().WithBucket(_bucket).WithPolicy(policy));
        }

        var size = stream.CanSeek ? stream.Length : -1;
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType));

        return objectName;
    }

    public async Task DeleteAsync(string urlOrObjectName)
    {
        if (string.IsNullOrEmpty(urlOrObjectName)) return;

        var objectName = urlOrObjectName;
        var newPrefix  = $"{_publicBase}/";
        if (objectName.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase))
            objectName = objectName[newPrefix.Length..];
        else
        {
            var bucketMarker = $"/{_bucket}/";
            var idx = objectName.IndexOf(bucketMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) objectName = objectName[(idx + bucketMarker.Length)..];
        }

        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName));
        }
        catch { }
    }

    public async Task DeletePrivateAsync(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return;
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_privateBucket)
                .WithObject(objectName));
        }
        catch { }
    }

    public async Task<string> UploadPrivateAsync(Stream stream, string filename, string contentType)
    {
        var ext        = Path.GetExtension(filename).TrimStart('.');
        var objectName = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{ext}";

        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_privateBucket));
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_privateBucket));
        // Không set public policy — bucket riêng tư, chỉ truy cập qua API

        var size = stream.CanSeek ? stream.Length : -1;
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_privateBucket)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType));

        return objectName;
    }

    public async Task<(Stream stream, string contentType)> GetObjectStreamAsync(string objectName)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_privateBucket)
            .WithObject(objectName)
            .WithCallbackStream(async (s, ct) => await s.CopyToAsync(ms, ct)));

        ms.Position = 0;
        var ext         = Path.GetExtension(objectName).TrimStart('.').ToLower();
        var contentType = ext switch
        {
            "pdf"        => "application/pdf",
            "docx"       => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx"       => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "mp4"        => "video/mp4",
            "jpg" or "jpeg" => "image/jpeg",
            "png"        => "image/png",
            "gif"        => "image/gif",
            _            => "application/octet-stream"
        };
        return (ms, contentType);
    }

    public async Task<(Stream stream, string contentType)> GetPublicObjectStreamAsync(string objectName)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithCallbackStream(async (s, ct) => await s.CopyToAsync(ms, ct)));
        ms.Position = 0;
        var ext         = Path.GetExtension(objectName).TrimStart('.').ToLower();
        var contentType = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png"           => "image/png",
            "gif"           => "image/gif",
            "webp"          => "image/webp",
            _               => "application/octet-stream"
        };
        return (ms, contentType);
    }

    public async Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 900)
        => await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_privateBucket)
            .WithObject(objectName)
            .WithExpiry(expirySeconds));

    public async Task<(List<MinioImageItem> Items, int TotalCount)> ListPublicImagesAsync(
        string? keyword, int page, int pageSize)
    {
        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        var all = new List<MinioImageItem>();
        var args = new ListObjectsArgs().WithBucket(_bucket).WithRecursive(true);

        await foreach (var item in _client.ListObjectsEnumAsync(args))
        {
            var ext = Path.GetExtension(item.Key);
            if (!imageExts.Contains(ext)) continue;

            var fileName = Path.GetFileName(item.Key);
            if (!string.IsNullOrEmpty(keyword) &&
                !fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            all.Add(new MinioImageItem
            {
                Path         = item.Key,
                FileName     = fileName,
                Size         = (long)item.Size,
                UploadedDate = item.LastModifiedDateTime ?? DateTime.UtcNow
            });
        }

        all = all.OrderByDescending(x => x.UploadedDate).ToList();
        var total = all.Count;
        var items = all.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<bool> PingAsync()
    {
        try { return await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket)); }
        catch { return false; }
    }

    public async IAsyncEnumerable<(string ObjectName, long Size)> ListPrivateBucketObjectsAsync()
    {
        var args = new ListObjectsArgs().WithBucket(_privateBucket).WithRecursive(true);
        await foreach (var item in _client.ListObjectsEnumAsync(args))
            yield return (item.Key, (long)item.Size);
    }
}
