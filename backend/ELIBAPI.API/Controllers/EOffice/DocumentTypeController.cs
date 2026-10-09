using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.EOffice;

[Route("api/EOffice/[controller]")]
public class DocumentTypeController : GenericController<DocumentType, DocumentTypeSearchRequest, DocumentTypeRequest>
{
    public DocumentTypeController(IGenericRepository<DocumentType, DocumentTypeSearchRequest, DocumentTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DOCUMENTTYPE", "add")]
    public override async Task<IActionResult> Add([FromBody] DocumentTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DOCUMENTTYPE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DocumentTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DOCUMENTTYPE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DOCUMENTTYPE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("DOCUMENTTYPE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DOCUMENTTYPE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DOCUMENTTYPE", "view")]
    public override async Task<IActionResult> Search([FromBody] DocumentTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DOCUMENTTYPE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DocumentTypeSearchRequest request) => await base.SearchAll(request);
}

