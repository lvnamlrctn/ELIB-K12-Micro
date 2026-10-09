using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class ConfigReceiptionController(IGenericRepository<ConfigReceiption, ConfigReceiptionSearchRequest, ConfigReceiptionRequest> repo)
    : GenericController<ConfigReceiption, ConfigReceiptionSearchRequest, ConfigReceiptionRequest>(repo)
{
    [HttpPost("Add")]    [Permission("CONFIG_RECEIPTION", "add")]
    public override Task<IActionResult> Add([FromBody] ConfigReceiptionRequest request) => base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("CONFIG_RECEIPTION", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] ConfigReceiptionRequest request) => base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("CONFIG_RECEIPTION", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("CONFIG_RECEIPTION", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]                  [Permission("CONFIG_RECEIPTION", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]    [Permission("CONFIG_RECEIPTION", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("CONFIG_RECEIPTION", "view")]
    public override Task<IActionResult> Search([FromBody] ConfigReceiptionSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")] [Permission("CONFIG_RECEIPTION", "view")]
    public override Task<IActionResult> SearchAll([FromBody] ConfigReceiptionSearchRequest request) => base.SearchAll(request);
}
