using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class SystemParaController : GenericController<SystemPara, SystemParaSearchRequest, SystemParaRequest>
{
    public SystemParaController(IGenericRepository<SystemPara, SystemParaSearchRequest, SystemParaRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SYSTEM_PARA", "add")]
    public override async Task<IActionResult> Add([FromBody] SystemParaRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SYSTEM_PARA", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SystemParaRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SYSTEM_PARA", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SYSTEM_PARA", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("SYSTEM_PARA", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SYSTEM_PARA", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SYSTEM_PARA", "view")]
    public override async Task<IActionResult> Search([FromBody] SystemParaSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SYSTEM_PARA", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SystemParaSearchRequest request) => await base.SearchAll(request);
}
