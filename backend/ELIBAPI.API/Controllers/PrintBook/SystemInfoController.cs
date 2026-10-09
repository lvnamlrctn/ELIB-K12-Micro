using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class SystemInfoController : GenericController<SystemInfo, SystemInfoSearchRequest, SystemInfoRequest>
{
    public SystemInfoController(IGenericRepository<SystemInfo, SystemInfoSearchRequest, SystemInfoRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SYSTEM_INFO", "add")]
    public override async Task<IActionResult> Add([FromBody] SystemInfoRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SYSTEM_INFO", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SystemInfoRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SYSTEM_INFO", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SYSTEM_INFO", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SYSTEM_INFO", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SYSTEM_INFO", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SYSTEM_INFO", "view")]
    public override async Task<IActionResult> Search([FromBody] SystemInfoSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SYSTEM_INFO", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SystemInfoSearchRequest request) => await base.SearchAll(request);
}
