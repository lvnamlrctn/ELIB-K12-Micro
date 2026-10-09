using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>Chi tiết 1 biểu ghi tài liệu in cho OPAC công khai. Không cần JWT.</summary>
[Route("api/public/[controller]")]
public class PublicPrintBookController(
    IPublicPrintBookRepository repo,
    IMinioService               minio) : PublicBaseController
{
    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> GetDetail(Guid publicId)
    {
        var detail = await repo.GetBibDetailByPublicIdAsync(publicId);
        if (detail == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));
        detail.Images = ResolveImageUrl(detail.Images);
        return Ok(ApiResponse<PublicBibDetailResponse>.Ok(detail));
    }

    /// <summary>Xuất biểu ghi MARC21 dạng nhị phân ISO 2709 (.mrc) — dùng chung encoder với MarcConvertController.</summary>
    [HttpGet("{publicId:guid}/marc21")]
    public async Task<IActionResult> GetMarc21Binary(Guid publicId)
    {
        var detail = await repo.GetBibDetailByPublicIdAsync(publicId);
        if (detail == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));

        var record = new Iso2709Reader.Record
        {
            Leader = detail.ControlFields.FirstOrDefault(c => c.Field == "000")?.Value is { Length: >= 24 } ldr
                ? ldr
                : "00000nam a2200000uu 4500",
        };
        foreach (var cf in detail.ControlFields.Where(c => c.Field != "000"))
            record.Fields.Add(new Iso2709Reader.Field { Tag = cf.Field, Control = cf.Value });

        foreach (var g in detail.MarcFields.GroupBy(m => new { m.Field, m.Indicator1, m.Indicator2 }))
        {
            var f = new Iso2709Reader.Field
            {
                Tag  = g.Key.Field,
                Ind1 = string.IsNullOrEmpty(g.Key.Indicator1) ? ' ' : g.Key.Indicator1[0],
                Ind2 = string.IsNullOrEmpty(g.Key.Indicator2) ? ' ' : g.Key.Indicator2[0],
            };
            foreach (var sf in g)
                if (!string.IsNullOrEmpty(sf.SubField))
                    f.Subfields.Add((sf.SubField[0], sf.Data));
            record.Fields.Add(f);
        }

        var bytes = Iso2709Reader.Write([record]);
        return File(bytes, "application/octet-stream", $"mfn-{detail.Mfn ?? detail.BibId}.mrc");
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{minio.PublicBaseUrl}/{value}";
}
