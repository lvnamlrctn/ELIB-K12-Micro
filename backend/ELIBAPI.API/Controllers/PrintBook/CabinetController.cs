using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CabinetController : GenericController<Cabinet, CabinetSearchRequest, CabinetRequest>
{
    public CabinetController(IGenericRepository<Cabinet, CabinetSearchRequest, CabinetRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CABINETS", "add")]
    public override async Task<IActionResult> Add([FromBody] CabinetRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CABINETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CabinetRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CABINETS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CABINETS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> Search([FromBody] CabinetSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CabinetSearchRequest request) => await base.SearchAll(request);
}
