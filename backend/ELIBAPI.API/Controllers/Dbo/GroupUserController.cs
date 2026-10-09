using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class GroupUserController : GenericController<GroupUser, GroupUserSearchRequest, GroupUserRequest>
{
    public GroupUserController(IGenericRepository<GroupUser, GroupUserSearchRequest, GroupUserRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("GROUPUSER", "add")]
    public override async Task<IActionResult> Add([FromBody] GroupUserRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("GROUPUSER", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] GroupUserRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("GROUPUSER", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("GROUPUSER", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("GROUPUSER", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("GROUPUSER", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("GROUPUSER", "view")]
    public override async Task<IActionResult> Search([FromBody] GroupUserSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("GROUPUSER", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] GroupUserSearchRequest request) => await base.SearchAll(request);
}

