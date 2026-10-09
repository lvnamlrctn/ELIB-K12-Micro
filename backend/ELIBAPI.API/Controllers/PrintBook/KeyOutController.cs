using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class KeyOutController : GenericController<KeyOut, KeyOutSearchRequest, KeyOutRequest>
{
    public KeyOutController(IGenericRepository<KeyOut, KeyOutSearchRequest, KeyOutRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("BORROW_KEYS", "add")]
    public override async Task<IActionResult> Add([FromBody] KeyOutRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BORROW_KEYS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] KeyOutRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BORROW_KEYS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("BORROW_KEYS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("BORROW_KEYS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BORROW_KEYS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("BORROW_KEYS", "view")]
    public override async Task<IActionResult> Search([FromBody] KeyOutSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BORROW_KEYS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] KeyOutSearchRequest request) => await base.SearchAll(request);
}
