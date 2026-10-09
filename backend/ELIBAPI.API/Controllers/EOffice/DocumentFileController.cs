using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.EOffice;

[Route("api/EOffice/[controller]")]
public class DocumentFileController : GenericController<DocumentFile, DocumentFileSearchRequest, DocumentFileRequest>
{
    public DocumentFileController(IGenericRepository<DocumentFile, DocumentFileSearchRequest, DocumentFileRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DOCUMENTFILE", "add")]
    public override async Task<IActionResult> Add([FromBody] DocumentFileRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DOCUMENTFILE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DocumentFileRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DOCUMENTFILE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DOCUMENTFILE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("DOCUMENTFILE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DOCUMENTFILE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DOCUMENTFILE", "view")]
    public override async Task<IActionResult> Search([FromBody] DocumentFileSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DOCUMENTFILE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DocumentFileSearchRequest request) => await base.SearchAll(request);
}

