using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record EbookFileUrl(string Url, DateTimeOffset? ExpiresAt);

/// <summary>URL có hạn của một file ở media — digital tự kiểm quyền trước khi xin (quyết định #3 của plan).</summary>
public interface IMediaFileAccess
{
    Task<EbookFileUrl> SignedUrlAsync(Guid mediaFileId, CancellationToken ct);
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
        var item = await db.Set<EbookItem>().FirstOrDefaultAsync(x => x.PublicId == itemPublicId && x.Status == IHasStatus.Active, ct)
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

        var url = await media.SignedUrlAsync(file.MediaFileId, ct);
        if (download) item.RecordDownload(); else item.RecordView();
        db.Set<EbookAccessLog>().Add(EbookAccessLog.Create(item.Id, readerPublicId, download, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return url;
    }
}
