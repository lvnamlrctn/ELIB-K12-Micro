using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Kết quả đánh lại 1 mã vạch — <see cref="Updated"/> null khi <c>previewOnly</c> (chưa ghi gì).</summary>
public record BarcodeReRegisterOutcome(bool Success, string? Error, string OldBarcode, string NewBarcode, Dictionary<string, int>? Updated);

/// <summary>Logic đánh lại mã ĐKCB dùng chung giữa <c>StoreReRegisterBarcodeController.ReRegister</c> (1 mã/lần)
/// và tác vụ nền "barcode-reregister-import" (Đợt 22.5 — hàng loạt qua Excel) — tách ra để 2 nơi không lệch
/// hành vi. KHÔNG tự mở transaction (khác bản gốc trong controller): tác vụ nền chạy trong transaction
/// Serializable đã mở sẵn bởi <c>AdminTaskChunks.RunNext</c>; bản đơn lẻ tự mở transaction ở controller.</summary>
public class BarcodeReRegisterService(ELIBAPIDbContext db)
{
    public async Task<BarcodeReRegisterOutcome> ExecuteAsync(long? tenantId, string oldBarcode, string newBarcode,
        long? storeId, long? userId, string? actorName, string? ip, bool previewOnly)
    {
        var (bc, ambiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, oldBarcode, tenantId);
        if (ambiguous) return new(false, BarcodeTenantLookup.AmbiguousMessage, oldBarcode, newBarcode, null);
        if (bc == null) return new(false, "Mã vạch cũ không tồn tại", oldBarcode, newBarcode, null);

        // Dữ liệu cũ có mã nhập trùng nhiều dòng trong cùng đơn vị — trước đây đổi bừa 1 bản (port ELIB-LRC 10-04: báo lỗi).
        if (await db.Barcodes.CountAsync(x => x.BarcodeValue == bc.BarcodeValue && x.TenantId == bc.TenantId && x.IsDelete != 2) > 1)
            return new(false, "Mã vạch cũ bị trùng nhiều bản trong đơn vị — đổi từng bản ở màn hình chất lượng dữ liệu", oldBarcode, newBarcode, null);

        // Mã ĐKCB duy nhất theo đơn vị (Đợt 20) — chỉ xung đột với mã cùng đơn vị của bản đang đánh lại. Loại chính bản
        // đang đổi và so không phân biệt hoa thường: đổi "ck.005" → "CK.005" trước đây bị báo "đã tồn tại" (trùng chính nó).
        var lowerNew = newBarcode.Trim().ToLower();
        var conflict = await db.Barcodes.AnyAsync(x => x.Id != bc.Id && x.BarcodeValue!.Trim().ToLower() == lowerNew
                                                       && x.TenantId == bc.TenantId && x.IsDelete != 2);
        if (conflict) return new(false, "Mã vạch mới đã tồn tại", oldBarcode, newBarcode, null);

        if (previewOnly) return new(true, null, oldBarcode, newBarcode, null);

        var oldBarcodeValue = bc.BarcodeValue;
        var oldStore = bc.Store;
        bc.BarcodeValue = newBarcode;
        if (storeId.HasValue) bc.Store = (int?)storeId;
        bc.UpdateRowBy = userId;
        bc.UpdatedRowDate = DateTime.Now;

        var changes = new List<EntityAuditService.FieldChange>
        {
            new() { Field = nameof(Barcode.BarcodeValue), OldValue = oldBarcodeValue, NewValue = bc.BarcodeValue },
        };
        if (oldStore != bc.Store)
            changes.Add(new EntityAuditService.FieldChange { Field = nameof(Barcode.Store), OldValue = oldStore?.ToString(), NewValue = bc.Store?.ToString() });
        EntityAuditService.QueueEntityChangeLog(db, "Barcode", bc.PublicId, userId ?? 0, actorName, tenantId, "ReRegister", reason: null, changes, ip: ip);

        await db.SaveChangesAsync();
        var updated = await RenameHistoryAsync(bc, oldBarcodeValue!, bc.BarcodeValue!);
        return new(true, null, oldBarcode, newBarcode, updated);
    }

    /// <summary>Các bảng lưu chuỗi mã ĐKCB (phiếu mượn/trả, đặt mượn, phạt, ảnh, kiểm kê, sách mất) đổi theo mã mới —
    /// trước đây giữ mã cũ nên lịch sử mượn, phạt, sách mất… của bản sách "biến mất" sau khi đánh lại mã, còn phiếu
    /// đang mượn thì trả bằng mã mới không khớp. Chỉ đổi bản ghi cùng đơn vị với bản sách (mã duy nhất theo đơn vị);
    /// bản ghi đã gắn chắc với BẢN SÁCH KHÁC (BookOut.Reg_Seq_Id khác, hoặc BookIn/C_Fine trỏ về phiếu mượn đó) được
    /// giữ nguyên — trường hợp mã cũ từng thuộc bản khác trước khi bản đó được đánh lại mã.</summary>
    private async Task<Dictionary<string, int>> RenameHistoryAsync(Barcode bc, string oldValue, string newValue)
    {
        var t = bc.TenantId ?? 0;
        var id = bc.Id;
        var otherCopyLoans = db.BookOuts.Where(o => o.Reg_Seq_Id != null && o.Reg_Seq_Id != id).Select(o => o.Id);

        return new Dictionary<string, int>
        {
            ["BookOut"] = await db.BookOuts
                .Where(x => x.Reg_Seq_Id == id || (x.Reg_Seq_Id == null && x.Barcode == oldValue && (x.TenantId ?? 0) == t))
                .Where(x => x.Barcode != newValue)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["BookIn"] = await db.BookIns
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t
                         && (x.BookOutId == null || !otherCopyLoans.Contains(x.BookOutId.Value)))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["C_Fine"] = await db.CFines
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t
                         && (x.Borrow_Id == null || !otherCopyLoans.Contains(x.Borrow_Id.Value)))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["BookRequest"] = await db.BookRequests
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["C_photo"] = await db.CPhotos
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["InventoryBarcode"] = await db.InventoryBarcodes
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
            ["LostBook"] = await db.LostBooks
                .Where(x => x.Barcode == oldValue && (x.TenantId ?? 0) == t)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Barcode, newValue)),
        };
    }
}
