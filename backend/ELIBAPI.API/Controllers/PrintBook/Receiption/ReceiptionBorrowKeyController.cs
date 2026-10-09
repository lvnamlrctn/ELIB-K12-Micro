using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Receiption/BorrowKey")]
[Authorize]
public class ReceiptionBorrowKeyController(ELIBAPIDbContext db) : BaseApiController
{
    [HttpPost("Snapshot")]
    [Permission("BORROW_KEYS", "view")]
    public async Task<IActionResult> Snapshot([FromBody] KeySnapshotRequest r)
    {
        // Port ELIB-LRC 10-04: thẻ rỗng trước đây gây 500.
        if (string.IsNullOrWhiteSpace(r.CardNo)) return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập số thẻ"));
        var reader = await ReaderCards.FindAsync(db, r.CardNo, GetTenantId());
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        var readerTypeName = reader.ReaderTypeId.HasValue
            ? await db.ReaderTypes.Where(x => x.Id == reader.ReaderTypeId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;
        var className = reader.ClassId.HasValue
            ? await db.Classes.Where(x => x.Id == reader.ClassId).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        var activeKeys = await db.KeyOuts
            .Where(x => x.Readerid == reader.Id && x.IsDelete != 2)
            .OrderBy(x => x.BorrowDate)
            .ToListAsync();
        var compartmentIds = activeKeys.Where(x => x.Keyid.HasValue).Select(x => x.Keyid!.Value).Distinct().ToList();
        var compartments = await db.CabinetCompartments.Where(x => compartmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x);
        var cabinetIds = compartments.Values.Select(x => x.CabinetId).Distinct().ToList();
        var cabinets = await db.Cabinets.Where(x => cabinetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x);

        var currentKeys = activeKeys.Select(k =>
        {
            CabinetCompartment? compartment = k.Keyid.HasValue && compartments.TryGetValue(k.Keyid.Value, out var c) ? c : null;
            Cabinet? cabinet = compartment != null && cabinets.TryGetValue(compartment.CabinetId, out var cab) ? cab : null;
            return new
            {
                id             = k.Id,
                cabinetId      = compartment?.Id,
                cabinetName    = cabinet != null ? $"{cabinet.Name} - {compartment!.Name}" : null,
                cabinetBarcode = compartment?.Code,
                borrowDate     = k.BorrowDate
            };
        }).ToList();

        // Tài liệu đang mượn theo PrintLoans.Open (dùng chung với vào/ra thư viện) — trước đây tính cả phiếu đã trả kiểu cũ.
        var currentLoans = await ReaderCards.CurrentLoansAsync(db, reader);

        return Ok(ApiResponse<object>.Ok(new
        {
            readerId   = reader.Id,
            cardNo     = reader.Cardno,
            fullName   = $"{reader.FirstName} {reader.LastName}".Trim(),
            readerType = readerTypeName,
            className,
            avatar     = reader.Photo,
            balance    = reader.Blane,
            status     = reader.Status,
            issueDate  = reader.IssueDate?.ToString("yyyy-MM-dd"),
            expireDate = reader.ExpireDate?.ToString("yyyy-MM-dd"),
            currentKeys,
            currentLoans
        }));
    }

    [HttpPost("Borrow")]
    [Permission("BORROW_KEYS", "add")]
    public async Task<IActionResult> Borrow([FromBody] BorrowKeyRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        var reader = await db.Readers.FirstOrDefaultAsync(x => x.Id == r.ReaderId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (reader == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bạn đọc"));

        // Port ELIB-LRC 10-04:
        //  - mã ngăn không tồn tại (gõ sai, quét nhầm) trước đây vẫn tạo lượt mượn "trống" (Keyid null) và báo thành công;
        //  - một ngăn có thể đồng thời được ghi cho 2 bạn đọc (chỉ kiểm tra khoá của bạn đọc đang tra);
        //  - quét lặp tạo lượt mượn trùng — nay trả lại lượt hiện có.
        // Tenant: ngăn tủ và lượt mượn cùng đơn vị với bạn đọc.
        var code = (r.CompartmentCode ?? "").Trim();
        if (code == "") return BadRequest(ApiResponse<string>.Fail("Vui lòng quét mã ngăn tủ"));
        var compartment = await db.CabinetCompartments.FirstOrDefaultAsync(x => x.Code == code && x.IsDelete != 2
            && x.TenantId == reader.TenantId);
        if (compartment == null) return NotFound(ApiResponse<string>.Fail($"Không tìm thấy ngăn tủ có mã {code}"));

        var holder = await db.KeyOuts.Where(x => x.Keyid == compartment.Id && x.IsDelete != 2)
            .OrderByDescending(x => x.BorrowDate).FirstOrDefaultAsync();
        if (holder != null)
        {
            if (holder.Readerid == reader.Id) return Ok(ApiResponse<KeyOut>.Ok(holder));
            var card = await db.Readers.Where(x => x.Id == holder.Readerid).Select(x => x.Cardno).FirstOrDefaultAsync();
            return BadRequest(ApiResponse<string>.Fail($"Ngăn tủ {code} đang được mượn bởi thẻ {card ?? "khác"}, cần trả trước khi cho mượn lại"));
        }

        var userId = GetCurrentUserId();
        var entity = new KeyOut
        {
            Keyid          = compartment.Id,
            Readerid       = reader.Id,
            BorrowDate     = DateTime.Now,
            Userid         = userId,
            CircPlaceId    = r.CircPlaceId,
            Note           = r.Note,
            TenantId       = reader.TenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        };
        db.KeyOuts.Add(entity);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<KeyOut>.Ok(entity));
    }

    [HttpPost("Return")]
    [Permission("BORROW_KEYS", "edit")]
    public async Task<IActionResult> Return([FromBody] ReturnKeyRequest r)
    {
        if (r.CircPlaceId is null || r.CircPlaceId <= 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn điểm lưu thông"));

        var tenantId = GetTenantId();
        KeyOut? keyOut = null;
        if (r.KeyOutId.HasValue)
            keyOut = await db.KeyOuts.FirstOrDefaultAsync(x => x.Id == r.KeyOutId && x.IsDelete != 2
                && (!tenantId.HasValue || x.TenantId == tenantId));
        else if (!string.IsNullOrWhiteSpace(r.CompartmentCode))
        {
            var code = r.CompartmentCode.Trim();
            var compartment = await db.CabinetCompartments.FirstOrDefaultAsync(x => x.Code == code && x.IsDelete != 2
                && (!tenantId.HasValue || x.TenantId == tenantId));
            if (compartment != null)
            {
                // Port ELIB-LRC 10-04: ưu tiên lượt của thẻ đang tra (trước đây lấy 1 lượt bất kỳ của ngăn).
                var open = db.KeyOuts.Where(x => x.Keyid == compartment.Id && x.IsDelete != 2);
                var reader = await ReaderCards.FindAsync(db, r.CardNo, tenantId);
                keyOut = (reader != null ? await open.FirstOrDefaultAsync(x => x.Readerid == reader.Id) : null)
                         ?? await open.OrderByDescending(x => x.BorrowDate).FirstOrDefaultAsync();
            }
        }

        if (keyOut == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy bản ghi mượn chìa khóa"));

        var userId = GetCurrentUserId();

        // Lưu lịch sử trả vào KeyIn
        var keyIn = new KeyIn
        {
            Keyid          = keyOut.Keyid,
            Readerid       = keyOut.Readerid,
            BorrowDate     = keyOut.BorrowDate,
            ReturnDate     = DateTime.Now,
            Userid         = userId,
            CircPlaceId    = r.CircPlaceId,
            Note           = keyOut.Note, // giữ ghi chú lúc mượn
            TenantId       = keyOut.TenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        };
        db.KeyIns.Add(keyIn);

        keyOut.IsDelete       = 2;
        keyOut.UpdateRowBy    = userId;
        keyOut.UpdatedRowDate = DateTime.Now;

        await db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Trả chìa khóa thành công"));
    }

    /// <summary>Port ELIB-LRC 10-04: trước đây bỏ qua mọi bộ lọc (số thẻ, tên, từ ngày, đến ngày) và không trả số thẻ, họ tên,
    /// trạng thái nên các cột đó luôn trống. Lượt đang mượn và lượt đã trả dùng chung bộ lọc; ngày "đến" tính trọn ngày.</summary>
    [HttpPost("Search")]
    [Permission("BORROW_KEYS", "view")]
    public async Task<IActionResult> Search([FromBody] KeyBorrowSearchRequest r)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        var returned = r.OnlyReturned == true;
        var rows = returned
            ? db.KeyIns.Where(x => x.IsDelete != 2).Select(x => new { x.Id, x.Keyid, x.Readerid, x.BorrowDate, ReturnDate = x.ReturnDate, x.TenantId })
            : db.KeyOuts.Where(x => x.IsDelete != 2).Select(x => new { x.Id, x.Keyid, x.Readerid, x.BorrowDate, ReturnDate = (DateTime?)null, x.TenantId });
        rows = rows.Where(x => scope.All || x.TenantId == scope.TenantId || (scope.IncludeShared && x.TenantId == null));

        var readers = db.Readers.AsQueryable();
        var byReader = false;
        var cardFilter = r.CardNumber ?? r.CardNo;
        if (!string.IsNullOrWhiteSpace(cardFilter))
        {
            var cn = cardFilter.Trim().ToLower(); byReader = true;
            readers = readers.Where(x => x.Cardno != null && x.Cardno.ToLower().Contains(cn));
        }
        if (!string.IsNullOrWhiteSpace(r.FirstName))
        {
            var kw = r.FirstName.Trim().ToLower(); byReader = true;
            readers = readers.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(kw));
        }
        if (byReader) { var ids = readers.Select(x => (long?)x.Id); rows = rows.Where(x => ids.Contains(x.Readerid)); }
        var (from, toExclusive, toInclusive) = DateRange.Parse(r.BorrowDateFrom, r.BorrowDateTo);
        if (from is DateTime a) rows = rows.Where(x => x.BorrowDate >= a);
        if (toExclusive is DateTime b) rows = rows.Where(x => x.BorrowDate < b);
        if (toInclusive is DateTime c) rows = rows.Where(x => x.BorrowDate <= c);

        var total = await rows.CountAsync();
        var size = Math.Clamp(r.PageSize ?? 20, 1, 200);
        var page = Math.Max(r.PageIndex ?? 1, 1);
        var ordered = returned ? rows.OrderByDescending(x => x.ReturnDate) : rows.OrderByDescending(x => x.BorrowDate);
        var list = await ordered.ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync();

        var keyIds = list.Where(x => x.Keyid != null).Select(x => x.Keyid!.Value).Distinct().ToList();
        var compartments = await db.CabinetCompartments.Where(x => keyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var cabinetIds = compartments.Values.Select(x => x.CabinetId).Distinct().ToList();
        var cabinets = await db.Cabinets.Where(x => cabinetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var readerIds = list.Where(x => x.Readerid != null).Select(x => x.Readerid!.Value).Distinct().ToList();
        var people = await db.Readers.Where(x => readerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Cardno, x.FirstName, x.LastName }).ToDictionaryAsync(x => x.Id);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, list.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value));

        var items = list.Select(x =>
        {
            var cc = x.Keyid is long k ? compartments.GetValueOrDefault(k) : null;
            var cab = cc != null ? cabinets.GetValueOrDefault(cc.CabinetId) : null;
            var p = x.Readerid is long rid ? people.GetValueOrDefault(rid) : null;
            return new
            {
                x.Id, x.Keyid, cabinetId = cc?.Id, x.Readerid,
                cardNo = p?.Cardno, fullName = p != null ? $"{p.FirstName} {p.LastName}".Trim() : null,
                cabinetName = cc == null ? null : cab != null ? $"{cab.Name} - {cc.Name}" : cc.Name,
                cabinetBarcode = cc?.Code,
                x.BorrowDate, x.ReturnDate, returned, status = returned ? 2 : 1,
                tenantName = x.TenantId is long tid ? tenantNames.GetValueOrDefault(tid) : null
            };
        });
        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total }));
    }
}

public class KeySnapshotRequest { public string? CardNo { get; set; } public long? CircPlaceId { get; set; } }

public class BorrowKeyRequest
{
    public long?   ReaderId        { get; set; }
    public string? CompartmentCode { get; set; }
    public long?   CircPlaceId     { get; set; }
    public string? Note            { get; set; }
}

public class ReturnKeyRequest
{
    public long?   KeyOutId        { get; set; }
    public string? CompartmentCode { get; set; }
    public string? CardNo          { get; set; }
    public long?   CircPlaceId     { get; set; }
}

public class KeyBorrowSearchRequest
{
    public string? CardNo      { get; set; }
    public string? CardNumber     { get; set; }
    public string? FirstName      { get; set; }
    public string? BorrowDateFrom { get; set; }
    public string? BorrowDateTo   { get; set; }
    public bool?   OnlyReturned { get; set; }
    public int?    PageIndex   { get; set; }
    public int?    PageSize    { get; set; }
    public Guid?   TenantId    { get; set; }
}
