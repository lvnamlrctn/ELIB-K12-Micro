using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Contracts.Events.Acquisition;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Bản sao đầu phiếu nhận (<see cref="ReceiptSnapshot"/>) — ghi từ event <see cref="ReceiptChanged"/> hoặc trạng thái acquisition gửi kèm.</summary>
public sealed class ReceiptSnapshots(ICrudDbContext db)
{
    /// <summary>Upsert theo ReceiptPublicId; trạng thái cũ hơn bản đang có bị bỏ qua. Chưa lưu — nơi gọi SaveChanges.</summary>
    public async Task<ReceiptSnapshot> ApplyAsync(ReceiptChanged e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var snapshot = await db.Set<ReceiptSnapshot>().FirstOrDefaultAsync(x => x.ReceiptPublicId == e.ReceiptPublicId, ct);
        if (snapshot is null)
        {
            snapshot = new ReceiptSnapshot { ReceiptPublicId = e.ReceiptPublicId, Version = -1 };
            db.Set<ReceiptSnapshot>().Add(snapshot);
        }
        if (e.Version <= snapshot.Version) return snapshot;

        snapshot.Code = e.Code;
        snapshot.Name = e.Name is { Length: > 250 } name ? name[..250] : e.Name;
        snapshot.ReceivedOn = e.ReceivedOn;
        snapshot.StoreId = e.StoreId;
        snapshot.Completed = e.Completed;
        snapshot.Deleted = e.Deleted;
        snapshot.Version = e.Version;
        return snapshot;
    }
}

/// <summary>
/// Đăng ký ĐKCB cho một dòng phiếu nhận — acquisition gọi qua /internal kèm trạng thái hiện tại của phiếu (đọc ngay trong DB của
/// acquisition, không chờ event). Theo lô (<see cref="Quantity"/> + tiền tố) hoặc một mã nhập tay (<see cref="Barcode"/>).
/// </summary>
public sealed record ReceiptItemsRequest(
    ReceiptChanged Receipt, Guid LinePublicId, int Quantity = 0, string? Prefix = null, int Digits = 6, long? StartNumber = null,
    string? Barcode = null, long? StoreId = null);

/// <summary>Bản sách của một phiếu nhận; <see cref="Delivered"/>: đã nằm ở phiếu giao nhận.</summary>
public sealed record ReceiptItemDto(
    Guid PublicId, Guid? LinePublicId, string Barcode, long Mfn, long? StoreId, string? StoreName, string Status, bool OnLoan, bool Delivered,
    DateTimeOffset CreatedAt);

/// <summary>
/// ĐKCB theo phiếu nhận của phân hệ bổ sung (monolith: CatalogueReceiptController RegisterBarcodes / RegisterSingleBarcode / DeleteBarcode).
/// Khoá số lượng (port ELIB-LRC 09-22): số ĐKCB của một dòng không vượt số bản nhận của dòng — chặn TRƯỚC khi ghi; hai phiên đăng ký
/// cùng phiếu một lúc thì phiên sau bị từ chối (khoá lạc quan trên bản sao phiếu) thay vì cùng vượt số lượng.
/// </summary>
public sealed class ReceiptItems(ICrudDbContext db, ItemResource items, BibSnapshots bibs, ReceiptSnapshots receipts, ItemEvents events,
    ICrudAuditSink audit)
{
    public async Task<IReadOnlyList<ReceiptItemDto>> ListAsync(Guid receiptPublicId, CancellationToken ct) => await (
        from i in db.Set<Item>().AsNoTracking().Where(i => i.ReceiptPublicId == receiptPublicId)
        join st in db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        orderby i.BarcodeKey
        select new ReceiptItemDto(i.PublicId, i.ReceiptLinePublicId, i.Barcode, i.BibId, i.StoreId, st == null ? null : st.Name, i.Status, i.OnLoan,
            db.Set<DeliveryLine>().Any(l => l.ItemId == i.Id), i.CreatedAt)).ToListAsync(ct);

    public async Task<IReadOnlyList<ItemDto>> RegisterAsync(ReceiptItemsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Receipt);
        var line = request.Receipt.Lines.FirstOrDefault(l => l.LinePublicId == request.LinePublicId)
            ?? throw new NotFoundException("Dòng phiếu nhận", request.LinePublicId);
        var receipt = await receipts.ApplyAsync(request.Receipt, ct);
        if (receipt.Deleted || receipt.Completed)
        {
            await SaveAsync(ct); // giữ trạng thái mới nhất — yêu cầu mang trạng thái cũ hơn tới sau vẫn bị chặn
            if (receipt.Deleted) throw new NotFoundException("Phiếu nhận", request.Receipt.Code);
            throw new BusinessRuleException("RECEIPT_COMPLETED", $"Phiếu nhận số {receipt.Code} đã hoàn tất — không đăng ký thêm ĐKCB được.");
        }

        var single = !string.IsNullOrWhiteSpace(request.Barcode);
        var wanted = single ? 1 : request.Quantity;
        var registered = await db.Set<Item>().CountAsync(i => i.ReceiptLinePublicId == line.LinePublicId, ct);
        if (wanted > 0 && registered + wanted > line.Quantity)
            throw new BusinessRuleException("RECEIPT_LINE_FULL", registered >= line.Quantity
                ? $"Đã đăng ký đủ {line.Quantity} bản của dòng phiếu nhận (MFN {line.Mfn})."
                : $"Số lượng đăng ký vượt số bản còn lại của dòng phiếu nhận (MFN {line.Mfn}: còn {line.Quantity - registered}).");

        var bib = await bibs.RequireAsync(line.Mfn, ct);
        var storeId = request.StoreId ?? receipt.StoreId;
        void Attach(Item item) => item.AttachReceipt(receipt.ReceiptPublicId, line.LinePublicId);
        var added = single
            ? [await items.AddSingleAsync(bib, request.Barcode!, storeId, Attach, ct)]
            : await items.AddBatchAsync(bib, new RegisterItemsRequest(line.Mfn, request.Quantity, request.Prefix, request.Digits, request.StartNumber, storeId),
                Attach, ct);
        receipt.RegistrationStamp++;
        await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", bib.BibPublicId, CrudChange.Added,
            $"Đăng ký {added.Count} ĐKCB {added[0].Barcode}" + (added.Count > 1 ? $"–{added[^1].Barcode}" : "")
            + $" cho MFN {bib.Mfn} theo phiếu nhận số {receipt.Code}"), ct);
        await SaveAsync(ct);
        return await items.ListAsync(added.Select(i => i.Id).ToList(), ct);
    }

    /// <summary>Xoá ĐKCB đăng ký nhầm khỏi phiếu nhận — chỉ bản còn "chưa xếp giá", chưa lưu thông, chưa nằm ở phiếu giao nhận.</summary>
    public async Task RemoveAsync(Guid receiptPublicId, Guid itemPublicId, CancellationToken ct)
    {
        var item = await db.Set<Item>().FirstOrDefaultAsync(i => i.PublicId == itemPublicId && i.ReceiptPublicId == receiptPublicId, ct)
            ?? throw new NotFoundException("Bản sách của phiếu nhận", itemPublicId);
        if (item.Status != ItemStatus.Unshelved || item.OnLoan)
            throw new ConflictException("ITEM_IN_USE", $"ĐKCB {item.Barcode} đã xếp giá hoặc đang lưu thông — không xoá khỏi phiếu nhận được.");
        var delivery = await db.Set<DeliveryLine>().Where(l => l.ItemId == item.Id).Select(l => (long?)l.DeliveryId).FirstOrDefaultAsync(ct);
        if (delivery is not null)
            throw new ConflictException("ITEM_IN_DELIVERY", $"ĐKCB {item.Barcode} đã nằm ở phiếu giao nhận số {delivery} — bỏ khỏi phiếu giao nhận trước.");

        db.Set<Item>().Remove(item); // interceptor chuyển thành xoá mềm
        item.MarkDeleted();
        await events.PublishAsync(item, deleted: true, ct);
        await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", item.PublicId, CrudChange.Deleted,
            $"ĐKCB {item.Barcode} (MFN {item.BibId}) — xoá khỏi phiếu nhận"), ct);
        await SaveAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Phiên khác vừa đăng ký trên cùng phiếu (RegistrationStamp đổi) hoặc vừa lấy cùng số ĐKCB / vừa tạo bản sao phiếu.
            throw new ConflictException("RECEIPT_BUSY", "Phiếu nhận vừa được cập nhật ở phiên khác — tải lại số lượng rồi thử lại.");
        }
    }
}
