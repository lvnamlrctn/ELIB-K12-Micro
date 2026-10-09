using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class NotificationChannelConfigController
    : GenericController<NotificationChannelConfig, NotificationChannelConfigSearchRequest, NotificationChannelConfigRequest>
{
    public NotificationChannelConfigController(
        IGenericRepository<NotificationChannelConfig, NotificationChannelConfigSearchRequest, NotificationChannelConfigRequest> repo)
        : base(repo) { }

    [HttpPost("Add")]
    [Permission("SMS_ZALO_CONFIG", "add")]
    public override async Task<IActionResult> Add([FromBody] NotificationChannelConfigRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SMS_ZALO_CONFIG", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] NotificationChannelConfigRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SMS_ZALO_CONFIG", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    // Không bao giờ trả access-token thật ra response — mask thành "***" khi đã có giá trị. Client để
    // trống input này khi Update để giữ nguyên giá trị cũ (xem NotificationChannelConfigRepository).
    [HttpGet("{id:long}")]
    [Permission("SMS_ZALO_CONFIG", "view")]
    public override async Task<IActionResult> GetById(long id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound(ApiResponse<NotificationChannelConfig>.Fail(Localizer["NotFound"], 404));
        return Ok(ApiResponse<NotificationChannelConfig>.Ok(Mask(entity)));
    }

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SMS_ZALO_CONFIG", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId)
    {
        var entity = await _repo.GetByPublicIdAsync(publicId);
        if (entity == null) return NotFound(ApiResponse<NotificationChannelConfig>.Fail(Localizer["NotFound"], 404));
        return Ok(ApiResponse<NotificationChannelConfig>.Ok(Mask(entity)));
    }

    [HttpPost("Search")]
    [Permission("SMS_ZALO_CONFIG", "view")]
    public override async Task<IActionResult> Search([FromBody] NotificationChannelConfigSearchRequest request)
    {
        var result = await _repo.SearchAsync(request);
        foreach (var item in result.Items) Mask(item);
        return Ok(ApiResponse<PagedResult<NotificationChannelConfig>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("SMS_ZALO_CONFIG", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] NotificationChannelConfigSearchRequest request)
    {
        var items = await _repo.SearchAllAsync(request);
        foreach (var item in items) Mask(item);
        return Ok(ApiResponse<List<NotificationChannelConfig>>.Ok(items));
    }

    private static NotificationChannelConfig Mask(NotificationChannelConfig e)
    {
        if (!string.IsNullOrWhiteSpace(e.SmsAccessToken))  e.SmsAccessToken  = "***";
        if (!string.IsNullOrWhiteSpace(e.ZaloAccessToken)) e.ZaloAccessToken = "***";
        return e;
    }
}
