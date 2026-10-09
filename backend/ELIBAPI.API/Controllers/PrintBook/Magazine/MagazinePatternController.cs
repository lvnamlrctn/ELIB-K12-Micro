using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Magazine/Pattern")]
public class MagazinePatternController : GenericController<PatternMagazine, PatternMagazineSearchRequest, PatternMagazineRequest>
{
    private readonly ELIBAPIDbContext _db;

    public MagazinePatternController(
        IGenericRepository<PatternMagazine, PatternMagazineSearchRequest, PatternMagazineRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    // Tenant: mẫu kỳ là danh mục — đơn vị thấy mẫu của mình và mẫu dùng chung (TenantId null); chỉ sửa chi tiết đánh số của
    // mẫu thuộc đơn vị mình (tài khoản đặc quyền: mọi mẫu). Trước đây GetByNumericId/Detail/SaveDetail không lọc đơn vị.
    private IQueryable<PatternMagazine> VisiblePatterns()
    {
        var tenantId = GetTenantId();
        var all = IsPrivilegedRole();
        return _db.PatternMagazines.Where(x => x.IsDelete != 2 && (all || x.TenantId == null || x.TenantId == tenantId));
    }

    [HttpPost("Add")] [Permission("PATTERNS", "add")]
    public override async Task<IActionResult> Add([FromBody] PatternMagazineRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("PATTERNS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PatternMagazineRequest r) => await base.Update(publicId, r);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("PATTERNS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("PATTERNS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    [HttpGet("{id:long}")] [Permission("PATTERNS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("PATTERNS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpGet("GetByNumericId/{id:long}")] [Permission("PATTERNS", "view")]
    public async Task<IActionResult> GetByNumericId(long id)
    {
        var pattern = await VisiblePatterns().FirstOrDefaultAsync(x => x.Id == id);
        if (pattern == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy pattern"));
        var detail = await _db.PartemMagazineDetails.FirstOrDefaultAsync(x => x.PatternId == id && x.IsDelete != 2);
        return Ok(ApiResponse<object>.Ok(new { pattern, detail }));
    }

    [HttpPost("Search")] [Permission("PATTERNS", "view")]
    public override async Task<IActionResult> Search([FromBody] PatternMagazineSearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("PATTERNS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PatternMagazineSearchRequest r) => await base.SearchAll(r);

    [HttpGet("Detail/{patternId:long}")]
    [Permission("PATTERNS", "view")]
    public async Task<IActionResult> Detail(long patternId)
    {
        var visible = VisiblePatterns().Select(p => (long?)p.Id);
        var details = await _db.PartemMagazineDetails
            .Where(x => x.PatternId == patternId && x.IsDelete != 2 && visible.Contains(x.PatternId))
            .ToListAsync();
        return Ok(ApiResponse<List<PartemMagazineDetail>>.Ok(details));
    }

    [HttpPost("SaveDetail")]
    [Permission("PATTERNS", "edit")]
    public async Task<IActionResult> SaveDetail([FromBody] PartemMagazineDetailRequest r)
    {
        if (r.PatternId == null) return BadRequest(ApiResponse<string>.Fail("PatternId is required"));
        // Port ELIB-LRC 10-04: trước đây không kiểm tra mẫu — PatternId sai/đã xoá vẫn tạo dòng chi tiết mồ côi.
        var tenantId = GetTenantId();
        var pattern = await _db.PatternMagazines.FirstOrDefaultAsync(x => x.Id == r.PatternId && x.IsDelete != 2
            && (IsPrivilegedRole() || x.TenantId == tenantId));
        if (pattern == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy pattern"));

        var existing = await _db.PartemMagazineDetails
            .FirstOrDefaultAsync(x => x.PatternId == r.PatternId && x.IsDelete != 2);

        var userId = GetCurrentUserId();
        bool isNew = existing == null;

        PartemMagazineDetail entity;
        if (existing != null)
        {
            entity = existing;
            PropertyMapper.Map(r, entity);
        }
        else
        {
            entity = new PartemMagazineDetail { PublicId = Guid.NewGuid() };
            PropertyMapper.Map(r, entity);
            _db.PartemMagazineDetails.Add(entity);
        }
        entity.UpdateRowBy    = userId;
        entity.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            entity.TenantId       = pattern.TenantId;
            entity.CreatedRowBy   = userId;
            entity.CreatedRowDate = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<PartemMagazineDetail>.Ok(entity));
    }
}
