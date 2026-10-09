using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DExportReasonController : GenericController<DExportReason, DExportReasonSearchRequest, DExportReasonRequest>
{
    public DExportReasonController(IGenericRepository<DExportReason, DExportReasonSearchRequest, DExportReasonRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EXPORT_REASON", "add")]
    public override async Task<IActionResult> Add([FromBody] DExportReasonRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EXPORT_REASON", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DExportReasonRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EXPORT_REASON", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EXPORT_REASON", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("EXPORT_REASON", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EXPORT_REASON", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EXPORT_REASON", "view")]
    public override async Task<IActionResult> Search([FromBody] DExportReasonSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EXPORT_REASON", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DExportReasonSearchRequest request) => await base.SearchAll(request);
}
