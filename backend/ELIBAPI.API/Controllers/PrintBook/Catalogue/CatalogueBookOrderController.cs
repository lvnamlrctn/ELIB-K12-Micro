using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

// Biểu ghi MARC "nháp" của Đơn đặt bổ sung — lưu vào BibOrder/BibXmlOrder/BibDataOrder/
// fixed_field_value_order (KHÔNG phải Bib/BibXml/BibData/fixed_field_value thật) để không làm bẩn danh
// mục chính khi sách chưa thực sự về kho. Mirror Save/GetByMfn của CatalogueBookController, tái dùng
// nguyên các helper build MARC ở đó (đã đổi private -> internal). BibOrder không có CollectionId/EbookId
// nên không có "Bộ sưu tập"/"Liên kết tài liệu số" ở đây — 2 tính năng đó chỉ áp dụng khi biểu ghi đã
// "thăng cấp" thành Bib thật (xem PromoteToBib), làm ở trang Đơn nhận.
[Route("api/PrintBook/Catalogue/BookOrder")]
public class CatalogueBookOrderController(ELIBAPIDbContext db, ILogger<CatalogueBookOrderController> logger) : BaseApiController
{
    [HttpGet("GetByMfn/{mfn:long}")]
    [Permission("AB_ORDERS", "view")]
    public async Task<IActionResult> GetByMfn(long mfn)
    {
        var tenantId = GetTenantId();
        var bib = await db.BibOrders.FirstOrDefaultAsync(x => x.Mfn == mfn && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (bib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi"));

        var xml = await db.BibXmlOrders.FirstOrDefaultAsync(x => x.BibId == bib.Bibid);
        var bibData = await db.BibDataOrders
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

        var controlFields = await db.FixedFieldValueOrders
            .Where(x => x.Bibid == bib.Bibid && x.IsDelete != 2)
            .OrderBy(x => x.Field)
            .Select(x => new BibMarcFieldResponse { Tag = x.Field, Value = x.Value })
            .ToListAsync();
        foreach (var tag in CatalogueBookController.ControlFieldTags)
            if (!controlFields.Any(x => x.Tag == tag))
                controlFields.Add(new BibMarcFieldResponse { Tag = tag, Value = "" });
        controlFields = controlFields.OrderBy(x => x.Tag).ToList();
        fields.InsertRange(0, controlFields);

        var response = new BibMarcResponse
        {
            Mfn         = bib.Mfn,
            BibId       = bib.Bibid,
            BibTypeId   = bib.Bib_Type_Id,
            Title       = xml?.Title,
            Author      = xml?.Author,
            Publisher   = xml?.Publisher,
            PublishDate = xml?.PublishDate,
            Fields      = fields
        };
        return Ok(ApiResponse<BibMarcResponse>.Ok(response));
    }

    [HttpPost("Save")]
    [Permission("AB_ORDERS", "edit")]
    public async Task<IActionResult> Save([FromBody] SaveBibRequest r)
    {
        BibOrder bib;
        bool isNew = false;
        if (r.BibId.HasValue && r.BibId > 0)
        {
            // Tenant: chỉ sửa được biểu ghi nháp của đơn vị mình (trước đây tra theo BibId không lọc đơn vị nên ghi
            // đè được biểu ghi nháp của đơn vị khác). BibId không thấy trong phạm vi → tạo mới như trước.
            var callerTenantId = GetTenantId();
            bib = await db.BibOrders.FirstOrDefaultAsync(x => x.Bibid == r.BibId && x.IsDelete != 2
                      && (!callerTenantId.HasValue || x.TenantId == callerTenantId))
                  ?? new BibOrder { PublicId = Guid.NewGuid() };
            if (bib.Bibid == 0) isNew = true;
        }
        else
        {
            bib = new BibOrder { PublicId = Guid.NewGuid() };
            isNew = true;
        }

        var userId = GetCurrentUserId();
        if (r.BibTypeId.HasValue) bib.Bib_Type_Id = r.BibTypeId;
        bib.UpdateRowBy    = userId;
        bib.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            bib.TenantId       = GetTenantId();
            bib.CreatedRowBy   = userId;
            bib.CreatedRowDate = DateTime.Now;
            db.BibOrders.Add(bib);
        }
        await db.SaveChangesAsync();

        // Mfn nháp = Bibid của BibOrder (quy ước riêng cho bảng nháp — không liên quan Mfn thật sẽ sinh
        // lại khi PromoteToBib).
        if (isNew && !bib.Mfn.HasValue)
        {
            bib.Mfn = bib.Bibid;
            await db.SaveChangesAsync();
        }

        var oldData = db.BibDataOrders.Where(x => x.BibId == bib.Bibid);
        db.BibDataOrders.RemoveRange(oldData);

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
                db.BibDataOrders.Add(new BibDataOrder
                {
                    BibId          = bib.Bibid,
                    Field          = field.Tag,
                    L1             = field.Ind1,
                    L2             = field.Ind2,
                    SubField       = sf.Code,
                    Data           = sf.Value,
                    TenantId       = bib.TenantId,
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now
                });
            }
        }
        await db.SaveChangesAsync();

        var xml = await db.BibXmlOrders.FirstOrDefaultAsync(x => x.BibId == bib.Bibid);
        if (xml == null)
        {
            xml = new BibXmlOrder
            {
                BibId          = bib.Bibid,
                PublicId       = Guid.NewGuid(),
                TenantId       = bib.TenantId,
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            };
            db.BibXmlOrders.Add(xml);
        }
        xml.Title       = CatalogueBookController.GetSubfieldValues(r.Fields, "245", 'a', 'b');
        xml.Author      = CatalogueBookController.GetSubfieldValue(r.Fields, "100", 'a') ?? CatalogueBookController.GetSubfieldValue(r.Fields, "700", 'a');
        xml.Publisher   = CatalogueBookController.GetSubfieldValue(r.Fields, "264", 'b') ?? CatalogueBookController.GetSubfieldValue(r.Fields, "260", 'b');
        xml.PublishDate = CatalogueBookController.GetSubfieldValue(r.Fields, "264", 'c') ?? CatalogueBookController.GetSubfieldValue(r.Fields, "260", 'c');
        xml.DDC         = CatalogueBookController.GetSubfieldValue(r.Fields, "082", 'a');
        xml.Keyword     = CatalogueBookController.GetSubfieldValues(r.Fields, "650", 'a', joinAll: true);
        xml.Isbd        = await CatalogueBookController.GenerateIsbdAsync(db, bib.Bib_Type_Id, r.Fields);
        xml.UpdateRowBy = userId;
        xml.UpdatedRowDate = DateTime.Now;
        await db.SaveChangesAsync();

        var bibType = bib.Bib_Type_Id.HasValue
            ? await db.BibTypes.FirstOrDefaultAsync(x => x.Id == bib.Bib_Type_Id)
            : null;
        var tenant = bib.TenantId.HasValue
            ? await db.Tenants.FirstOrDefaultAsync(x => x.Id == bib.TenantId)
            : null;
        var existingControlFields = await db.FixedFieldValueOrders
            .Where(x => x.Bibid == bib.Bibid && x.IsDelete != 2)
            .ToListAsync();

        foreach (var tag in CatalogueBookController.ControlFieldTags)
        {
            var existing = existingControlFields.FirstOrDefault(x => x.Field == tag);
            string value;
            if (tag == "005")
            {
                value = CatalogueBookController.BuildDefaultControlValue(tag, bibType, tenant?.Code, bib.Mfn, xml?.PublishDate);
            }
            else if (tag != "001" && controlFieldValues.TryGetValue(tag, out var clientVal))
            {
                value = clientVal;
            }
            else if (existing != null)
            {
                // 001 = Số kiểm soát/MFN — khoá không cho client ghi đè (giữ nguyên giá trị đã có)
                continue;
            }
            else
            {
                value = CatalogueBookController.BuildDefaultControlValue(tag, bibType, tenant?.Code, bib.Mfn, xml?.PublishDate);
            }

            if (existing != null)
            {
                existing.Value          = value;
                existing.UpdateRowBy    = userId;
                existing.UpdatedRowDate = DateTime.Now;
            }
            else
            {
                db.FixedFieldValueOrders.Add(new FixedFieldValueOrder
                {
                    Bibid          = bib.Bibid,
                    Field          = tag,
                    Value          = value,
                    TenantId       = bib.TenantId,
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now
                });
            }
        }
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            mfn       = bib.Mfn,
            bibId     = bib.Bibid,
            title     = xml?.Title,
            author    = xml?.Author,
            publisher = xml?.Publisher
        }));
    }

    // Chuyển 1 bộ BibOrder/BibXmlOrder/BibDataOrder/fixed_field_value_order thành biểu ghi Bib THẬT —
    // gọi khi 1 dòng đơn đặt được thêm vào Đơn nhận ("Từ đơn đặt"), đúng thời điểm sách "về kho". Sinh
    // Mfn thật mới (khác Mfn nháp của BibOrder) và ghi đè vào control field 001 cho khớp quy ước
    // Mfn == Bibid áp dụng cho toàn bộ Bib thật.
    // Port ELIB-LRC 10-03:
    //  - dòng đơn đặt đã được nhận ở đợt trước (có dòng đơn nhận trỏ OrderDetailId về nó, Bib còn sống) thì dùng lại
    //    Bib đó — trước đây mỗi lần nhận sinh 1 Bib mới, sách về 2 đợt / bấm 2 lần thì danh mục có 2 biểu ghi trùng;
    //  - Bib mới được đưa vào hàng đợi lập chỉ mục (ES + Zebra), trước đây không tìm thấy cho tới lần dựng lại toàn bộ;
    //  - ghi trong 1 transaction, lỗi giữa chừng không để lại biểu ghi dở dang.
    // Tenant: Bib mới thuộc đơn vị của biểu ghi nháp (kể cả khi tài khoản hệ thống thao tác); Bib dùng lại phải cùng đơn vị.
    [HttpPost("PromoteToBib/{bibOrderId:long}")]
    [Permission("AB_ORDERS", "edit")]
    public async Task<IActionResult> PromoteToBib(long bibOrderId)
    {
        var tenantId = GetTenantId();
        var orderBib = await db.BibOrders.FirstOrDefaultAsync(x => x.Bibid == bibOrderId && x.IsDelete != 2
            && (!tenantId.HasValue || x.TenantId == tenantId));
        if (orderBib == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy biểu ghi nháp của đơn đặt"));
        var bibTenantId = orderBib.TenantId;

        var orderLineIds = db.AbOrderDetails.Where(d => d.Bibid == orderBib.Bibid && d.IsDelete != 2
                                                         && d.TenantId == bibTenantId).Select(d => d.Id);
        var reused = await db.AbReceiptDetails
            .Where(r => r.IsDelete != 2 && r.OrderDetailId != null && orderLineIds.Contains(r.OrderDetailId.Value) && r.Bibid != null)
            .Join(db.Bibs.Where(b => b.IsDelete != 2 && b.TenantId == bibTenantId), r => r.Bibid, b => b.Bibid, (r, b) => new { b.Bibid, b.Mfn })
            .OrderBy(x => x.Bibid)
            .FirstOrDefaultAsync();
        if (reused != null) return Ok(ApiResponse<object>.Ok(new { bibId = reused.Bibid, mfn = reused.Mfn, reused = true }));

        var orderXml  = await db.BibXmlOrders.FirstOrDefaultAsync(x => x.BibId == orderBib.Bibid);
        var orderData = await db.BibDataOrders.Where(x => x.BibId == orderBib.Bibid && x.IsDelete != 2).ToListAsync();
        var orderCtrl = await db.FixedFieldValueOrders.Where(x => x.Bibid == orderBib.Bibid && x.IsDelete != 2).ToListAsync();

        var userId = GetCurrentUserId();
        await using var tx = await db.Database.BeginTransactionAsync();
        var bib = new Bib
        {
            PublicId       = Guid.NewGuid(),
            Bib_type_id    = orderBib.Bib_Type_Id,
            TenantId       = bibTenantId,
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now,
            UpdateRowBy    = userId,
            UpdatedRowDate = DateTime.Now
        };
        db.Bibs.Add(bib);
        await db.SaveChangesAsync();
        bib.Mfn = bib.Bibid;

        foreach (var d in orderData)
        {
            db.BibDatas.Add(new BibData
            {
                BibId          = bib.Bibid,
                Field          = d.Field,
                SubField       = d.SubField,
                Data           = d.Data,
                L1             = d.L1,
                L2             = d.L2,
                TenantId       = bibTenantId,
                PublicId       = Guid.NewGuid(),
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            });
        }

        db.BibXmls.Add(new BibXml
        {
            BibId          = bib.Bibid,
            Title          = orderXml?.Title,
            Author         = orderXml?.Author,
            Publisher      = orderXml?.Publisher,
            PublishDate    = orderXml?.PublishDate,
            DDC            = orderXml?.DDC,
            Keyword        = orderXml?.Keyword,
            Isbd           = orderXml?.Isbd,
            TenantId       = bibTenantId,
            PublicId       = Guid.NewGuid(),
            CreatedRowBy   = userId,
            CreatedRowDate = DateTime.Now
        });

        foreach (var c in orderCtrl)
        {
            var isMfnField = c.Field == "001";
            db.FixedFieldValues.Add(new FixedFieldValue
            {
                Bibid          = bib.Bibid,
                Field          = c.Field,
                Value          = isMfnField ? bib.Mfn?.ToString() : c.Value,
                TenantId       = bibTenantId,
                PublicId       = Guid.NewGuid(),
                CreatedRowBy   = userId,
                CreatedRowDate = DateTime.Now
            });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var services = HttpContext.RequestServices;
        BackgroundJobs.TryEnqueue<PrintBookIndexingJob>(services, j => j.RunAsync(bib.Bibid), logger);
        BackgroundJobs.TryEnqueue<ZebraExportJob>(services, j => j.RunAsync(bib.Bibid), logger);

        return Ok(ApiResponse<object>.Ok(new { bibId = bib.Bibid, mfn = bib.Mfn, reused = false }));
    }
}
