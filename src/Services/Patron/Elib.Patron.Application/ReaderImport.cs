using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>
/// Tuỳ chọn nhập (ELIB-LRC: mapping, overwrite, readerTypeId, autoCreateRefs). <see cref="Columns"/>: khoá trường → số cột (bắt đầu 1) trong file.
/// <see cref="ReaderTypeId"/>: loại bạn đọc cho cả file (thay cột Loại bạn đọc). <see cref="Overwrite"/>: số thẻ đã có thì cập nhật, ô trống giữ
/// giá trị cũ. <see cref="AutoCreateRefs"/>: lớp/khoá chưa có thì tạo mới (đơn vị/dân tộc/trình độ thuộc service tenant — phải có sẵn).
/// <see cref="OpacCardNoPassword"/>: bạn đọc mới được đăng nhập OPAC bằng mật khẩu = số thẻ (identity đặt qua event, bắt đổi lần đầu).
/// </summary>
public sealed record ReaderImportOptions(int HeaderRow, IReadOnlyDictionary<string, int> Columns, bool Overwrite = false, long? ReaderTypeId = null,
    bool AutoCreateRefs = false, bool OpacCardNoPassword = false);

public sealed record ImportHeader(int Index, string Title);

public sealed record ImportFieldDto(string Key, string Header, bool Required, string? Note);

/// <summary>Dòng tiêu đề tìm được (trong 20 dòng đầu), các cột của file và gợi ý ghép cột (khoá trường → số cột).</summary>
public sealed record ImportColumnsDto(int HeaderRow, IReadOnlyList<ImportHeader> Headers, IReadOnlyDictionary<string, int> Suggested,
    IReadOnlyList<ImportFieldDto> Fields, int DataRows);

public sealed record ReaderImportJobDto(
    Guid Id, string Status, string FileName, int Total, int ToCreate, int ToUpdate, int ErrorCount, int Processed, int Created, int Updated,
    int Failed, bool HasErrorFile, string? Message, IReadOnlyList<CrudImportError> Errors, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);

/// <summary>Kế hoạch một dòng: thêm mới, cập nhật (ghi đè) hoặc lỗi.</summary>
public sealed record ReaderImportPlan(CrudImportRow Row, string?[] Raw, ReaderRequest? Request, string? CardNo, bool IsUpdate, string? Error);

/// <summary>Đọc file Excel của bạn đọc với dòng tiêu đề bất kỳ và ghép cột tuỳ chọn; tạo file "Dòng cần sửa".</summary>
public static class ReaderWorkbook
{
    private const int MaxHeaderSearchRows = 20;

    public static ImportColumnsDto Inspect(Stream file, IReadOnlyList<CrudImportColumn> fields)
    {
        using var workbook = Open(file);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed() ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có dữ liệu.");
        var lastColumn = used.LastColumn().ColumnNumber();
        var lastRow = used.LastRow().RowNumber();

        // Dòng tiêu đề: dòng có nhiều ô khớp tên trường nhất trong 20 dòng đầu (file có thể có dòng tên trường/tiêu đề phía trên).
        var best = (Row: 1, Score: -1);
        for (var r = 1; r <= Math.Min(lastRow, MaxHeaderSearchRows); r++)
        {
            var score = Enumerable.Range(1, lastColumn).Count(c => Match(fields, sheet.Cell(r, c).GetString()) is not null);
            if (score > best.Score) best = (r, score);
        }
        var headers = Enumerable.Range(1, lastColumn).Select(c => new ImportHeader(c, sheet.Cell(best.Row, c).GetString().Trim()))
            .Where(h => h.Title.Length > 0).ToList();
        var suggested = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var h in headers)
            if (Match(fields, h.Title) is { } field) suggested.TryAdd(field.Key, h.Index);
        return new ImportColumnsDto(best.Row, headers, suggested,
            [.. fields.Select(f => new ImportFieldDto(f.Key, f.Header, f.Required, f.Note))], Math.Max(0, lastRow - best.Row));
    }

    public static (IReadOnlyList<string> Headers, IReadOnlyList<(CrudImportRow Row, string?[] Raw)> Rows) Read(Stream file, ReaderImportOptions options)
    {
        using var workbook = Open(file);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed() ?? throw new BusinessRuleException("IMPORT_FILE_EMPTY", "File không có dữ liệu.");
        var lastColumn = used.LastColumn().ColumnNumber();
        var lastRow = used.LastRow().RowNumber();
        if (options.HeaderRow < 1 || options.HeaderRow >= lastRow)
            throw new BusinessRuleException("IMPORT_HEADER_ROW_INVALID", "Dòng tiêu đề không hợp lệ hoặc file không có dòng dữ liệu.");
        if (lastRow - options.HeaderRow > ReaderImportJob.MaxRows)
            throw new BusinessRuleException("IMPORT_TOO_MANY_ROWS", $"Mỗi lần nhập tối đa {ReaderImportJob.MaxRows} dòng.");
        foreach (var (key, column) in options.Columns)
            if (column < 1 || column > lastColumn) throw new BusinessRuleException("IMPORT_COLUMN_INVALID", $"Cột ghép cho '{key}' không có trong file.");
        if (!options.Columns.ContainsKey("cardno"))
            throw new BusinessRuleException("IMPORT_COLUMN_MISSING", "Chưa ghép cột Số thẻ.");

        var headers = Enumerable.Range(1, lastColumn).Select(c => sheet.Cell(options.HeaderRow, c).GetString().Trim()).ToList();
        var rows = new List<(CrudImportRow, string?[])>();
        for (var r = options.HeaderRow + 1; r <= lastRow; r++)
        {
            var raw = Enumerable.Range(1, lastColumn).Select(c => Clean(CellText(sheet.Cell(r, c)))).ToArray();
            if (raw.All(v => v is null)) continue;
            var values = options.Columns.ToDictionary(m => m.Key, m => raw[m.Value - 1]);
            rows.Add((new CrudImportRow(r, values), raw));
        }
        if (rows.Count == 0) throw new BusinessRuleException("IMPORT_NO_DATA", "File không có dòng dữ liệu nào dưới dòng tiêu đề.");
        return (headers, rows);
    }

    /// <summary>File "Dòng cần sửa" (như ELIB-LRC): cột gốc của file + cột Lỗi, giữ số dòng gốc.</summary>
    public static byte[] ErrorFile(IReadOnlyList<string> headers, IEnumerable<ReaderImportPlan> failed)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Dong can sua");
        sheet.Cell(1, 1).Value = "Dòng";
        for (var c = 0; c < headers.Count; c++) sheet.Cell(1, c + 2).Value = headers[c];
        sheet.Cell(1, headers.Count + 2).Value = "Lỗi";
        sheet.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var plan in failed)
        {
            sheet.Cell(r, 1).Value = plan.Row.Number;
            for (var c = 0; c < plan.Raw.Length; c++) sheet.Cell(r, c + 2).SetValue(plan.Raw[c] ?? "");
            sheet.Cell(r, headers.Count + 2).Value = plan.Error;
            sheet.Cell(r, headers.Count + 2).Style.Font.FontColor = XLColor.Red;
            r++;
        }
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLWorkbook Open(Stream file)
    {
        try
        {
            return new XLWorkbook(file);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BusinessRuleException("IMPORT_FILE_INVALID", "Không đọc được file. Chỉ nhận file Excel .xlsx.");
        }
    }

    private static CrudImportColumn? Match(IReadOnlyList<CrudImportColumn> fields, string? title)
    {
        var key = Normalize(title);
        return key.Length == 0 ? null
            : fields.FirstOrDefault(f => new[] { f.Header, f.Key }.Concat(f.Aliases).Any(n => Normalize(n) == key));
    }

    private static string CellText(IXLCell cell) =>
        cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : cell.GetFormattedString();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"Họ và tên *" → "ho va ten" — so khớp không phân biệt hoa thường/dấu.</summary>
    internal static string Normalize(string? text)
    {
        var decomposed = (text ?? "").Replace("*", "", StringComparison.Ordinal).Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed partial class ReaderResource
{
    private Dictionary<string, long>? _typeIds, _classIds, _courseIds;

    /// <summary>
    /// Kế hoạch nhập cho từng dòng — không ghi gì: đọc giá trị, số thẻ trùng trong file/đã có, UID trùng, tên danh mục, quy tắc của hồ sơ.
    /// Lớp/khoá chưa có mà bật tự tạo thì coi như hợp lệ (tạo khi chạy).
    /// </summary>
    public async Task<IReadOnlyList<ReaderImportPlan>> PlanImportAsync(IReadOnlyList<(CrudImportRow Row, string?[] Raw)> rows, ReaderImportOptions o,
        CancellationToken ct)
    {
        await LoadCatalogIdsAsync(ct);
        var parsed = rows.Select(x =>
        {
            try
            {
                var request = MapImportRowRaw(x.Row);
                if (o.ReaderTypeId is { } type) request = request with { ReaderTypeId = type, ReaderTypeName = null };
                return (x.Row, x.Raw, Request: request, Card: Reader.NormalizeCardNo(request.CardNo), Error: (string?)null);
            }
            catch (BusinessRuleException ex)
            {
                return (x.Row, x.Raw, Request: (ReaderRequest?)null, Card: (string?)null, Error: ex.Message);
            }
        }).ToList();

        var cards = parsed.Where(p => p.Card is not null).Select(p => p.Card!).Distinct().ToList();
        var existing = new Dictionary<string, (long Id, string? CardUid)>(StringComparer.Ordinal);
        foreach (var chunk in cards.Chunk(2000))
            foreach (var r in await Set.AsNoTracking().Where(x => chunk.Contains(x.CardNo)).Select(x => new { x.Id, x.CardNo, x.CardUid }).ToListAsync(ct))
                existing[r.CardNo] = (r.Id, r.CardUid);
        var uids = parsed.Select(p => NormalizeUid(p.Request?.CardUid)).OfType<string>().Distinct().ToList();
        var uidOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in uids.Chunk(2000))
            foreach (var r in await Set.AsNoTracking().Where(x => x.CardUid != null && chunk.Contains(x.CardUid)).Select(x => new { x.CardUid, x.CardNo }).ToListAsync(ct))
                uidOwners[r.CardUid!] = r.CardNo;

        var seenCards = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenUids = new Dictionary<string, int>(StringComparer.Ordinal);
        var plans = new List<ReaderImportPlan>(parsed.Count);
        foreach (var p in parsed)
        {
            if (p.Error is not null || p.Request is null || p.Card is null)
            {
                plans.Add(new ReaderImportPlan(p.Row, p.Raw, null, null, false, p.Error));
                continue;
            }
            string? error = null;
            var isUpdate = existing.ContainsKey(p.Card);
            var uid = NormalizeUid(p.Request.CardUid);
            if (seenCards.TryGetValue(p.Card, out var firstRow)) error = $"Số thẻ {p.Card} trùng với dòng {firstRow}.";
            else if (isUpdate && !o.Overwrite) error = $"Số thẻ {p.Card} đã có — bật \"Ghi đè\" để cập nhật.";
            else if (uid is not null && seenUids.TryGetValue(uid, out var uidRow)) error = $"UID thẻ {uid} trùng với dòng {uidRow}.";
            else if (uid is not null && uidOwners.TryGetValue(uid, out var owner) && owner != p.Card) error = $"UID thẻ {uid} đã gán cho bạn đọc {owner}.";
            if (error is null)
            {
                try
                {
                    var resolved = await ResolveImportNamesAsync(p.Request, o.AutoCreateRefs, create: false, ct);
                    Reader.Create(ToData(isUpdate ? resolved : WithImportDefaults(resolved))); // quy tắc của hồ sơ (email, ngày, độ dài…)
                }
                catch (BusinessRuleException ex)
                {
                    error = ex.Message;
                }
            }
            seenCards.TryAdd(p.Card, p.Row.Number);
            if (uid is not null) seenUids.TryAdd(uid, p.Row.Number);
            plans.Add(new ReaderImportPlan(p.Row, p.Raw, p.Request, p.Card, isUpdate, error));
        }
        return plans;
    }

    /// <summary>
    /// Ghi một dòng đã lập kế hoạch (một SaveChanges: hồ sơ + event outbox). Dòng cập nhật: ô trống giữ giá trị cũ. Trả true = thêm mới,
    /// false = cập nhật; lỗi nghiệp vụ ném <see cref="BusinessRuleException"/>.
    /// </summary>
    public async Task<bool> ApplyImportRowAsync(ReaderImportPlan plan, ReaderImportOptions o, CancellationToken ct)
    {
        await LoadCatalogIdsAsync(ct);
        var request = await ResolveImportNamesAsync(plan.Request!, o.AutoCreateRefs, create: true, ct);
        var existing = await Set.FirstOrDefaultAsync(x => x.CardNo == plan.CardNo, ct);
        Reader reader;
        if (existing is null)
        {
            reader = Create(WithImportDefaults(request));
            await ValidateAsync(reader, ct);
            Set.Add(reader);
        }
        else
        {
            if (!o.Overwrite) throw new ConflictException("READER_CARDNO_EXISTS", $"Số thẻ {existing.CardNo} đã có.");
            reader = existing;
            reader.Update(Merge(reader, request));
            await ValidateAsync(reader, ct);
        }
        await PublishAsync(reader, deleted: false, initialCardNoPassword: existing is null && o.OpacCardNoPassword, ct);
        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            Db.Set<Reader>().Entry(reader).State = EntityState.Detached;
            throw new ConflictException("READER_SAVE_CONFLICT", "Số thẻ hoặc UID vừa được bạn đọc khác dùng — nhập lại dòng này.");
        }
        return existing is null;
    }

    /// <summary>Một dòng nhật ký cho cả lượt nhập.</summary>
    public async Task RecordImportAuditAsync(string summary, CancellationToken ct)
    {
        if (AuditSink is null) return;
        await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Reader), EntityName, Guid.Empty, CrudChange.Imported, summary), ct);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Ô có giá trị thay giá trị cũ; ô trống giữ nguyên (ghi đè kiểu ELIB-LRC).</summary>
    private static ReaderData Merge(Reader r, ReaderRequest x) => new(
        r.CardNo, x.LastName ?? r.LastName, x.FirstName is { Length: > 0 } first ? first : r.FirstName, x.CitizenId ?? r.CitizenId,
        x.CardUid ?? r.CardUid, x.Email ?? r.Email, x.Phone ?? r.Phone, x.Address ?? r.Address, x.BirthDate ?? r.BirthDate, x.Sex ?? r.Sex,
        x.ReaderTypeId ?? r.ReaderTypeId, x.ClassId ?? r.ClassId, x.CourseId ?? r.CourseId, x.OrgId ?? r.OrgId, x.DegreeId ?? r.DegreeId,
        x.EthnicityId ?? r.EthnicityId, x.AcademicTitleId ?? r.AcademicTitleId, x.IssueDate ?? r.IssueDate, x.ExpireDate ?? r.ExpireDate);

    private async Task LoadCatalogIdsAsync(CancellationToken ct)
    {
        _typeIds ??= await CatalogIdsAsync<ReaderType>(ct);
        _classIds ??= await CatalogIdsAsync<SchoolClass>(ct);
        _courseIds ??= await CatalogIdsAsync<Course>(ct);
    }

    private async Task<Dictionary<string, long>> CatalogIdsAsync<T>(CancellationToken ct) where T : NamedCatalogItem
    {
        var all = await Db.Set<T>().AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var item in all) map.TryAdd(CatalogKey(item.Name), item.Id);
        return map;
    }

    /// <summary>
    /// Tên loại/lớp/khoá (patron) và đơn vị/dân tộc/trình độ/học hàm (tenant) → id. Lớp/khoá chưa có: tự tạo nếu được phép
    /// (<paramref name="create"/> = false khi xem trước — chỉ kiểm tra).
    /// </summary>
    private async Task<ReaderRequest> ResolveImportNamesAsync(ReaderRequest r, bool autoCreate, bool create, CancellationToken ct) => r with
    {
        ReaderTypeId = r.ReaderTypeId ?? NameId(_typeIds!, r.ReaderTypeName, "Loại bạn đọc"),
        ReaderTypeName = null,
        ClassId = r.ClassId ?? await CatalogIdAsync<SchoolClass>(_classIds!, r.ClassName, "Lớp", autoCreate, create, ct),
        ClassName = null,
        CourseId = r.CourseId ?? await CatalogIdAsync<Course>(_courseIds!, r.CourseName, "Khoá", autoCreate, create, ct),
        CourseName = null,
        OrgId = r.OrgId ?? await ReferenceIdAsync(ReferenceKinds.Orgs, r.OrgName, "Đơn vị", ct),
        EthnicityId = r.EthnicityId ?? await ReferenceIdAsync(ReferenceKinds.Ethnicities, r.EthnicityName, "Dân tộc", ct),
        DegreeId = r.DegreeId ?? await ReferenceIdAsync(ReferenceKinds.Degrees, r.DegreeName, "Trình độ", ct),
        AcademicTitleId = r.AcademicTitleId ?? await ReferenceIdAsync(ReferenceKinds.AcademicTitles, r.AcademicTitleName, "Học hàm", ct),
        OrgName = null, EthnicityName = null, DegreeName = null, AcademicTitleName = null,
    };

    private static long? NameId(Dictionary<string, long> ids, string? name, string label)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return ids.TryGetValue(CatalogKey(name), out var id) ? id
            : throw new BusinessRuleException("IMPORT_REF_NOT_FOUND", $"{label} '{name.Trim()}' chưa có trong danh mục — thêm ở Tham số bạn đọc trước.");
    }

    private async Task<long?> CatalogIdAsync<T>(Dictionary<string, long> ids, string? name, string label, bool autoCreate, bool create, CancellationToken ct)
        where T : NamedCatalogItem, new()
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = CatalogKey(name);
        if (ids.TryGetValue(key, out var id)) return id;
        if (!autoCreate)
            throw new BusinessRuleException("IMPORT_REF_NOT_FOUND", $"{label} '{name.Trim()}' chưa có trong danh mục — bật \"Tự tạo lớp/khoá\" hoặc thêm trước.");
        if (!create)
        {
            NamedCatalogItem.New<T>(name); // kiểm tra tên hợp lệ
            return null;
        }
        var item = NamedCatalogItem.New<T>(name);
        Db.Set<T>().Add(item);
        await Db.SaveChangesAsync(ct);
        ids[key] = item.Id;
        return item.Id;
    }

    private static string CatalogKey(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string? NormalizeUid(string? uid)
    {
        var value = new string((uid ?? "").Where(ch => !char.IsWhiteSpace(ch) && ch is not (':' or '-')).ToArray()).ToUpperInvariant();
        return value.Length == 0 ? null : value;
    }
}

/// <summary>Hàng đợi lượt nhập đã bấm chạy (đơn vị, id) — worker nền đọc; khởi động lại thì worker quét lại lượt đang chờ.</summary>
public interface IReaderImportDispatcher
{
    void Enqueue(long tenantId, long jobId);
    IAsyncEnumerable<(long TenantId, long JobId)> ReadAllAsync(CancellationToken ct);
}

public sealed class ReaderImportDispatcher : IReaderImportDispatcher
{
    private readonly Channel<(long, long)> _channel = Channel.CreateUnbounded<(long, long)>();

    public void Enqueue(long tenantId, long jobId) => _channel.Writer.TryWrite((tenantId, jobId));

    public IAsyncEnumerable<(long TenantId, long JobId)> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Xem trước (tạo lượt nhập Ready), xem tiến độ, bấm chạy, tải file lỗi — quyền READERS:add.</summary>
public sealed class ReaderImportService(ReaderResource readers, ICrudDbContext db, ITenantContext tenant, IReaderImportDispatcher queue,
    CrudAuditContext audit)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ImportColumnsDto Inspect(Stream file) => ReaderWorkbook.Inspect(file, readers.ImportColumns);

    public async Task<ReaderImportJobDto> PreviewAsync(string fileName, byte[] file, ReaderImportOptions options, CancellationToken ct)
    {
        if (file.Length is 0 or > ReaderImportJob.MaxFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {ReaderImportJob.MaxFileBytes / 1024 / 1024} MB.");
        var (headers, rows) = ReaderWorkbook.Read(new MemoryStream(file), options);
        var plans = await readers.PlanImportAsync(rows, options, ct);
        var failed = plans.Where(p => p.Error is not null).ToList();
        var job = ReaderImportJob.Prepare(fileName, file, JsonSerializer.Serialize(options, Json), plans.Count,
            plans.Count(p => p.Error is null && !p.IsUpdate), plans.Count(p => p.Error is null && p.IsUpdate), failed.Count,
            ErrorsJson(failed), failed.Count > 0 ? ReaderWorkbook.ErrorFile(headers, failed) : null);
        db.Set<ReaderImportJob>().Add(job);
        await db.SaveChangesAsync(ct);
        return ToDto(job);
    }

    public async Task<ReaderImportJobDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await LoadAsync(id, ReaderJobKind.Import, ct));

    /// <summary>Tiến độ sửa hàng loạt chạy nền (quyền READERS:edit).</summary>
    public async Task<ReaderImportJobDto> GetBatchAsync(Guid id, CancellationToken ct) => ToDto(await LoadAsync(id, ReaderJobKind.BatchUpdate, ct));

    public async Task<ReaderImportJobDto> StartAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ReaderJobKind.Import, ct);
        if (job.ToCreate + job.ToUpdate == 0) throw new BusinessRuleException("IMPORT_NOTHING_TO_DO", "Không có dòng hợp lệ nào để nhập — sửa file rồi xem trước lại.");
        job.Queue(JsonSerializer.Serialize(audit.Capture(), Json));
        await db.SaveChangesAsync(ct);
        queue.Enqueue(tenant.RequireTenantId(), job.Id);
        return ToDto(job);
    }

    public async Task<(byte[] File, string Name)> ErrorFileAsync(Guid id, CancellationToken ct)
    {
        var job = await LoadAsync(id, ReaderJobKind.Import, ct);
        return job.ErrorFile is { Length: > 0 } bytes
            ? (bytes, $"dong-can-sua-{Path.GetFileNameWithoutExtension(job.FileName)}.xlsx")
            : throw new NotFoundException("file dòng lỗi", id);
    }

    private async Task<ReaderImportJob> LoadAsync(Guid id, ReaderJobKind kind, CancellationToken ct) =>
        await db.Set<ReaderImportJob>().FirstOrDefaultAsync(j => j.PublicId == id && j.Kind == kind, ct) ?? throw new NotFoundException("lượt chạy nền", id);

    public static string ErrorsJson(IEnumerable<ReaderImportPlan> failed) =>
        JsonSerializer.Serialize(failed.Take(CrudResource<ReaderResource, Reader, ReaderSearch, ReaderRequest, ReaderDto>.MaxImportErrors)
            .Select(p => new CrudImportError(p.Row.Number, p.Error!)), Json);

    public static ReaderImportJobDto ToDto(ReaderImportJob j) => new(
        j.PublicId, j.Status.ToString(), j.FileName, j.Total, j.ToCreate, j.ToUpdate, j.ErrorCount, j.Processed, j.Created, j.Updated, j.Failed,
        j.ErrorFile is { Length: > 0 }, j.Message, JsonSerializer.Deserialize<List<CrudImportError>>(j.ErrorsJson, Json) ?? [], j.CreatedAt, j.FinishedAt);
}
