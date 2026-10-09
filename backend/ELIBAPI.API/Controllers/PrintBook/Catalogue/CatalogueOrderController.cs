using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/Order")]
public class CatalogueOrderController : GenericController<AbOrder, AbOrderSearchRequest, AbOrderRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CatalogueOrderController(
        IGenericRepository<AbOrder, AbOrderSearchRequest, AbOrderRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    // Mã đơn (Code) được quy ước bằng đúng Id trong toàn hệ thống (giống AbMove.Code, Bib.Mfn) — Id chỉ
    // có sau khi insert (identity) nên phải gán ở bước riêng sau SaveChangesAsync đầu tiên.
    [HttpPost("Add")]
    [Permission("AB_ORDERS", "add")]
    public override async Task<IActionResult> Add([FromBody] AbOrderRequest request)
    {
        var result = await base.Add(request);
        if (result is OkObjectResult { Value: ApiResponse<AbOrder> { Data: { } order } })
        {
            order.Code = order.Id;
            await _db.SaveChangesAsync();
        }
        return result;
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_ORDERS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbOrderRequest request)
    {
        var result = await base.Update(publicId, request);
        // PropertyMapper.Map ghi đè Code bằng bất kỳ giá trị nào FE gửi lên (kể cả null) — luôn gán lại
        // Code = Id ở đây để đảm bảo trường này không bao giờ lệch/khác Id, bất kể payload gửi gì.
        if (result is OkObjectResult { Value: ApiResponse<AbOrder> { Data: { } order } })
        {
            order.Code = order.Id;
            await _db.SaveChangesAsync();
        }
        return result;
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_ORDERS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_ORDERS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> Search([FromBody] AbOrderSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_ORDERS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbOrderSearchRequest request) => await base.SearchAll(request);

    [HttpGet("Lines/{orderId:long}")]
    [Permission("AB_ORDERS", "view")]
    public async Task<IActionResult> Lines(long orderId)
    {
        var tenantId = GetTenantId();
        var orderExists = await _db.AbOrders.AnyAsync(x => x.Id == orderId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!orderExists) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn đặt mua"));

        var lines = await _db.AbOrderDetails
            .Where(x => x.Order_Id == orderId && x.IsDelete != 2)
            .ToListAsync();

        // Biểu ghi MARC của dòng đơn đặt là bản nháp, nằm ở BibOrder/BibXmlOrder (không phải Bib/BibXml
        // thật) — xem CatalogueBookOrderController.
        var bibIds = lines.Where(x => x.Bibid.HasValue).Select(x => x.Bibid!.Value).Distinct().ToList();
        var xmlMap = await _db.BibXmlOrders
            .Where(x => bibIds.Contains(x.BibId))
            .ToDictionaryAsync(x => x.BibId);
        var mfnMap = await _db.BibOrders
            .Where(x => bibIds.Contains(x.Bibid))
            .ToDictionaryAsync(x => x.Bibid, x => x.Mfn);

        // Tính số lượng đã nhận theo OrderDetailId (Id của chính dòng đơn đặt), KHÔNG so khớp theo Bibid
        // nữa — sau khi tách bảng, Bibid của đơn đặt (BibOrder) và Bibid của đơn nhận (Bib thật, sinh ra
        // khi PromoteToBib) là 2 chuỗi identity độc lập, không còn dùng chung giá trị cho cùng 1 cuốn.
        var receivedMap = await _db.AbReceiptDetails
            .Where(x => x.Order_Id == orderId && x.IsDelete != 2 && x.OrderDetailId.HasValue)
            .GroupBy(x => x.OrderDetailId!.Value)
            .Select(g => new { OrderDetailId = g.Key, Total = g.Sum(x => x.Amount ?? 0) })
            .ToDictionaryAsync(x => x.OrderDetailId, x => x.Total);

        var result = lines.Select(l =>
        {
            xmlMap.TryGetValue(l.Bibid ?? 0, out var xml);
            mfnMap.TryGetValue(l.Bibid ?? 0, out var mfn);
            var received = receivedMap.TryGetValue(l.Id, out var r) ? r : 0;
            return new
            {
                l.Id, l.Order_Id, l.Bibid, l.Amount, l.Price, l.CURRENCY, l.Rate, l.Cancel_Reson, l.PublicId,
                Mfn = mfn,
                Title = xml?.Title, Author = xml?.Author, Publisher = xml?.Publisher, PublishDate = xml?.PublishDate,
                ReceivedAmount  = received,
                RemainingAmount = (l.Amount ?? 0) - received
            };
        }).ToList();

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpDelete("DeleteLine/{lineId:long}")]
    [Permission("AB_ORDERS", "delete")]
    public async Task<IActionResult> DeleteLine(long lineId)
    {
        var tenantId = GetTenantId();
        var line = await _db.AbOrderDetails.FirstOrDefaultAsync(x => x.Id == lineId && x.IsDelete != 2);
        if (line == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng đơn đặt"));

        var orderOwned = await _db.AbOrders.AnyAsync(x => x.Id == line.Order_Id && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (!orderOwned) return NotFound(ApiResponse<string>.Fail("Không tìm thấy dòng đơn đặt"));

        line.IsDelete       = 2;
        line.UpdateRowBy    = GetCurrentUserId();
        line.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã xóa dòng"));
    }

    // Tra cứu dòng đơn đặt còn hàng chưa nhận, trên NHIỀU đơn cùng lúc (theo mã đơn/MFN/nhan đề/tác giả)
    // — dùng cho modal "Chọn ấn phẩm đã nhận được" ở Đơn nhận (khác Lines() chỉ tra theo 1 orderId).
    [HttpPost("LookupReceivableLines")]
    [Permission("AB_ORDERS", "view")]
    public async Task<IActionResult> LookupReceivableLines([FromBody] AbOrderLineLookupRequest r)
    {
        var tenantId = GetTenantId();
        var q = from d in _db.AbOrderDetails.Where(x => x.IsDelete != 2)
                join o in _db.AbOrders.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId))
                    on d.Order_Id equals o.Id
                join bo in _db.BibOrders.Where(x => x.IsDelete != 2) on d.Bibid equals bo.Bibid
                join x in _db.BibXmlOrders on bo.Bibid equals x.BibId into xs
                from x in xs.DefaultIfEmpty()
                select new { D = d, O = o, Bo = bo, X = x };

        if (r.OrderCodeFrom.HasValue) q = q.Where(t => t.O.Code >= r.OrderCodeFrom);
        if (r.OrderCodeTo.HasValue)   q = q.Where(t => t.O.Code <= r.OrderCodeTo);
        if (r.MfnFrom.HasValue)       q = q.Where(t => t.Bo.Mfn >= r.MfnFrom);
        if (r.MfnTo.HasValue)         q = q.Where(t => t.Bo.Mfn <= r.MfnTo);
        if (!string.IsNullOrEmpty(r.Title))  q = q.Where(t => t.X != null && t.X.Title!.Contains(r.Title));
        if (!string.IsNullOrEmpty(r.Author)) q = q.Where(t => t.X != null && t.X.Author!.Contains(r.Author));

        var all = await q.ToListAsync();
        var lineIds = all.Select(t => t.D.Id).ToList();
        var receivedMap = await _db.AbReceiptDetails
            .Where(x => x.IsDelete != 2 && x.OrderDetailId.HasValue && lineIds.Contains(x.OrderDetailId.Value))
            .GroupBy(x => x.OrderDetailId!.Value)
            .Select(g => new { OrderDetailId = g.Key, Total = g.Sum(x => x.Amount ?? 0) })
            .ToDictionaryAsync(x => x.OrderDetailId, x => x.Total);

        var rows = all.Select(t =>
        {
            var received = receivedMap.TryGetValue(t.D.Id, out var rec) ? rec : 0;
            return new
            {
                id = t.D.Id, order_Id = t.O.Id, orderCode = t.O.Code, bibid = t.D.Bibid, mfn = t.Bo.Mfn,
                title = t.X?.Title, author = t.X?.Author, publisher = t.X?.Publisher,
                amount = t.D.Amount, price = t.D.Price, currency = t.D.CURRENCY, rate = t.D.Rate,
                receivedAmount = received, remainingAmount = (t.D.Amount ?? 0) - received
            };
        })
        .Where(x => x.remainingAmount > 0)
        .OrderByDescending(x => x.order_Id).ThenBy(x => x.mfn)
        .ToList();

        var pageIndex = r.PageIndex < 1 ? 1 : r.PageIndex;
        var pageSize  = r.PageSize  < 1 ? 20 : r.PageSize;
        var paged = rows.Skip((Math.Max(pageIndex, 1) - 1) * pageSize).Take(pageSize).ToList();

        return Ok(ApiResponse<object>.Ok(new { items = paged, totalCount = rows.Count }));
    }

    [HttpPost("SaveDetail")]
    [Permission("AB_ORDERS", "edit")]
    public async Task<IActionResult> SaveDetail([FromBody] AbOrderDetailRequest r)
    {
        if (r.Order_Id == null) return BadRequest(ApiResponse<string>.Fail("Order_Id is required"));

        AbOrderDetail entity;
        bool isNew = !_db.AbOrderDetails.Any(x => x.Order_Id == r.Order_Id && x.Bibid == r.Bibid && x.IsDelete != 2);
        if (!isNew)
        {
            entity = await _db.AbOrderDetails
                .FirstAsync(x => x.Order_Id == r.Order_Id && x.Bibid == r.Bibid && x.IsDelete != 2);
            ELIBAPI.Core.Common.PropertyMapper.Map(r, entity);
        }
        else
        {
            entity = new AbOrderDetail { PublicId = Guid.NewGuid() };
            ELIBAPI.Core.Common.PropertyMapper.Map(r, entity);
            _db.AbOrderDetails.Add(entity);
        }
        // PropertyMapper.Map bỏ qua "Bibid" (audit-field dùng chung cho entity khác coi BibId là khóa hệ
        // thống) — với AbOrderDetail thì Bibid là dữ liệu nghiệp vụ bắt buộc nên phải gán tay (cùng fix
        // đã áp dụng cho AbReceiptDetail.SaveDetail).
        entity.Bibid = r.Bibid;
        var userId = GetCurrentUserId();
        entity.UpdateRowBy    = userId;
        entity.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            entity.TenantId       = GetTenantId();
            entity.CreatedRowBy   = userId;
            entity.CreatedRowDate = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<AbOrderDetail>.Ok(entity));
    }
}

public class AbOrderLineLookupRequest
{
    public long?   OrderCodeFrom { get; set; }
    public long?   OrderCodeTo   { get; set; }
    public long?   MfnFrom       { get; set; }
    public long?   MfnTo         { get; set; }
    public string? Title         { get; set; }
    public string? Author        { get; set; }
    public int     PageIndex     { get; set; } = 1;
    public int     PageSize      { get; set; } = 20;
}
