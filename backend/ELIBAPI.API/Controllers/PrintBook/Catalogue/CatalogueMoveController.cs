using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>Phiếu điều chuyển kho. Nghiệp vụ ở <see cref="IStoreMoveService"/> (port ELIB-LRC 10-04, lọc theo đơn vị JWT).
/// Phiếu đã hoàn thành bị khoá: không sửa, không xoá, không thêm/bớt dòng.</summary>
[Route("api/PrintBook/Catalogue/Move")]
public class CatalogueMoveController : GenericController<AbMove, AbMoveSearchRequest, AbMoveRequest>
{
    private readonly IStoreMoveService _moves;

    public CatalogueMoveController(
        IGenericRepository<AbMove, AbMoveSearchRequest, AbMoveRequest> repo,
        IStoreMoveService moves) : base(repo) => _moves = moves;

    private long? CurrentUserId => GetCurrentUserId() is var id and > 0 ? id : null;

    // Mã đơn (Code) được quy ước bằng đúng Id trong toàn hệ thống (giống Bib.Mfn = Bib.Bibid) — Id chỉ
    // có sau khi insert (identity) nên phải gán ở bước riêng sau lần lưu đầu tiên.
    [HttpPost("Add")]
    [Permission("AB_MOVES", "add")]
    public override async Task<IActionResult> Add([FromBody] AbMoveRequest request)
    {
        if (await _moves.ValidateAsync(request, null, GetTenantId()) is string error) return BadRequest(ApiResponse<string>.Fail(error));
        var result = await base.Add(request);
        if (result is OkObjectResult { Value: ApiResponse<AbMove> { Data: { } move } }) { await _moves.SyncCodeAsync(move.Id); move.Code = move.Id; }
        return result;
    }

    // PropertyMapper.Map ghi đè Code bằng bất kỳ giá trị nào FE gửi lên — luôn gán lại Code = Id sau khi lưu.
    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbMoveRequest request)
    {
        if (await _moves.ValidateAsync(request, publicId, GetTenantId()) is string error) return BadRequest(ApiResponse<string>.Fail(error));
        var result = await base.Update(publicId, request);
        if (result is OkObjectResult { Value: ApiResponse<AbMove> { Data: { } move } }) { await _moves.SyncCodeAsync(move.Id); move.Code = move.Id; }
        return result;
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_MOVES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) =>
        await _moves.LockedErrorAsync(movePublicId: publicId) is string error ? BadRequest(ApiResponse<string>.Fail(error)) : await base.Delete(publicId);

    // Đặt "đã hoàn thành" phải qua Complete (để thực sự chuyển ĐKCB), không qua đổi trạng thái chung.
    [HttpPut("ChangeStatus")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        if (request.Status == StoreMoveService.Completed) return BadRequest(ApiResponse<string>.Fail("Dùng chức năng Hoàn thành điều chuyển"));
        return await _moves.LockedErrorAsync(movePublicId: request.PublicId) is string error ? BadRequest(ApiResponse<string>.Fail(error)) : await base.ChangeStatus(request);
    }

    /// <summary>Hoàn thành điều chuyển: ĐKCB trong phiếu chuyển sang kho nhận, phiếu bị khoá.</summary>
    [HttpPost("Complete/{moveId:long}")]
    [Permission("AB_MOVES", "edit")]
    public async Task<IActionResult> Complete(long moveId) =>
        this.FromServiceResult(await _moves.CompleteAsync(moveId, CurrentUserId, GetTenantId()), r => r);

    [HttpGet("{id:long}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> Search([FromBody] AbMoveSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbMoveSearchRequest request) => await base.SearchAll(request);

    // ─── Chọn tài liệu (Barcode) thuộc Kho nguồn của phiếu để gắn vào AbMoveDetail ───────────────────

    [HttpPost("SearchDocuments")]
    [Permission("AB_MOVES", "view")]
    public async Task<IActionResult> SearchDocuments([FromBody] MoveDocumentSearchRequest r) =>
        this.FromServiceResult(await _moves.SearchDocumentsAsync(new MoveDocumentQuery(r.MoveId, r.MfnFrom, r.MfnTo, r.Title, r.Author,
            r.Publisher, r.PublishYear, r.BarcodeFrom, r.BarcodeTo, r.PageIndex, r.PageSize), GetTenantId()),
            p => (object)new { items = p.Items, recordsTotal = p.Total });

    [HttpGet("Lines/{moveId:long}")]
    [Permission("AB_MOVES", "view")]
    public async Task<IActionResult> Lines(long moveId) => this.FromServiceResult(await _moves.LinesAsync(moveId, GetTenantId()), l => l);

    [HttpPost("AddDetails")]
    [Permission("AB_MOVES", "edit")]
    public async Task<IActionResult> AddDetails([FromBody] MoveAddDetailsRequest r) =>
        this.FromServiceResult(await _moves.AddDetailsAsync(r.MoveId, r.BarcodeIds, CurrentUserId, GetTenantId()),
            a => (object)new { success = true, added = a.Added, skipped = a.Skipped });
}

public class MoveDocumentSearchRequest
{
    public long    MoveId      { get; set; }
    public long?   MfnFrom     { get; set; }
    public long?   MfnTo       { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishYear { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo   { get; set; }
    public int?    PageIndex   { get; set; }
    public int?    PageSize    { get; set; }
}

public class MoveAddDetailsRequest
{
    public long       MoveId     { get; set; }
    public List<long> BarcodeIds { get; set; } = [];
}
