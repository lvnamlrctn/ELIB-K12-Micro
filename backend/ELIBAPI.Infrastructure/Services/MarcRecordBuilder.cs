using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Đợt 22.3 — port từ ELIB-LRC (không đổi gì, chỉ thêm chú thích tenant). Dựng 1 bản ghi MARC
/// (<see cref="Iso2709Reader.Record"/>) đầy đủ từ dữ liệu biên mục thật của 1 Bib — field điều khiển
/// (001/008...) từ <c>FixedFieldValue</c>, field dữ liệu (010 trở lên, có phân trường) từ <c>BibData</c>
/// (nhóm theo Field+L1+L2, L1/L2 chính là 2 chỉ thị MARC). Dùng chung cho <c>ZebraExportJob</c> và
/// <c>OaiPmhService</c> (marc21) — cả 2 nơi gọi đều TỰ lọc theo tenant từ trước khi có bibId ở đây (Bib
/// đã thuộc đúng tenant do nơi gọi truy vấn), nên hàm này không cần tham số tenantId.
/// </summary>
public static class MarcRecordBuilder
{
    private const string ApprovedStatus = "f";

    public static async Task<Iso2709Reader.Record?> BuildAsync(ELIBAPIDbContext db, long bibId)
    {
        var bib = await db.Bibs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Bibid == bibId && b.IsDelete != 2 && b.Status == ApprovedStatus);
        if (bib == null) return null;

        var controlRows = await db.FixedFieldValues.AsNoTracking()
            .Where(f => f.Bibid == bibId && f.IsDelete != 2 && f.Value != null && f.Value != "")
            .OrderBy(f => f.Field)
            .Select(f => new { f.Field, f.Value })
            .ToListAsync();

        var dataRows = await db.BibDatas.AsNoTracking()
            .Where(d => d.BibId == bibId && d.IsDelete != 2)
            .OrderBy(d => d.BibDataId)
            .Select(d => new { d.Field, d.L1, d.L2, d.SubField, d.Data })
            .ToListAsync();

        var record = new Iso2709Reader.Record();

        foreach (var f in controlRows)
        {
            var tag = (f.Field ?? "").PadLeft(3, '0');
            record.Fields.Add(new Iso2709Reader.Field { Tag = tag, Control = f.Value });
        }

        foreach (var g in dataRows.GroupBy(d => new { d.Field, d.L1, d.L2 }).OrderBy(g => g.Key.Field))
        {
            var tag   = (g.Key.Field ?? "").PadLeft(3, '0');
            var field = new Iso2709Reader.Field
            {
                Tag  = tag,
                Ind1 = string.IsNullOrEmpty(g.Key.L1) ? ' ' : g.Key.L1![0],
                Ind2 = string.IsNullOrEmpty(g.Key.L2) ? ' ' : g.Key.L2![0]
            };
            foreach (var sf in g)
            {
                if (string.IsNullOrEmpty(sf.Data)) continue;
                var code = string.IsNullOrEmpty(sf.SubField) ? 'a' : sf.SubField![0];
                field.Subfields.Add((code, sf.Data));
            }
            if (field.Subfields.Count > 0) record.Fields.Add(field);
        }

        return record.Fields.Count > 0 ? record : null;
    }
}
