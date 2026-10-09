using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>File đính kèm tin tức — xem <see cref="IAttachFileService"/>.</summary>
public class AttachFileService(
    ELIBAPIDbContext db,
    IAttachFileRepository files,
    IMinioService storage,
    IConfiguration config,
    ILogger<AttachFileService> logger) : IAttachFileService
{
    /// <summary>Khớp client_max_body_size 50m của nginx.</summary>
    public const long MaxBytes = 50L * 1024 * 1024;

    /// <summary>Chỉ nhận tài liệu/ảnh/nén/âm thanh/video thông dụng — chặn file chạy được hoặc hiển thị như trang web
    /// (exe, html, svg, js, chm…) vì file được tải về từ chính tên miền của thư viện.</summary>
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "txt", "csv", "rtf",
        "zip", "rar", "7z", "jpg", "jpeg", "png", "gif", "webp", "mp3", "mp4",
    };

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<ServiceResult<AttachFile>> UploadAsync(Stream content, string fileName, string contentType, long length, Guid newsPublicId,
        string? displayName, long? tenantId, long? userId)
    {
        if (length <= 0) return ServiceResult<AttachFile>.BadRequest("File rỗng.");
        if (length > MaxBytes) return ServiceResult<AttachFile>.BadRequest("File vượt quá 50 MB.");
        var ext = FileExtension.Normalize(null, fileName);
        if (!AllowedExtensions.Contains(ext))
            return ServiceResult<AttachFile>.BadRequest($"Không cho phép đính kèm file .{(ext == "" ? "?" : ext)}. Định dạng hợp lệ: {string.Join(", ", AllowedExtensions)}.");

        // Tenant: chỉ đính kèm vào tin của đơn vị mình; file gắn đơn vị của tin (kể cả khi tài khoản hệ thống tải lên).
        var news = await db.News.Where(n => n.PublicId == newsPublicId && n.IsDelete != 2
                && (!tenantId.HasValue || n.TenantId == tenantId))
            .Select(n => new { n.Id, n.TenantId }).FirstOrDefaultAsync();
        if (news == null) return ServiceResult<AttachFile>.NotFound("Không tìm thấy tin tức.");

        var name = Clean(displayName) ?? Clean(Path.GetFileName(fileName)) ?? $"file.{ext}";
        var objectName = await storage.UploadPrivateAsync(content, $"attach.{ext}", ContentTypeOf(ext, contentType));
        var now = DateTime.Now;
        var file = new AttachFile
        {
            Name = EnsureExtension(name, ext), Url = objectName, FileSize = Math.Round(length / 1024.0, 2), NewsId = news.Id,
            TenantId = news.TenantId, PublicId = Guid.NewGuid(), CreatedDate = now, CreatedRowDate = now, CreatedRowBy = userId,
        };
        db.AttachFiles.Add(file);
        try { await db.SaveChangesAsync(); }
        catch
        {
            // Không để object mồ côi trong kho khi ghi CSDL lỗi.
            try { await storage.DeletePrivateAsync(objectName); } catch { /* đã ghi log ở dưới nếu cần */ }
            throw;
        }
        return ServiceResult<AttachFile>.Ok(file);
    }

    public async Task<ServiceResult<AttachFileContent>> OpenAsync(Guid filePublicId)
    {
        var file = await files.GetByPublicIdAsync(filePublicId); // repository: chỉ file của đơn vị mình hoặc dùng chung
        return file == null ? ServiceResult<AttachFileContent>.NotFound("Không tìm thấy file đính kèm.") : await ReadAsync(file);
    }

    public async Task<ServiceResult<AttachFileContent>> OpenPublicAsync(Guid filePublicId, Guid? hostTenantPublicId)
    {
        var file = await db.AttachFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == filePublicId && f.IsDelete != 2);
        if (file == null || file.NewsId == null || !await PublishedNews(hostTenantPublicId).AnyAsync(n => n.Id == file.NewsId))
            return ServiceResult<AttachFileContent>.NotFound("Không tìm thấy file đính kèm.");
        return await ReadAsync(file);
    }

    private async Task<ServiceResult<AttachFileContent>> ReadAsync(AttachFile file)
    {
        if (IsLegacy(file.Url)) return ServiceResult<AttachFileContent>.NotFound("File chưa được đồng bộ lên kho lưu trữ.");
        var ext = FileExtension.Normalize(null, file.Name) is { Length: > 0 } e ? e : FileExtension.Normalize(null, file.Url);
        try
        {
            var (stream, _) = await storage.GetObjectStreamAsync(file.Url!);
            return ServiceResult<AttachFileContent>.Ok(new AttachFileContent(stream, ContentTypeOf(ext, null), EnsureExtension(file.Name ?? "file", ext)));
        }
        catch (Exception ex)
        {
            // Không trả tên bucket/object cho người gọi — chỉ ghi log.
            logger.LogWarning(ex, "Không đọc được file đính kèm {FileId} ({Object})", file.Id, file.Url);
            return ServiceResult<AttachFileContent>.NotFound("File không tồn tại trong kho lưu trữ.");
        }
    }

    public async Task<List<AttachFile>> PublishedFilesAsync(Guid newsPublicId, Guid? hostTenantPublicId)
    {
        var newsId = await PublishedNews(hostTenantPublicId).Where(n => n.PublicId == newsPublicId).Select(n => (long?)n.Id).FirstOrDefaultAsync();
        if (newsId == null) return [];
        return await db.AttachFiles.AsNoTracking().Where(f => f.NewsId == newsId && f.IsDelete != 2)
            .OrderByDescending(f => f.CreatedDate).ThenByDescending(f => f.Id).ToListAsync();
    }

    /// <summary>Tin đã xuất bản; có đơn vị theo tên miền thì chỉ tin của đơn vị đó hoặc dùng chung (như GetNewsById).</summary>
    private IQueryable<News> PublishedNews(Guid? hostTenantPublicId)
    {
        var q = db.News.AsNoTracking().Where(n => n.IsDelete != 2 && n.Status == 2);
        if (hostTenantPublicId is { } t && t != Guid.Empty)
        {
            var tenantIds = db.Tenants.Where(d => d.PublicId == t && d.IsDelete != 2).Select(d => (long?)d.Id);
            q = q.Where(n => n.TenantId == null || tenantIds.Contains(n.TenantId));
        }
        return q;
    }

    public async Task<ServiceResult<bool>> RenameAsync(Guid filePublicId, string name)
    {
        var clean = Clean(name);
        if (clean == null) return ServiceResult<bool>.BadRequest("Tên file không được để trống.");
        if (clean.Length > 255) return ServiceResult<bool>.BadRequest("Tên file tối đa 255 ký tự.");
        var file = await files.GetByPublicIdAsync(filePublicId);
        if (file == null) return ServiceResult<bool>.NotFound("Không tìm thấy file đính kèm.");
        // Giữ đuôi file để tải về vẫn mở đúng ứng dụng.
        var ext = FileExtension.Normalize(null, file.Name) is { Length: > 0 } e ? e : FileExtension.Normalize(null, file.Url);
        await files.UpdateAsync(filePublicId, new AttachFileRequest { Name = EnsureExtension(clean, ext), Url = file.Url, FileSize = file.FileSize, NewsId = file.NewsId });
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid filePublicId)
    {
        var file = await files.GetByPublicIdAsync(filePublicId);
        if (file == null) return ServiceResult<bool>.NotFound("Không tìm thấy file đính kèm.");
        await files.DeleteAsync(filePublicId);
        if (!string.IsNullOrEmpty(file.Url) && !IsLegacy(file.Url))
        {
            try { await storage.DeletePrivateAsync(file.Url); }
            catch (Exception ex) { logger.LogWarning(ex, "Đã xoá file đính kèm {FileId} nhưng không xoá được object {Object}", file.Id, file.Url); }
        }
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<AttachFileSyncResult>> SyncLegacyFilesAsync(long? tenantId)
    {
        var root = config["PathSettings:PathAttachment"];
        if (string.IsNullOrWhiteSpace(root))
            return ServiceResult<AttachFileSyncResult>.BadRequest("Chưa cấu hình thư mục file cũ (PathSettings:PathAttachment).");

        var legacy = await files.GetLegacyAsync(tenantId);
        int synced = 0, failed = 0;
        var errors = new List<string>();
        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar)) rootFull += Path.DirectorySeparatorChar;
        foreach (var file in legacy)
        {
            var fullPath = Path.GetFullPath(Path.Combine(rootFull, file.Url!.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar)));
            // Url lấy từ CSDL cũ — không cho thoát khỏi thư mục cấu hình bằng "../" (kể cả thư mục anh em cùng tiền tố tên).
            if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) { failed++; errors.Add($"Id={file.Id}: đường dẫn không hợp lệ"); continue; }
            if (!File.Exists(fullPath)) { failed++; errors.Add($"Id={file.Id}: không thấy file {file.Url}"); continue; }
            try
            {
                var ext = FileExtension.Normalize(null, fullPath);
                await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var objectName = await storage.UploadPrivateAsync(stream, $"attach.{(ext == "" ? "bin" : ext)}", ContentTypeOf(ext, null));
                if (await files.UpdateUrlAsync(file.Id, objectName)) synced++;
                else { failed++; errors.Add($"Id={file.Id}: cập nhật CSDL thất bại sau khi tải lên kho"); }
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Đồng bộ file đính kèm cũ {FileId} lỗi", file.Id);
                errors.Add($"Id={file.Id}: {ex.GetType().Name}");
            }
        }
        return ServiceResult<AttachFileSyncResult>.Ok(new AttachFileSyncResult(synced, failed, legacy.Count, errors));
    }

    public static bool IsLegacy(string? url) => url?.StartsWith("Upload", StringComparison.OrdinalIgnoreCase) == true;

    private static string ContentTypeOf(string ext, string? fallback) =>
        ContentTypes.TryGetContentType($"x.{ext}", out var ct) ? ct
        : string.IsNullOrWhiteSpace(fallback) ? "application/octet-stream" : fallback;

    private static string? Clean(string? s)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        foreach (var c in Path.GetInvalidFileNameChars().Concat(['/', '\\'])) t = t.Replace(c, '_');
        return t;
    }

    private static string EnsureExtension(string name, string ext) =>
        ext == "" || name.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.{ext}";
}
