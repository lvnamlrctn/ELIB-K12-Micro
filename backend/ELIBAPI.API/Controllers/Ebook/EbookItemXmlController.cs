using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookItemXmlController : GenericController<EbookItemXml, EbookItemXmlSearchRequest, EbookItemXmlRequest>
{
    public EbookItemXmlController(IGenericRepository<EbookItemXml, EbookItemXmlSearchRequest, EbookItemXmlRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EBOOKITEMXML", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookItemXmlRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOKITEMXML", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookItemXmlRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOKITEMXML", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOKITEMXML", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EBOOKITEMXML", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOKITEMXML", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOKITEMXML", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookItemXmlSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EBOOKITEMXML", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookItemXmlSearchRequest request) => await base.SearchAll(request);
}

