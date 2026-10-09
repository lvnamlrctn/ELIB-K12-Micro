using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

// Port ELIB-LRC 10-03: nghiệp vụ kỳ ấn phẩm chuyển sang ISerialIssueService. Đường dẫn giữ nguyên; SaveItem nhận đúng tên
// trường giao diện gửi (subscriptionId, serialSeqX, plannedDate…) và các API kỳ trả SerialIssueView camelCase.
[Route("api/PrintBook/Magazine/Serial")]
[Authorize]
public class MagazineSerialController(
    IGenericRepository<Serial, SerialSearchRequest, SerialRequest> repo,
    ISerialIssueService issues) : GenericController<Serial, SerialSearchRequest, SerialRequest>(repo)
{
    /// <summary>Phạm vi đơn vị cho service: tài khoản đặc quyền (không đơn vị / ReadOnlyPolicy) thấy mọi đơn vị.</summary>
    private long? ScopeTenantId() => IsPrivilegedRole() ? null : GetTenantId();

    [HttpPost("Search")] [Permission("SUBSCRIPTIONS", "view")]
    public override async Task<IActionResult> Search([FromBody] SerialSearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("SUBSCRIPTIONS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SerialSearchRequest r) => await base.SearchAll(r);

    [HttpGet("{id:long}")] [Permission("SUBSCRIPTIONS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("SUBSCRIPTIONS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Add")] [Permission("SUBSCRIPTIONS", "add")]
    public override async Task<IActionResult> Add([FromBody] SerialRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("SUBSCRIPTIONS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SerialRequest r)
    {
        if (await issues.IsApprovedAsync(publicId, ScopeTenantId()))
            return BadRequest(ApiResponse<string>.Fail("Không thể sửa đăng ký đã được duyệt."));
        return await base.Update(publicId, r);
    }

    [HttpDelete("Delete/{publicId:guid}")] [Permission("SUBSCRIPTIONS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        if (await issues.IsApprovedAsync(publicId, ScopeTenantId()))
            return BadRequest(ApiResponse<string>.Fail("Không thể xóa đăng ký đã được duyệt."));
        return await base.Delete(publicId);
    }

    [HttpPut("Approve")] [Permission("SUBSCRIPTIONS", "edit")]
    public async Task<IActionResult> Approve([FromBody] SerialApproveRequest r) =>
        this.FromServiceResult(await issues.ApproveAsync(r.PublicId, ScopeTenantId(), UserId()), x => (object)x);

    [HttpPut("ChangeStatus")] [Permission("SUBSCRIPTIONS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    // --- Kỳ ấn phẩm (SerialItem) ---

    [HttpPost("SearchReceipt")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> SearchReceipt([FromBody] SerialReceiptSearchRequest r) =>
        this.FromServiceResult(await issues.SearchAsync(r, ScopeTenantId()),
            page => (object)new { items = page.Items, totalCount = page.TotalCount, pageIndex = page.PageIndex, pageSize = page.PageSize });

    [HttpPost("SaveItem")]
    [Permission("SUBSCRIPTIONS", "edit")]
    public async Task<IActionResult> SaveItem([FromBody] SerialIssueSaveRequest r) =>
        this.FromServiceResult(await issues.SaveAsync(r, ScopeTenantId(), UserId()), x => x);

    [HttpPost("Claim")]
    [Permission("SUBSCRIPTIONS", "edit")]
    public async Task<IActionResult> Claim([FromBody] ClaimRequest r) =>
        this.FromServiceResult(await issues.ClaimAsync(r.Id, ScopeTenantId(), UserId()), x => x);

    [HttpPost("PredictIssues")]
    [Permission("SUBSCRIPTIONS", "edit")]
    public async Task<IActionResult> PredictIssues([FromBody] PredictIssuesRequest r) =>
        this.FromServiceResult(await issues.PredictAsync(r, ScopeTenantId(), UserId()), x => x);

    [HttpGet("Statistics/{subscriptionId:long}")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> Statistics(long subscriptionId) =>
        this.FromServiceResult(await issues.StatisticsAsync(subscriptionId, ScopeTenantId()), x => (object)x);

    [HttpGet("LastPublishDate/{subscriptionId:long}")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> LastPublishDate(long subscriptionId) =>
        this.FromServiceResult(await issues.LastReceivedAsync(subscriptionId, ScopeTenantId()),
            last => (object)new { publishedDate = last?.PublishedDate, serialSeq = last?.SerialSeq });

    [HttpDelete("DeleteItem/{id:long}")]
    [Permission("SUBSCRIPTIONS", "delete")]
    public async Task<IActionResult> DeleteItem(long id) =>
        this.FromServiceResult(await issues.DeleteAsync(id, null, ScopeTenantId(), UserId()), x => x);

    /// <summary>Màn "Nhận kỳ" xoá theo PublicId (kể cả khi ghép số) — trước đây chỉ có route theo Id số nên luôn 404.</summary>
    [HttpDelete("DeleteItem/{publicId:guid}")]
    [Permission("SUBSCRIPTIONS", "delete")]
    public async Task<IActionResult> DeleteItemByPublicId(Guid publicId) =>
        this.FromServiceResult(await issues.DeleteAsync(null, publicId, ScopeTenantId(), UserId()), x => x);

    private long? UserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
