using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class ConfigAacr2Controller : GenericController<ConfigAacr2, ConfigAacr2SearchRequest, ConfigAacr2Request>
{
    public ConfigAacr2Controller(IGenericRepository<ConfigAacr2, ConfigAacr2SearchRequest, ConfigAacr2Request> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CONFIG_AACR2", "add")]
    public override async Task<IActionResult> Add([FromBody] ConfigAacr2Request request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CONFIG_AACR2", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ConfigAacr2Request request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CONFIG_AACR2", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CONFIG_AACR2", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CONFIG_AACR2", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CONFIG_AACR2", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CONFIG_AACR2", "view")]
    public override async Task<IActionResult> Search([FromBody] ConfigAacr2SearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CONFIG_AACR2", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ConfigAacr2SearchRequest request) => await base.SearchAll(request);
}
