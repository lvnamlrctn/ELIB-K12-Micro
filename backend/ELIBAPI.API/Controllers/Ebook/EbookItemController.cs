using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookItemController : GenericController<EbookItem, EbookItemSearchRequest, EbookItemRequest>
{
    private readonly IEbookItemRepository      _ebookRepo;
    private readonly IMinioService             _minio;
    private readonly IEbookFileRepository      _fileRepo;
    private readonly IConfiguration            _config;
    private readonly IBookImageAnalyzerService _analyzerService;
    private readonly ELIBAPIDbContext          _db;

    public EbookItemController(IEbookItemRepository repo, IMinioService minio, IEbookFileRepository fileRepo, IConfiguration config, IBookImageAnalyzerService analyzerService, ELIBAPIDbContext db) : base(repo)
    {
        _ebookRepo       = repo;
        _minio           = minio;
        _fileRepo        = fileRepo;
        _config          = config;
        _analyzerService = analyzerService;
        _db              = db;
    }

    private static readonly System.Text.Json.JsonSerializerOptions _camelCaseOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly List<(string Code, string Name)> ExportableFields =
    [
        ("Title",         "Tiêu đề"),
        ("OtherTitle",    "Tiêu đề khác"),
        ("Author",        "Tác giả"),
        ("OldAuthor",     "Người hướng dẫn"),
        ("Publisher",     "Nhà xuất bản"),
        ("PublishDate",   "Năm xuất bản"),
        ("Keyword",       "Từ khóa"),
        ("Page",          "Số trang"),
        ("Language",      "Ngôn ngữ"),
        ("Status",        "Trạng thái"),
        ("Submited",      "Ngày nộp"),
        ("AllowDownload", "Cho phép tải"),
        ("Free",          "Miễn phí"),
        ("Images",        "Ảnh bìa"),
    ];

    [NonAction]
    public override Task<IActionResult> Add([FromBody] EbookItemRequest request) => throw new NotSupportedException();

    [HttpPut("BulkMoveCollection")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> BulkMoveCollection([FromBody] EbookItemBulkMoveCollectionRequest request)
    {
        if (request.PublicIds.Count == 0) return BadRequest(ApiResponse<string>.Fail("Chưa chọn tài liệu nào"));
        try
        {
            var count = await _ebookRepo.BulkMoveCollectionAsync(request);
            return Ok(ApiResponse<object>.Ok(new { UpdatedCount = count }, "Đã chuyển bộ sưu tập"));
        }
        catch (Exception ex) { return BadRequest(ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpPost("Add")]
    [Permission("DIGITAL_DOC", "add")]
    public async Task<IActionResult> AddEbook([FromBody] EbookItemAddRequest request)
    {
        request.Images = await UploadIfBase64Async(request.Images);
        try
        {
            var item = await _ebookRepo.AddEbookAsync(request);
            return Ok(ApiResponse<EbookItem>.Ok(item, Localizer["AddSuccess"]));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    [Consumes("multipart/form-data")]
    public override async Task<IActionResult> Update(Guid publicId, [FromForm] EbookItemRequest request)
    {
        // Lấy ảnh cũ trước khi upload ảnh mới
        var existing  = await _repo.GetByPublicIdAsync(publicId);
        var oldImages = existing?.Images;

        // Ảnh: ưu tiên file upload, fallback về base64 string
        var imageFile = Request.Form.Files.GetFile("imageFile");
        if (imageFile != null)
            request.Images = await _minio.UploadAsync(
                imageFile.OpenReadStream(), imageFile.FileName, imageFile.ContentType);
        else
            request.Images = await UploadIfBase64Async(request.Images);

        // Xóa ảnh cũ nếu đã thay bằng ảnh mới
        if (request.Images != null && request.Images != oldImages && IsMinioUrl(oldImages))
            _ = _minio.DeleteAsync(oldImages!);

        // MetaDataEntries gửi dưới dạng JSON string trong form field
        var metaJson = Request.Form["metaDataEntriesJson"].FirstOrDefault();
        if (!string.IsNullOrEmpty(metaJson))
        {
            request.MetaDataEntries = System.Text.Json.JsonSerializer
                .Deserialize<List<MetaDataEntryRequest>>(metaJson, _camelCaseOptions) ?? [];
        }

        try
        {
            var item = await _ebookRepo.UpdateEbookAsync(publicId, request);
            return Ok(ApiResponse<EbookItem>.Ok(item, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    [HttpGet("{publicId:guid}/CoverPreview/{page:int}")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> CoverPreview(Guid publicId, int page)
    {
        var item = await _repo.GetByPublicIdAsync(publicId);
        if (item == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy tài liệu"));

        var pdfFile = await GetPdfFileAsync(item.Id);
        if (pdfFile?.Url == null) return NotFound(ApiResponse<string>.Fail("Tài liệu chưa có file PDF"));

        byte[]? png;
        try
        {
            var (stream, _) = await _minio.GetObjectStreamAsync(pdfFile.Url);
            await using (stream) png = PdfPageExtractHelper.RenderPageAsPng(stream, page);
        }
        catch (Exception ex)
        {
            return NotFound(ApiResponse<string>.Fail($"Không thể tải file từ MinIO: {ex.Message}"));
        }

        if (png == null) return NotFound(ApiResponse<string>.Fail("Trang không tồn tại"));
        return File(png, "image/png");
    }

    [HttpPost("{publicId:guid}/SetCoverFromPage")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> SetCoverFromPage(Guid publicId, [FromBody] SetCoverFromPageRequest request)
    {
        var item = await _repo.GetByPublicIdAsync(publicId);
        if (item == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy tài liệu"));

        var pdfFile = await GetPdfFileAsync(item.Id);
        if (pdfFile?.Url == null) return NotFound(ApiResponse<string>.Fail("Tài liệu chưa có file PDF"));

        byte[] png;
        try
        {
            var (stream, _) = await _minio.GetObjectStreamAsync(pdfFile.Url);
            byte[]? rendered;
            await using (stream) rendered = PdfPageExtractHelper.RenderPageAsPng(stream, request.Page);
            if (rendered == null) return NotFound(ApiResponse<string>.Fail("Trang không tồn tại"));
            png = rendered;
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail($"Không thể tạo ảnh bìa: {ex.Message}"));
        }

        var oldImages = item.Images;
        await using var ms = new MemoryStream(png);
        var newKey = await _minio.UploadAsync(ms, $"cover-{publicId}.png", "image/png");

        if (IsMinioUrl(oldImages)) _ = _minio.DeleteAsync(oldImages!);

        item.Images = newKey;
        await _db.SaveChangesAsync();
        await _ebookRepo.SyncElasticAsync(publicId);

        return Ok(ApiResponse<object>.Ok(new { images = ResolveImageUrl(newKey) }, "Đã cập nhật ảnh bìa"));
    }

    private async Task<EbookFile?> GetPdfFileAsync(long ebookId) =>
        await _db.EbookFiles
            .Where(f => f.EbookId == ebookId && f.IsDelete != 2 &&
                        (f.FileExt == "pdf" || f.FileExt == ".pdf" || f.FileType == "application/pdf"))
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync();

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DIGITAL_DOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPut("ChangeStatus/{publicId:guid}")]
    [HttpPatch("ChangeStatus/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    public Task<IActionResult> ChangeStatusById(Guid publicId, [FromBody] System.Text.Json.JsonElement body)
    {
        byte status = body.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True   => 2,
            System.Text.Json.JsonValueKind.False  => 1,
            System.Text.Json.JsonValueKind.Number => body.GetByte(),
            _ => 1
        };
        return base.ChangeStatus(new ChangeStatusRequest { PublicId = publicId, Status = status });
    }

    [HttpGet("{id:long}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<EbookItem>.Fail(Localizer["NotFound"], 404));
        entity.Images = ResolveImageUrl(entity.Images);
        return Ok(ApiResponse<EbookItem>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var detail = await _ebookRepo.GetDetailAsync(publicId);
        if (detail == null) return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        detail.Item.Images = ResolveImageUrl(detail.Item.Images);
        return Ok(ApiResponse<EbookItemDetailResponse>.Ok(detail));
    }

    [HttpPost("Search")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookItemSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<PagedResult<EbookItem>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookItemSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<List<EbookItem>>.Ok(items));
    }

    [HttpGet("GetExportFields")]
    [Permission("DIGITAL_DOC", "view")]
    public IActionResult GetExportFields()
    {
        var fields = ExportableFields.Select(f => new { code = f.Code, name = f.Name });
        return Ok(ApiResponse<object>.Ok(fields));
    }

    [HttpPost("ExportExcel")]
    [HttpPost("Export")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> ExportExcel([FromBody] EbookItemExportRequest request, [FromServices] ISystemParameterService sysParam)
    {
        var items = await _ebookRepo.SearchAllAsync(request);

        var selected = (request.Fields?.Count > 0
            ? ExportableFields.Where(f => request.Fields.Any(rf => rf.Equals(f.Code, StringComparison.OrdinalIgnoreCase)))
            : ExportableFields.AsEnumerable()).ToList();

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Tài liệu điện tử");

        var lastColumn = selected.Count + 1;
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH TÀI LIỆU ĐIỆN TỬ", lastColumn);

        ws.Cell(startRow, 1).Value = "STT";
        for (int i = 0; i < selected.Count; i++)
            ws.Cell(startRow, i + 2).Value = selected[i].Name;

        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (int r = 0; r < items.Count; r++)
        {
            ws.Cell(startRow + 1 + r, 1).Value = r + 1;
            for (int c = 0; c < selected.Count; c++)
                ws.Cell(startRow + 1 + r, c + 2).Value = GetEbookFieldValue(items[r], selected[c].Code);
        }

        ExcelReportHelper.ApplyTableBorders(ws, startRow, startRow + items.Count, 1, lastColumn);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "danh-sach-tai-lieu.xlsx");
    }

    [HttpPost("ImportXml")]
    [Permission("DIGITAL_DOC", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportXml(
        [FromForm] long?   collectionId = null,
        [FromForm] long?   subjectId    = null,
        [FromForm] long?   topicId      = null,
        [FromForm] byte?   status       = null,
        [FromForm] string? portalId     = null,
        [FromForm] string? language     = null)
    {
        var files = Request.Form.Files;
        if (files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Chưa chọn file XML"));

        int imported = 0, failed = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var xmlContent   = await reader.ReadToEndAsync();
                var request      = ParseDublinCoreXml(xmlContent);

                request.CollectionId = collectionId;
                request.SubjectId    = subjectId;
                request.TopicId      = topicId;
                request.Status       = status ?? 1;
                request.PortalId     = portalId;
                if (!string.IsNullOrEmpty(language)) request.Language = language;

                await _ebookRepo.AddEbookAsync(request);
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<object>.Ok(
            new { imported, failed, errors },
            $"Import hoàn tất: {imported} thành công, {failed} lỗi"));
    }

    [HttpPost("SyncImages")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> SyncImages()
    {
        var legacyBase = (_config["LegacyFileServer:BaseUrl"] ?? "http://103.97.134.58:8089").TrimEnd('/');
        var items = await _ebookRepo.GetItemsWithOldImagesAsync(_minio.PublicBaseUrl);
        int synced = 0, failed = 0;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        foreach (var item in items)
        {
            if (item.Images == null || IsMinioUrl(item.Images)) continue;
            var downloadUrl = item.Images.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? item.Images
                : $"{legacyBase}/{item.Images.TrimStart('/')}";
            try
            {
                using var resp = await http.GetAsync(downloadUrl);
                if (!resp.IsSuccessStatusCode) { failed++; continue; }
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var ext = Path.GetExtension(downloadUrl.Split('?')[0]).TrimStart('.');
                if (string.IsNullOrEmpty(ext)) ext = "jpg";
                await using var stream = await resp.Content.ReadAsStreamAsync();
                var newUrl = await _minio.UploadAsync(stream, $"img.{ext}", contentType);
                var updated = await _ebookRepo.UpdateItemImagesAsync(item.Id, newUrl);
                if (updated) synced++;
                else failed++;
            }
            catch { failed++; }
        }

        return Ok(ApiResponse<object>.Ok(
            new { synced, failed, total = items.Count },
            $"Đã sync {synced}/{items.Count} ảnh bìa lên MinIO"));
    }

    [HttpPost("AnalyzeBookImage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalyzeBookImage([FromForm] List<IFormFile> images)
    {
        if (images == null || images.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng gửi ít nhất 1 ảnh"));

        var streams = images.Select(f => f.OpenReadStream()).ToList();
        try
        {
            var result = await _analyzerService.AnalyzeAsync(streams);
            return Ok(ApiResponse<BookMetadataResult>.Ok(result, "Phân tích thành công"));
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    [HttpPost("SyncElastic")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> SyncElastic()
    {
        var count = await _ebookRepo.BulkSyncElasticAsync();
        return Ok(ApiResponse<object>.Ok(new { count }, $"Đã đồng bộ {count} tài liệu lên Elasticsearch"));
    }

    [HttpPost("BulkSyncChunks")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> BulkSyncChunks([FromBody] BulkSyncChunksRequest? request)
    {
        var result = await _ebookRepo.BulkSyncChunksAsync(request ?? new BulkSyncChunksRequest());
        return Ok(ApiResponse<BulkSyncChunksResult>.Ok(result,
            $"Đã enqueue {result.Enqueued} tài liệu, bỏ qua {result.Skipped}"));
    }

    [HttpPost("ImportExcel")]
    [Permission("DIGITAL_DOC", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportExcel(
        [FromForm] long?   collectionId = null,
        [FromForm] long?   subjectId    = null,
        [FromForm] long?   topicId      = null,
        [FromForm] byte?   status       = null,
        [FromForm] string? portalId     = null,
        [FromForm] string? language     = null)
    {
        var file = Request.Form.Files.GetFile("file");
        if (file == null)
            return BadRequest(ApiResponse<object>.Fail("Chưa chọn file Excel"));

        int imported = 0, failed = 0;
        var errors = new List<string>();

        using var wb = new XLWorkbook(file.OpenReadStream());
        var ws = wb.Worksheets.First();

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int col = 1; col <= lastCol; col++)
        {
            var h = ws.Cell(1, col).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) headerMap[h] = col;
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            try
            {
                string? Get(string name) =>
                    headerMap.TryGetValue(name, out var c) ? ws.Cell(row, c).GetString().Trim() : null;
                static List<string> Split(string? v) =>
                    string.IsNullOrWhiteSpace(v) ? [] : v.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                static bool? ParseBool(string? v) =>
                    v == "Có" ? true : v == "Không" ? false : null;

                var req = new EbookItemAddRequest
                {
                    CollectionId  = collectionId,
                    SubjectId     = subjectId,
                    TopicId       = topicId,
                    Status        = status ?? 1,
                    PortalId      = portalId,
                    Language      = Get("Ngôn ngữ") is { Length: > 0 } l ? l : (language ?? "vi"),
                    Title         = Get("Tiêu đề"),
                    OtherTitles   = Split(Get("Tiêu đề khác")),
                    Authors       = Split(Get("Tác giả")),
                    Advisors      = Split(Get("Người hướng dẫn")),
                    Publisher     = Get("Nhà xuất bản"),
                    PublishDate   = Get("Năm xuất bản"),
                    Keywords      = Split(Get("Từ khóa")),
                    Images        = Get("Ảnh bìa"),
                    AllowDownload = ParseBool(Get("Cho phép tải")),
                    IsFree        = ParseBool(Get("Miễn phí")),
                };

                await _ebookRepo.AddEbookAsync(req);
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Row {row}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<object>.Ok(
            new { imported, failed, errors },
            $"Import hoàn tất: {imported} thành công, {failed} lỗi"));
    }

    [HttpPost("ImportDSpace")]
    [Permission("DIGITAL_DOC", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportDSpace(
        [FromForm] long?   collectionId = null,
        [FromForm] long?   subjectId    = null,
        [FromForm] long?   topicId      = null,
        [FromForm] byte?   status       = null,
        [FromForm] string? portalId     = null,
        [FromForm] string? language     = null)
    {
        var file = Request.Form.Files.GetFile("file");
        if (file == null)
            return BadRequest(ApiResponse<object>.Fail("Chưa chọn file ZIP"));

        int imported = 0, failed = 0;
        var errors = new List<string>();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        // Tìm tất cả folder chứa dublin_core.xml (bất kể tên folder)
        var itemFolders = zip.Entries
            .Where(e => e.Name.Equals("dublin_core.xml", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.Contains('/'))
            .Select(e => e.FullName[..e.FullName.LastIndexOf('/')])
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        foreach (var folder in itemFolders)
        {
            var prefix  = folder + "/";
            var entries = zip.Entries
                .Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Length > 0)
                .ToList();

            try
            {
                var xmlEntry = entries.FirstOrDefault(e =>
                    e.Name.Equals("dublin_core.xml", StringComparison.OrdinalIgnoreCase));
                if (xmlEntry == null)
                {
                    failed++;
                    errors.Add($"{folder}: thiếu dublin_core.xml");
                    continue;
                }

                string xmlContent;
                await using (var s = xmlEntry.Open())
                using (var reader = new StreamReader(s, System.Text.Encoding.UTF8))
                    xmlContent = await reader.ReadToEndAsync();

                var req = ParseDublinCoreXml(xmlContent);
                req.CollectionId = collectionId;
                req.SubjectId    = subjectId;
                req.TopicId      = topicId;
                req.Status       = status ?? 1;
                req.PortalId     = portalId;
                if (!string.IsNullOrEmpty(language)) req.Language = language;

                // Đọc danh sách file từ contents (nếu có), fallback lấy tất cả file trong folder
                var fileNames = new List<string>();
                var contentsEntry = entries.FirstOrDefault(e =>
                    e.Name.Equals("contents", StringComparison.OrdinalIgnoreCase));
                if (contentsEntry != null)
                {
                    await using var cs = contentsEntry.Open();
                    using var cr = new StreamReader(cs);
                    var text = await cr.ReadToEndAsync();
                    fileNames = [.. text
                        .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim().Split('\t')[0].Trim())
                        .Where(l => !string.IsNullOrEmpty(l))];
                }
                else
                {
                    // Không có contents → lấy mọi file không phải xml/metadata
                    fileNames = [.. entries
                        .Select(e => e.Name)
                        .Where(n => !n.Equals("dublin_core.xml", StringComparison.OrdinalIgnoreCase)
                                 && !n.Equals("contents", StringComparison.OrdinalIgnoreCase)
                                 && !n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))];
                }

                var item = await _ebookRepo.AddEbookAsync(req);

                int sortOrder = 1;
                foreach (var fileName in fileNames)
                {
                    var fileEntry = entries.FirstOrDefault(e =>
                        e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (fileEntry == null) continue;
                    try
                    {
                        var ext = Path.GetExtension(fileName).TrimStart('.');
                        if (string.IsNullOrEmpty(ext)) ext = "bin";
                        await using var fs = fileEntry.Open();
                        var objectName = await _minio.UploadPrivateAsync(fs, fileName, GetMimeType(ext));
                        await _fileRepo.AddAsync(new EbookFileRequest
                        {
                            EbookId   = item.Id,
                            Url       = objectName,
                            FileExt   = ext,
                            FileType  = GetMimeType(ext),
                            SortOrder = sortOrder++,
                            Type      = "ORIGINAL",
                        });
                    }
                    catch { }
                }

                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{folder}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<object>.Ok(
            new { imported, failed, errors },
            $"Import hoàn tất: {imported} thành công, {failed} lỗi"));
    }

    private static string GetMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        "pdf"  => "application/pdf",
        "doc"  => "application/msword",
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "xls"  => "application/vnd.ms-excel",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "ppt"  => "application/vnd.ms-powerpoint",
        "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "epub" => "application/epub+zip",
        "jpg" or "jpeg" => "image/jpeg",
        "png"  => "image/png",
        _      => "application/octet-stream",
    };

    [HttpPost("ExportMetaData")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> ExportMetaData([FromBody] EbookItemSearchRequest request)
    {
        var rows = await _ebookRepo.ExportDSpaceDataAsync(request, false);
        var zip  = await BuildDSpaceZipAsync(rows, false);
        return File(zip, "application/zip", $"dspace-metadata-{DateTime.Now:yyyyMMddHHmmss}.zip");
    }

    [HttpPost("ExportMetaDataFile")]
    [Permission("DIGITAL_DOC", "view")]
    public async Task<IActionResult> ExportMetaDataFile([FromBody] EbookItemSearchRequest request)
    {
        var rows = await _ebookRepo.ExportDSpaceDataAsync(request, true);
        var zip  = await BuildDSpaceZipAsync(rows, true);
        return File(zip, "application/zip", $"dspace-saf-{DateTime.Now:yyyyMMddHHmmss}.zip");
    }

    private static string? GetEbookFieldValue(EbookItem item, string code) => code switch
    {
        "Title"         => item.ItemXml?.Title,
        "OtherTitle"    => item.ItemXml?.OtherTitle,
        "Author"        => item.ItemXml?.Author,
        "OldAuthor"     => item.ItemXml?.OldAuthor,
        "Publisher"     => item.ItemXml?.Publisher,
        "PublishDate"   => item.ItemXml?.PublishDate,
        "Keyword"       => item.ItemXml?.Keyword,
        "Page"          => item.ItemXml?.Page,
        "Language"      => item.Language,
        "Status"        => item.Status == 2 ? "Xuất bản" : "Ẩn",
        "Submited"      => item.Submited?.ToString("dd/MM/yyyy"),
        "AllowDownload" => item.AllowDownload == 1 ? "Có" : "Không",
        "Free"          => item.Free == 1 ? "Có" : "Không",
        "Images"        => item.Images,
        _               => null
    };

    private async Task<byte[]> BuildDSpaceZipAsync(List<DSpaceItemExportRow> rows, bool includeFiles)
    {
        var ms = new MemoryStream();

        using (var zip  = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row    = rows[i];
                var folder = $"item_{i + 1:D3}";

                var xmlEntry = zip.CreateEntry($"{folder}/dublin_core.xml", CompressionLevel.Fastest);
                using (var s = xmlEntry.Open())
                {
                    var b = System.Text.Encoding.UTF8.GetBytes(BuildDublinCoreXml(row));
                    await s.WriteAsync(b);
                }

                if (!includeFiles || row.Files.Count == 0) continue;

                var contentLines = new List<string>();
                foreach (var file in row.Files)
                {
                    if (string.IsNullOrEmpty(file.Url)) continue;
                    var fileName = $"{row.PublicId}.{file.FileExt}";
                    try
                    {
                        Stream fileStream;
                        if (file.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            var resp = await http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead);
                            if (!resp.IsSuccessStatusCode) continue;
                            fileStream = await resp.Content.ReadAsStreamAsync();
                        }
                        else
                        {
                            var (s, _) = await _minio.GetObjectStreamAsync(file.Url);
                            fileStream = s;
                        }

                        var fileEntry = zip.CreateEntry($"{folder}/{fileName}", CompressionLevel.Fastest);
                        using (var entryStream = fileEntry.Open())
                        using (fileStream)
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                        contentLines.Add($"{fileName}\tbundle:{file.Bundle}");
                    }
                    catch { }
                }

                if (contentLines.Count > 0)
                {
                    var ce = zip.CreateEntry($"{folder}/contents", CompressionLevel.Fastest);
                    using (var cs = ce.Open())
                    {
                        var b = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", contentLines));
                        await cs.WriteAsync(b);
                    }
                }
            }
        }
        // ZipArchive disposed → central directory flushed to ms (leaveOpen: true giữ ms còn mở)

        var result = ms.ToArray();
        ms.Dispose();
        return result;
    }

    private static string BuildDublinCoreXml(DSpaceItemExportRow row)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<dublin_core schema=\"dc\">");
        foreach (var e in row.MetaEntries)
        {
            var q   = string.IsNullOrEmpty(e.Qualifier) ? "none" : e.Qualifier;
            var val = System.Security.SecurityElement.Escape(e.Value ?? "") ?? "";
            sb.AppendLine($"  <dcvalue element=\"{e.Element}\" qualifier=\"{q}\">{val}</dcvalue>");
        }
        sb.AppendLine("</dublin_core>");
        return sb.ToString();
    }

    private async Task<string?> UploadIfBase64Async(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith("data:image/")) return value;
        var comma = value.IndexOf(',');
        if (comma < 0) return value;
        var mime  = value[5..comma].Split(';')[0];
        var ext   = mime.Split('/')[1];
        var bytes = Convert.FromBase64String(value[(comma + 1)..]);
        await using var ms = new MemoryStream(bytes);
        return await _minio.UploadAsync(ms, $"img.{ext}", mime);
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";

    private bool IsMinioUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        (url.StartsWith(_minio.PublicBaseUrl, StringComparison.OrdinalIgnoreCase)
         || (url.Length >= 8 && char.IsDigit(url[0]) && url[4] == '/'));

    private static EbookItemAddRequest ParseDublinCoreXml(string xml)
    {
        var doc    = XDocument.Parse(xml);
        var values = doc.Root?.Elements("dcvalue").ToList() ?? [];

        string? Get(string element, string? qualifier = null) =>
            values.FirstOrDefault(e =>
                (string?)e.Attribute("element") == element &&
                (qualifier == null || (string?)e.Attribute("qualifier") == qualifier))
            ?.Value.Trim();

        List<string> GetAll(string element, string? qualifier = null) =>
            values
                .Where(e =>
                    (string?)e.Attribute("element") == element &&
                    (qualifier == null || (string?)e.Attribute("qualifier") == qualifier))
                .Select(e => e.Value.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

        return new EbookItemAddRequest
        {
            Title       = Get("title"),
            OtherTitles = GetAll("title", "alternative"),
            Authors     = GetAll("contributor", "author"),
            Advisors    = GetAll("contributor", "advisor"),
            Publisher   = Get("publisher"),
            PublishDate = Get("date", "issued") ?? Get("date"),
            Keywords    = GetAll("subject"),
            Abstract    = Get("description", "abstract"),
            Description = Get("description", "none"),
            DocLanguage = Get("language", "iso") ?? Get("language"),
            Language    = Get("language", "iso") ?? Get("language") ?? "vi",
            Isbn        = Get("identifier", "isbn"),
            Issn        = Get("identifier", "issn"),
            Uri         = Get("identifier", "uri"),
        };
    }
}

public class SetCoverFromPageRequest { public int Page { get; set; } }
