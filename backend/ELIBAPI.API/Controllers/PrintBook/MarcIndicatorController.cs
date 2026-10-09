using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class MarcIndicatorController : GenericController<MarcIndicator, MarcIndicatorSearchRequest, MarcIndicatorRequest>
{
    public MarcIndicatorController(IGenericRepository<MarcIndicator, MarcIndicatorSearchRequest, MarcIndicatorRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("MARC_DICTIONARY", "add")]
    public override async Task<IActionResult> Add([FromBody] MarcIndicatorRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MARC_DICTIONARY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MarcIndicatorRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MARC_DICTIONARY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MARC_DICTIONARY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("MARC_DICTIONARY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MARC_DICTIONARY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MARC_DICTIONARY", "view")]
    public override async Task<IActionResult> Search([FromBody] MarcIndicatorSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MARC_DICTIONARY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MarcIndicatorSearchRequest request) => await base.SearchAll(request);
}
