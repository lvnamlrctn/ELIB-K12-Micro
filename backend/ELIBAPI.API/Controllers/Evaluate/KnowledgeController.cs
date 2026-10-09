using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class KnowledgeController : GenericController<Knowledge, KnowledgeSearchRequest, KnowledgeRequest>
{
    public KnowledgeController(IGenericRepository<Knowledge, KnowledgeSearchRequest, KnowledgeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("KNOWLEDGE", "add")]
    public override async Task<IActionResult> Add([FromBody] KnowledgeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("KNOWLEDGE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] KnowledgeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("KNOWLEDGE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("KNOWLEDGE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("KNOWLEDGE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("KNOWLEDGE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("KNOWLEDGE", "view")]
    public override async Task<IActionResult> Search([FromBody] KnowledgeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("KNOWLEDGE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] KnowledgeSearchRequest request) => await base.SearchAll(request);
}

