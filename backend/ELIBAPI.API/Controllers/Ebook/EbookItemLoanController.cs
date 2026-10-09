using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookItemLoanController : GenericController<EbookItemLoan, EbookItemLoanSearchRequest, EbookItemLoanRequest>
{
    private readonly IEbookItemLoanRepository _loanRepo;
    private readonly ELIBAPIDbContext         _dbContext;

    public EbookItemLoanController(IEbookItemLoanRepository repo, ELIBAPIDbContext dbContext) : base(repo)
    {
        _loanRepo  = repo;
        _dbContext = dbContext;
    }

    // ── Xem — dùng chung ModuleCode DIGITAL_DOC (bảng con của trang Tài liệu số, không có menu riêng) ──

    [HttpGet("{id:long}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookItemLoanSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookItemLoanSearchRequest request) => await base.SearchAll(request);

    [HttpPost("Add")]
    [Permission("DIGITAL_DOC", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookItemLoanRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookItemLoanRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DIGITAL_DOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    // ── Thu hồi ──────────────────────────────────────────────────────────────

    [HttpPut("Recall/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> Recall(Guid publicId, [FromBody] RecallLoanRequest request)
    {
        var ok = await _loanRepo.RecallAsync(publicId, GetCurrentUserId(), request.Reason);
        return ok
            ? Ok(ApiResponse<object>.Ok(null!, "Đã thu hồi tài liệu."))
            : NotFound(ApiResponse<object>.Fail("Không tìm thấy lượt mượn đang hoạt động.", 404));
    }

    [HttpPut("RecallAll/{ebookItemPublicId:guid}")]
    [Permission("DIGITAL_DOC", "edit")]
    public async Task<IActionResult> RecallAll(Guid ebookItemPublicId, [FromBody] RecallLoanRequest request)
    {
        var ebookItemId = await _dbContext.EbookItems
            .Where(e => e.PublicId == ebookItemPublicId && e.IsDelete != 2)
            .Select(e => (long?)e.Id)
            .FirstOrDefaultAsync();

        if (ebookItemId == null)
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));

        var count = await _loanRepo.RecallAllActiveForItemAsync(ebookItemId.Value, GetCurrentUserId(), request.Reason);
        return Ok(ApiResponse<object>.Ok(new { count }, $"Đã thu hồi {count} lượt mượn đang hoạt động."));
    }
}
