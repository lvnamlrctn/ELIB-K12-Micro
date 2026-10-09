using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Đợt 20 — mã ĐKCB chỉ duy nhất TRONG 1 đơn vị (2 đơn vị có thể cùng dùng "NV.00001"), nên mọi chỗ tìm bản sách
/// từ chuỗi mã phải kèm đơn vị. Các bảng giao dịch (BookOut/BookIn/BookRequest…) lưu chuỗi mã + TenantId của chính
/// giao dịch; BookOut còn lưu <c>Reg_Seq_Id</c> = <c>Barcode.Id</c> lúc mượn → ưu tiên Id, chỉ rơi về (mã + đơn vị)
/// cho dữ liệu cũ thiếu Reg_Seq_Id.
/// </summary>
public static class BarcodeTenantLookup
{
    /// <summary>Bản sách của 1 giao dịch (tracked — gọi xong có thể sửa trạng thái rồi SaveChanges).</summary>
    public static async Task<Barcode?> ForTransactionAsync(ELIBAPIDbContext db, long? barcodeId, string? value, long? tenantId)
    {
        if (barcodeId is > 0)
        {
            var byId = await db.Barcodes.FirstOrDefaultAsync(b => b.Id == barcodeId.Value);
            if (byId != null) return byId;
        }
        if (string.IsNullOrEmpty(value)) return null;
        return await db.Barcodes
            .Where(b => b.BarcodeValue == value && b.TenantId == tenantId && b.IsDelete != 2)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Tìm bản sách theo mã do cán bộ quét/nhập. Có đơn vị (<paramref name="tenantId"/>) → chỉ trong đơn vị đó.
    /// Không có đơn vị (tài khoản hệ thống) mà mã khớp bản sách ở &gt;1 đơn vị → <c>Ambiguous = true</c>, người gọi
    /// báo lỗi thay vì lấy bừa bản đầu tiên. <paramref name="normalize"/>: so sánh bỏ khoảng trắng + không phân biệt
    /// hoa/thường (như quầy mượn).
    /// </summary>
    public static async Task<(Barcode? Barcode, bool Ambiguous)> ByValueAsync(
        IQueryable<Barcode> source, string value, long? tenantId, bool normalize = false)
    {
        var v = normalize ? value.Trim().ToLower() : value;
        var q = normalize
            ? source.Where(x => x.BarcodeValue!.Trim().ToLower() == v && x.IsDelete != 2)
            : source.Where(x => x.BarcodeValue == v && x.IsDelete != 2);
        if (tenantId.HasValue) return (await q.FirstOrDefaultAsync(x => x.TenantId == tenantId), false);

        var hits = await q.Take(2).ToListAsync();
        if (hits.Count > 1 && hits[0].TenantId != hits[1].TenantId) return (null, true);
        return (hits.FirstOrDefault(), false);
    }

    public const string AmbiguousMessage =
        "Mã ĐKCB này tồn tại ở nhiều đơn vị — vui lòng đăng nhập bằng tài khoản của đơn vị để thao tác.";
}
