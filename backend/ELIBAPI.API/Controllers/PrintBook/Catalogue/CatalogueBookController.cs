using System.Text;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/Book")]
public class CatalogueBookController(
    IGenericRepository<Bib, BibSearchRequest, BibRequest> repo,
    ELIBAPIDbContext db,
    IBookCoverLookupService coverLookup,
    ILogger<CatalogueBookController> logger) : GenericController<Bib, BibSearchRequest, BibRequest>(repo)
{
    // Các trường điều khiển MARC được tự sinh khi Save (Leader + 001-008 đầy đủ)
    // internal (không phải private): CatalogueBookOrderController tái dùng nguyên các helper này để
    // build MARC cho bảng BibOrder/BibXmlOrder/BibDataOrder/fixed_field_value_order, tránh copy-paste.
    internal static readonly string[] ControlFieldTags = ["000", "001", "002", "003", "004", "005", "006", "007", "008"];

    // Lịch sử thay đổi chi tiết (Đợt 17) — không có field nhạy cảm ở biểu ghi MARC.
    private static readonly HashSet<string> EmptyMaskedFields = [];

    internal static string BuildDefaultControlValue(string tag, BibType? bibType, string? tenantCode, long? mfn, string? publishDate) => tag switch
    {
        "000" => BuildLeader(bibType),
        "001" => mfn?.ToString() ?? "",
        "003" => string.IsNullOrEmpty(tenantCode) ? "VN-ELIB" : tenantCode,
        "005" => DateTime.Now.ToString("yyyyMMddHHmmss") + ".0",
        "008" => BuildField008(publishDate),
        _     => ""
    };

    // Sinh chuỗi ISBD từ MARC vừa lưu, dựa theo cấu hình dấu câu Config_Isbd/isbd_field/isbd_subfield
    // của loại tài liệu (Bib_Type_Id). Trả về null nếu chưa có cấu hình cho loại tài liệu này — không
    // fallback dấu câu cứng, để trống chờ người dùng tự nhập cấu hình qua trang "Cấu hình ISBD".
    // static + nhận db qua tham số (thay vì field instance) để CatalogueBookOrderController gọi lại được.
    internal static async Task<string?> GenerateIsbdAsync(ELIBAPIDbContext db, long? bibTypeId, List<BibMarcFieldResponse> fields)
    {
        if (bibTypeId == null) return null;
        var config = await db.ConfigIsbds.FirstOrDefaultAsync(x => x.Bib_Type_Id == bibTypeId && x.IsDelete != 2);
        if (config == null) return null;

        var fieldRules = await db.IsbdFields.Where(x => x.Config_Id == config.Id && x.IsDelete != 2)
            .OrderBy(x => x.Fieldindex).ToListAsync();
        var subfieldRules = await db.IsbdSubfields.Where(x => x.Config_Id == config.Id && x.IsDelete != 2)
            .OrderBy(x => x.Subfieldindex).ToListAsync();

        var sb = new StringBuilder();
        foreach (var fr in fieldRules)
        {
            var subRules = subfieldRules.Where(s => s.Field == fr.Field).ToList();
            foreach (var occ in fields.Where(f => f.Tag == fr.Field)) // field có thể lặp lại
            {
                var fieldText = new StringBuilder();
                foreach (var sr in subRules)
                {
                    var values = occ.SubFields
                        .Where(sf => sf.Code == sr.Subfield && !string.IsNullOrWhiteSpace(sf.Value))
                        .Select(sf => sf.Value).ToList();
                    if (values.Count == 0) continue;
                    fieldText.Append(sr.Starttp).Append(string.Join(sr.Nexttp ?? "", values)).Append(sr.Stoptp);
                }
                if (fieldText.Length > 0) sb.Append(fr.Starttp).Append(fieldText).Append(fr.Stoptp);
            }
        }
        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    // Lấy giá trị subfield đầu tiên khớp tag+code (dùng cho Author/Publisher/PublishDate/DDC — single-value)
    internal static string? GetSubfieldValue(List<BibMarcFieldResponse> fields, string tag, char code)
    {
        foreach (var f in fields)
        {
            if (f.Tag != tag) continue;
            foreach (var sf in f.SubFields)
                if (!string.IsNullOrEmpty(sf.Code) && sf.Code[0] == code)
                    return sf.Value;
        }
        return null;
    }

    // Title (245 $a + $b của field đầu tiên) khi code2 != null; Keyword (650 $a nối mọi lần lặp bằng "; ") khi joinAll = true
    internal static string? GetSubfieldValues(List<BibMarcFieldResponse> fields, string tag, char code, char? code2 = null, bool joinAll = false)
    {
        if (joinAll)
        {
            var values = fields.Where(f => f.Tag == tag)
                .SelectMany(f => f.SubFields.Where(sf => !string.IsNullOrEmpty(sf.Code) && sf.Code[0] == code))
                .Select(sf => sf.Value?.TrimEnd('.', ' '))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var joined = string.Join("; ", values);
            return string.IsNullOrEmpty(joined) ? null : joined;
        }

        var first = fields.FirstOrDefault(f => f.Tag == tag);
        if (first == null) return null;
        var parts = first.SubFields
            .Where(sf => !string.IsNullOrEmpty(sf.Code) && (sf.Code[0] == code || (code2.HasValue && sf.Code[0] == code2.Value)))
            .Select(sf => sf.Value?.TrimEnd('/', ' '))
            .Where(v => !string.IsNullOrWhiteSpace(v));
        var result = string.Join(" ", parts).Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    internal static string BuildLeader(BibType? bibType)
    {
        var leader = new char[24];
        Array.Fill(leader, ' ');
        "00000".CopyTo(0, leader, 0, 5);           // 00-04: độ dài bản ghi — tính lại lúc export ISO2709
        leader[5]  = 'n';                          // 05: trạng thái bản ghi (n = mới)
        leader[6]  = string.IsNullOrEmpty(bibType?.Record_Type_Code) ? 'a' : bibType!.Record_Type_Code![0];
        leader[7]  = string.IsNullOrEmpty(bibType?.Bib_Level)        ? 'm' : bibType!.Bib_Level![0];
        leader[9]  = 'a';                          // 09: character coding scheme = UTF-8
        leader[10] = '2';                          // 10: indicator count
        leader[11] = '2';                          // 11: subfield code count
        "00000".CopyTo(0, leader, 12, 5);          // 12-16: base address — tính lại lúc export ISO2709
        leader[18] = 'a';                          // 18: descriptive cataloging form = AACR2
        "4500".CopyTo(0, leader, 20, 4);           // 20-23: entry map (hằng số MARC21)
        return new string(leader);
    }

    internal static string BuildField008(string? publishDate)
    {
        var f = new char[40];
        Array.Fill(f, ' ');
        DateTime.Now.ToString("yyMMdd").CopyTo(0, f, 0, 6); // 00-05: ngày nhập vào hệ thống
        f[6] = 's';                                          // 06: loại ngày xuất bản (đơn giản hoá: 1 ngày biết rõ)
        var year = ExtractYear(publishDate);
        (year ?? "    ").CopyTo(0, f, 7, 4);                  // 07-10: năm xuất bản (date1)
        "vm ".CopyTo(0, f, 15, 3);                            // 15-17: nơi xuất bản mặc định (Việt Nam)
        "vie".CopyTo(0, f, 35, 3);                            // 35-37: ngôn ngữ mặc định (tiếng Việt)
        return new string(f);
    }

    private static string? ExtractYear(string? publishDate)
    {
        if (string.IsNullOrEmpty(publishDate)) return null;
        var digits = new string(publishDate.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[..4] : null;
    }

    [HttpPost("Search")]
    [Permission("CATALOG_BIBS", "view")]
    public override async Task<IActionResult> Search([FromBody] BibSearchRequest request)
    {
        var paged = await repo.SearchAsync(request);
        var bibIds = paged.Items.Select(x => x.Bibid).ToList();
        var xmlMap = await db.BibXmls
            .Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);
        var receiptBibIds = await db.AbReceiptDetails
            .Where(x => x.Bibid.HasValue && bibIds.Contains(x.Bibid.Value) && x.IsDelete != 2)
            .Select(x => x.Bibid!.Value).Distinct().ToListAsync();
        var orderBibIds = await db.AbOrderDetails
            .Where(x => x.Bibid.HasValue && bibIds.Contains(x.Bibid.Value) && x.IsDelete != 2)
            .Select(x => x.Bibid!.Value).Distinct().ToListAsync();
        var receiptSet = receiptBibIds.ToHashSet();
        var orderSet   = orderBibIds.ToHashSet();

        var result = paged.Items.Select(b =>
        {
            xmlMap.TryGetValue(b.Bibid, out var x);
            return new
            {
                // Đặt tên tường minh "bibId"/"bibTypeId" (khớp GetByMfn/Save) — dùng shorthand "b.Bibid" sẽ
                // serialize thành "bibid" (toàn chữ thường) do quy tắc CamelCase của System.Text.Json, và
                // "b.Bib_type_id" giữ nguyên dấu gạch dưới thành "bib_type_id" thay vì "bibTypeId", khác
                // hẳn tên field model frontend Bib đang dùng.
                bibId = b.Bibid, b.Mfn, bibTypeId = b.Bib_type_id, b.Status, b.PublicId,
                Title      = x?.Title,
                Author     = x?.Author,
                Publisher  = x?.Publisher,
                PublishDate = x?.PublishDate,
                HasReceipt = receiptSet.Contains(b.Bibid),
                HasOrder   = orderSet.Contains(b.Bibid)
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new { items = result, paged.TotalCount, paged.PageIndex, paged.PageSize }));
    }

    [HttpPost("SearchAll")]
    [Permission("CATALOG_BIBS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BibSearchRequest request) => await base.SearchAll(request);

    [HttpGet("{id:long}")]
    [Permission("CATALOG_BIBS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CATALOG_BIBS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Add")]
    [Permission("CATALOG_BIBS", "add")]
    public override async Task<IActionResult> Add([FromBody] BibRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CATALOG_BIBS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BibRequest request) => await base.Update(publicId, request);

    [HttpPut("ChangeStatus")]
    [Permission("CATALOG_BIBS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CATALOG_BIBS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    // Kiểm tra trùng ISBN (MARC 020$a) trong kho — dùng để cảnh báo khi thêm biểu ghi mới (Đơn nhận).
    // Không gắn [Permission] — chỉ cần đăng nhập, giống pattern GetById.
    [HttpGet("CheckIsbn")]
    public async Task<IActionResult> CheckIsbn([FromQuery] string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return Ok(ApiResponse<List<IsbnDuplicateResponse>>.Ok(new List<IsbnDuplicateResponse>()));
        var tenantId = GetTenantId();
        var bibIds = await db.BibDatas
            .Where(x => x.Field == "020" && x.SubField == "a" && x.Data == isbn && x.IsDelete != 2)
            .Select(x => x.BibId!.Value).Distinct().ToListAsync();

        var results = await (
            from b in db.Bibs
            where bibIds.Contains(b.Bibid) && b.IsDelete != 2 && (!tenantId.HasValue || b.TenantId == tenantId)
            join x in db.BibXmls on b.Bibid equals x.BibId into xj
            from x in xj.DefaultIfEmpty()
            select new IsbnDuplicateResponse { BibId = b.Bibid, Mfn = b.Mfn, Title = x != null ? x.Title : null }
        ).ToListAsync();

        return Ok(ApiResponse<List<IsbnDuplicateResponse>>.Ok(results));
    }

    // Tra cứu ảnh bìa theo ISBN (Google Books, dự phòng OpenLibrary) — dùng ở màn hình thêm nhan đề mới
    // trước khi biểu ghi Bib tồn tại. Không gắn [Permission] — chỉ cần đăng nhập, giống CheckIsbn.
    [HttpGet("FetchCoverByIsbn")]
    public async Task<IActionResult> FetchCoverByIsbn([FromQuery] string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return Ok(ApiResponse<string?>.Ok(null));
        var url = await coverLookup.FindCoverUrlAsync(isbn);
        return Ok(ApiResponse<string?>.Ok(url, url != null ? "Đã tìm thấy ảnh bìa" : "Không tìm thấy ảnh bìa"));
    }

    [HttpGet("GetByMfn/{mfn:long}")]
    [Permission("CATALOG_BIBS", "view")]
    public async Task<IActionResult> GetByMfn(long mfn)
    {
        var tenantId = GetTenantId();
        var bib = await db.Bibs.FirstOrDefaultAsync(x => x.Mfn == mfn && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));

        var xml = await db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bib.Bibid);
        var bibData = await db.BibDatas
            .Where(x => x.BibId == bib.Bibid && x.IsDelete != 2)
            .ToListAsync();

        var fields = bibData
            .GroupBy(x => new { x.Field, x.L1, x.L2 })
            .OrderBy(g => g.Key.Field)
            .Select(g => new BibMarcFieldResponse
            {
                Tag  = g.Key.Field,
                Ind1 = g.Key.L1,
                Ind2 = g.Key.L2,
                SubFields = g.Select(sf => new BibMarcSubFieldResponse
                {
                    Code  = sf.SubField,
                    Value = sf.Data
                }).ToList()
            }).ToList();

        var controlFields = await db.FixedFieldValues
            .Where(x => x.Bibid == bib.Bibid && x.IsDelete != 2)
            .OrderBy(x => x.Field)
            .Select(x => new BibMarcFieldResponse { Tag = x.Field, Value = x.Value })
            .ToListAsync();
        // Biểu ghi cũ có thể chưa có đủ field điều khiển (VD 002/004/006/007 mới mở rộng phạm vi) —
        // điền placeholder rỗng để UI luôn hiển thị đủ khối 001-008, giá trị thật sẽ được sinh ở lần Save kế tiếp.
        foreach (var tag in ControlFieldTags)
            if (!controlFields.Any(x => x.Tag == tag))
                controlFields.Add(new BibMarcFieldResponse { Tag = tag, Value = "" });
        controlFields = controlFields.OrderBy(x => x.Tag).ToList();
        fields.InsertRange(0, controlFields);

        // Tự động lấy ảnh bìa theo ISBN (020$a) nếu biểu ghi chưa có ảnh — chỉ chạy 1 lần, kết quả được
        // lưu lại nên các lần xem sau không gọi lại. Nuốt lỗi, không chặn hiển thị biểu ghi.
        if (string.IsNullOrEmpty(bib.Images))
        {
            var isbn = bibData.FirstOrDefault(x => x.Field == "020" && x.SubField == "a")?.Data;
            if (!string.IsNullOrWhiteSpace(isbn))
            {
                try
                {
                    var coverUrl = await coverLookup.FindCoverUrlAsync(isbn);
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        bib.Images = coverUrl;
                        await db.SaveChangesAsync();
                    }
                }
                catch
                {
                    // nuốt lỗi có chủ đích — cover lookup không được phép chặn hiển thị biểu ghi
                }
            }
        }

        var response = new BibMarcResponse
        {
            Mfn         = bib.Mfn,
            BibId       = bib.Bibid,
            BibTypeId   = bib.Bib_type_id,
            CollectionId = bib.CollectionId,
            Title       = xml?.Title,
            Author      = xml?.Author,
            Publisher   = xml?.Publisher,
            PublishDate = xml?.PublishDate,
            Images      = bib.Images,
            Fields      = fields
        };
        return Ok(ApiResponse<BibMarcResponse>.Ok(response));
    }

    [HttpPost("Save")]
    [Permission("CATALOG_BIBS", "add")]
    public async Task<IActionResult> Save([FromBody] SaveBibRequest r)
    {
        Bib bib;
        bool isNew = false;
        if (r.BibId.HasValue && r.BibId > 0)
        {
            // Tenant: chỉ sửa được biểu ghi của đơn vị mình — trước đây tra theo BibId không lọc đơn vị nên cán bộ
            // đơn vị A ghi đè được biểu ghi của đơn vị B. BibId không thấy trong phạm vi → 404.
            var callerTenantId = GetTenantId();
            var found = await db.Bibs.FirstOrDefaultAsync(x => x.Bibid == r.BibId && x.IsDelete != 2);
            if (found != null && callerTenantId.HasValue && found.TenantId != callerTenantId)
                return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));
            bib = found ?? new Bib { PublicId = Guid.NewGuid() };
            if (bib.Bibid == 0) isNew = true;
        }
        else
        {
            bib = new Bib { PublicId = Guid.NewGuid() };
            isNew = true;
        }

        var userId = GetCurrentUserId();
        if (r.BibTypeId.HasValue) bib.Bib_type_id = r.BibTypeId;
        if (r.CollectionId.HasValue) bib.CollectionId = r.CollectionId;
        if (!string.IsNullOrWhiteSpace(r.Images)) bib.Images = r.Images;
        bib.UpdateRowBy    = userId;
        bib.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            bib.TenantId       = GetTenantId();
            bib.CreatedRowBy   = userId;
            bib.CreatedRowDate = DateTime.Now;
            db.Bibs.Add(bib);
        }
        // Lịch sử thay đổi chi tiết (Đợt 17) — diff cấp Bib TRƯỚC SaveChangesAsync (ChangeTracker mất
        // OriginalValue sau khi lưu). Gộp cùng diff BibData/field điều khiển bên dưới, ghi 1 sự kiện.
        var bibChanges = EntityAuditService.DiffTrackedEntity(db, bib,
            [nameof(Bib.Bib_type_id), nameof(Bib.CollectionId), nameof(Bib.Images)], EmptyMaskedFields);
        await db.SaveChangesAsync();

        // Mfn được quy ước bằng đúng Bibid trong toàn hệ thống (đối chiếu dữ liệu thật) — Bibid chỉ có
        // sau khi insert (identity), nên gán Mfn ở bước riêng cho biểu ghi mới.
        if (isNew && !bib.Mfn.HasValue)
        {
            bib.Mfn = bib.Bibid;
            await db.SaveChangesAsync();
        }

        // Xóa BibData cũ, insert mới (chỉ field dữ liệu, Tag >= "010" — field điều khiển xử lý riêng bên dưới)
        // Lịch sử thay đổi chi tiết (Đợt 17) — BibData lưu kiểu XÓA-RỒI-TẠO-LẠI (mỗi dòng mới cấp PublicId
        // mới hoàn toàn, không giữ định danh qua các lần lưu — khác Reader/LoanTransaction/DigitalDocument
        // vốn diff qua ChangeTracker). Viết lại engine lưu để giữ định danh dòng (như ELIB-LRC từng làm)
        // là rủi ro cao vào lõi biên mục — KHÔNG làm ở đợt này. Thay vào đó diff Ở MỨC TAG: gộp mọi
        // subfield/occurrence cùng 1 tag MARC thành 1 chuỗi so sánh — mất độ chi tiết theo từng subfield/
        // occurrence riêng lẻ, nhưng không đụng gì tới cơ chế lưu hiện tại (0 rủi ro cho luồng biên mục),
        // vẫn cho biết chính xác "trường nào đã đổi, từ gì sang gì" ở mức tag.
        var oldDataList = await db.BibDatas.Where(x => x.BibId == bib.Bibid).ToListAsync();
        db.BibDatas.RemoveRange(oldDataList);
        var oldTagSig = oldDataList
            .Where(x => !string.IsNullOrEmpty(x.Field) && string.CompareOrdinal(x.Field, "010") >= 0)
            .GroupBy(x => x.Field!)
            .ToDictionary(g => g.Key, g => string.Join(" ",
                g.OrderBy(x => x.BibDataId).Select(x => $"({x.L1}{x.L2})${x.SubField}={x.Data}")));
        var newTagSig = new Dictionary<string, List<string>>();

        var controlFieldValues = new Dictionary<string, string>();
        foreach (var field in r.Fields)
        {
            if (string.IsNullOrEmpty(field.Tag)) continue;
            if (string.CompareOrdinal(field.Tag, "010") < 0)
            {
                if (!string.IsNullOrEmpty(field.Value)) controlFieldValues[field.Tag] = field.Value;
                continue;
            }
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
                    TenantId       = GetTenantId(),
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now
                });
                if (!newTagSig.TryGetValue(field.Tag, out var parts)) newTagSig[field.Tag] = parts = [];
                parts.Add($"({field.Ind1}{field.Ind2})${sf.Code}={sf.Value}");
            }
        }

        var bibDataTagChanges = new List<EntityAuditService.FieldChange>();
        foreach (var tag in oldTagSig.Keys.Union(newTagSig.Keys).Distinct())
        {
            var oldSig = oldTagSig.TryGetValue(tag, out var o) ? o : null;
            var newSig = newTagSig.TryGetValue(tag, out var n) ? string.Join(" ", n) : null;
            if (!string.Equals(oldSig, newSig, StringComparison.Ordinal))
                bibDataTagChanges.Add(new EntityAuditService.FieldChange
                {
                    Field = tag, OldValue = EntityAuditService.Truncate(oldSig), NewValue = EntityAuditService.Truncate(newSig),
                });
        }
        await db.SaveChangesAsync();

        // ─── Đồng bộ PrintBook.BibXML (Title/Author/Publisher/PublishDate/DDC/Keyword/Isbd) từ MARC vừa lưu ───
        // Accr2 tạm hoãn — chưa có yêu cầu/cấu hình sinh AACR2.
        var xml = await db.BibXmls.FirstOrDefaultAsync(x => x.BibId == bib.Bibid);
        if (xml == null)
        {
            xml = new BibXml
            {
                BibId          = bib.Bibid,
                PublicId       = Guid.NewGuid(),
                TenantId       = GetTenantId(),
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            };
            db.BibXmls.Add(xml);
        }
        xml.Title       = GetSubfieldValues(r.Fields, "245", 'a', 'b');
        xml.Author      = GetSubfieldValue(r.Fields, "100", 'a') ?? GetSubfieldValue(r.Fields, "700", 'a');
        xml.Publisher   = GetSubfieldValue(r.Fields, "264", 'b') ?? GetSubfieldValue(r.Fields, "260", 'b');
        xml.PublishDate = GetSubfieldValue(r.Fields, "264", 'c') ?? GetSubfieldValue(r.Fields, "260", 'c');
        xml.DDC         = GetSubfieldValue(r.Fields, "082", 'a');
        xml.Keyword     = GetSubfieldValues(r.Fields, "650", 'a', joinAll: true);
        xml.Isbd        = await GenerateIsbdAsync(db, bib.Bib_type_id, r.Fields);
        xml.UpdateRowBy = userId;
        xml.UpdatedRowDate = DateTime.Now;
        await db.SaveChangesAsync();

        // ─── Tự sinh + lưu Leader(000)/001/003/005/008 vào PrintBook.fixed_field_value ───
        var bibType = bib.Bib_type_id.HasValue
            ? await db.BibTypes.FirstOrDefaultAsync(x => x.Id == bib.Bib_type_id)
            : null;
        var tenant = bib.TenantId.HasValue
            ? await db.Tenants.FirstOrDefaultAsync(x => x.Id == bib.TenantId)
            : null;
        var existingControlFields = await db.FixedFieldValues
            .Where(x => x.Bibid == bib.Bibid && x.IsDelete != 2)
            .ToListAsync();

        var controlFieldChanges = new List<EntityAuditService.FieldChange>();
        foreach (var tag in ControlFieldTags)
        {
            var existing = existingControlFields.FirstOrDefault(x => x.Field == tag);
            string value;
            if (tag == "005")
            {
                // 005 = thời điểm giao dịch gần nhất — luôn sinh lại, không giữ giá trị cũ/client
                value = BuildDefaultControlValue(tag, bibType, tenant?.Code, bib.Mfn, xml?.PublishDate);
            }
            else if (tag != "001" && controlFieldValues.TryGetValue(tag, out var clientVal))
            {
                value = clientVal;
            }
            else if (existing != null)
            {
                // 001 = Số kiểm soát/MFN — khoá không cho client ghi đè (giữ nguyên giá trị đã có);
                // các tag khác: giữ nguyên giá trị đã có, không tự sinh đè lên giá trị đã tồn tại
                continue;
            }
            else
            {
                value = BuildDefaultControlValue(tag, bibType, tenant?.Code, bib.Mfn, xml?.PublishDate);
            }

            if (existing != null)
            {
                // Diff chính xác tuyệt đối (khác BibData ở trên) — logic update-tại-chỗ đã có sẵn cho
                // field điều khiển, so existing.Value trước khi ghi đè.
                if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
                    controlFieldChanges.Add(new EntityAuditService.FieldChange { Field = tag, OldValue = existing.Value, NewValue = value });
                existing.Value          = value;
                existing.UpdateRowBy    = userId;
                existing.UpdatedRowDate = DateTime.Now;
            }
            else
            {
                controlFieldChanges.Add(new EntityAuditService.FieldChange { Field = tag, OldValue = null, NewValue = value });
                db.FixedFieldValues.Add(new FixedFieldValue
                {
                    Bibid          = bib.Bibid,
                    Field          = tag,
                    Value          = value,
                    TenantId       = GetTenantId(),
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now
                });
            }
        }

        // Lịch sử thay đổi chi tiết (Đợt 17) — gộp Bib + BibData(mức tag) + field điều khiển thành 1 sự
        // kiện, queue trước SaveChangesAsync CUỐI CÙNG của hàm. LƯU Ý: khác Reader/LoanTransaction/
        // DigitalDocument (atomic tuyệt đối) — hàm Save này gọi SaveChangesAsync nhiều lần rải rác,
        // KHÔNG bọc transaction chung sẵn có; không thêm transaction wrapper mới ở đợt này (rủi ro cao
        // cho luồng lưu lõi) — chấp nhận 1 khung hở lý thuyết hẹp nếu lỗi xảy ra giữa các bước.
        var allBibChanges = bibChanges.Concat(bibDataTagChanges).Concat(controlFieldChanges).ToList();
        if (allBibChanges.Count > 0)
        {
            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync();
            var reason = EntityAuditService.ReadReasonHeader(HttpContext);
            EntityAuditService.QueueEntityChangeLog(db, "PrintBib", bib.PublicId, userId ?? 0, actorName,
                GetTenantId(), isNew ? "Add" : "Update", reason, allBibChanges,
                HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        await db.SaveChangesAsync();

        // Đồng bộ lại chỉ mục tìm kiếm cho biểu ghi vừa lưu (chạy nền, không chặn response).
        // Port ELIB-LRC 09-30: qua IBackgroundJobClient — máy chủ không bật Hangfire thì bỏ qua (trước đây lưu xong vẫn 500).
        BackgroundJobs.TryEnqueue<PrintBookIndexingJob>(HttpContext.RequestServices, j => j.RunAsync(bib.Bibid), logger);
        // Đợt 22.3 — xuất lại MARC cho Z39.50/SRU (Zebra); bib chưa duyệt (Status khác "f") tự bị gỡ khỏi
        // thư mục xuất (MarcRecordBuilder trả null cho bib chưa duyệt).
        BackgroundJobs.TryEnqueue<ZebraExportJob>(HttpContext.RequestServices, j => j.RunAsync(bib.Bibid), logger);

        return Ok(ApiResponse<object>.Ok(new
        {
            mfn          = bib.Mfn,
            bibId        = bib.Bibid,
            collectionId = bib.CollectionId,
            title        = xml?.Title,
            author       = xml?.Author,
            publisher    = xml?.Publisher
        }));
    }

    [HttpDelete("Delete/{mfn:long}")]
    [Permission("CATALOG_BIBS", "delete")]
    public async Task<IActionResult> DeleteByMfn(long mfn)
    {
        // Tenant: trước đây không lọc đơn vị — xoá được biểu ghi của đơn vị khác theo MFN.
        var tenantId = GetTenantId();
        var bib = await db.Bibs.FirstOrDefaultAsync(x => x.Mfn == mfn && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));
        bib.IsDelete       = 2;
        bib.UpdateRowBy    = GetCurrentUserId();
        bib.UpdatedRowDate = DateTime.Now;
        await db.SaveChangesAsync();

        // Gỡ khỏi chỉ mục — nếu bỏ bước này, biểu ghi đã xóa vẫn tìm thấy trên OPAC
        // (đúng lỗi đang tồn tại ở phía tài liệu số, không lặp lại ở đây).
        BackgroundJobs.TryEnqueue<PrintBookIndexingJob>(HttpContext.RequestServices, j => j.RemoveAsync(bib.Bibid), logger);
        BackgroundJobs.TryEnqueue<ZebraExportJob>(HttpContext.RequestServices, j => j.RemoveAsync(bib.Bibid), logger); // Đợt 22.3

        return Ok(ApiResponse<string>.Ok("Đã xóa biểu ghi"));
    }

    // Di chuyển hàng loạt nhiều biểu ghi sang bộ sưu tập khác (Quản lý tài liệu số/Biên mục biểu ghi/Quản lý đơn nhận).
    [HttpPut("BulkMoveCollection")]
    [Permission("CATALOG_BIBS", "edit")]
    public async Task<IActionResult> BulkMoveCollection([FromBody] BibBulkMoveCollectionRequest r)
    {
        if (r.BibIds == null || r.BibIds.Count == 0) return BadRequest(ApiResponse<string>.Fail("Chưa chọn biểu ghi nào"));
        var collectionExists = await db.EbookCollections.AnyAsync(x => x.Id == r.CollectionId && x.IsDelete != 2);
        if (!collectionExists) return BadRequest(ApiResponse<string>.Fail("Bộ sưu tập đích không tồn tại"));

        var tenantId = GetTenantId();
        var bibs = await db.Bibs.Where(x => r.BibIds.Contains(x.Bibid) && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId)).ToListAsync();
        var userId = GetCurrentUserId();
        foreach (var b in bibs) { b.CollectionId = r.CollectionId; b.UpdateRowBy = userId; b.UpdatedRowDate = DateTime.Now; }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { UpdatedCount = bibs.Count }, "Đã chuyển bộ sưu tập"));
    }

    /// <summary>
    /// Tạo lại toàn bộ chỉ mục tìm kiếm cho tài liệu in (đặc tả ở docs/BACKEND-PRINT-DOCUMENT-MGMT.md
    /// mục 10). Chạy nền qua Hangfire và trả success ngay — FE không polling.
    /// </summary>
    [HttpPost("RebuildIndex")]
    [Permission("CATALOG_BIBS", "edit")]
    public IActionResult RebuildIndex()
    {
        // Đợt 18: cán bộ 1 đơn vị chỉ dựng lại chỉ mục biểu ghi của đơn vị mình (trước đây dựng lại TOÀN BỘ
        // mọi đơn vị); tài khoản hệ thống (không tenant) vẫn dựng lại toàn bộ như cũ.
        var tenantId = GetTenantId();
        var jobId = BackgroundJobs.TryEnqueue<PrintBookIndexingJob>(HttpContext.RequestServices, j => j.RunBulkForTenantAsync(tenantId, null, null, 500), logger);
        if (jobId == null) return BadRequest(ApiResponse<string>.Fail("Xử lý nền (Hangfire) chưa được bật trên hệ thống."));
        return Ok(ApiResponse<object>.Ok(new { success = true, jobId }));
    }

    [HttpGet("Items/{bibId:long}")]
    [Permission("CATALOG_BIBS", "view")]
    public async Task<IActionResult> Items(long bibId)
    {
        var tenantId = GetTenantId();
        var bibExists = await db.Bibs.AnyAsync(x => x.Bibid == bibId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!bibExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));

        var barcodes = await db.Barcodes
            .Where(x => x.BibId == bibId && x.IsDelete != 2)
            .OrderBy(x => x.BarcodeValue)
            .Select(x => new { x.Id, barcode = x.BarcodeValue, storeId = x.Store, x.Status, x.PublicId })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(barcodes));
    }

    // Đăng ký ĐKCB theo lô trực tiếp cho 1 biểu ghi (không qua phiếu nhập)
    [HttpPost("RegisterBarcodes")]
    [Permission("CATALOG_BIBS", "edit")]
    public async Task<IActionResult> RegisterBarcodes([FromBody] RegisterBibBarcodesRequest r)
    {
        var callerTenantId = GetTenantId();
        var bib = await db.Bibs.FirstOrDefaultAsync(x => x.Bibid == r.BibId && x.IsDelete != 2
            && (!callerTenantId.HasValue || x.TenantId == callerTenantId));
        if (bib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));
        // Đợt 20: ĐKCB thuộc đơn vị của biểu ghi; mã duy nhất THEO ĐƠN VỊ.
        var tenantId = bib.TenantId;

        var userId = GetCurrentUserId();
        var batch = await BarcodeNumbering.CreateBatchAsync(db, tenantId, r.Prefix, r.DigitLength, r.Quantity, r.StartNumber, bc =>
        {
            bc.BibId        = bib.Bibid;
            bc.Store        = r.StoreId;
            bc.CreatedRowBy = userId;
        });
        if (!batch.IsOk) return BadRequest(ApiResponse<string>.Fail(batch.Error!));
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { items = batch.Value!.Select(bc => new { id = bc.Id, barcode = bc.BarcodeValue, storeId = bc.Store }) }));
    }

    // Đăng ký 1 số ĐKCB nhập tay trực tiếp cho 1 biểu ghi (không qua phiếu nhập)
    [HttpPost("RegisterSingleBarcode")]
    [Permission("CATALOG_BIBS", "edit")]
    public async Task<IActionResult> RegisterSingleBarcode([FromBody] RegisterBibSingleBarcodeRequest r)
    {
        var callerTenantId = GetTenantId();
        var bib = await db.Bibs.FirstOrDefaultAsync(x => x.Bibid == r.BibId && x.IsDelete != 2
            && (!callerTenantId.HasValue || x.TenantId == callerTenantId));
        if (bib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));
        // Đợt 20: ĐKCB thuộc đơn vị của biểu ghi; mã duy nhất THEO ĐƠN VỊ.
        var tenantId = bib.TenantId;

        var value = (r.BarcodeValue ?? "").Trim();
        if (string.IsNullOrEmpty(value)) return BadRequest(ApiResponse<string>.Fail("Số ĐKCB không được để trống"));

        if (await BarcodeNumbering.ExistsAsync(db, tenantId, value)) return BadRequest(ApiResponse<string>.Fail($"Số ĐKCB '{value}' đã tồn tại"));

        var userId = GetCurrentUserId();
        var bc = new Barcode
        {
            BibId          = bib.Bibid,
            BarcodeValue   = value,
            Store          = r.StoreId,
            Status         = "I",
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now,
            TenantId       = tenantId
        };
        db.Barcodes.Add(bc);
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { id = bc.Id, barcode = bc.BarcodeValue, storeId = bc.Store, status = bc.Status, publicId = bc.PublicId }));
    }

    // Xóa 1 số ĐKCB đã đăng ký trực tiếp cho biểu ghi
    [HttpDelete("DeleteBarcode/{barcodeId:long}")]
    [Permission("CATALOG_BIBS", "delete")]
    public async Task<IActionResult> DeleteBarcode(long barcodeId)
    {
        var tenantId = GetTenantId();
        var bc = await db.Barcodes.FirstOrDefaultAsync(x => x.Id == barcodeId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bc == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy số ĐKCB"));

        bc.IsDelete       = 2;
        bc.UpdateRowBy    = GetCurrentUserId();
        bc.UpdatedRowDate = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã xóa số ĐKCB"));
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}

public class RegisterBibBarcodesRequest
{
    public long    BibId       { get; set; }
    public string? Prefix      { get; set; }
    public int     DigitLength { get; set; } = 6;
    public int     Quantity    { get; set; }
    public int?    StoreId     { get; set; }
    public int?    StartNumber { get; set; }
}

public class RegisterBibSingleBarcodeRequest
{
    public long    BibId        { get; set; }
    public string? BarcodeValue { get; set; }
    public int?    StoreId      { get; set; }
}
