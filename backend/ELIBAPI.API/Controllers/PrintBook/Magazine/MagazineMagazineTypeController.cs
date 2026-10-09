using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Magazine/MagazineType")]
public class MagazineMagazineTypeController : GenericController<MagazineType, MagazineTypeSearchRequest, MagazineTypeRequest>
{
    public MagazineMagazineTypeController(IGenericRepository<MagazineType, MagazineTypeSearchRequest, MagazineTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")] [Permission("MAGAZINE_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] MagazineTypeRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("MAGAZINE_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MagazineTypeRequest r) => await base.Update(publicId, r);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("MAGAZINE_TYPES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("MAGAZINE_TYPES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    [HttpGet("{id:long}")] [Permission("MAGAZINE_TYPES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("MAGAZINE_TYPES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")] [Permission("MAGAZINE_TYPES", "view")]
    public override async Task<IActionResult> Search([FromBody] MagazineTypeSearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("MAGAZINE_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MagazineTypeSearchRequest r) => await base.SearchAll(r);
}
