using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BookRequestController : GenericController<BookRequest, BookRequestSearchRequest, BookRequestRequest>
{
    public BookRequestController(IGenericRepository<BookRequest, BookRequestSearchRequest, BookRequestRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("REQUEST_BOOKS", "add")]
    public override async Task<IActionResult> Add([FromBody] BookRequestRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BookRequestRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("REQUEST_BOOKS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> Search([FromBody] BookRequestSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("REQUEST_BOOKS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BookRequestSearchRequest request) => await base.SearchAll(request);
}
