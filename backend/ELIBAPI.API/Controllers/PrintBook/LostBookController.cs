using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class LostBookController : GenericController<LostBook, LostBookSearchRequest, LostBookRequest>
{
    public LostBookController(IGenericRepository<LostBook, LostBookSearchRequest, LostBookRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("LOST_BOOKS", "add")]
    public override async Task<IActionResult> Add([FromBody] LostBookRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LOST_BOOKS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] LostBookRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LOST_BOOKS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LOST_BOOKS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> Search([FromBody] LostBookSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("LOST_BOOKS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LostBookSearchRequest request) => await base.SearchAll(request);
}
