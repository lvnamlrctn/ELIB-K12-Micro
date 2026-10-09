using ClosedXML.Excel;
using ELIBAPI.API.Filters;
using ELIBAPI.API.Helpers;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Receiption/Check")]
[Authorize]
public class ReceiptionCheckController(ELIBAPIDbContext db, ISystemParameterService sysParam) : BaseApiController
{
    [HttpPost("Status")]
    [Permission("CHECK_IN_OUT", "view")]
    public async Task<IActionResult> Status([FromBody] CheckStatusRequest r)
    {
        // Port ELIB-LRC 10-04: thẻ rỗng trước đây gây 500 (NullReferenceException).
        if (string.IsNullOrWhiteSpace(r.CardNo)) return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập số thẻ"));
        var reader = await ReaderCards.FindAsync(db, r.CardNo, GetTenantId());
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        var readerTypeName = reader.ReaderTypeId.HasValue
            ? await db.ReaderTypes.Where(x => x.Id == reader.ReaderTypeId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;
        var className = reader.ClassId.HasValue
            ? await db.Classes.Where(x => x.Id == reader.ClassId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        // Lượt vào đang mở MỚI NHẤT (trước đây lấy 1 lượt bất kỳ).
        var openCheckIn = await OpenCheckInAsync(reader.Id);
        var checkInCount = await db.CheckOuts.CountAsync(x => x.ReaderId == reader.Id && x.IsDelete != 2);
        // Tài liệu đang mượn theo PrintLoans.Open — trước đây tính cả phiếu đã trả kiểu cũ (Status "1"/"O" nhưng đã có BookIn).
        var currentLoans = await ReaderCards.CurrentLoansAsync(db, reader);

        return Ok(ApiResponse<object>.Ok(new
        {
            readerId    = reader.Id,
            cardNo      = reader.Cardno,
            fullName    = $"{reader.FirstName} {reader.LastName}".Trim(),
            readerType  = readerTypeName,
            className,
            avatar      = reader.Photo,
            balance     = reader.Blane,
            status      = reader.Status,
            issueDate   = reader.IssueDate?.ToString("yyyy-MM-dd"),
            expireDate  = reader.ExpireDate?.ToString("yyyy-MM-dd"),
            checkedIn   = openCheckIn != null,
            checkInId   = openCheckIn?.Id,
            checkInTime = openCheckIn?.CheckInTime,
            checkInCount,
            currentLoans
        }));
    }

    private Task<CheckIn?> OpenCheckInAsync(long readerId) =>
        db.CheckIns.Where(x => x.Readerid == readerId && x.IsDelete != 2)
            .OrderByDescending(x => x.CheckInTime).ThenByDescending(x => x.Id).FirstOrDefaultAsync();

    [HttpPost("CheckIn")]
    [Permission("CHECK_IN_OUT", "add")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInActionRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        if (string.IsNullOrWhiteSpace(r.CardNo)) return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập số thẻ"));
        var reader = await ReaderCards.FindAsync(db, r.CardNo, GetTenantId());
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        // Port ELIB-LRC 10-04: quét 2 lần (đầu đọc lặp, bấm lại) trước đây tạo nhiều lượt vào đang mở — nay trả lại lượt đang mở.
        var open = await OpenCheckInAsync(reader.Id);
        if (open != null) return Ok(ApiResponse<CheckIn>.Ok(open));

        var userId = GetCurrentUserId();
        var entity = new CheckIn
        {
            Readerid       = reader.Id,
            UserId         = userId,
            CheckInTime    = DateTime.Now,
            StoreId        = r.CircPlaceId,
            CircPlaceId    = r.CircPlaceId,
            TenantId       = reader.TenantId, // đơn vị của bạn đọc (kể cả khi tài khoản hệ thống quét)
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        };
        db.CheckIns.Add(entity);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<CheckIn>.Ok(entity));
    }

    [HttpPost("CheckOut")]
    [Permission("CHECK_IN_OUT", "edit")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutActionRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(x => x.Id == r.CheckInId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (checkIn == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bản ghi vào cửa"));

        var userId = GetCurrentUserId();
        var entity = new CheckOut
        {
            ReaderId       = checkIn.Readerid,
            UserId         = userId,
            CheckInTime    = checkIn.CheckInTime,
            CheckOutTime   = DateTime.Now,
            StoreId        = r.CircPlaceId,
            CircPlaceId    = r.CircPlaceId,
            CheckInId      = checkIn.Id,
            TenantId       = checkIn.TenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        };
        db.CheckOuts.Add(entity);

        checkIn.IsDelete        = 2;
        checkIn.UpdateRowBy     = userId;
        checkIn.UpdatedRowDate  = DateTime.Now;

        await db.SaveChangesAsync();
        return Ok(ApiResponse<CheckOut>.Ok(entity));
    }

    [HttpPost("SearchHistory")]
    [Permission("CHECK_IN_OUT", "view")]
    public async Task<IActionResult> SearchHistory([FromBody] CheckHistorySearchRequest r)
    {
        var (items, total) = await BuildCheckHistoryRows(r, r.PageIndex ?? 1, r.PageSize ?? 20);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }

    [HttpPost("ExportHistory")]
    [Permission("CHECK_IN_OUT", "view")]
    public async Task<IActionResult> ExportHistory([FromBody] CheckHistorySearchRequest r)
    {
        var (items, _) = await BuildCheckHistoryRows(r, null, null);

        var parentLibrary = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("ParentLibrary"));
        var libraryName   = ExcelReportHelper.StripHtml(await sysParam.GetValueAsync("LibraryName"));

        using var wb = new XLWorkbook();
        ExcelReportHelper.ApplyDefaultFont(wb);
        var ws = wb.Worksheets.Add("Lịch sử vào ra");
        var startRow = ExcelReportHelper.WriteLetterhead(ws, parentLibrary, libraryName, "LỊCH SỬ VÀO RA THƯ VIỆN", 5);
        ws.Cell(startRow, 1).Value = "Số thẻ";
        ws.Cell(startRow, 2).Value = "Họ tên";
        ws.Cell(startRow, 3).Value = "Loại bạn đọc";
        ws.Cell(startRow, 4).Value = "Giờ vào";
        ws.Cell(startRow, 5).Value = "Giờ ra";

        int row = startRow + 1;
        foreach (var x in items)
        {
            ws.Cell(row, 1).Value = x.cardNo ?? "";
            ws.Cell(row, 2).Value = x.fullName ?? "";
            ws.Cell(row, 3).Value = x.readerType ?? "";
            ws.Cell(row, 4).Value = x.checkInTime?.ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cell(row, 5).Value = x.checkOutTime?.ToString("dd/MM/yyyy HH:mm") ?? "(đang ở trong)";
            row++;
        }
        ExcelReportHelper.ApplyTableBorders(ws, startRow, row - 1, 1, 5);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "checkin-history.xlsx");
    }

    /// <summary>Port ELIB-LRC 10-04:
    /// <list type="bullet">
    /// <item>gồm cả lượt đang ở trong (<see cref="ReaderVisits"/>) — trước đây chỉ lượt đã quét ra, người chưa quét ra không
    /// xuất hiện dù thống kê vẫn đếm 1 lượt vào; cột Ra để trống = "Đang ở trong";</item>
    /// <item>ngày "đến" không kèm giờ tính trọn ngày (<see cref="DateRange"/>);</item>
    /// <item>lọc theo điểm lưu thông (<c>circPlaceId</c>, vẫn nhận tên cũ <c>storeId</c>) — dữ liệu vào/ra lưu mã điểm lưu thông.</item>
    /// </list></summary>
    private async Task<(List<CheckHistoryRow> items, int total)> BuildCheckHistoryRows(CheckHistorySearchRequest r, int? pageIndex, int? pageSize)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        var query = ReaderVisits.Query(db)
            .Where(x => scope.All || x.TenantId == scope.TenantId || (scope.IncludeShared && x.TenantId == null));

        var readers = db.Readers.AsQueryable();
        var byReader = false;
        if (!string.IsNullOrWhiteSpace(r.CardNumber))
        {
            var cn = r.CardNumber.Trim().ToLower(); byReader = true;
            readers = readers.Where(x => x.Cardno != null && x.Cardno.ToLower().Contains(cn));
        }
        if (!string.IsNullOrWhiteSpace(r.FirstName))
        {
            var kw = r.FirstName.Trim().ToLower(); byReader = true;
            readers = readers.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(kw));
        }
        if (r.ReaderTypeId > 0) { byReader = true; readers = readers.Where(x => x.ReaderTypeId == r.ReaderTypeId); }
        if (byReader)
        {
            var readerIds = readers.Select(x => (long?)x.Id);
            query = query.Where(x => readerIds.Contains(x.ReaderId));
        }
        var place = r.CircPlaceId ?? r.StoreId;
        if (place > 0) query = query.Where(x => x.StoreId == place);
        var (from, toExclusive, toInclusive) = DateRange.Parse(r.CheckInTimeFrom, r.CheckInTimeTo);
        if (from is DateTime f) query = query.Where(x => x.CheckInTime >= f);
        if (toExclusive is DateTime te) query = query.Where(x => x.CheckInTime < te);
        if (toInclusive is DateTime ti) query = query.Where(x => x.CheckInTime <= ti);

        var total = await query.CountAsync();
        var ordered = query.OrderByDescending(x => x.CheckInTime).ThenByDescending(x => x.Id);
        var size = pageSize is int ps ? Math.Clamp(ps, 1, 200) : 5000;
        var page = pageSize is null ? 1 : Math.Max(pageIndex ?? 1, 1);
        var rows = await ordered.Skip((page - 1) * size).Take(size).ToListAsync();

        var ids = rows.Where(x => x.ReaderId != null).Select(x => x.ReaderId!.Value).Distinct().ToList();
        var people = await db.Readers.Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Cardno, x.FirstName, x.LastName, x.ReaderTypeId }).ToDictionaryAsync(x => x.Id);
        var typeIds = people.Values.Where(x => x.ReaderTypeId != null).Select(x => x.ReaderTypeId!.Value).Distinct().ToList();
        var types = await db.ReaderTypes.Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, rows.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value));

        var items = rows.Select(x =>
        {
            var p = x.ReaderId is long rid ? people.GetValueOrDefault(rid) : null;
            return new CheckHistoryRow(x.Id, x.PublicId, p?.Cardno, p != null ? $"{p.FirstName} {p.LastName}".Trim() : null,
                p?.ReaderTypeId is long t ? types.GetValueOrDefault(t) : null, x.CheckInTime, x.CheckOutTime, x.StoreId,
                x.TenantId is long tid ? tenantNames.GetValueOrDefault(tid) : null);
        }).ToList();
        return (items, total);
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CHECK_IN_OUT", "delete")]
    public async Task<IActionResult> Delete(Guid publicId)
    {
        // Lịch sử nay gồm cả lượt đang ở trong (CheckIn) — xoá được cả lượt đó.
        var tenantId = GetTenantId();
        var userId = GetCurrentUserId();
        var visit = await db.CheckOuts.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (visit != null) { visit.IsDelete = 2; visit.UpdateRowBy = userId; visit.UpdatedRowDate = DateTime.Now; }
        else
        {
            var inside = await db.CheckIns.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2
                && (!tenantId.HasValue || x.TenantId == tenantId));
            if (inside == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bản ghi"));
            inside.IsDelete = 2; inside.UpdateRowBy = userId; inside.UpdatedRowDate = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã xóa"));
    }
}

public class CheckStatusRequest { public string? CardNo { get; set; } public long? CircPlaceId { get; set; } }
public class CheckInActionRequest { public string? CardNo { get; set; } public long? CircPlaceId { get; set; } }
public class CheckOutActionRequest { public long CheckInId { get; set; } public long? CircPlaceId { get; set; } }
public class CheckHistorySearchRequest
{
    public string?   CardNumber      { get; set; }
    public string?   FirstName       { get; set; }
    /// <summary>Điểm lưu thông (CheckIn/CheckOut.StoreId lưu mã điểm lưu thông). Nhận cả tên cũ <c>storeId</c>.</summary>
    public long?     CircPlaceId     { get; set; }
    public long?     StoreId         { get; set; }
    public long?     ReaderTypeId    { get; set; }
    public string?   CheckInTimeFrom { get; set; }
    public string?   CheckInTimeTo   { get; set; }
    public int?      PageIndex       { get; set; }
    public int?      PageSize        { get; set; }
    public Guid?     TenantId        { get; set; }
}

public record CheckHistoryRow(long id, Guid publicId, string? cardNo, string? fullName, string? readerType, DateTime? checkInTime, DateTime? checkOutTime, long? storeId, string? tenantName);
