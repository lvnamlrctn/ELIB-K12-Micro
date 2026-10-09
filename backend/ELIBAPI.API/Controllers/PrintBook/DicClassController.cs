using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DicClassController : GenericController<DicClass, DicClassSearchRequest, DicClassRequest>
{
    public DicClassController(IGenericRepository<DicClass, DicClassSearchRequest, DicClassRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("DIC_CLASSES", "add")]
    public override async Task<IActionResult> Add([FromBody] DicClassRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIC_CLASSES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DicClassRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIC_CLASSES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DIC_CLASSES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> Search([FromBody] DicClassSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DicClassSearchRequest request) => await base.SearchAll(request);
}
