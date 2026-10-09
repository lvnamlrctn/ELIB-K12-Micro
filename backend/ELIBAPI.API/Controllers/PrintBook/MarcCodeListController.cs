using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class MarcCodeListController : GenericController<MarcCodeList, MarcCodeListSearchRequest, MarcCodeListRequest>
{
    public MarcCodeListController(IGenericRepository<MarcCodeList, MarcCodeListSearchRequest, MarcCodeListRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("MARC_CODE_LIST", "add")]
    public override async Task<IActionResult> Add([FromBody] MarcCodeListRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MARC_CODE_LIST", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MarcCodeListRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MARC_CODE_LIST", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MARC_CODE_LIST", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("MARC_CODE_LIST", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MARC_CODE_LIST", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MARC_CODE_LIST", "view")]
    public override async Task<IActionResult> Search([FromBody] MarcCodeListSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MARC_CODE_LIST", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MarcCodeListSearchRequest request) => await base.SearchAll(request);
}
