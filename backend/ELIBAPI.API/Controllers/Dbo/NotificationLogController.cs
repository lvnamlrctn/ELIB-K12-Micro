using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class NotificationLogController
    : GenericController<NotificationLog, NotificationLogSearchRequest, NotificationLogRequest>
{
    public NotificationLogController(IGenericRepository<NotificationLog, NotificationLogSearchRequest, NotificationLogRequest> repo) : base(repo) { }

    // ── Chỉ đọc — ghi duy nhất từ NotificationDispatcher, không có Add/Update/Delete/ChangeStatus ──

    [HttpGet("{id:long}")]
    [Permission("NOTIFICATION_LOG", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("NOTIFICATION_LOG", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("NOTIFICATION_LOG", "view")]
    public override async Task<IActionResult> Search([FromBody] NotificationLogSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("NOTIFICATION_LOG", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] NotificationLogSearchRequest request) => await base.SearchAll(request);
}
