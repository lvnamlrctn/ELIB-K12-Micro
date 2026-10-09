using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class PolicyDigitalByCollectionController : GenericController<PolicyDigitalByCollection, PolicyDigitalByCollectionSearchRequest, PolicyDigitalByCollectionRequest>
{
    public PolicyDigitalByCollectionController(IGenericRepository<PolicyDigitalByCollection, PolicyDigitalByCollectionSearchRequest, PolicyDigitalByCollectionRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("ACCESS_POLICY", "add")]
    public override async Task<IActionResult> Add([FromBody] PolicyDigitalByCollectionRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PolicyDigitalByCollectionRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ACCESS_POLICY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> Search([FromBody] PolicyDigitalByCollectionSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PolicyDigitalByCollectionSearchRequest request) => await base.SearchAll(request);
}

