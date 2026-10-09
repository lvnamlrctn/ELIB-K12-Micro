using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

/// <summary>Đóng tập báo tạp chí — xem <see cref="ISerialBindingService"/> (port ELIB-LRC 10-04).</summary>
[Route("api/PrintBook/Magazine/Binding")]
[Authorize]
public class MagazineBindingController(ELIBAPIDbContext db, ISerialBindingService bindings) : BaseApiController
{
    /// <summary>Phạm vi đơn vị cho thao tác theo Id: tài khoản đặc quyền thấy mọi đơn vị.</summary>
    private long? ScopeTenantId() => IsPrivilegedRole() ? null : GetTenantId();

    [HttpPost("Search")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> Search([FromBody] BindingSearchRequest r)
    {
        var scope = await TenantScopeHelper.ResolveScopeAsync(db, r.TenantId, GetTenantId(), IsPrivilegedRole());
        var (items, total) = await bindings.SearchAsync(r.AccessionNo, r.VolumeTitle, r.StoreId, r.PageIndex, r.PageSize,
            scope.TenantId, scope.IncludeShared, scope.All);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount = total, pageIndex = Math.Max(r.PageIndex ?? 1, 1), pageSize = Math.Clamp(r.PageSize ?? 20, 1, 1000) }));
    }

    /// <summary>Trả bộ đóng tập dạng phẳng kèm <c>items</c> — trước đây trả <c>{ binding, items }</c> trong khi màn hình đọc
    /// <c>accessionNo</c>… ở cấp ngoài, nên mở sửa thấy form trống và lưu lại thì xoá trắng số ĐKCB/nhan đề.</summary>
    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SUBSCRIPTIONS", "view")]
    public async Task<IActionResult> GetById(Guid publicId)
    {
        var binding = await bindings.GetAsync(publicId, ScopeTenantId());
        return binding == null
            ? NotFound(ApiResponse<string>.Fail("Không tìm thấy bộ đóng tập"))
            : Ok(ApiResponse<SerialBindingView>.Ok(binding));
    }

    [HttpPost("Add")]
    [Permission("SUBSCRIPTIONS", "add")]
    public async Task<IActionResult> Add([FromBody] BindingRequest r) =>
        this.FromServiceResult(await bindings.SaveAsync(null, ToInput(r), ScopeTenantId(), GetCurrentUserId()), b => new { id = b.Id, publicId = b.PublicId });

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SUBSCRIPTIONS", "edit")]
    public async Task<IActionResult> Update(Guid publicId, [FromBody] BindingRequest r) =>
        this.FromServiceResult(await bindings.SaveAsync(publicId, ToInput(r), ScopeTenantId(), GetCurrentUserId()), _ => "Đã cập nhật bộ đóng tập");

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SUBSCRIPTIONS", "delete")]
    public async Task<IActionResult> Delete(Guid publicId) =>
        this.FromServiceResult(await bindings.DeleteAsync(publicId, ScopeTenantId(), GetCurrentUserId()), _ => "Đã xóa bộ đóng tập");

    private static SerialBindingInput ToInput(BindingRequest r) =>
        new(r.AccessionNo, r.StoreId, r.VolumeTitle, r.SubscriptionId, r.SubscriptionTitle, r.BindingDate, r.Note,
            r.Items?.Where(i => i.IssueId.HasValue).Select(i => i.IssueId!.Value).ToList());
}

public class BindingSearchRequest
{
    public string?   AccessionNo { get; set; }
    public string?   VolumeTitle { get; set; }
    public long?     StoreId     { get; set; }
    public int?      PageIndex   { get; set; }
    public int?      PageSize    { get; set; }
    public Guid?     TenantId    { get; set; }
}

public class BindingItemRequest
{
    public long?     IssueId       { get; set; }
    public string?   SerialSeq     { get; set; }
    public DateTime? PublishedDate { get; set; }
}

public class BindingRequest
{
    public string?              AccessionNo       { get; set; }
    public long?                StoreId           { get; set; }
    public string?              VolumeTitle       { get; set; }
    public long?                SubscriptionId    { get; set; }
    public string?              SubscriptionTitle { get; set; }
    public DateTime?            BindingDate       { get; set; }
    public string?              Note              { get; set; }
    public List<BindingItemRequest>? Items        { get; set; }
}
