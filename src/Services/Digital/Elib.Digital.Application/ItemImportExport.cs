using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record ItemExportRequest(ItemSelection? Selection = null, IReadOnlyList<string>? Fields = null);

/// <summary>Giá trị mặc định khi file nhập không có ô tương ứng (LRC: collectionId/subjectId/topicId/status/language + cờ miễn phí, tải).</summary>
public sealed record ItemImportDefaults(
    long? CollectionId = null, long? SubjectId = null, long? TopicId = null, bool Published = false, bool Free = false, bool AllowDownload = false, string? Language = null);

public sealed record ItemImportResult(int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, string Message);

/// <summary>Xuất/nhập tài liệu số (LRC: ExportExcel, ExportMetaData, ImportExcel, ImportXml) — tiêu đề cột trùng nhau nên file xuất nhập lại được.</summary>
public sealed partial class EbookItemResource
{
    public const int MaxImportRows = 2000;
    public const int MaxImportXmlFiles = 200;
    public const long MaxImportXmlBytes = 1_000_000;

    private sealed record ExportRow(EbookItem Item, string? Collection);

    private static readonly IReadOnlyList<CrudExportColumn<ExportRow>> ExportColumns =
    [
        new("title", "Tiêu đề", r => r.Item.Title),
        new("othertitle", "Tiêu đề khác", r => r.Item.OtherTitle),
        new("author", "Tác giả", r => r.Item.Author),
        new("advisor", "Người hướng dẫn", r => r.Item.OldAuthor),
        new("publisher", "Nhà xuất bản", r => r.Item.Publisher),
        new("publishdate", "Năm xuất bản", r => r.Item.PublishDate),
        new("keyword", "Từ khóa", r => r.Item.Keyword),
        new("page", "Số trang", r => r.Item.Page),
        new("language", "Ngôn ngữ", r => r.Item.Language),
        new("status", "Trạng thái", r => r.Item.Status == IHasStatus.Active ? "Xuất bản" : "Ẩn"),
        new("submited", "Ngày nộp", r => r.Item.Submited is { } at ? DateOnly.FromDateTime(at.ToOffset(TimeSpan.FromHours(7)).DateTime) : null),
        new("allowdownload", "Cho phép tải", r => r.Item.AllowDownload ? "Có" : "Không"),
        new("free", "Miễn phí", r => r.Item.Free ? "Có" : "Không"),
        new("cover", "Ảnh bìa", r => r.Item.CoverUrl),
        new("abstract", "Tóm tắt", r => r.Item.Abstract),
        new("description", "Mô tả", r => r.Item.Description),
        new("journal", "Tên tạp chí", r => r.Item.JournalName),
        new("collection", "Bộ sưu tập", r => r.Collection),
    ];

    public static IReadOnlyList<CrudExportField> ExportFields => [.. ExportColumns.Select(c => new CrudExportField(c.Key, c.Header))];

    public async Task<byte[]> ExportAsync(ItemExportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var columns = CrudExcel.Select(ExportColumns, request.Fields?.ToList());
        return CrudExcel.Export("Tài liệu số", columns, await LoadForExportAsync(request.Selection, ct));
    }

    /// <summary>ZIP mỗi tài liệu một thư mục <c>item_001/dublin_core.xml</c> (Dublin Core kiểu DSpace) — nhập lại bằng <see cref="ImportXmlAsync"/>. Không kèm file nội dung.</summary>
    public async Task<byte[]> ExportMetadataAsync(ItemExportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await LoadForExportAsync(request.Selection, ct);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var entry = zip.CreateEntry($"item_{i + 1:D3}/dublin_core.xml", CompressionLevel.Optimal);
                await using var writer = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(ItemDublinCore.Build(rows[i].Item));
                await writer.WriteAsync(bytes, ct);
            }
        }
        return stream.ToArray();
    }

    private async Task<List<ExportRow>> LoadForExportAsync(ItemSelection? selection, CancellationToken ct)
    {
        IQueryable<EbookItem> query;
        if (selection is null) query = Query(new EbookItemSearch());
        else if (selection.PublicIds is { } given && selection.Search is null)
            query = Set.AsNoTracking().Where(x => given.Contains(x.PublicId)).OrderByDescending(x => x.Id);
        else if (selection.Search is { } search && selection.PublicIds is null) query = Query(search);
        else throw new BusinessRuleException("SELECTION_INVALID", "Chọn các dòng hoặc dùng bộ lọc hiện tại (một trong hai).");
        if (await query.CountAsync(ct) > CrudExcel.MaxExportRows)
            throw new BusinessRuleException("EXPORT_TOO_MANY_ROWS", $"Mỗi lần xuất tối đa {CrudExcel.MaxExportRows} tài liệu — lọc thêm theo bộ sưu tập, năm… rồi xuất từng phần.");
        var items = await query.ToListAsync(ct);
        var collections = await Db.Set<EbookCollection>().AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return [.. items.Select(i => new ExportRow(i, i.CollectionId is { } id ? collections.GetValueOrDefault(id) : null))];
    }

    // ── Nhập ──

    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi"] = "vi", ["tiếng việt"] = "vi", ["vietnamese"] = "vi", ["en"] = "en", ["english"] = "en", ["tiếng anh"] = "en",
        ["fr"] = "fr", ["français"] = "fr", ["de"] = "de", ["deutsch"] = "de", ["zh"] = "zh", ["中文"] = "zh", ["ja"] = "ja", ["日本語"] = "ja",
        ["ru"] = "ru", ["русский"] = "ru", ["pусский"] = "ru", ["es"] = "es", ["español"] = "es",
    };

    /// <summary>
    /// Nhập từ Excel: sheet đầu, dòng 1 là tiêu đề (tên cột như file xuất; thiếu cột là null), nhiều giá trị tách bằng ";". Mỗi dòng độc lập
    /// (dòng lỗi không chặn dòng khác); dòng lỗi báo "Dòng N: …". Khác LRC: thiếu Tiêu đề là lỗi dòng.
    /// </summary>
    public async Task<ItemImportResult> ImportExcelAsync(Stream file, ItemImportDefaults defaults, CancellationToken ct)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(file);
        }
#pragma warning disable CA1031 // ClosedXML/OpenXML ném nhiều kiểu ngoại lệ khác nhau cho file hỏng — mọi lỗi mở file đều là "không đọc được"
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            throw new BusinessRuleException("IMPORT_FILE_INVALID", "Không đọc được tệp Excel (.xlsx).");
        }
        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có dữ liệu.");
            var used = sheet.RangeUsed() ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có dữ liệu.");
            var lastRow = used.LastRow().RowNumber();
            var lastColumn = used.LastColumn().ColumnNumber();
            if (lastRow - 1 > MaxImportRows) throw new BusinessRuleException("IMPORT_TOO_MANY_ROWS", $"Mỗi lần nhập tối đa {MaxImportRows} dòng.");
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= lastColumn; c++)
                if (sheet.Cell(1, c).GetString().Trim() is { Length: > 0 } header) columns.TryAdd(header, c);
            if (!columns.ContainsKey("Tiêu đề")) throw new BusinessRuleException("IMPORT_COLUMN_MISSING", "File thiếu cột \"Tiêu đề\" ở dòng 1.");

            var rows = new List<(string Label, Func<string, string?> Cell)>();
            for (var r = 2; r <= lastRow; r++)
            {
                var row = r;
                string? Cell(string header) => columns.TryGetValue(header, out var c) ? Clean(sheet.Cell(row, c).GetString()) : null;
                if (columns.Keys.All(h => Cell(h) is null)) continue;
                rows.Add(($"Dòng {r}", h => Cell(h)));
            }
            if (rows.Count == 0) throw new BusinessRuleException("IMPORT_NO_DATA", "File không có dòng dữ liệu nào dưới dòng tiêu đề.");
            return await ImportRowsAsync(rows.Select(x => (x.Label, (Func<EbookItemFields>)(() => FromExcelRow(x.Cell, defaults)))), ct);
        }
    }

    /// <summary>Nhập nhiều file Dublin Core (mỗi file một tài liệu; lỗi báo theo tên file).</summary>
    public async Task<ItemImportResult> ImportXmlAsync(IReadOnlyList<(string Name, Stream Content)> files, ItemImportDefaults defaults, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0) throw new BusinessRuleException("IMPORT_FILE_EMPTY", "Chưa chọn file XML.");
        if (files.Count > MaxImportXmlFiles) throw new BusinessRuleException("IMPORT_TOO_MANY_ROWS", $"Mỗi lần nhập tối đa {MaxImportXmlFiles} file XML.");
        var rows = files.Select(f => (f.Name, (Func<EbookItemFields>)(() => ItemDublinCore.Parse(f.Content, defaults)))).ToList();
        return await ImportRowsAsync(rows, ct);
    }

    private async Task<ItemImportResult> ImportRowsAsync(IEnumerable<(string Label, Func<EbookItemFields> Build)> rows, CancellationToken ct)
    {
        var imported = 0;
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (var (label, build) in rows)
        {
            EbookItem? entity = null;
            try
            {
                var fields = build();
                if (fields.CoverUrl is not null)
                {
                    // Chỉ nhận URL ảnh bìa của chính đơn vị (file xuất ra nhập lại được); không tải URL ngoài — URL khác thì bỏ ảnh và cảnh báo.
                    try
                    {
                        _ = EbookCover.Validate(fields.CoverUrl, _options.MediaPublicPrefix, tenant.RequireTenantId());
                    }
                    catch (BusinessRuleException)
                    {
                        warnings.Add($"{label}: đã nhập nhưng bỏ ảnh bìa vì không phải ảnh đã tải lên hệ thống.");
                        fields = fields with { CoverUrl = null };
                    }
                }
                entity = await CreateAsync(fields, ct); // luật miền ném trước khi thêm vào ngữ cảnh
                await ValidateAsync(entity, ct);
                await AddCoreAsync(entity, ct);
                imported++;
            }
            catch (BusinessRuleException e)
            {
                Forget(entity);
                errors.Add($"{label}: {e.Message}");
            }
            catch (Exception e) when (e is XmlException or FormatException)
            {
                Forget(entity);
                errors.Add($"{label}: XML không hợp lệ.");
            }
            catch (Exception e) when (e is DbUpdateException or InvalidOperationException)
            {
                // Lưu hỏng giữa chừng: nhật ký và sự kiện của dòng này có thể còn nằm trong ngữ cảnh — dừng hẳn để dòng sau không commit kèm chúng.
                Forget(entity);
                errors.Add($"{label}: không lưu được tài liệu này, đã dừng nhập các dòng còn lại.");
                break;
            }
        }
        return new ItemImportResult(imported, errors.Count, errors, warnings, $"Import hoàn tất: {imported} thành công, {errors.Count} lỗi");
    }

    /// <summary>Bỏ thực thể lỗi khỏi ngữ cảnh để lần lưu của dòng sau không bị kéo theo.</summary>
    private void Forget(EbookItem? entity)
    {
        if (entity is not null && Set.Entry(entity).State != EntityState.Detached) Set.Entry(entity).State = EntityState.Detached;
    }

    private static EbookItemFields FromExcelRow(Func<string, string?> cell, ItemImportDefaults d)
    {
        var language = cell("Ngôn ngữ") is { } l
            ? LanguageNames.GetValueOrDefault(l) ?? throw new BusinessRuleException("LANGUAGE_INVALID", $"Ngôn ngữ \"{l}\" không nhận ra (vi, en, fr, de, zh, ja, ru, es).")
            : d.Language ?? "vi";
        return new EbookItemFields(
            cell("Tiêu đề") ?? throw new BusinessRuleException("TITLE_INVALID", "Thiếu Tiêu đề."),
            CollectionId: d.CollectionId, SubjectId: d.SubjectId, TopicId: d.TopicId,
            Author: Join(cell("Tác giả")), Publisher: cell("Nhà xuất bản"), PublishDate: cell("Năm xuất bản"), Keyword: Join(cell("Từ khóa")),
            OtherTitle: Join(cell("Tiêu đề khác")), OldAuthor: Join(cell("Người hướng dẫn")), Language: language,
            Free: Flag(cell("Miễn phí")) ?? d.Free, AllowDownload: Flag(cell("Cho phép tải")) ?? d.AllowDownload,
            CoverUrl: cell("Ảnh bìa"), Abstract: cell("Tóm tắt"), Description: cell("Mô tả"), JournalName: cell("Tên tạp chí"), Page: cell("Số trang"),
            Status: d.Published ? IHasStatus.Active : IHasStatus.Inactive);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"A;B ; C" → "A; B; C" (bỏ giá trị rỗng).</summary>
    private static string? Join(string? value) =>
        value is null ? null : string.Join("; ", value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) is { Length: > 0 } joined ? joined : null;

    private static bool? Flag(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "có" or "co" or "1" or "true" or "yes" or "x" => true,
        "không" or "khong" or "0" or "false" or "no" => false,
        _ => null,
    };
}

/// <summary>Dublin Core kiểu DSpace SAF (<c>&lt;dcvalue element qualifier&gt;</c>) — dựng và đọc cùng một bảng ánh xạ.</summary>
public static class ItemDublinCore
{
    public static string Build(EbookItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var root = new XElement("dublin_core", new XAttribute("schema", "dc"));
        void Add(string element, string qualifier, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) root.Add(new XElement("dcvalue", new XAttribute("element", element), new XAttribute("qualifier", qualifier), value.Trim()));
        }
        void AddMany(string element, string qualifier, string? joined)
        {
            foreach (var part in (joined ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) Add(element, qualifier, part);
        }
        Add("title", "none", item.Title);
        AddMany("title", "alternative", item.OtherTitle);
        foreach (var author in item.Authors) Add("contributor", "author", author.Display);
        if (item.Authors.Count == 0) AddMany("contributor", "author", item.Author);
        foreach (var advisor in item.Advisors) Add("contributor", "advisor", advisor.Display);
        if (item.Advisors.Count == 0) AddMany("contributor", "advisor", item.OldAuthor);
        Add("publisher", "none", item.Publisher);
        Add("date", "issued", item.PublishDate);
        AddMany("subject", "none", item.Keyword);
        Add("description", "abstract", item.Abstract);
        Add("description", "none", item.Description);
        Add("language", "iso", item.Language);
        foreach (var identifier in item.Identifiers) Add("identifier", identifier.Type, identifier.Value);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
    }

    public static EbookItemFields Parse(Stream content, ItemImportDefaults d)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(d);
        // DTD bị cấm và không tải tài nguyên ngoài (XXE / billion laughs).
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 };
        XDocument doc;
        using (var reader = XmlReader.Create(content, settings)) doc = XDocument.Load(reader);

        var values = doc.Descendants("dcvalue").Select(e => (
            Element: ((string?)e.Attribute("element") ?? "").Trim().ToLowerInvariant(),
            Qualifier: ((string?)e.Attribute("qualifier") ?? "none").Trim().ToLowerInvariant(),
            Value: e.Value.Trim())).Where(v => v.Value.Length > 0).ToList();
        string? One(string element, params string[] qualifiers) =>
            qualifiers.Select(q => values.FirstOrDefault(v => v.Element == element && v.Qualifier == q).Value).FirstOrDefault(v => v is not null);
        string? Many(string element, string qualifier) =>
            values.Where(v => v.Element == element && v.Qualifier == qualifier).Select(v => v.Value) is { } list && list.Any() ? string.Join("; ", list) : null;

        var language = One("language", "iso", "none");
        var identifiers = values.Where(v => v.Element == "identifier" && EbookIdentifier.Types.Contains(v.Qualifier))
            .Select(v => new EbookIdentifier { Type = v.Qualifier, Value = v.Value }).ToList();
        return new EbookItemFields(
            One("title", "none") ?? throw new BusinessRuleException("TITLE_INVALID", "Thiếu dcvalue title."),
            CollectionId: d.CollectionId, SubjectId: d.SubjectId, TopicId: d.TopicId,
            Author: Many("contributor", "author"), OldAuthor: Many("contributor", "advisor"), OtherTitle: Many("title", "alternative"),
            Publisher: One("publisher", "none"), PublishDate: One("date", "issued", "none"), Keyword: Many("subject", "none"),
            Abstract: One("description", "abstract"), Description: One("description", "none"),
            Language: language is { Length: > 0 and <= 10 } ? language : d.Language ?? "vi",
            Free: d.Free, AllowDownload: d.AllowDownload, Status: d.Published ? IHasStatus.Active : IHasStatus.Inactive,
            Identifiers: identifiers.Count > 0 ? identifiers : null);
    }
}
