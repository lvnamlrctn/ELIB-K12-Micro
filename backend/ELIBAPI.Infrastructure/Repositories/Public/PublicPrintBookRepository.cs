using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicPrintBookRepository
{
    Task<PublicBibDetailResponse?> GetBibDetailByPublicIdAsync(Guid publicId);

    /// <summary>Tồn kho THỜI GIAN THỰC (số bản/số bản còn trống) cho nhiều bib cùng lúc — dùng
    /// chung giữa trang chi tiết OPAC và widget chat tìm tài liệu, tránh mỗi nơi tự tính 1 kiểu.</summary>
    Task<Dictionary<long, (int CopyCount, int AvailableCount)>> GetAvailabilityStatsAsync(List<long> bibIds);
}

/// <summary>
/// Chi tiết 1 biểu ghi tài liệu in cho OPAC công khai — truy vấn SQL trực tiếp (không qua
/// Elasticsearch, tránh lệ thuộc độ trễ reindex), cùng công thức denormalize với
/// <see cref="ELIBAPI.Infrastructure.Jobs.PrintBookIndexingJob"/> nhưng chỉ cho 1 bib.
/// </summary>
public class PublicPrintBookRepository(ELIBAPIDbContext db) : IPublicPrintBookRepository
{
    // Trùng với CatalogueBookController.ControlFieldTags (ELIBAPI.API) — không tham chiếu chéo được vì
    // Infrastructure không phụ thuộc API, chấp nhận lặp lại mảng nhỏ 9 chuỗi này.
    private static readonly string[] ControlFieldTags =
        ["000", "001", "002", "003", "004", "005", "006", "007", "008"];

    public async Task<PublicBibDetailResponse?> GetBibDetailByPublicIdAsync(Guid publicId)
    {
        var bib = await db.Bibs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicId == publicId && b.IsDelete != 2);
        if (bib == null) return null;

        var xml = await db.BibXmls.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BibId == bib.Bibid);

        var marc = await db.BibDatas.AsNoTracking()
            .Where(d => d.BibId == bib.Bibid && d.IsDelete != 2 && d.Data != null && d.Data != "")
            .Select(d => new { d.Field, d.SubField, d.Data, d.L1, d.L2 })
            .ToListAsync();

        string? Sub(string field, string sub) => marc
            .FirstOrDefault(m => m.Field == field && m.SubField == sub)?.Data;
        string? SubJoined(string field, string sub)
        {
            var vals = marc.Where(m => m.Field == field && m.SubField == sub)
                           .Select(m => m.Data!).ToList();
            return vals.Count > 0 ? string.Join("; ", vals) : null;
        }

        var controlFieldsRaw = await db.FixedFieldValues.AsNoTracking()
            .Where(f => f.Bibid == bib.Bibid && f.IsDelete != 2
                && f.Field != null && ControlFieldTags.Contains(f.Field) && f.Value != null && f.Value != "")
            .Select(f => new { f.Field, f.Value })
            .ToListAsync();
        var field008 = controlFieldsRaw.FirstOrDefault(f => f.Field == "008")?.Value;

        // Từ điển mô tả trường/tiểu trường MARC tiếng Việt (PrintBook.Marc_Field/Marc_SubField) —
        // có thể rỗng nếu chưa seed dữ liệu, không phải lỗi.
        var fieldTags = marc.Select(m => m.Field).Concat(ControlFieldTags).Distinct().ToList();
        var fieldDescMap = await db.MarcFields.AsNoTracking()
            .Where(f => f.Field != null && fieldTags.Contains(f.Field) && f.IsDelete != 2)
            .ToDictionaryAsync(f => f.Field!, f => f.Vndescription ?? f.Description);
        var subFieldDescList = await db.MarcSubFields.AsNoTracking()
            .Where(sf => sf.Field != null && fieldTags.Contains(sf.Field) && sf.IsDelete != 2)
            .Select(sf => new { sf.Field, sf.Subfield, Description = sf.Vndescription ?? sf.Description })
            .ToListAsync();
        string? SubFieldDesc(string? field, string? sub) => subFieldDescList
            .FirstOrDefault(sf => sf.Field == field && sf.Subfield == sub)?.Description;

        var controlFields = controlFieldsRaw
            .OrderBy(f => f.Field)
            .Select(f => new PublicBibControlFieldResponse
            {
                Field       = f.Field ?? "",
                Value       = f.Value ?? "",
                Description = fieldDescMap.TryGetValue(f.Field ?? "", out var cd) ? cd : null,
            }).ToList();

        var barcodes = await db.Barcodes.AsNoTracking()
            .Where(bc => bc.BibId == bib.Bibid && bc.IsDelete != 2)
            .Select(bc => new { bc.Id, bc.BarcodeValue, bc.Store, bc.Status })
            .ToListAsync();

        var storeIds = barcodes.Where(b => b.Store.HasValue).Select(b => (long)b.Store!.Value).Distinct().ToList();
        var storeMap = await db.Stores.AsNoTracking()
            .Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var statusIds = barcodes.Where(b => b.Status != null).Select(b => b.Status!).Distinct().ToList();
        var statusMap = await db.BarcodeStatuses.AsNoTracking()
            .Where(s => s.Id != null && statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id!, s => s.CommentStatus);

        var stats = await GetAvailabilityStatsAsync([bib.Bibid]);
        var (copyCount, availableCount) = stats.TryGetValue(bib.Bibid, out var s0) ? s0 : (barcodes.Count, 0);

        // BibType/EbookCollection/DBibStatus: bọc try/catch — vài bảng tra cứu trên PostgreSQL
        // hiện có lệch cách viết hoa cột "Name" so với model EF (lỗi tiền tồn tại, không phải do
        // truy vấn này), không để 1 bảng tra cứu phụ làm hỏng cả trang chi tiết.
        string? materialType = null;
        if (bib.Bib_type_id.HasValue)
        {
            try
            {
                materialType = await db.BibTypes.AsNoTracking()
                    .Where(t => t.Id == bib.Bib_type_id.Value).Select(t => t.Name).FirstOrDefaultAsync();
            }
            catch { }
        }

        string? collectionName = null;
        if (bib.CollectionId.HasValue)
        {
            try
            {
                collectionName = await db.EbookCollections.AsNoTracking()
                    .Where(c => c.Id == bib.CollectionId.Value).Select(c => c.Name).FirstOrDefaultAsync();
            }
            catch { }
        }

        string? statusName = null;
        if (bib.Status != null)
        {
            try
            {
                statusName = await db.DBibStatuses.AsNoTracking()
                    .Where(s => s.Id == bib.Status).Select(s => s.Name).FirstOrDefaultAsync();
            }
            catch { }
        }

        var holdings = barcodes.Select(bc => new PublicBibHoldingResponse
        {
            BarcodeId  = bc.Id,
            Barcode    = bc.BarcodeValue,
            StoreName  = bc.Store.HasValue && storeMap.TryGetValue(bc.Store.Value, out var sn) ? sn : null,
            Status     = bc.Status,
            StatusName = bc.Status != null && statusMap.TryGetValue(bc.Status, out var stn) ? stn : null,
        }).ToList();

        return new PublicBibDetailResponse
        {
            PublicId = bib.PublicId,
            BibId    = bib.Bibid,
            Mfn      = bib.Mfn,

            Title       = xml?.Title,
            Author      = xml?.Author,
            Publisher   = xml?.Publisher,
            PublishDate = xml?.PublishDate,
            PublishYear = MarcHelper.ExtractYear(xml?.PublishDate),
            Ddc         = xml?.DDC,
            Cutter      = Sub("082", "b"),
            Isbn        = Sub("020", "a"),
            Summary     = Sub("520", "a"),
            Keyword     = xml?.Keyword,
            Language    = MarcHelper.ExtractLanguage(field008),
            MaterialType   = materialType,
            CollectionId   = bib.CollectionId?.ToString(),
            CollectionName = collectionName,
            Contributor = SubJoined("700", "a"),
            Images      = bib.Images,
            Isbd        = xml?.Isbd,
            Status      = bib.Status,
            StatusName  = statusName,
            Url         = bib.Url,

            CopyCount      = copyCount,
            AvailableCount = availableCount,

            Holdings     = holdings,
            ControlFields = controlFields,
            MarcFields   = marc.OrderBy(m => m.Field).Select(m => new PublicBibMarcFieldResponse
            {
                Field      = m.Field ?? "",
                Indicator1 = m.L1 ?? "",
                Indicator2 = m.L2 ?? "",
                SubField   = m.SubField ?? "",
                Data       = m.Data ?? "",
                FieldDescription    = fieldDescMap.TryGetValue(m.Field ?? "", out var mfd) ? mfd : null,
                SubFieldDescription = SubFieldDesc(m.Field, m.SubField),
            }).ToList(),
        };
    }

    public async Task<Dictionary<long, (int CopyCount, int AvailableCount)>> GetAvailabilityStatsAsync(List<long> bibIds)
    {
        var result = new Dictionary<long, (int, int)>();
        if (bibIds.Count == 0) return result;

        var barcodes = await db.Barcodes.AsNoTracking()
            .Where(bc => bc.BibId != null && bibIds.Contains(bc.BibId.Value) && bc.IsDelete != 2)
            .Select(bc => new { bc.BibId, bc.BarcodeValue, bc.Status, bc.TenantId })
            .ToListAsync();

        var statusIds = barcodes.Where(b => b.Status != null).Select(b => b.Status!).Distinct().ToList();
        var statusMap = await db.BarcodeStatuses.AsNoTracking()
            .Where(s => s.Id != null && statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id!, s => s.CommentStatus);

        // Tồn kho THỜI GIAN THỰC: không chỉ tin tên trạng thái (Barcode_Status.CommentStatus, có
        // thể lệch nếu chưa cập nhật khi cho mượn) mà còn đối chiếu thẳng sổ mượn (BookOut/BookIn)
        // và hàng đợi đặt mượn (BookRequest) đang mở — cùng cách MyLibraryController.Hold chọn bản
        // để giữ chỗ, nên con số OPAC hiển thị luôn khớp với việc bạn đọc có đặt mượn được hay không.
        var barcodeValues = barcodes.Where(bc => bc.BarcodeValue != null).Select(bc => bc.BarcodeValue!).Distinct().ToList();
        var openBarcodes = await db.BookOuts.AsNoTracking()
            .Where(o => o.Barcode != null && barcodeValues.Contains(o.Barcode) && o.IsDelete != 2
                && !db.BookIns.Any(i => i.BookOutId == o.Id && i.IsDelete != 2))
            .Select(o => new { o.Barcode, o.TenantId }).Distinct().ToListAsync();
        var pendingBarcodes = await db.BookRequests.AsNoTracking()
            .Where(r => r.Barcode != null && barcodeValues.Contains(r.Barcode) && r.Status == "pending" && r.IsDelete != 2)
            .Select(r => new { r.Barcode, r.TenantId }).Distinct().ToListAsync();
        // So theo cặp (mã, đơn vị) — mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20); lượt mượn/đặt của đơn vị khác
        // trùng mã không được làm giảm số bản sẵn sàng.
        var busy = openBarcodes.Concat(pendingBarcodes)
            .Select(x => (x.Barcode!, x.TenantId ?? 0)).ToHashSet();

        foreach (var group in barcodes.Where(bc => bc.BibId.HasValue).GroupBy(bc => bc.BibId!.Value))
        {
            var copyCount = group.Count();
            var availableCount = group.Count(bc =>
            {
                var statusName = bc.Status != null && statusMap.TryGetValue(bc.Status, out var sn) ? sn : null;
                return ((statusName ?? "").ToLower().Contains("sẵn sàng") || bc.Status == null)
                    && bc.BarcodeValue != null && !busy.Contains((bc.BarcodeValue, bc.TenantId ?? 0));
            });
            result[group.Key] = (copyCount, availableCount);
        }
        return result;
    }
}
