using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class PhongBanController : GenericController<PhongBan, PhongBanSearchRequest, PhongBanRequest>
{
    public PhongBanController(IGenericRepository<PhongBan, PhongBanSearchRequest, PhongBanRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PHONG_BAN", "add")]
    public override async Task<IActionResult> Add([FromBody] PhongBanRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PHONG_BAN", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PhongBanRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PHONG_BAN", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PHONG_BAN", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("PHONG_BAN", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PHONG_BAN", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PHONG_BAN", "view")]
    public override async Task<IActionResult> Search([FromBody] PhongBanSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PHONG_BAN", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PhongBanSearchRequest request) => await base.SearchAll(request);
}
