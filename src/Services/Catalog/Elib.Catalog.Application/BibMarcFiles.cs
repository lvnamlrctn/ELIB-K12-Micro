using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

/// <summary>
/// Nhập biểu ghi từ file MARC. <see cref="BibTypeId"/> trống → tự chọn loại biểu ghi theo Leader/06–07 của từng biểu ghi.
/// <see cref="SkipDuplicates"/>: biểu ghi trùng (cùng ISBN; không ISBN thì cùng nhan đề + tác giả + năm) bị bỏ qua.
/// <see cref="SkipInvalid"/>: biểu ghi lỗi bị bỏ qua, các biểu ghi đúng vẫn được nhập; mặc định có lỗi là không nhập gì.
/// </summary>
public sealed record MarcImportOptions(long? BibTypeId = null, int? Status = null, bool SkipDuplicates = true, bool SkipInvalid = false);

/// <summary>Lỗi của biểu ghi thứ <see cref="Record"/> (đếm từ 1, theo thứ tự trong file).</summary>
public sealed record MarcImportError(int Record, string? Title, string Message);

/// <summary>Kết quả nhập. <see cref="Rejected"/>: có biểu ghi lỗi mà không chọn bỏ qua → không nhập biểu ghi nào.</summary>
public sealed record MarcImportResult(int Total, int Imported, int Skipped, int Failed, IReadOnlyList<MarcImportError> Errors, bool Rejected = false)
{
    public string Detail => Rejected
        ? $"File có {Failed} biểu ghi lỗi, chưa nhập biểu ghi nào. Sửa file, hoặc chọn bỏ qua biểu ghi lỗi rồi nhập lại."
        : $"Đã nhập {Imported}/{Total} biểu ghi"
          + (Skipped > 0 ? $", bỏ qua {Skipped} biểu ghi đã có" : "")
          + (Failed > 0 ? $", bỏ qua {Failed} biểu ghi lỗi" : "") + ".";
}

/// <summary>Một biểu ghi đọc thử từ file (nạp vào màn biên mục). <see cref="BibTypeId"/>: loại biểu ghi khớp Leader, nếu có.</summary>
public sealed record MarcPreviewRecord(int Record, string Leader, IReadOnlyList<MarcField> Fields, string? Title, long? BibTypeId, string? Error);

public sealed record MarcFilePreview(int Total, IReadOnlyList<MarcPreviewRecord> Records);

/// <summary>Xuất biểu ghi: theo danh sách <see cref="Ids"/> nếu có, không thì theo điều kiện tìm <see cref="Search"/>. Format: iso2709 | marcxml.</summary>
public sealed record MarcExportRequest(BibSearch? Search = null, IReadOnlyList<Guid>? Ids = null, string? Format = null);

public sealed record MarcExportFile(byte[] Content, string ContentType, string FileName);

public sealed partial class BibResource
{
    public const long MaxMarcFileBytes = 20 * 1024 * 1024;
    public const int MaxImportRecords = 5000;
    public const int MaxExportRecords = 20000;
    public const int MaxPreviewRecords = 100;
    private const int ImportBatchSize = 200;

    /// <summary>Đọc file (ISO2709 / MARCXML / text hệ cũ) để nạp vào màn biên mục — không ghi gì (monolith: MarcConvert/MarcFileToFields).</summary>
    public async Task<MarcFilePreview> PreviewMarcAsync(byte[] data, string? fileName, CancellationToken ct)
    {
        var records = ReadFile(data, fileName);
        var types = await Db.Set<BibType>().AsNoTracking().ToListAsync(ct);
        var preview = records.Take(MaxPreviewRecords).Select((record, i) =>
        {
            if (record.Error is not null) return new MarcPreviewRecord(i + 1, record.Leader, record.Fields, null, null, record.Error);
            try
            {
                var fields = MarcRecord.Normalize(record.Fields);
                return new MarcPreviewRecord(i + 1, record.Leader, fields, MarcRecord.Summarize(fields).Title, MatchType(types, record.Leader)?.Id, null);
            }
            catch (BusinessRuleException ex)
            {
                return new MarcPreviewRecord(i + 1, record.Leader, record.Fields, RawTitle(record), MatchType(types, record.Leader)?.Id, ex.Message);
            }
        }).ToList();
        return new MarcFilePreview(records.Count, preview);
    }

    /// <summary>
    /// Nhập hàng loạt từ file MARC (đưa dữ liệu biên mục sẵn có vào hệ mới). Cả file trong MỘT transaction; lưu theo lô để có MFN
    /// rồi phát <see cref="Contracts.Events.Catalog.BibChanged"/> cho từng biểu ghi (outbox). 001/005 trong file bị bỏ — biểu ghi
    /// nhận MFN mới; 003/008 thiếu thì sinh như khi biên mục tay.
    /// </summary>
    public async Task<MarcImportResult> ImportMarcAsync(byte[] data, string? fileName, MarcImportOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        var records = ReadFile(data, fileName);
        if (records.Count > MaxImportRecords)
            throw new BusinessRuleException("MARC_FILE_TOO_MANY", $"File có {records.Count} biểu ghi, tối đa {MaxImportRecords} biểu ghi mỗi lần nhập — chia nhỏ file.");
        var status = options.Status is { } s ? StatusRules.Validate(s) : IHasStatus.Active;
        var types = await Db.Set<BibType>().AsNoTracking().ToListAsync(ct);
        var chosen = options.BibTypeId is { } typeId
            ? types.FirstOrDefault(t => t.Id == typeId) ?? throw new BusinessRuleException("BIB_TYPE_NOT_FOUND", "Loại biểu ghi đã chọn không còn.")
            : null;
        var agency = await AgencyCodeAsync(ct);
        var today = Today();

        var strategy = Db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            var errors = new List<MarcImportError>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var batch = new List<Bib>(ImportBatchSize);
            int imported = 0, skipped = 0, failed = 0;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                Bib bib;
                try
                {
                    if (record.Error is not null) throw new BusinessRuleException("MARC_RECORD_INVALID", record.Error);
                    bib = Bib.Create(chosen ?? MatchType(types, record.Leader), null, record.Leader, record.Fields, today, agency);
                    if (status != IHasStatus.Active) bib.ChangeStatus(status);
                }
                catch (BusinessRuleException ex)
                {
                    failed++;
                    if (errors.Count < MaxImportErrors)
                        errors.Add(new MarcImportError(i + 1, RawTitle(record), ex.Message));
                    continue;
                }

                // Trùng trong file hoặc với biểu ghi đã có.
                var duplicate = false;
                foreach (var key in DuplicateKeys(bib)) duplicate |= !seen.Add(key);
                if (!duplicate && options.SkipDuplicates) duplicate = await ExistsAsync(bib, token);
                if (duplicate && options.SkipDuplicates)
                {
                    skipped++;
                    continue;
                }
                if (failed > 0 && !options.SkipInvalid) continue; // đã chắc không nhập — chỉ đọc tiếp để báo đủ lỗi

                Set.Add(bib);
                batch.Add(bib);
                imported++;
                if (batch.Count >= ImportBatchSize) await FlushAsync(batch, token);
            }

            if (failed > 0 && !options.SkipInvalid)
            {
                await transaction.RollbackAsync(token);
                return new MarcImportResult(records.Count, 0, skipped, failed, errors, Rejected: true);
            }
            await FlushAsync(batch, token);
            if (imported > 0 && AuditSink is not null)
            {
                await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Bib), EntityName, Guid.Empty, CrudChange.Imported,
                    $"Nhập MARC ({fileName}): {imported} biểu ghi" + (skipped > 0 ? $", bỏ qua {skipped} trùng" : "") + (failed > 0 ? $", {failed} lỗi" : "")), token);
                await Db.SaveChangesAsync(token);
            }
            await transaction.CommitAsync(token);
            return new MarcImportResult(records.Count, imported, skipped, failed, errors);
        }, ct);
    }

    /// <summary>Xuất biểu ghi ra ISO2709 (.mrc, UTF-8) hoặc MARCXML — kèm 001 (MFN) và 005, để chuyển sang hệ khác hoặc sao lưu.</summary>
    public async Task<MarcExportFile> ExportMarcAsync(MarcExportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var xml = (request.Format ?? "iso2709").Trim().ToLowerInvariant() switch
        {
            "iso2709" or "mrc" => false,
            "marcxml" or "xml" => true,
            _ => throw new BusinessRuleException("MARC_FORMAT_INVALID", "Định dạng xuất là iso2709 hoặc marcxml."),
        };
        IQueryable<Bib> query = request.Ids is { Count: > 0 } ids
            ? Set.AsNoTracking().Where(x => ids.Contains(x.PublicId)).OrderBy(x => x.Id)
            : Query(request.Search ?? new BibSearch());
        var rows = await query.Take(MaxExportRecords + 1)
            .Select(x => new { x.Id, x.Leader, x.Fields, ChangedAt = x.UpdatedAt ?? x.CreatedAt }).ToListAsync(ct);
        if (rows.Count > MaxExportRecords)
            throw new BusinessRuleException("EXPORT_TOO_MANY", $"Xuất tối đa {MaxExportRecords} biểu ghi mỗi lần — lọc bớt (theo loại biểu ghi, từ khoá…).");
        if (rows.Count == 0) throw new BusinessRuleException("EXPORT_EMPTY", "Không có biểu ghi nào để xuất.");

        var records = rows.Select(r => new MarcFileRecord(r.Leader, WithSystemFields(r.Fields, r.Id, r.ChangedAt)));
        if (!xml) return new MarcExportFile(MarcFormats.WriteIso2709(records), "application/marc", "bieu-ghi.mrc");
        using var output = new MemoryStream();
        MarcFormats.WriteMarcXml(output, records);
        return new MarcExportFile(output.ToArray(), "application/marcxml+xml", "bieu-ghi.xml");
    }

    private static List<MarcFileRecord> ReadFile(byte[] data, string? fileName)
    {
        if (data.Length is 0 || data.Length > MaxMarcFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {MaxMarcFileBytes / 1024 / 1024} MB.");
        var records = MarcFormats.Read(data, fileName);
        return records.Count > 0 ? records
            : throw new BusinessRuleException("MARC_FILE_EMPTY", "Không tìm thấy biểu ghi MARC nào trong file (cần ISO2709 .mrc, MARCXML .xml hoặc text MARC của hệ cũ).");
    }

    /// <summary>Loại biểu ghi có cùng Leader/06 (loại) và /07 (cấp thư mục); không có thì chỉ cùng /06; không có nữa → null (sách).</summary>
    private static BibType? MatchType(List<BibType> types, string leader)
    {
        if (leader is not { Length: 24 }) return null;
        string type = leader[6].ToString(), level = leader[7].ToString();
        return types.Where(t => t.RecordType == type && t.BibLevel == level).OrderBy(t => t.Id).FirstOrDefault()
            ?? types.Where(t => t.RecordType == type).OrderBy(t => t.Id).FirstOrDefault();
    }

    private static string[] DuplicateKeys(Bib bib) => bib.IsbnList.Count > 0
        ? [.. bib.IsbnList.Select(i => "isbn:" + i)]
        : [$"bib:{MarcRecord.Fold(bib.Title)}|{MarcRecord.Fold(bib.Author)}|{bib.PublishYear}"];

    private async Task<bool> ExistsAsync(Bib bib, CancellationToken ct)
    {
        foreach (var isbn in bib.IsbnList)
        {
            var key = "|" + isbn + "|";
            if (await Set.AnyAsync(x => x.Isbns.Contains(key), ct)) return true;
        }
        return bib.IsbnList.Count == 0
            && await Set.AnyAsync(x => x.Isbns == "" && x.Title == bib.Title && x.Author == bib.Author && x.PublishYear == bib.PublishYear, ct);
    }

    private async Task FlushAsync(List<Bib> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;
        await Db.SaveChangesAsync(ct); // có MFN
        foreach (var bib in batch) await PublishAsync(bib, deleted: false, ct);
        await Db.SaveChangesAsync(ct);
        foreach (var bib in batch) Set.Entry(bib).State = EntityState.Detached; // file lớn: không giữ hàng nghìn entity trong change tracker
        batch.Clear();
    }

    private static string? RawTitle(MarcFileRecord record) =>
        record.Fields.FirstOrDefault(f => f.Tag == "245")?.Values('a').FirstOrDefault()?.Trim().TrimEnd('/', ':', ' ');
}
