using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class IsbdFieldController : GenericController<IsbdField, IsbdFieldSearchRequest, IsbdFieldRequest>
{
    public IsbdFieldController(IGenericRepository<IsbdField, IsbdFieldSearchRequest, IsbdFieldRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CONFIG_ISBD", "add")]
    public override async Task<IActionResult> Add([FromBody] IsbdFieldRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CONFIG_ISBD", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] IsbdFieldRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CONFIG_ISBD", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CONFIG_ISBD", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CONFIG_ISBD", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CONFIG_ISBD", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CONFIG_ISBD", "view")]
    public override async Task<IActionResult> Search([FromBody] IsbdFieldSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CONFIG_ISBD", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] IsbdFieldSearchRequest request) => await base.SearchAll(request);
}
