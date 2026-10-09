using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookLogController : GenericController<EbookLog, EbookLogSearchRequest, EbookLogRequest>
{
    public EbookLogController(IGenericRepository<EbookLog, EbookLogSearchRequest, EbookLogRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EBOOKLOG", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookLogRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOKLOG", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookLogRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOKLOG", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOKLOG", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EBOOKLOG", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOKLOG", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOKLOG", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookLogSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EBOOKLOG", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookLogSearchRequest request) => await base.SearchAll(request);
}

