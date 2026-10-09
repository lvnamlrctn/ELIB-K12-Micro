using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ScheduledReportController : GenericController<ScheduledReport, ScheduledReportSearchRequest, ScheduledReportRequest>
{
    public ScheduledReportController(IGenericRepository<ScheduledReport, ScheduledReportSearchRequest, ScheduledReportRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SCHEDULED_REPORT", "add")]
    public override async Task<IActionResult> Add([FromBody] ScheduledReportRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SCHEDULED_REPORT", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ScheduledReportRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SCHEDULED_REPORT", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SCHEDULED_REPORT", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SCHEDULED_REPORT", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SCHEDULED_REPORT", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SCHEDULED_REPORT", "view")]
    public override async Task<IActionResult> Search([FromBody] ScheduledReportSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SCHEDULED_REPORT", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ScheduledReportSearchRequest request) => await base.SearchAll(request);
}
