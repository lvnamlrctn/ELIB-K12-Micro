using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class MenuTypeController : GenericController<MenuType, MenuTypeSearchRequest, MenuTypeRequest>
{
    private readonly IMenuTypeRepository _menuTypeRepo;

    public MenuTypeController(IMenuTypeRepository repo) : base(repo) => _menuTypeRepo = repo;

    [HttpPost("Add")]    [Permission("MENU_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] MenuTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("MENU_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MenuTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("MENU_TYPES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("MENU_TYPES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")] [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> Search([FromBody] MenuTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
     [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MenuTypeSearchRequest request) => await base.SearchAll(request);

    [HttpGet("CheckCodeExists")] [Permission("MENU_TYPES", "view")]
    public async Task<IActionResult> CheckCodeExists([FromQuery] string code, [FromQuery] Guid? excludePublicId = null)
    {
        var exists = await _menuTypeRepo.CheckCodeExistsAsync(code, excludePublicId);
        return Ok(ApiResponse<bool>.Ok(exists));
    }
}
