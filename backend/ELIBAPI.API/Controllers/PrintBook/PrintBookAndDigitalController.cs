using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class PrintBookAndDigitalController : GenericController<PrintBookAndDigital, PrintBookAndDigitalSearchRequest, PrintBookAndDigitalRequest>
{
    private readonly ELIBAPIDbContext _db;

    public PrintBookAndDigitalController(
        IGenericRepository<PrintBookAndDigital, PrintBookAndDigitalSearchRequest, PrintBookAndDigitalRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("Add")]
    [Permission("AB_RECEIPTS", "add")]
    public override async Task<IActionResult> Add([FromBody] PrintBookAndDigitalRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PrintBookAndDigitalRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> Search([FromBody] PrintBookAndDigitalSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_RECEIPTS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PrintBookAndDigitalSearchRequest request) => await base.SearchAll(request);

    // ==================== Liên kết 1-1 Tài liệu số ↔ Tài liệu in ====================
    // Lọc đơn vị theo 2 đầu liên kết (Bib/EbookItem), không theo bản ghi liên kết: các liên kết cũ được tạo
    // không gán TenantId (=null). User thường chỉ thấy/sửa liên kết khi Bib thuộc đơn vị mình và tài liệu số
    // thuộc đơn vị mình hoặc dùng chung; tài khoản đặc quyền không bị giới hạn.

    private IQueryable<ELIBAPI.Core.Entities.PrintBook.Bib> VisibleBibs()
    {
        var tenantId = GetTenantId();
        var q = _db.Bibs.Where(x => x.IsDelete != 2);
        return IsPrivilegedRole() ? q : q.Where(x => x.TenantId == tenantId);
    }

    private IQueryable<ELIBAPI.Core.Entities.Ebook.EbookItem> VisibleEbooks()
    {
        var tenantId = GetTenantId();
        var q = _db.EbookItems.Where(x => x.IsDelete != 2);
        return IsPrivilegedRole() ? q : q.Where(x => x.TenantId == tenantId || x.TenantId == null);
    }

    private IQueryable<PrintBookAndDigital> VisibleLinks()
    {
        var bibs   = VisibleBibs();
        var ebooks = VisibleEbooks();
        return _db.PrintBookAndDigitals.Where(x => x.IsDelete != 2
               && (x.BibId == null || bibs.Any(b => b.Bibid == x.BibId))
               && (x.EbookId == null || ebooks.Any(e => e.Id == x.EbookId)));
    }

    [HttpGet("GetByBibId/{bibId:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> GetByBibId(long bibId)
    {
        var link = await VisibleLinks()
            .Where(x => x.BibId == bibId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (link?.EbookId == null) return Ok(ApiResponse<object?>.Ok(null));

        var item = await VisibleEbooks()
            .Include(x => x.ItemXml)
            .Where(x => x.Id == link.EbookId)
            .FirstOrDefaultAsync();
        if (item == null) return Ok(ApiResponse<object?>.Ok(null));

        return Ok(ApiResponse<object>.Ok(new
        {
            item.Id,
            item.PublicId,
            title = item.ItemXml?.Title,
            author = item.ItemXml?.Author,
            publisher = item.ItemXml?.Publisher,
            publishDate = item.ItemXml?.PublishDate,
        }));
    }

    [HttpGet("GetByEbookId/{ebookId:long}")]
    [Permission("AB_RECEIPTS", "view")]
    public async Task<IActionResult> GetByEbookId(long ebookId)
    {
        var link = await VisibleLinks()
            .Where(x => x.EbookId == ebookId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (link?.BibId == null) return Ok(ApiResponse<object?>.Ok(null));

        var bib = await VisibleBibs()
            .Where(x => x.Bibid == link.BibId)
            .FirstOrDefaultAsync();
        if (bib == null) return Ok(ApiResponse<object?>.Ok(null));

        var xml = await _db.BibXmls
            .Where(x => x.IsDelete != 2 && x.BibId == bib.Bibid)
            .FirstOrDefaultAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            bib.Bibid,
            bib.PublicId,
            bib.Mfn,
            title = xml?.Title,
            author = xml?.Author,
            publisher = xml?.Publisher,
            publishDate = xml?.PublishDate,
        }));
    }

    [HttpPost("Link")]
    [Permission("AB_RECEIPTS", "add")]
    public async Task<IActionResult> Link([FromBody] PrintBookAndDigitalLinkRequest r)
    {
        long? bibTenantId = null;
        if (r.BibId != null)
        {
            var bib = await VisibleBibs().Where(x => x.Bibid == r.BibId).Select(x => new { x.TenantId }).FirstOrDefaultAsync();
            if (bib == null) return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
            bibTenantId = bib.TenantId;
        }
        if (r.EbookId != null && !await VisibleEbooks().AnyAsync(x => x.Id == r.EbookId))
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));

        // Đảm bảo quan hệ 1-1: gỡ mọi liên kết active hiện có của cả 2 phía trước khi tạo liên kết mới
        var existing = await _db.PrintBookAndDigitals
            .Where(x => x.IsDelete != 2 && ((r.BibId != null && x.BibId == r.BibId) || (r.EbookId != null && x.EbookId == r.EbookId)))
            .ToListAsync();
        foreach (var e in existing) e.IsDelete = 2;

        var entity = new PrintBookAndDigital
        {
            BibId = r.BibId, EbookId = r.EbookId, PublicId = Guid.NewGuid(), IsDelete = 1,
            TenantId = bibTenantId ?? GetTenantId(),
        };
        _db.PrintBookAndDigitals.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { entity.PublicId }));
    }

    [HttpPost("Unlink")]
    [Permission("AB_RECEIPTS", "delete")]
    public async Task<IActionResult> Unlink([FromBody] PrintBookAndDigitalLinkRequest r)
    {
        var existing = await VisibleLinks()
            .Where(x => (r.BibId != null && x.BibId == r.BibId) || (r.EbookId != null && x.EbookId == r.EbookId))
            .ToListAsync();
        foreach (var e in existing) e.IsDelete = 2;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(true));
    }
}
