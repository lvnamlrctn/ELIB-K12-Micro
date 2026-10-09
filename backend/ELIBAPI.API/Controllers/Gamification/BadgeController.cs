using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Gamification;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Gamification;

[Route("api/Gamification/[controller]")]
public class BadgeController : GenericController<Badge, BadgeSearchRequest, BadgeRequest>
{
    public BadgeController(IGenericRepository<Badge, BadgeSearchRequest, BadgeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("BADGES", "add")]
    public override async Task<IActionResult> Add([FromBody] BadgeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("BADGES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BadgeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("BADGES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("BADGES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("BADGES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("BADGES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("BADGES", "view")]
    public override async Task<IActionResult> Search([FromBody] BadgeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("BADGES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] BadgeSearchRequest request) => await base.SearchAll(request);
}
