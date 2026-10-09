using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Đánh số ĐKCB theo lô — dùng chung cho đơn nhận (<c>CatalogueReceiptController</c>) và biên mục
/// (<c>CatalogueBookController</c>), trước đây chép 2 bản (port ELIB-LRC 09-30).
/// <list type="bullet">
/// <item>Mã ĐKCB duy nhất THEO ĐƠN VỊ: số lớn nhất và kiểm tra trùng chỉ xét ĐKCB cùng <paramref name="tenantId"/>.</item>
/// <item>Số lớn nhất lấy bằng <c>MAX</c> trên CSDL (trước đây tải mọi số về bộ nhớ).</item>
/// <item>Cả lô bị từ chối nếu có mã trùng ĐKCB đã có (không phân biệt hoa/thường); lưu 1 lần cho cả lô.</item>
/// </list></summary>
public static class BarcodeNumbering
{
    public const int MaxBatch = 5000;

    /// <summary>Thêm (chưa lưu) các ĐKCB mới vào <paramref name="db"/>; <paramref name="fill"/> gán các trường riêng của
    /// nơi gọi (BibId, Receipt_Id, Store, người tạo…). Gọi <c>SaveChangesAsync</c> sau khi nhận kết quả Ok.</summary>
    public static async Task<ServiceResult<List<Barcode>>> CreateBatchAsync(ELIBAPIDbContext db, long? tenantId,
        string? prefix, int digitLength, int quantity, int? startNumber, Action<Barcode> fill)
    {
        if (quantity <= 0) return ServiceResult<List<Barcode>>.BadRequest("Số lượng đăng ký phải lớn hơn 0");
        if (quantity > MaxBatch) return ServiceResult<List<Barcode>>.BadRequest($"Mỗi lần đăng ký tối đa {MaxBatch} ĐKCB");
        digitLength = Math.Clamp(digitLength, 1, 20);
        prefix ??= "";

        int maxNum;
        if (startNumber is > 0)
        {
            maxNum = startNumber.Value - 1;
        }
        else
        {
            var p = prefix;
            maxNum = await db.Barcodes
                .Where(x => x.IsDelete != 2 && x.TenantId == tenantId && x.BarcodeValue != null && x.BarcodeValue.StartsWith(p))
                .MaxAsync(x => x.BarcodeNumber) ?? 0;
        }

        var numbers = Enumerable.Range(maxNum + 1, quantity).ToList();
        var values = numbers.Select(n => prefix + n.ToString().PadLeft(digitLength, '0')).ToList();
        var lowered = values.Select(v => v.ToLower()).ToList();
        var conflicts = await db.Barcodes
            .Where(x => x.IsDelete != 2 && x.TenantId == tenantId && x.BarcodeValue != null && lowered.Contains(x.BarcodeValue.ToLower()))
            .Select(x => x.BarcodeValue!).Take(5).ToListAsync();
        if (conflicts.Count > 0)
            return ServiceResult<List<Barcode>>.BadRequest($"Số ĐKCB đã tồn tại: {string.Join(", ", conflicts)}");

        var created = new List<Barcode>(quantity);
        for (var i = 0; i < quantity; i++)
        {
            var bc = new Barcode
            {
                BarcodeValue      = values[i],
                BarcodeNumber     = numbers[i],
                BarcodeNumber_str = numbers[i].ToString().PadLeft(digitLength, '0'),
                Status            = "I", // Trong kho chưa sẵn sàng cho mượn (chưa xếp giá)
                PublicId          = Guid.NewGuid(),
                CreatedRowDate    = DateTime.Now,
                TenantId          = tenantId
            };
            fill(bc);
            db.Barcodes.Add(bc);
            created.Add(bc);
        }
        return ServiceResult<List<Barcode>>.Ok(created);
    }

    /// <summary>Mã ĐKCB nhập tay đã có trong đơn vị chưa (không phân biệt hoa/thường).</summary>
    public static Task<bool> ExistsAsync(ELIBAPIDbContext db, long? tenantId, string value)
    {
        var lower = value.Trim().ToLower();
        return db.Barcodes.AnyAsync(x => x.IsDelete != 2 && x.TenantId == tenantId && x.BarcodeValue != null
                                      && x.BarcodeValue.Trim().ToLower() == lower);
    }
}
