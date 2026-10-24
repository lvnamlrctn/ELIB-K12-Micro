using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Đóng tập báo/tạp chí tạo một ĐKCB: số ĐKCB = <see cref="AccessionNo"/>, gắn biểu ghi đầu báo <see cref="Mfn"/>.</summary>
public sealed record SerialItemRegisterRequest(Guid BindingPublicId, long Mfn, string AccessionNo, long? StoreId = null, string? Note = null);

/// <summary>ĐKCB của tập đóng (trả cả khi gọi lại — idempotent theo mã tập đóng).</summary>
public sealed record SerialItemDto(Guid PublicId, string Barcode, long Mfn, long? StoreId, string? StoreName, string Status);

/// <summary>
/// ĐKCB cho tập đóng báo/tạp chí của phân hệ Ấn phẩm định kỳ (serials gọi qua /internal). Đăng ký idempotent theo
/// <see cref="SerialItemRegisterRequest.BindingPublicId"/>; gỡ theo cùng quy tắc xoá ĐKCB khỏi phiếu nhận: chỉ bản còn "chưa xếp giá",
/// chưa lưu thông, chưa nằm ở phiếu giao nhận.
/// </summary>
public sealed class SerialItems(ICrudDbContext db, ItemResource items, BibSnapshots bibs, ItemEvents events, ICrudAuditSink audit)
{
    public async Task<SerialItemDto> RegisterAsync(SerialItemRegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await Load(request.BindingPublicId).FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        if (string.IsNullOrWhiteSpace(request.AccessionNo))
            throw new BusinessRuleException("BARCODE_REQUIRED", "Nhập số ĐKCB của tập đóng.");

        var bib = await bibs.RequireAsync(request.Mfn, ct);
        var item = await items.AddSingleAsync(bib, request.AccessionNo, request.StoreId, i => i.AttachSerialBinding(request.BindingPublicId), ct);
        await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", bib.BibPublicId, CrudChange.Added,
            $"Đăng ký ĐKCB {item.Barcode} cho MFN {bib.Mfn} theo tập đóng báo/tạp chí"), ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Hai lần gọi cùng lúc cho một tập đóng, hoặc trùng ĐKCB vừa được đăng ký ở phiên khác.
            if (await Load(request.BindingPublicId).FirstOrDefaultAsync(ct) is { } raced) return raced;
            throw new ConflictException("ITEM_BARCODE_EXISTS", $"ĐKCB {item.Barcode} đã tồn tại.");
        }
        return await Load(request.BindingPublicId).FirstAsync(ct);
    }

    public async Task RemoveAsync(Guid bindingPublicId, CancellationToken ct)
    {
        var list = await db.Set<Item>().Where(i => i.SerialBindingPublicId == bindingPublicId).ToListAsync(ct);
        foreach (var item in list)
        {
            if (item.Status != ItemStatus.Unshelved || item.OnLoan)
                throw new ConflictException("ITEM_IN_USE", $"ĐKCB {item.Barcode} đã xếp giá hoặc đang lưu thông — không gỡ khỏi tập đóng được.");
            var delivery = await db.Set<DeliveryLine>().Where(l => l.ItemId == item.Id).Select(l => (long?)l.DeliveryId).FirstOrDefaultAsync(ct);
            if (delivery is not null)
                throw new ConflictException("ITEM_IN_DELIVERY", $"ĐKCB {item.Barcode} đã nằm ở phiếu giao nhận số {delivery} — bỏ khỏi phiếu giao nhận trước.");
        }
        foreach (var item in list)
        {
            db.Set<Item>().Remove(item); // interceptor chuyển thành xoá mềm
            item.MarkDeleted();
            await events.PublishAsync(item, deleted: true, ct);
            await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", item.PublicId, CrudChange.Deleted,
                $"ĐKCB {item.Barcode} (MFN {item.BibId}) — gỡ khỏi tập đóng báo/tạp chí"), ct);
        }
        if (list.Count > 0) await db.SaveChangesAsync(ct);
    }

    private IQueryable<SerialItemDto> Load(Guid bindingPublicId) =>
        from i in db.Set<Item>().AsNoTracking().Where(i => i.SerialBindingPublicId == bindingPublicId)
        join st in db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        select new SerialItemDto(i.PublicId, i.Barcode, i.BibId, i.StoreId, st == null ? null : st.Name, i.Status);
}
