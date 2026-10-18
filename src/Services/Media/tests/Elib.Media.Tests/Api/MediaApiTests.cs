using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Media.Application;
using Elib.Media.Domain;
using Elib.Media.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Media.Tests.Api;

public sealed class MediaApiTests : IClassFixture<MediaApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 1, 2, 3];
    private static readonly byte[] PdfBytes = "%PDF-1.7\n%âãÏÓ\n1 0 obj"u8.ToArray();
    private static long _nextTenantId = 5000;

    private readonly MediaApiFactory _factory;

    public MediaApiTests(MediaApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private HttpClient Staff(long tenantId) => _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId));

    private HttpClient SuperAdmin() => _factory.CreateClient().WithClaims(TestClaims.SuperAdmin);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static async Task<UploadTicketDto> Ticket(HttpClient client, string purpose, string contentType, long size, string fileName = "tep.png") =>
        await Read<UploadTicketDto>(await client.PostAsJsonAsync(U("/api/files/uploads"), new UploadRequest(purpose, fileName, contentType, size)));

    [Fact]
    public async Task Staff_uploads_private_attachment_and_gets_signed_download_url()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var client = Staff(tenantId);

        var ticket = await Ticket(client, "attachment", "application/pdf", PdfBytes.Length, @"C:\fakepath\Bien ban.pdf");
        Assert.StartsWith($"/s3/media-private/{tenantId}/attachment/", ticket.UploadUrl, StringComparison.Ordinal);
        _factory.Storage.Upload(ticket.UploadUrl, PdfBytes);

        var file = await Read<MediaFileDto>(await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null));
        Assert.Equal(MediaFileStatus.Ready, file.Status);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Bien ban.pdf", file.FileName);
        Assert.Null(file.Url); // riêng tư: không có URL cố định

        var download = await Read<DownloadDto>(await client.GetAsync(U($"/api/files/{ticket.FileId}/download")));
        Assert.StartsWith($"/s3/media-private/{tenantId}/attachment/", download.Url, StringComparison.Ordinal);
        Assert.Contains("sig=get", download.Url, StringComparison.Ordinal);
        Assert.NotNull(download.ExpiresAt);

        // Gọi complete lần nữa (mạng chập chờn) → trả kết quả cũ.
        Assert.Equal(MediaFileStatus.Ready, (await Read<MediaFileDto>(await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null))).Status);
    }

    [Fact]
    public async Task Batch_urls_return_only_ready_files_of_the_same_tenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var client = Staff(tenantId);
        async Task<Guid> Uploaded(HttpClient c)
        {
            var t = await Ticket(c, "reader-photo", "image/png", PngBytes.Length, "anh.png");
            _factory.Storage.Upload(t.UploadUrl, PngBytes);
            await Read<MediaFileDto>(await c.PostAsync(U($"/api/files/{t.FileId}/complete"), null));
            return t.FileId;
        }
        var a = await Uploaded(client);
        var b = await Uploaded(client);
        var pending = (await Ticket(client, "reader-photo", "image/png", PngBytes.Length)).FileId; // chưa upload xong
        var foreign = await Uploaded(Staff(Interlocked.Increment(ref _nextTenantId)));

        var urls = await Read<List<FileUrlDto>>(await client.PostAsJsonAsync(U("/api/files/urls"), new FileUrlsRequest([a, b, pending, foreign, a])));
        Assert.Equal(new[] { a, b }.Order(), urls.Select(u => u.Id).Order());
        Assert.All(urls, u => Assert.Contains("sig=get", u.Url, StringComparison.Ordinal));

        var many = await client.PostAsJsonAsync(U("/api/files/urls"), new FileUrlsRequest(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList()));
        Assert.Equal("MEDIA_URLS_TOO_MANY", await ErrorCode(many));
    }

    [Fact]
    public async Task Service_can_read_reader_photo_bytes_only_for_its_own_tenant_and_only_that_purpose()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var client = Staff(tenantId);
        var t = await Ticket(client, "reader-photo", "image/png", PngBytes.Length, "anh.png");
        _factory.Storage.Upload(t.UploadUrl, PngBytes);
        await Read<MediaFileDto>(await client.PostAsync(U($"/api/files/{t.FileId}/complete"), null));
        var logo = await Ticket(client, "attachment", "image/png", PngBytes.Length, "tep.png");
        _factory.Storage.Upload(logo.UploadUrl, PngBytes);
        await Read<MediaFileDto>(await client.PostAsync(U($"/api/files/{logo.FileId}/complete"), null));

        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        var ok = await service.GetAsync(U($"/internal/tenants/{tenantId}/files/{t.FileId}/content"));
        Assert.Equal(PngBytes, await ok.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{tenantId}/files/{logo.FileId}/content"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{Interlocked.Increment(ref _nextTenantId)}/files/{t.FileId}/content"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(U($"/internal/tenants/{tenantId}/files/{t.FileId}/content"))).StatusCode);
    }

    [Fact]
    public async Task Content_that_is_not_the_declared_type_is_rejected_and_deleted()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var client = Staff(tenantId);
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        var ticket = await Ticket(client, "attachment", "image/png", html.Length);
        _factory.Storage.Upload(ticket.UploadUrl, html);

        var response = await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEDIA_TYPE_INVALID", await ErrorCode(response));
        Assert.DoesNotContain(_factory.Storage.Objects.Keys, k => k.Contains(ticket.FileId.ToString("N"), StringComparison.Ordinal));

        var again = await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null);
        Assert.Equal("MEDIA_REJECTED", await ErrorCode(again));
    }

    [Fact]
    public async Task Upload_larger_than_the_purpose_limit_is_rejected()
    {
        var client = Staff(Interlocked.Increment(ref _nextTenantId));

        var declared = await client.PostAsJsonAsync(U("/api/files/uploads"), new UploadRequest("attachment", "a.pdf", "application/pdf", 21L * 1024 * 1024));
        Assert.Equal("MEDIA_TOO_LARGE", await ErrorCode(declared));

        // Khai nhỏ nhưng PUT lên file lớn hơn giới hạn → complete từ chối.
        var ticket = await Ticket(client, "attachment", "application/pdf", 10);
        _factory.Storage.Upload(ticket.UploadUrl, [.. PdfBytes, .. new byte[20 * 1024 * 1024]]);
        var response = await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null);
        Assert.Equal("MEDIA_TOO_LARGE", await ErrorCode(response));
    }

    [Fact]
    public async Task Svg_and_unknown_purposes_are_refused()
    {
        var client = Staff(Interlocked.Increment(ref _nextTenantId));
        Assert.Equal("MEDIA_TYPE_INVALID", await ErrorCode(await client.PostAsJsonAsync(U("/api/files/uploads"),
            new UploadRequest("attachment", "x.svg", "image/svg+xml", 100))));
        Assert.Equal("MEDIA_PURPOSE_INVALID", await ErrorCode(await client.PostAsJsonAsync(U("/api/files/uploads"),
            new UploadRequest("virus", "x.png", "image/png", 100))));
    }

    [Fact]
    public async Task Another_tenant_cannot_see_or_complete_the_file()
    {
        var owner = Staff(Interlocked.Increment(ref _nextTenantId));
        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        var ticket = await Ticket(owner, "attachment", "image/png", PngBytes.Length);
        _factory.Storage.Upload(ticket.UploadUrl, PngBytes);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(U($"/api/files/{ticket.FileId}/download"))).StatusCode);
        Assert.Equal(MediaFileStatus.Ready, (await Read<MediaFileDto>(await owner.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null))).Status);
    }

    [Fact]
    public async Task Tenant_logo_is_uploaded_by_platform_admin_into_the_public_bucket()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);

        var staffTry = await Staff(tenantId).PostAsJsonAsync(U("/api/files/uploads"), new UploadRequest("tenant-logo", "logo.png", "image/png", 100));
        Assert.Equal(HttpStatusCode.Forbidden, staffTry.StatusCode);
        Assert.Equal("MEDIA_SYSTEM_ONLY", await ErrorCode(staffTry));

        var admin = SuperAdmin();
        var ticket = await Read<UploadTicketDto>(await admin.PostAsJsonAsync(U("/api/system/files/uploads"),
            new SystemUploadRequest(tenantId, "tenant-logo", "logo.png", "image/png", PngBytes.Length)));
        Assert.StartsWith($"/s3/media-private/{tenantId}/tenant-logo/", ticket.UploadUrl, StringComparison.Ordinal); // chờ kiểm tra ở bucket riêng tư
        _factory.Storage.Upload(ticket.UploadUrl, PngBytes);

        var file = await Read<MediaFileDto>(await admin.PostAsync(U($"/api/system/files/{tenantId}/{ticket.FileId}/complete"), null));
        Assert.Equal(MediaFileStatus.Ready, file.Status);
        Assert.NotNull(file.Url);
        Assert.StartsWith($"/s3/media-public/{tenantId}/tenant-logo/", file.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", file.Url, StringComparison.Ordinal);

        var published = _factory.Storage.Objects[file.Url!["/s3/".Length..]];
        Assert.Equal("image/png", published.ContentType); // Content-Type theo nội dung thật, không theo trình duyệt
        Assert.False(_factory.Storage.Objects.ContainsKey(ticket.UploadUrl.Split('?')[0]["/s3/".Length..]));

        // Bản ghi thuộc đúng đơn vị đích.
        using var scope = _factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Files.IgnoreQueryFilters().SingleAsync(f => f.PublicId == ticket.FileId);
        Assert.Equal(tenantId, row.TenantId);
    }

    [Fact]
    public async Task Staff_token_cannot_use_system_upload_and_impersonation_cannot_upload()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var system = await Staff(tenantId).PostAsJsonAsync(U("/api/system/files/uploads"),
            new SystemUploadRequest(tenantId, "tenant-logo", "logo.png", "image/png", 100));
        Assert.Equal(HttpStatusCode.Forbidden, system.StatusCode);

        var impersonating = _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId) + ";imp=readonly");
        var upload = await impersonating.PostAsJsonAsync(U("/api/files/uploads"), new UploadRequest("attachment", "a.png", "image/png", 100));
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal("IMPERSONATION_READONLY", await ErrorCode(upload));

        // Token bạn đọc: không upload, cũng không đọc/tải file của đơn vị (ảnh thẻ riêng tư...) — chặn ngay ở policy.
        var reader = _factory.CreateClient().WithClaims(TestClaims.Reader(tenantId));
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await reader.PostAsJsonAsync(U("/api/files/uploads"),
            new UploadRequest("attachment", "a.png", "image/png", 100))));
        var file = await Ticket(Staff(tenantId), "attachment", "image/png", PngBytes.Length);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(U($"/api/files/{file.FileId}/download"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(U($"/api/files/{file.FileId}"))).StatusCode);
    }

    [Fact]
    public async Task Cleanup_removes_stale_pending_uploads()
    {
        var client = Staff(Interlocked.Increment(ref _nextTenantId));
        var ticket = await Ticket(client, "attachment", "image/png", PngBytes.Length);
        _factory.Storage.Upload(ticket.UploadUrl, PngBytes);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await db.Files.IgnoreQueryFilters().Where(f => f.PublicId == ticket.FileId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, DateTimeOffset.UtcNow.AddDays(-2)));
        }

        Assert.True(await _factory.Services.GetRequiredService<PendingUploadCleanup>().CleanupAsync(CancellationToken.None) >= 1);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(U($"/api/files/{ticket.FileId}/complete"), null)).StatusCode);
        Assert.DoesNotContain(_factory.Storage.Objects.Keys, k => k.Contains(ticket.FileId.ToString("N"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Buckets_are_created_with_public_read_only_on_the_public_bucket()
    {
        await WaitUntilAsync(() => _factory.Storage.Buckets.Count == 2);
        Assert.True(_factory.Storage.Buckets["media-public"]);
        Assert.False(_factory.Storage.Buckets["media-private"]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(condition());
    }

    [Fact]
    public async Task Service_requests_an_upload_ticket_only_for_a_service_requestable_purpose()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var service = _factory.CreateClient().WithClaims(TestClaims.Service);

        var ticket = await Read<UploadTicketDto>(await service.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/files/uploads"),
            new UploadRequest("document-submission", "luan-van.pdf", "application/pdf", PdfBytes.Length)));
        Assert.StartsWith($"/s3/media-private/{tenantId}/document-submission/", ticket.UploadUrl, StringComparison.Ordinal);
        _factory.Storage.Upload(ticket.UploadUrl, PdfBytes);

        var file = await Read<MediaFileDto>(await service.PostAsync(U($"/internal/tenants/{tenantId}/files/{ticket.FileId}/complete"), null));
        Assert.Equal((MediaFileStatus.Ready, "application/pdf"), (file.Status, file.ContentType));

        // Mục đích không ServiceRequestable (ebook-file) bị chặn ngay khi xin vé hộ.
        var denied = await service.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/files/uploads"),
            new UploadRequest("ebook-file", "a.pdf", "application/pdf", 10));
        Assert.Equal((HttpStatusCode.Forbidden, "MEDIA_SERVICE_NOT_ALLOWED"), (denied.StatusCode, await ErrorCode(denied)));

        // Token cán bộ (không phải service) không gọi được endpoint nội bộ này.
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId).PostAsJsonAsync(U($"/internal/tenants/{tenantId}/files/uploads"),
            new UploadRequest("document-submission", "a.pdf", "application/pdf", 10))).StatusCode);
    }

    [Fact]
    public async Task Docx_content_is_detected_by_magic_bytes_for_document_submission()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        byte[] docxBytes = [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4];

        var ticket = await Read<UploadTicketDto>(await service.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/files/uploads"),
            new UploadRequest("document-submission", "luan-van.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docxBytes.Length)));
        _factory.Storage.Upload(ticket.UploadUrl, docxBytes);

        var file = await Read<MediaFileDto>(await service.PostAsync(U($"/internal/tenants/{tenantId}/files/{ticket.FileId}/complete"), null));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
    }

    [Fact]
    public async Task Staff_imports_bib_cover_from_url_into_public_bucket_after_content_check()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var client = Staff(tenantId);
        _factory.Remote.Files["https://covers.openlibrary.org/b/isbn/9780747532699-L.jpg"] = PngBytes;
        _factory.Remote.Files["https://evil.example/cover.png"] = "<html><script>alert(1)</script></html>"u8.ToArray();

        var file = await Read<MediaFileDto>(await client.PostAsJsonAsync(U("/api/files/import"),
            new ImportRequest("bib-cover", "https://covers.openlibrary.org/b/isbn/9780747532699-L.jpg")));
        Assert.Equal(("image/png", MediaFileStatus.Ready, "9780747532699-L.jpg"), (file.ContentType, file.Status, file.FileName));
        Assert.StartsWith($"/s3/media-public/{tenantId}/bib-cover/", file.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", file.Url, StringComparison.Ordinal); // đuôi theo nội dung thật, không theo URL
        Assert.Contains(_factory.Storage.Objects.Keys, k => k.StartsWith("media-public/", StringComparison.Ordinal) && file.Url!.EndsWith(k, StringComparison.Ordinal));

        foreach (var (purpose, url, code) in new[]
        {
            ("bib-cover", "https://evil.example/cover.png", "MEDIA_TYPE_INVALID"),
            ("bib-cover", "https://khong-co.example/x.jpg", "MEDIA_FETCH_FAILED"),
            ("bib-cover", "ftp://x/y.jpg", "MEDIA_URL_INVALID"),
            ("attachment", "https://covers.openlibrary.org/b/isbn/9780747532699-L.jpg", "MEDIA_IMPORT_NOT_ALLOWED"),
            ("tenant-logo", "https://covers.openlibrary.org/b/isbn/9780747532699-L.jpg", "MEDIA_IMPORT_NOT_ALLOWED"),
        })
        {
            var response = await client.PostAsJsonAsync(U("/api/files/import"), new ImportRequest(purpose, url));
            Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, await ErrorCode(response)));
        }
    }
}
