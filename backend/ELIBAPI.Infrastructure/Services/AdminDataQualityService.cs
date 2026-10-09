using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Cảnh báo chất lượng dữ liệu (Đợt 21 — port từ ELIB-LRC <c>AdminDataQualityService</c>). Chỉ đọc, không tự
/// sửa; đếm và danh sách dùng CHUNG một điều kiện. Khác LRC (đơn tenant): mọi quy tắc lọc theo đơn vị của người gọi
/// (<paramref name="tenantId"/> null = tài khoản hệ thống, xem toàn bộ) và mã ĐKCB trùng được nhóm theo
/// (đơn vị, mã chuẩn hoá) — mã chỉ duy nhất trong từng đơn vị (Đợt 20), 2 đơn vị dùng chung 1 mã KHÔNG phải lỗi.</summary>
public sealed class AdminDataQualityService(ELIBAPIDbContext db, long? tenantId)
{
    private static readonly string[] BibRules = ["missing-title", "missing-author", "missing-publisher", "missing-ddc", "missing-isbn"];

    public static readonly string[] Rules =
        ["missing-title", "missing-author", "missing-publisher", "missing-ddc", "missing-isbn", "duplicate-barcode", "unshelved"];

    /// <summary>Module cần quyền xem để đọc quy tắc — đúng module của màn hình sửa tương ứng.</summary>
    public static string Module(string rule) => rule switch
    {
        "missing-title" or "missing-author" or "missing-publisher" or "missing-ddc" or "missing-isbn" => "CATALOG_BIBS",
        "duplicate-barcode" => "AB_RECEIPTS",
        "unshelved" => "MAP_SHELVING",
        _ => throw new ArgumentException("Quy tắc không được hỗ trợ."),
    };

    private IQueryable<Bib> ActiveBibs() =>
        db.Bibs.AsNoTracking().Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId));

    /// <summary>Biểu ghi không có trường con MARC nào trong <paramref name="fields"/>$<paramref name="sub"/> có nội dung.</summary>
    private IQueryable<Bib> Missing(string[] fields, string sub) => ActiveBibs().Where(b => !db.BibDatas.Any(d =>
        d.BibId == b.Bibid && d.IsDelete != 2 && fields.Contains(d.Field!) && d.SubField == sub && d.Data != null && d.Data.Trim() != ""));

    private IQueryable<Bib> BibQuery(string rule) => rule switch
    {
        "missing-title"     => Missing(["245"], "a"),
        "missing-author"    => Missing(["100"], "a"),
        // 260$b (MARC cũ) hoặc 264$b (RDA) — worksheet cho nhập ở cả 2.
        "missing-publisher" => Missing(["260", "264"], "b"),
        "missing-ddc"       => Missing(["082"], "a"),
        "missing-isbn"      => Missing(["020"], "a"),
        _ => throw new ArgumentException("Quy tắc không được hỗ trợ."),
    };

    private IQueryable<Barcode> BarcodeQuery(string rule)
    {
        var active = db.Barcodes.AsNoTracking().Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId));
        if (rule == "unshelved") return active.Where(b => b.Status == "I" && b.MapObjectId == null);

        // Trùng sau khi bỏ khoảng trắng 2 đầu + không phân biệt hoa/thường, giữ số 0 đầu. Unique index Đợt 20 chỉ
        // chặn trùng tuyệt đối nên "NV.1" và "nv.1 " vẫn có thể cùng tồn tại trong 1 đơn vị — máy quét sẽ nhầm.
        // JOIN với tập khoá trùng (nhóm 1 lần) — KHÔNG dùng duplicates.Any(...) tương quan: EF dịch thành EXISTS gom nhóm
        // lại toàn bảng cho từng dòng, đã đo quá 60s trên ~6.700 bản ở DB dev.
        var duplicates = active.Where(b => b.BarcodeValue != null && b.BarcodeValue.Trim() != "")
            .GroupBy(b => new { T = b.TenantId ?? 0, K = b.BarcodeValue!.Trim().ToLower() })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        return from b in active.Where(b => b.BarcodeValue != null)
               join k in duplicates on new { T = b.TenantId ?? 0, K = b.BarcodeValue!.Trim().ToLower() } equals new { k.T, k.K }
               select b;
    }

    public Task<int> CountAsync(string rule, CancellationToken ct)
    {
        Module(rule);
        return BibRules.Contains(rule) ? BibQuery(rule).CountAsync(ct) : BarcodeQuery(rule).CountAsync(ct);
    }

    public async Task<object> ListAsync(string rule, int page, int size, CancellationToken ct)
    {
        Module(rule);
        page = Math.Clamp(page, 1, 100000);
        size = Math.Clamp(size, 1, 100);
        var total = await CountAsync(rule, ct);

        if (BibRules.Contains(rule))
        {
            var bibs = await BibQuery(rule).OrderBy(b => b.Bibid).Skip((Math.Max(page, 1) - 1) * size).Take(size)
                .Select(b => new { b.PublicId, b.Mfn, b.Bibid, b.TenantId }).ToListAsync(ct);
            var ids = bibs.Select(b => b.Bibid).ToList();
            var titles = await db.BibXmls.AsNoTracking().Where(x => ids.Contains(x.BibId))
                .Select(x => new { x.BibId, x.Title }).ToListAsync(ct);
            var titleMap = titles.GroupBy(x => x.BibId).ToDictionary(g => g.Key, g => g.First().Title);
            var items = bibs.Select(b => new
            {
                id = b.PublicId, mfn = b.Mfn, bibId = (long?)b.Bibid, barcode = (string?)null,
                title = titleMap.GetValueOrDefault(b.Bibid), tenantId = b.TenantId,
            }).ToList();
            return new { rule, total, page, pageSize = size, items, checkedAt = DateTime.UtcNow };
        }

        var copies = await BarcodeQuery(rule)
            .OrderBy(b => b.TenantId).ThenBy(b => b.BarcodeValue!.Trim().ToLower()).ThenBy(b => b.Id)
            .Skip((Math.Max(page, 1) - 1) * size).Take(size)
            .Select(b => new { b.PublicId, b.BarcodeValue, b.BibId, b.TenantId }).ToListAsync(ct);
        var bibIds = copies.Where(c => c.BibId.HasValue).Select(c => c.BibId!.Value).Distinct().ToList();
        var copyTitles = await db.BibXmls.AsNoTracking().Where(x => bibIds.Contains(x.BibId))
            .Select(x => new { x.BibId, x.Title }).ToListAsync(ct);
        var copyTitleMap = copyTitles.GroupBy(x => x.BibId).ToDictionary(g => g.Key, g => g.First().Title);
        var copyItems = copies.Select(c => new
        {
            id = c.PublicId, mfn = (long?)null, bibId = c.BibId, barcode = c.BarcodeValue,
            title = c.BibId.HasValue ? copyTitleMap.GetValueOrDefault(c.BibId.Value) : null, tenantId = c.TenantId,
        }).ToList();
        return new { rule, total, page, pageSize = size, items = copyItems, checkedAt = DateTime.UtcNow };
    }
}
