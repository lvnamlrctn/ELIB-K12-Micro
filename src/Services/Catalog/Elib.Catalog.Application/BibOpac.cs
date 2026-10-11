using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Tiền tố đường dẫn công khai của media (gateway /s3 → MinIO, bucket công khai) — ảnh bìa đã upload nằm dưới đây.</summary>
    public string MediaPublicPrefix { get; set; } = "/s3/media-public";
}

/// <summary>Tra ảnh bìa theo ISBN ở nguồn ngoài (monolith: BookCoverLookupService — Google Books, rồi Open Library).</summary>
public interface ICoverLookup
{
    /// <summary>URL https của ảnh bìa, hoặc null nếu không nguồn nào có (lỗi mạng cũng coi như không có).</summary>
    Task<string?> FindByIsbnAsync(string isbn, CancellationToken ct);
}

/// <summary>MARC của biểu ghi trên trang chi tiết OPAC: Leader, trường (kèm 001/005) và các đoạn ISBD.</summary>
public sealed record OpacMarcDto(Guid PublicId, long Mfn, string Leader, IReadOnlyList<MarcField> Fields, IReadOnlyList<string> Isbd);

/// <summary>Đặt ảnh bìa; <see cref="CoverUrl"/> trống = bỏ ảnh bìa.</summary>
public sealed record SetCoverRequest(Guid PublicId, string? CoverUrl);

public sealed record CoverLookupResult(string? Url);

public sealed partial class BibResource
{
    /// <summary>Biểu ghi đang hiện trên OPAC (ẩn hoặc đã xoá → không tìm thấy, như trang chi tiết của search).</summary>
    private async Task<Bib> OpacBibAsync(Guid publicId, CancellationToken ct) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId && x.Status == IHasStatus.Active, ct)
        ?? throw new NotFoundException(EntityName, publicId);

    /// <summary>Xem MARC / ISBD trên OPAC (monolith: OPAC chi tiết — tab MARC, ISBD).</summary>
    public async Task<OpacMarcDto> OpacMarcAsync(Guid publicId, CancellationToken ct)
    {
        var bib = await OpacBibAsync(publicId, ct);
        var fields = WithSystemFields(bib.Fields, bib.Id, bib.UpdatedAt ?? bib.CreatedAt);
        return new OpacMarcDto(bib.PublicId, bib.Id, bib.Leader, fields, Isbd.Build(bib.Fields));
    }

    /// <summary>Tải biểu ghi trên OPAC: ISO2709 (.mrc, UTF-8) hoặc MARCXML — thư viện khác nhập lại vào hệ của họ.</summary>
    public async Task<MarcExportFile> OpacExportAsync(Guid publicId, string? format, CancellationToken ct)
    {
        var bib = await OpacBibAsync(publicId, ct);
        var record = new MarcFileRecord(bib.Leader, WithSystemFields(bib.Fields, bib.Id, bib.UpdatedAt ?? bib.CreatedAt));
        var name = $"mfn-{bib.Id}";
        if ((format ?? "").Trim().ToLowerInvariant() is "marcxml" or "xml")
        {
            using var output = new MemoryStream();
            MarcFormats.WriteMarcXml(output, [record]);
            return new MarcExportFile(output.ToArray(), "application/marcxml+xml", name + ".xml");
        }
        return new MarcExportFile(MarcFormats.WriteIso2709([record]), "application/marc", name + ".mrc");
    }

    /// <summary>Đặt/bỏ ảnh bìa (quyền sửa biểu ghi) — phát <see cref="Contracts.Events.Catalog.BibChanged"/> để OPAC hiện ngay.</summary>
    public async Task<BibDto> SetCoverAsync(SetCoverRequest request, string mediaPublicPrefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bib = await LoadAsync(request.PublicId, ct);
        var before = bib.Version;
        bib.SetCover(request.CoverUrl, mediaPublicPrefix);
        if (bib.Version == before) return ToDto(bib);
        await OnSavingAsync(bib, CrudChange.Updated, ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Bib), EntityName, bib.PublicId, CrudChange.Updated,
                $"{Describe(bib)}: {(bib.CoverUrl is null ? "bỏ ảnh bìa" : "đổi ảnh bìa")}"), ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(bib);
    }

    /// <summary>Tìm ảnh bìa theo ISBN của biểu ghi (hoặc ISBN nhập tay) — chỉ trả URL, cán bộ xem rồi mới bấm lưu.</summary>
    public static async Task<CoverLookupResult> LookupCoverAsync(string? isbn, ICoverLookup lookup, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        var normalized = MarcRecord.NormalizeIsbn(isbn)
            ?? throw new BusinessRuleException("ISBN_INVALID", "ISBN không hợp lệ (10 hoặc 13 chữ số).");
        return new CoverLookupResult(await lookup.FindByIsbnAsync(normalized, ct));
    }
}
