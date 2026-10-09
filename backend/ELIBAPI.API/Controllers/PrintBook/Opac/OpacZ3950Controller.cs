using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>
/// Tra cứu Z39.50/SRU liên thư viện dùng cho "Thêm sách từ Z3950" ở trang Đơn nhận/biên mục.
/// Không lưu trạng thái phiên tìm kiếm ở server — resultId là 1 token tự chứa (base64 JSON,
/// mã hoá configId + vị trí bản ghi + điều kiện tìm kiếm gốc) để Marc/Import có thể truy vấn lại
/// đúng bản ghi đó mà không cần cache/session.
/// </summary>
[Route("api/PrintBook/Opac/Z3950")]
[Authorize]
public class OpacZ3950Controller(IZ3950SearchService z3950, ELIBAPIDbContext db, ILogger<OpacZ3950Controller> logger) : ControllerBase
{
    private long GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : 0;
    }

    private long? GetTenantId()
    {
        var value = User.FindFirstValue("TenantId");
        return long.TryParse(value, out var id) ? id : null;
    }

    [HttpPost("Search")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> Search([FromBody] OpacZ3950SearchRequest r)
    {
        var fields = new List<Z3950SearchField>();
        if (!string.IsNullOrWhiteSpace(r.Title))     fields.Add(new Z3950SearchField { Field = "Title",     Value = r.Title });
        if (!string.IsNullOrWhiteSpace(r.Author))    fields.Add(new Z3950SearchField { Field = "Author",    Value = r.Author });
        if (!string.IsNullOrWhiteSpace(r.Publisher)) fields.Add(new Z3950SearchField { Field = "Publisher", Value = r.Publisher });
        if (!string.IsNullOrWhiteSpace(r.Isbn))      fields.Add(new Z3950SearchField { Field = "ISBN",      Value = r.Isbn });
        if (!string.IsNullOrWhiteSpace(r.Keyword))   fields.Add(new Z3950SearchField { Field = "Keyword",   Value = r.Keyword });
        // ISSN chưa có attribute BIB-1 riêng trong Z3950BerClient hiện tại — tạm tìm như từ khóa.
        if (!string.IsNullOrWhiteSpace(r.Issn))      fields.Add(new Z3950SearchField { Field = "Keyword",   Value = r.Issn });

        if (fields.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Cần ít nhất 1 điều kiện tìm kiếm"));

        var tenantId = GetTenantId();
        var configsQuery = db.Z3950Configs.Where(c => c.IsDelete != 2 && c.Systax != "Internal"
            && (!tenantId.HasValue || c.TenantId == tenantId));
        if (r.ConfigIds is { Count: > 0 }) configsQuery = configsQuery.Where(c => r.ConfigIds.Contains(c.Id));
        if (r.GroupId.HasValue)  configsQuery = configsQuery.Where(c => c.GroupId == r.GroupId);
        var configs = await configsQuery.ToListAsync();
        if (configs.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { items = Array.Empty<object>(), totalCount = 0 }));

        // Không lưu phiên ở server (resultId tự chứa) — mỗi lần đổi trang phải fetch lại từ đầu
        // cho tới hết trang đang yêu cầu, cho MỖI thư viện, rồi mới gộp + cắt đúng đoạn cần.
        // Giới hạn trần để tránh lật quá nhiều trang gây fetch lại quá lớn từ server Z3950 thật.
        const int maxFetchPerConfig = 200;
        var fetchCount = Math.Min(r.PageIndex * r.PageSize, maxFetchPerConfig);
        var results = await Task.WhenAll(configs.Select(async c =>
        {
            var detail = await z3950.SearchDetailAsync(c, fields, "AND", 1, fetchCount);
            return (config: c, detail);
        }));

        var items = new List<object>();
        var errors = new List<object>();
        foreach (var (config, detail) in results)
        {
            if (!detail.Connected)
            {
                errors.Add(new { configId = config.Id, configName = config.Name, error = detail.Error ?? "Không kết nối được" });
                continue;
            }
            for (int i = 0; i < detail.Records.Count; i++)
            {
                var rec = detail.Records[i];
                var resultId = EncodeToken(new ResultToken
                {
                    ConfigId = config.Id,
                    Position = i + 1,
                    Op       = "AND",
                    Fields   = fields.Select(f => new TokenField { Field = f.Field, Value = f.Value }).ToList()
                });
                items.Add(new
                {
                    id          = resultId,
                    configId    = config.Id,
                    configName  = config.Name,
                    title       = rec.Title,
                    author      = rec.Author,
                    publisher   = rec.Publisher,
                    publishDate = rec.PublishDate,
                });
            }
        }

        // Tổng THẬT (resultCount từng thư viện báo về), không phải items.Count bị giới hạn bởi fetchCount.
        var total = results.Where(x => x.detail.Connected).Sum(x => x.detail.TotalCount);
        var paged = items.Skip((Math.Max(r.PageIndex, 1) - 1) * r.PageSize).Take(r.PageSize).ToList();
        return Ok(ApiResponse<object>.Ok(new { items = paged, totalCount = total, errors }));
    }

    [HttpPost("Marc")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> Marc([FromBody] OpacZ3950MarcRequest r)
    {
        var token = DecodeToken(r.ResultId);
        if (token == null) return BadRequest(ApiResponse<string>.Fail("resultId không hợp lệ"));

        var tenantId = GetTenantId();
        var config = await db.Z3950Configs.FirstOrDefaultAsync(c => c.Id == token.ConfigId && c.IsDelete != 2
            && (!tenantId.HasValue || c.TenantId == tenantId));
        if (config == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy cấu hình Z3950"));

        var fields = token.Fields.Select(f => new Z3950SearchField { Field = f.Field, Value = f.Value }).ToList();
        var (connected, marcFields, error) = await z3950.FetchFullRecordAsync(config, fields, token.Op, token.Position);
        if (!connected)
            return Ok(ApiResponse<object>.Ok(new { marc = "", fields = Array.Empty<object>() }, error ?? "Không lấy được bản ghi"));

        // Cấu trúc {tag, ind1, ind2, subFields} đúng shape MarcField[] frontend dùng cho editor
        // (giống MarcConvertController.MarcFileToFields) — "marc" (text) giữ lại cho xem trước ở trang z3950-search.
        var fieldsJson = marcFields.Select(f => f.Control != null
            ? (object)new { tag = f.Tag, value = f.Control }
            : new
            {
                tag       = f.Tag,
                ind1      = f.Ind1.ToString(),
                ind2      = f.Ind2.ToString(),
                subFields = f.Subfields.Select(s => new { code = s.Code.ToString(), value = s.Value ?? "" }).ToList()
            }).ToList();

        return Ok(ApiResponse<object>.Ok(new { marc = FormatMarcText(marcFields), fields = fieldsJson }));
    }

    [HttpPost("Import")]
    [Permission("AB_RECEIPTS", "add")]
    public async Task<IActionResult> Import([FromBody] OpacZ3950ImportRequest r)
    {
        var token = DecodeToken(r.ResultId);
        if (token == null) return BadRequest(ApiResponse<string>.Fail("resultId không hợp lệ"));

        var tenantId = GetTenantId();
        var config = await db.Z3950Configs.FirstOrDefaultAsync(c => c.Id == token.ConfigId && c.IsDelete != 2
            && (!tenantId.HasValue || c.TenantId == tenantId));
        if (config == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy cấu hình Z3950"));

        var fields = token.Fields.Select(f => new Z3950SearchField { Field = f.Field, Value = f.Value }).ToList();
        var (connected, marcFields, error) = await z3950.FetchFullRecordAsync(config, fields, token.Op, token.Position);
        if (!connected || marcFields.Count == 0)
            return BadRequest(ApiResponse<string>.Fail(error ?? "Không lấy được bản ghi MARC"));

        // Port ELIB-LRC 10-04: trước đây chỉ ghi Bib + BibData nên biểu ghi nhập về "mồ côi" — không có Mfn (không mở
        // được theo Mfn, API trả mfn = null), không có BibXml (không hiện nhan đề/tác giả trong danh sách, tìm kiếm), mất
        // trường điều khiển 001–008, và không được lập chỉ mục. Nay dựng giống biên mục tay (CatalogueBookController.Save),
        // trong 1 transaction.
        var userId = GetCurrentUserId();
        // Tenant: biểu ghi thuộc đơn vị người nhập; tài khoản hệ thống (không đơn vị) → đơn vị của cấu hình Z39.50.
        var bibTenantId = tenantId ?? config.TenantId;
        var marc = marcFields.Select(f => new BibMarcFieldResponse
        {
            Tag  = f.Tag,
            Ind1 = f.Ind1.ToString(),
            Ind2 = f.Ind2.ToString(),
            Value = f.Control,
            SubFields = f.Subfields.Select(sf => new BibMarcSubFieldResponse { Code = sf.Code.ToString(), Value = sf.Value }).ToList()
        }).ToList();

        await using var tx = await db.Database.BeginTransactionAsync();
        var bib = new Bib
        {
            PublicId       = Guid.NewGuid(),
            Bib_type_id    = r.BibTypeId,
            CollectionId   = r.CollectionId,
            TenantId       = bibTenantId,
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now,
            UpdateRowBy    = userId,
            UpdatedRowDate = DateTime.Now
        };
        db.Bibs.Add(bib);
        await db.SaveChangesAsync();
        bib.Mfn = bib.Bibid;

        foreach (var field in marc.Where(f => string.CompareOrdinal(f.Tag, "010") >= 0))
        {
            foreach (var sf in field.SubFields)
            {
                db.BibDatas.Add(new BibData
                {
                    BibId          = bib.Bibid,
                    Field          = field.Tag,
                    L1             = field.Ind1,
                    L2             = field.Ind2,
                    SubField       = sf.Code,
                    Data           = sf.Value,
                    TenantId       = bibTenantId,
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now
                });
            }
        }

        var publishDate = CatalogueBookController.GetSubfieldValue(marc, "264", 'c') ?? CatalogueBookController.GetSubfieldValue(marc, "260", 'c');
        db.BibXmls.Add(new BibXml
        {
            BibId          = bib.Bibid,
            Title          = CatalogueBookController.GetSubfieldValues(marc, "245", 'a', 'b'),
            Author         = CatalogueBookController.GetSubfieldValue(marc, "100", 'a') ?? CatalogueBookController.GetSubfieldValue(marc, "700", 'a'),
            Publisher      = CatalogueBookController.GetSubfieldValue(marc, "264", 'b') ?? CatalogueBookController.GetSubfieldValue(marc, "260", 'b'),
            PublishDate    = publishDate,
            DDC            = CatalogueBookController.GetSubfieldValue(marc, "082", 'a'),
            Keyword        = CatalogueBookController.GetSubfieldValues(marc, "650", 'a', joinAll: true),
            Isbd           = await CatalogueBookController.GenerateIsbdAsync(db, bib.Bib_type_id, marc),
            TenantId       = bibTenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        });

        // Trường điều khiển: 001 = Mfn mới, 003 = mã đơn vị, 005 = thời điểm nhập; các trường còn lại lấy từ bản ghi nguồn
        // nếu có (Leader/008 của thư viện nguồn), không có thì sinh mặc định như biên mục tay.
        var bibType = bib.Bib_type_id.HasValue ? await db.BibTypes.FirstOrDefaultAsync(x => x.Id == bib.Bib_type_id) : null;
        var tenantCode = bibTenantId.HasValue ? await db.Tenants.Where(x => x.Id == bibTenantId).Select(x => x.Code).FirstOrDefaultAsync() : null;
        foreach (var tag in CatalogueBookController.ControlFieldTags)
        {
            var source = tag is "001" or "003" or "005" ? null : marc.FirstOrDefault(f => f.Tag == tag)?.Value;
            db.FixedFieldValues.Add(new FixedFieldValue
            {
                Bibid          = bib.Bibid,
                Field          = tag,
                Value          = !string.IsNullOrEmpty(source) ? source
                                 : CatalogueBookController.BuildDefaultControlValue(tag, bibType, tenantCode, bib.Mfn, publishDate),
                TenantId       = bibTenantId,
                PublicId       = Guid.NewGuid(),
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        BackgroundJobs.TryEnqueue<PrintBookIndexingJob>(HttpContext.RequestServices, j => j.RunAsync(bib.Bibid), logger);
        BackgroundJobs.TryEnqueue<ZebraExportJob>(HttpContext.RequestServices, j => j.RunAsync(bib.Bibid), logger);

        return Ok(ApiResponse<object>.Ok(new { mfn = bib.Mfn, bibId = bib.Bibid }));
    }

    // ── resultId token (base64 JSON, không cần cache/session) ─────────────────

    private static string EncodeToken(ResultToken token) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(token));

    private static ResultToken? DecodeToken(string resultId)
    {
        try
        {
            var bytes = Convert.FromBase64String(resultId);
            return JsonSerializer.Deserialize<ResultToken>(bytes);
        }
        catch { return null; }
    }

    private static string FormatMarcText(List<Iso2709Reader.Field> fields)
    {
        var sb = new StringBuilder();
        foreach (var f in fields)
        {
            if (f.Control != null) { sb.AppendLine($"{f.Tag}    {f.Control}"); continue; }
            sb.Append($"{f.Tag} {f.Ind1}{f.Ind2} ");
            foreach (var (code, value) in f.Subfields) sb.Append($"${code}{value}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private class ResultToken
    {
        public long              ConfigId { get; set; }
        public int               Position { get; set; }
        public string             Op      { get; set; } = "AND";
        public List<TokenField>   Fields   { get; set; } = new();
    }

    private class TokenField
    {
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
    }
}

public class OpacZ3950SearchRequest
{
    public string? Title     { get; set; }
    public string? Author    { get; set; }
    public string? Isbn      { get; set; }
    public string? Issn      { get; set; }
    public string? Publisher { get; set; }
    public string? Keyword   { get; set; }
    public List<long>? ConfigIds { get; set; }
    public long?   GroupId   { get; set; }
    public int     PageIndex { get; set; } = 1;
    public int     PageSize  { get; set; } = 10;
}

public class OpacZ3950MarcRequest
{
    public string ResultId { get; set; } = "";
    public long?  ConfigId { get; set; }
}

public class OpacZ3950ImportRequest
{
    public string ResultId    { get; set; } = "";
    public long?  ConfigId    { get; set; }
    public long   BibTypeId   { get; set; }
    public long?  WorksheetId { get; set; }
    public long?  CollectionId { get; set; }
}
