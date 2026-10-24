using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record EbookFileUrl(string Url, DateTimeOffset? ExpiresAt);

/// <summary>URL có hạn của một file ở media — digital tự kiểm quyền trước khi xin (quyết định #3 của plan).</summary>
public interface IMediaFileAccess
{
    /// <param name="mediaFileId">Id file ở media.</param>
    /// <param name="sourceTenantId">Đơn vị giữ file khi đây là file của tài liệu dùng chung (đơn vị chủ SHARED); null = đơn vị hiện tại.</param>
    Task<EbookFileUrl> SignedUrlAsync(Guid mediaFileId, long? sourceTenantId, CancellationToken ct);
}

/// <summary>
/// Cấp URL đọc/tải file tài liệu số — tự kiểm quyền TRƯỚC khi xin URL từ media (quyết định #3): tài liệu công khai (Free) thì ai
/// cũng xin được; còn lại phải có lượt mượn còn hiệu lực (xem EbookLoanResource.ValidateAccessAsync). Tải (<paramref name="download"/>)
/// còn cần AllowDownload.
/// </summary>
public sealed class EbookFileAccess(ICrudDbContext db, EbookLoanResource loans, IMediaFileAccess media, TimeProvider clock)
{
    public async Task<EbookFileUrl> SignedUrlAsync(Guid itemPublicId, Guid filePublicId, Guid? readerPublicId, bool download, CancellationToken ct)
    {
        var item = await db.Set<EbookItem>().FirstOrDefaultAsync(x => x.PublicId == itemPublicId && x.Status == IHasStatus.Active && !x.HiddenByTenant, ct)
            ?? throw new NotFoundException("Tài liệu số", itemPublicId);
        var file = await db.Set<EbookFile>().AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == filePublicId && f.EbookItemId == item.Id, ct)
            ?? throw new NotFoundException("File", filePublicId);
        if (download && !item.AllowDownload)
            throw new BusinessRuleException("EBOOK_DOWNLOAD_NOT_ALLOWED", "Tài liệu này không cho phép tải về.", 403);

        if (!item.Free)
        {
            if (readerPublicId is not { } reader)
                throw new BusinessRuleException("LOGIN_REQUIRED", "Đăng nhập để đọc tài liệu này.", 401);
            if (!await loans.ValidateAccessAsync(item.Id, reader, ct))
                throw new BusinessRuleException("EBOOK_NOT_BORROWED", "Bạn chưa mượn tài liệu này hoặc đã hết hạn mượn.", 403);
        }

        // File của tài liệu dùng chung nằm ở kho của đơn vị chủ — chỉ tin SourceTenantId khi đó đúng là đơn vị SHARED (không cho dòng bất kỳ trỏ sang kho đơn vị khác).
        long? sourceTenant = null;
        if (item.IsReplica && file.IsReplica && file.SourceTenantId is { } source && item.SourceTenantId == source
            && await db.Set<TenantReplicaRecord>().AsNoTracking().AnyAsync(t => t.TenantId == source && t.Code == SharedTenant.Code, ct))
            sourceTenant = source;
        var url = await media.SignedUrlAsync(file.MediaFileId, sourceTenant, ct);
        if (download) item.RecordDownload(); else item.RecordView();
        db.Set<EbookAccessLog>().Add(EbookAccessLog.Create(item.Id, readerPublicId, download, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return url;
    }
}
