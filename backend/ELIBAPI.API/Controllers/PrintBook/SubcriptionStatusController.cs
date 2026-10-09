using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class SubcriptionStatusController : GenericController<SubcriptionStatus, SubcriptionStatusSearchRequest, SubcriptionStatusRequest>
{
    public SubcriptionStatusController(IGenericRepository<SubcriptionStatus, SubcriptionStatusSearchRequest, SubcriptionStatusRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SUBCRIPTION_STATUS", "add")]
    public override async Task<IActionResult> Add([FromBody] SubcriptionStatusRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SUBCRIPTION_STATUS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SubcriptionStatusRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SUBCRIPTION_STATUS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SUBCRIPTION_STATUS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SUBCRIPTION_STATUS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SUBCRIPTION_STATUS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SUBCRIPTION_STATUS", "view")]
    public override async Task<IActionResult> Search([FromBody] SubcriptionStatusSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SUBCRIPTION_STATUS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SubcriptionStatusSearchRequest request) => await base.SearchAll(request);
}
