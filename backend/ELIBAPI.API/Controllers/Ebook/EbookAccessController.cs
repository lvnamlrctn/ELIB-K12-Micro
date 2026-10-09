using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookAccessController : GenericController<EbookAccess, EbookAccessSearchRequest, EbookAccessRequest>
{
    public EbookAccessController(IGenericRepository<EbookAccess, EbookAccessSearchRequest, EbookAccessRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EBOOKACCESS", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookAccessRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOKACCESS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookAccessRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOKACCESS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOKACCESS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EBOOKACCESS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOKACCESS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOKACCESS", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookAccessSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EBOOKACCESS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookAccessSearchRequest request) => await base.SearchAll(request);
}

