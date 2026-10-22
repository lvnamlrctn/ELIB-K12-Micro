using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record BookmarkRequest(Guid EbookPublicId, int Page, string? Note);

public sealed record BookmarkDto(Guid PublicId, Guid EbookPublicId, int Page, string? Note, DateTimeOffset CreatedAt);

/// <summary>Dấu trang / ghi chú cá nhân của bạn đọc trong trình đọc — riêng tư, lấy bạn đọc từ claim. Mỗi trang một dấu (đặt lại = sửa ghi chú).</summary>
public sealed class ReaderBookmarks(ICrudDbContext db)
{
    public const int MaxPerEbook = 200;

    public async Task<IReadOnlyList<BookmarkDto>> ListAsync(Guid readerPublicId, Guid ebookPublicId, CancellationToken ct)
    {
        var itemId = await db.Set<EbookItem>().Where(i => i.PublicId == ebookPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) return [];
        return await db.Set<EbookBookmark>().AsNoTracking().Where(b => b.EbookItemId == itemId && b.ReaderPublicId == readerPublicId)
            .OrderBy(b => b.Page).Select(b => new BookmarkDto(b.PublicId, ebookPublicId, b.Page, b.Note, b.CreatedAt)).ToListAsync(ct);
    }

    public async Task<BookmarkDto> SaveAsync(Guid readerPublicId, BookmarkRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await db.Set<EbookItem>().AsNoTracking().Where(i => i.PublicId == request.EbookPublicId && i.Status == IHasStatus.Active).Select(i => new { i.Id }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Tài liệu số", request.EbookPublicId);
        var mine = db.Set<EbookBookmark>().Where(b => b.EbookItemId == item.Id && b.ReaderPublicId == readerPublicId);
        var bookmark = await mine.FirstOrDefaultAsync(b => b.Page == request.Page, ct);
        if (bookmark is null)
        {
            if (await mine.CountAsync(ct) >= MaxPerEbook)
                throw new BusinessRuleException("BOOKMARK_LIMIT", $"Tối đa {MaxPerEbook} dấu trang cho một tài liệu.");
            db.Set<EbookBookmark>().Add(bookmark = EbookBookmark.Create(item.Id, readerPublicId, request.Page, request.Note));
        }
        else bookmark.UpdateNote(request.Note);
        await db.SaveChangesAsync(ct);
        return new BookmarkDto(bookmark.PublicId, request.EbookPublicId, bookmark.Page, bookmark.Note, bookmark.CreatedAt);
    }

    public async Task DeleteAsync(Guid readerPublicId, Guid bookmarkPublicId, CancellationToken ct)
    {
        var bookmark = await db.Set<EbookBookmark>().FirstOrDefaultAsync(b => b.PublicId == bookmarkPublicId && b.ReaderPublicId == readerPublicId, ct);
        if (bookmark is null) return; // không phải của bạn đọc này (hoặc đã xoá) — không lộ sự tồn tại
        db.Set<EbookBookmark>().Remove(bookmark);
        await db.SaveChangesAsync(ct);
    }
}
