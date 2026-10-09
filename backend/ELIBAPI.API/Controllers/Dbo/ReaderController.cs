using System.IO.Compression;
using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Circulation/[controller]")]
public class ReaderController : GenericController<Reader, ReaderSearchRequest, ReaderRequest>
{
    private readonly IReaderRepository _readerRepo;
    private readonly IMinioService     _minio;
    private readonly ELIBAPIDbContext  _db;

    public ReaderController(IReaderRepository repo, IMinioService minio, ELIBAPIDbContext db) : base(repo)
    {
        _readerRepo = repo;
        _minio      = minio;
        _db         = db;
    }

    [HttpPost("Add")]
    [Permission("READERS", "add")]
    public override async Task<IActionResult> Add([FromBody] ReaderRequest request)
    {
        // Bạn đọc mới thuộc đơn vị của người tạo (BaseRepository.ApplyTenantOnAdd).
        var uidError = await CardUidErrorAsync(request.CardUid, GetTenantId(), null);
        if (uidError != null) return BadRequest(ApiResponse<object>.Fail(uidError));
        request.Photo = await UploadIfBase64Async(request.Photo);
        return await base.Add(request);
    }

    /// <summary>UID thẻ chip phải là chữ/số (sau khi bỏ khoảng trắng, ':' '-') và chưa gán cho bạn đọc khác cùng đơn vị (port ELIB-LRC 10-04).</summary>
    private async Task<string?> CardUidErrorAsync(string? raw, long? tenantId, Guid? excludePublicId)
    {
        var uid = CardUid.Normalize(raw);
        if (uid == null) return null;
        if (!CardUid.IsValid(uid)) return "UID thẻ chỉ gồm chữ và số, tối đa 64 ký tự.";
        return await _readerRepo.CardUidTakenAsync(uid, tenantId, excludePublicId) ? $"UID thẻ {uid} đã gán cho bạn đọc khác." : null;
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("READERS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ReaderRequest request)
    {
        var existing = await _repo.GetByPublicIdAsync(publicId);
        var oldPhoto = existing?.Photo;
        if (existing != null)
        {
            var uidError = await CardUidErrorAsync(request.CardUid, existing.TenantId, publicId);
            if (uidError != null) return BadRequest(ApiResponse<object>.Fail(uidError));
        }

        request.Photo = await UploadIfBase64Async(request.Photo);

        if (request.Photo != oldPhoto && IsMinioUrl(oldPhoto))
            _ = _minio.DeleteAsync(oldPhoto!);

        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("READERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("READERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpPut("ResetPassword/{publicId:guid}")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> ResetPassword(Guid publicId, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu không được trống"));

        var hashed = PasswordHasher.Hash(request.Password);

        try
        {
            await _readerRepo.ResetPasswordAsync(publicId, hashed, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(null!, "Đặt lại mật khẩu bạn đọc thành công"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    [HttpPut("BulkResetPassword")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> BulkResetPassword([FromBody] BulkResetPasswordRequest request)
    {
        if (request.PublicIds == null || request.PublicIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Danh sách bạn đọc trống"));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu phải có ít nhất 6 ký tự"));

        var hashed = PasswordHasher.Hash(request.Password);

        var count = await _readerRepo.BulkResetPasswordAsync(request.PublicIds, hashed, GetCurrentUserId());
        return Ok(ApiResponse<object>.Ok(new { updatedCount = count }, $"Đã đặt lại mật khẩu cho {count} bạn đọc"));
    }

    // ── Ảnh khuôn mặt bổ sung (ReaderPhoto) — dùng cùng Reader.Photo khi nhận diện khuôn mặt ──

    [HttpGet("Photos/{publicId:guid}")]
    [Permission("READERS", "view")]
    public async Task<IActionResult> GetPhotos(Guid publicId)
    {
        var tenantId = GetTenantId();
        var readerId = await _db.Readers
            .Where(r => r.PublicId == publicId && (!tenantId.HasValue || r.TenantId == tenantId))
            .Select(r => (long?)r.Id).FirstOrDefaultAsync();
        if (readerId == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy bạn đọc", 404));

        var rows = await _db.ReaderPhotos
            .Where(p => p.ReaderId == readerId && p.IsDelete != 2 && (!tenantId.HasValue || p.TenantId == tenantId))
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.PhotoUrl })
            .ToListAsync();
        var photos = rows.Select(p => new { id = p.Id, photoUrl = ResolveImageUrl(p.PhotoUrl) }).ToList();
        return Ok(ApiResponse<object>.Ok(photos));
    }

    [HttpPost("Photos/{publicId:guid}")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> AddPhoto(Guid publicId, [FromBody] AddReaderPhotoRequest request)
    {
        var tenantId = GetTenantId();
        var reader = await _db.Readers
            .Where(r => r.PublicId == publicId && (!tenantId.HasValue || r.TenantId == tenantId))
            .Select(r => new { r.Id, r.TenantId }).FirstOrDefaultAsync();
        if (reader == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy bạn đọc", 404));

        var url = await UploadIfBase64Async(request.Photo);
        if (string.IsNullOrEmpty(url)) return BadRequest(ApiResponse<object>.Fail("Thiếu ảnh"));

        var photo = new ReaderPhoto
        {
            PublicId = Guid.NewGuid(), ReaderId = reader.Id, TenantId = reader.TenantId,
            PhotoUrl = url, IsDelete = 1, CreatedRowDate = DateTime.Now
        };
        _db.ReaderPhotos.Add(photo);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { id = photo.Id, photoUrl = ResolveImageUrl(url) }));
    }

    [HttpDelete("Photos/{id:long}")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> DeletePhoto(long id)
    {
        var tenantId = GetTenantId();
        var photo = await _db.ReaderPhotos
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDelete != 2 && (!tenantId.HasValue || p.TenantId == tenantId));
        if (photo == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy ảnh", 404));

        photo.IsDelete = 2;
        await _db.SaveChangesAsync();

        if (IsMinioUrl(photo.PhotoUrl)) _ = _minio.DeleteAsync(photo.PhotoUrl!);

        return Ok(ApiResponse<object>.Ok(null!, "Đã xoá ảnh"));
    }

    [HttpPost("Lock/{publicId:guid}")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> Lock(Guid publicId, [FromBody] LockReaderRequest request)
    {
        try
        {
            await _readerRepo.LockAsync(publicId, request.Reason, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(null!, "Đã khoá thẻ bạn đọc"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    [HttpGet("{id:long}")]
    [Permission("READERS", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<Reader>.Fail(Localizer["NotFound"], 404));
        entity.Photo = ResolveImageUrl(entity.Photo);
        return Ok(ApiResponse<Reader>.Ok(entity));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("READERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        if (entity == null) return NotFound(ApiResponse<Reader>.Fail(Localizer["NotFound"], 404));
        entity.Photo = ResolveImageUrl(entity.Photo);
        return Ok(ApiResponse<Reader>.Ok(entity));
    }

    [HttpGet("CheckExist")]
    [Permission("READERS", "view")]
    public async Task<IActionResult> CheckExist([FromQuery] string cardno, [FromQuery] Guid? excludePublicId = null)
    {
        if (string.IsNullOrWhiteSpace(cardno))
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidRequest"], 400));
        var exists = await _readerRepo.CheckCardnoExistsAsync(cardno, excludePublicId);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpPost("Search")]
    [Permission("READERS", "view")]
    public override async Task<IActionResult> Search([FromBody] ReaderSearchRequest request)
    {
        var result = await _readerRepo.SearchWithNamesAsync(request);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("READERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ReaderSearchRequest request)
    {
        var result = await _readerRepo.SearchAllWithNamesAsync(request);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPut("BatchUpdate")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> BatchUpdate([FromBody] ReaderBatchUpdateRequest request)
    {
        var validActions = new[] { "changeclass", "changecourse", "changereadertype", "changeissuedate", "changeexpiredate", "changestatus", "changepassword" };
        if (string.IsNullOrWhiteSpace(request.Action) || !validActions.Contains(request.Action.ToLower()))
            return BadRequest(ApiResponse<object>.Fail("Action không hợp lệ. Các giá trị hợp lệ: changeClass, changeCourse, changeReaderType, changeIssueDate, changeExpireDate, changeStatus, changePassword", 400));

        if (request.Action.ToLower() == "changepassword")
        {
            if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Length < 6)
                return BadRequest(ApiResponse<object>.Fail("Mật khẩu phải có ít nhất 6 ký tự"));

            request.Value = PasswordHasher.Hash(request.Value);
        }

        var count = await _readerRepo.BatchUpdateAsync(request);
        return Ok(ApiResponse<object>.Ok(new { updatedCount = count }, $"Đã cập nhật {count} bản ghi"));
    }

    [HttpPut("BulkUpdate")]
    [Permission("READERS", "edit")]
    public async Task<IActionResult> BulkUpdate([FromBody] ReaderBulkUpdateRequest request)
    {
        if ((request.PublicIds == null || request.PublicIds.Count == 0) && request.Filter == null)
            return BadRequest(ApiResponse<object>.Fail(Localizer["InvalidRequest"], 400));

        var count = await _readerRepo.BulkUpdateAsync(request);
        return Ok(ApiResponse<object>.Ok(new { UpdatedCount = count }, Localizer["UpdateSuccess"]));
    }

    [HttpGet("GetExportFields")]
    [Permission("READERS", "view")]
    public IActionResult GetExportFields()
    {
        var fieldsdotne = ExportableFields.Select(f => new { code = f.Code, name = f.Name });
        return Ok(ApiResponse<object>.Ok(fieldsdotne));
    }

    [HttpPost("Export")]
    [Permission("READERS", "view")]
    public async Task<IActionResult> Export([FromBody] ReaderExportRequest request, [FromServices] ISystemParameterService sysParam)
    {
        var readers = await _readerRepo.SearchAllAsync(request);

        var classIds  = readers.Where(r => r.ClassId.HasValue) .Select(r => r.ClassId!.Value);
        var courseIds = readers.Where(r => r.CourseId.HasValue).Select(r => r.CourseId!.Value);
        var orgIds    = readers.Where(r => r.OrgId.HasValue)   .Select(r => r.OrgId!.Value);

        var classMap  = await _readerRepo.GetClassMapAsync(classIds);
        var courseMap = await _readerRepo.GetCourseMapAsync(courseIds);
        var orgMap    = await _readerRepo.GetOrgMapAsync(orgIds);

        var selected = (request.Fields?.Count > 0
            ? ExportableFields.Where(f => request.Fields.Any(rf => rf.Equals(f.Code, StringComparison.OrdinalIgnoreCase)))
            : ExportableFields.AsEnumerable()).ToList();

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Bạn đọc");

        var lastColumn = selected.Count + 1;
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "DANH SÁCH BẠN ĐỌC", lastColumn);

        ws.Cell(startRow, 1).Value = "STT";
        for (int i = 0; i < selected.Count; i++)
            ws.Cell(startRow, i + 2).Value = selected[i].Name;

        var headerRow = ws.Row(startRow);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

        for (int r = 0; r < readers.Count; r++)
        {
            ws.Cell(startRow + 1 + r, 1).Value = r + 1;
            for (int c = 0; c < selected.Count; c++)
                ws.Cell(startRow + 1 + r, c + 2).Value = GetFieldValue(readers[r], selected[c].Code, classMap, courseMap, orgMap);
        }

        ExcelReportHelper.ApplyTableBorders(ws, startRow, startRow + readers.Count, 1, lastColumn);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "danh-sach-ban-doc.xlsx");
    }

    /// <summary>Đọc dòng tiêu đề của file Excel để người dùng ghép cột ↔ trường (port ELIB-LRC 09-15, Đợt 20).</summary>
    [HttpPost("Import/Columns")]
    [Permission("READERS", "add")]
    [Consumes("multipart/form-data")]
    public IActionResult ImportColumns(IFormFile file, [FromForm] int headerRow = 1)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"], 400));
        try
        {
            using var stream = file.OpenReadStream();
            return Ok(ApiResponse<object>.Ok(ReaderImportWorkbook.Inspect(stream, headerRow)));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message, 400)); }
        catch (Exception) { return BadRequest(ApiResponse<object>.Fail("File Excel bị lỗi hoặc không đúng định dạng.", 400)); }
    }

    [HttpPost("Import")]
    [Permission("READERS", "add")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file,
        [FromForm] string? overwrite    = "false",
        [FromForm] string? portalId     = null,
        [FromForm] string? language     = null,
        [FromForm] long?   readerTypeId = null,
        [FromForm] bool    classByCode  = false,
        [FromForm] bool    courseByCode = false,
        [FromForm] bool    orgByCode    = false,
        [FromForm] bool    background   = false,
        [FromForm] bool    autoCreateRefs = false,
        [FromForm] string? mapping      = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"], 400));

        bool shouldOverwrite = overwrite?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        // Đợt 20: ghép cột theo lựa chọn người dùng (dòng tiêu đề, cột ↔ trường, "Họ và Tên" một cột). Không gửi
        // mapping → đọc theo bố cục cột cố định cũ (B=Số thẻ … P=Số căn cước), tương thích file mẫu hiện có.
        ReaderImportMapping? importMapping = null;
        if (!string.IsNullOrWhiteSpace(mapping))
        {
            try
            {
                importMapping = System.Text.Json.JsonSerializer.Deserialize<ReaderImportMapping>(mapping,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(ApiResponse<object>.Fail("Ánh xạ cột không hợp lệ.", 400));
            }
        }

        List<ReaderImportRow> rows;
        try
        {
            using var stream = file.OpenReadStream();
            rows = ReaderImportWorkbook.Read(stream, importMapping);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
        catch (Exception)
        {
            return BadRequest(ApiResponse<object>.Fail("File Excel bị lỗi hoặc không đúng định dạng.", 400));
        }

        if (rows.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["NoDataToImport"], 400));

        // Xử lý nền (Đợt 10) — không chặn request HTTP, cho phép nhập file lớn hơn nhiều (10.000 so với
        // 1.000 dòng đường đồng bộ). Đường đồng bộ (background=false, mặc định) giữ NGUYÊN hành vi cũ.
        if (background)
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            if (!config.GetValue<bool>("AdminTasks:Enabled"))
                return BadRequest(ApiResponse<object>.Fail("Xử lý nền chưa được bật trên hệ thống.", 400));

            var tasks = HttpContext.RequestServices.GetRequiredService<AdminTaskService>();
            try
            {
                var task = await tasks.Enqueue(GetCurrentUserId(), GetTenantId(), new AdminTaskRequest
                {
                    Kind = "reader-import",
                    Preview = true,
                    Rows = rows,
                    PortalId = portalId,
                    Language = language,
                    Overwrite = shouldOverwrite,
                    ReaderTypeId = readerTypeId,
                    ClassByCode = classByCode,
                    CourseByCode = courseByCode,
                    OrgByCode = orgByCode,
                    AutoCreateRefs = autoCreateRefs,
                });
                return Accepted(ApiResponse<object>.Ok(AdminTaskService.View(task), "Đã tạo tác vụ xem trước."));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
            }
        }

        try
        {
            var result = await _readerRepo.ImportAsync(rows, portalId, language, shouldOverwrite, readerTypeId, classByCode, courseByCode, orgByCode,
                autoCreateRefs: autoCreateRefs);
            return Ok(ApiResponse<object>.Ok(
                new { imported = result.SuccessCount, skipped = result.SkippedCount, failed = result.FailedCount, errors = result.Errors,
                      createdRefs = result.CreatedRefs },
                $"Import hoàn tất: {result.SuccessCount} thành công, {result.SkippedCount} bỏ qua, {result.FailedCount} lỗi"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ── Upload ảnh hàng loạt theo ZIP: tên file (bỏ phần mở rộng) = Số thẻ (Cardno) ──
    // Khác với Photos/{publicId} (Task 1: ảnh khuôn mặt bổ sung), thao tác này CẬP NHẬT ảnh
    // chính (Reader.Photo) — refresh ảnh đại diện hàng loạt từ 1 lô ảnh đã đặt tên theo mã thẻ.
    private static readonly HashSet<string> PhotoZipExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp" };

    [HttpPost("UploadPhotosZip")]
    [Permission("READERS", "edit")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhotosZip(IFormFile zipFile)
    {
        if (zipFile == null || zipFile.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(Localizer["FileNotFound"], 400));

        var tenantId = GetTenantId();
        var results = new List<object>();
        int matched = 0, notFound = 0, failed = 0;

        using var zipStream = zipFile.OpenReadStream();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var ext = Path.GetExtension(entry.Name);
            if (string.IsNullOrEmpty(entry.Name) || !PhotoZipExtensions.Contains(ext))
                continue; // bỏ qua thư mục / file không phải ảnh (vd. __MACOSX, .DS_Store)

            var cardno = Path.GetFileNameWithoutExtension(entry.Name).Trim();

            // Số thẻ chỉ được khớp với bạn đọc CÙNG tenant với người đang upload.
            var reader = await _db.Readers.FirstOrDefaultAsync(r =>
                r.Cardno != null && r.Cardno.Trim().ToLower() == cardno.ToLower() &&
                r.IsDelete != 2 && (!tenantId.HasValue || r.TenantId == tenantId));

            if (reader == null)
            {
                notFound++;
                results.Add(new { fileName = entry.Name, cardNo = cardno, matched = false, error = "Không tìm thấy bạn đọc có mã thẻ này" });
                continue;
            }

            try
            {
                var oldPhoto = reader.Photo;
                await using var entryStream = entry.Open();
                await using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms);
                ms.Position = 0;
                var url = await _minio.UploadAsync(ms, entry.Name, MimeFromExtension(ext));

                reader.Photo          = url;
                reader.UpdateRowBy    = GetCurrentUserId();
                reader.UpdatedRowDate = DateTime.Now;
                await _db.SaveChangesAsync();

                if (IsMinioUrl(oldPhoto)) _ = _minio.DeleteAsync(oldPhoto!);

                matched++;
                results.Add(new { fileName = entry.Name, cardNo = cardno, matched = true, error = (string?)null });
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new { fileName = entry.Name, cardNo = cardno, matched = false, error = ex.Message });
            }
        }

        return Ok(ApiResponse<object>.Ok(
            new { matched, notFound, failed, results },
            $"Upload hoàn tất: {matched} khớp thành công, {notFound} không tìm thấy bạn đọc, {failed} lỗi"));
    }

    private static string MimeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"             => "image/png",
        ".webp"            => "image/webp",
        _                  => "application/octet-stream"
    };

    private static readonly List<(string Code, string Name)> ExportableFields =
    [
        ("Cardno",     "Số thẻ"),
        ("LastName",   "Họ"),
        ("FirstName",  "Tên"),
        ("Sex",        "Giới tính"),
        ("BirthDate",  "Ngày sinh"),
        ("Email",      "Email"),
        ("Phone",      "Phone"),
        ("Address",    "Địa chỉ"),
        ("IssueDate",  "Ngày cấp"),
        ("ExpireDate", "Ngày hết hạn"),
        ("ClassName",  "Lớp"),
        ("CourseName", "Khóa"),
        ("OrgName",    "Đơn vị"),
        ("Status",     "Trạng thái"),
    ];

    private static string GetFieldValue(Reader r, string code,
        Dictionary<long, string> classMap,
        Dictionary<long, string> courseMap,
        Dictionary<long, string> orgMap) => code switch
    {
        "Cardno"     => r.Cardno    ?? "",
        "LastName"   => r.LastName  ?? "",
        "FirstName"  => r.FirstName ?? "",
        "Sex"        => r.Sex == 1 ? "Nam" : r.Sex == 2 ? "Nữ" : "",
        "BirthDate"  => r.BirthDate?.ToString("dd/MM/yyyy")  ?? "",
        "Email"      => r.Email     ?? "",
        "Phone"      => r.Phone     ?? "",
        "Address"    => r.Address   ?? "",
        "IssueDate"  => r.IssueDate?.ToString("dd/MM/yyyy")  ?? "",
        "ExpireDate" => r.ExpireDate?.ToString("dd/MM/yyyy") ?? "",
        "ClassName"  => r.ClassId.HasValue  ? classMap .GetValueOrDefault(r.ClassId.Value,  "") : "",
        "CourseName" => r.CourseId.HasValue ? courseMap.GetValueOrDefault(r.CourseId.Value, "") : "",
        "OrgName"    => r.OrgId.HasValue    ? orgMap   .GetValueOrDefault(r.OrgId.Value,    "") : "",
        "Status"     => r.Status == 2 ? "Hoạt động" : "Ẩn",
        _            => ""
    };

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";

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

    private bool IsMinioUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        (url.StartsWith(_minio.PublicBaseUrl, StringComparison.OrdinalIgnoreCase)
         || (url.Length >= 8 && char.IsDigit(url[0]) && url[4] == '/'));
}

public class AddReaderPhotoRequest
{
    /// Base64 (data:image/...) hoặc URL đã upload sẵn.
    public string? Photo { get; set; }
}

