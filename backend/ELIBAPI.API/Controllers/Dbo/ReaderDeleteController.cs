using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Circulation/[controller]")]
public class ReaderDeleteController : GenericController<ReaderDelete, ReaderDeleteSearchRequest, ReaderDeleteRequest>
{
    public ReaderDeleteController(IGenericRepository<ReaderDelete, ReaderDeleteSearchRequest, ReaderDeleteRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("READERDELETE", "add")]
    public override async Task<IActionResult> Add([FromBody] ReaderDeleteRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("READERDELETE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ReaderDeleteRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("READERDELETE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("READERDELETE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("READERDELETE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("READERDELETE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("READERDELETE", "view")]
    public override async Task<IActionResult> Search([FromBody] ReaderDeleteSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("READERDELETE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ReaderDeleteSearchRequest request) => await base.SearchAll(request);
}

