using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>Ảnh khuôn mặt bổ sung của bạn đọc (ngoài ảnh thẻ): id file đã upload xong ở media (mục đích reader-photo).</summary>
public sealed record ReaderExtraPhotoRequest(Guid FileId);

public sealed record ReaderExtraPhotoDto(Guid FileId, DateTimeOffset CreatedAt);

/// <summary>
/// Ảnh khuôn mặt bổ sung (ELIB-LRC: ReaderPhoto) — dùng cùng ảnh thẻ khi nhận diện khuôn mặt, để nhận ra bạn đọc dù đổi kiểu tóc, đeo kính,
/// góc chụp khác. Mỗi bạn đọc tối đa <see cref="ReaderPhoto.MaxPerReader"/> ảnh; xoá bạn đọc thì xoá luôn các dòng này (khoá ngoại cascade).
/// </summary>
public sealed partial class ReaderResource
{
    public async Task<IReadOnlyList<ReaderExtraPhotoDto>> ExtraPhotosAsync(Guid publicId, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        return await Db.Set<ReaderPhoto>().AsNoTracking().Where(p => p.ReaderId == reader.Id).OrderBy(p => p.Id)
            .Select(p => new ReaderExtraPhotoDto(p.FileId, p.CreatedAt)).ToListAsync(ct);
    }

    public async Task<ReaderExtraPhotoDto> AddExtraPhotoAsync(Guid publicId, Guid fileId, CancellationToken ct)
    {
        if (fileId == Guid.Empty) throw new BusinessRuleException("PHOTO_FILE_REQUIRED", "Chưa chọn ảnh.");
        var reader = await LoadAsync(publicId, ct);
        var existing = await Db.Set<ReaderPhoto>().Where(p => p.ReaderId == reader.Id).Select(p => p.FileId).ToListAsync(ct);
        if (existing.Contains(fileId)) throw new BusinessRuleException("PHOTO_EXISTS", "Ảnh này đã được thêm.");
        if (existing.Count >= ReaderPhoto.MaxPerReader)
            throw new BusinessRuleException("PHOTO_LIMIT", $"Mỗi bạn đọc tối đa {ReaderPhoto.MaxPerReader} ảnh bổ sung — xoá bớt ảnh cũ trước.");
        var photo = ReaderPhoto.Create(reader.Id, fileId, clock.GetUtcNow());
        Db.Set<ReaderPhoto>().Add(photo);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Reader), EntityName, reader.PublicId, CrudChange.Updated, "thêm ảnh khuôn mặt bổ sung"), ct);
        await Db.SaveChangesAsync(ct);
        return new ReaderExtraPhotoDto(photo.FileId, photo.CreatedAt);
    }

    public async Task RemoveExtraPhotoAsync(Guid publicId, Guid fileId, CancellationToken ct)
    {
        var reader = await LoadAsync(publicId, ct);
        var photo = await Db.Set<ReaderPhoto>().FirstOrDefaultAsync(p => p.ReaderId == reader.Id && p.FileId == fileId, ct)
            ?? throw new NotFoundException("Ảnh bổ sung", fileId);
        Db.Set<ReaderPhoto>().Remove(photo);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Reader), EntityName, reader.PublicId, CrudChange.Updated, "xoá ảnh khuôn mặt bổ sung"), ct);
        await Db.SaveChangesAsync(ct);
    }
}
