using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class ConfigImportReaderController : GenericController<ConfigImportReader, ConfigImportReaderSearchRequest, ConfigImportReaderRequest>
{
    public ConfigImportReaderController(IGenericRepository<ConfigImportReader, ConfigImportReaderSearchRequest, ConfigImportReaderRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CONFIGIMPORTREADER", "add")]
    public override async Task<IActionResult> Add([FromBody] ConfigImportReaderRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CONFIGIMPORTREADER", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ConfigImportReaderRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CONFIGIMPORTREADER", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CONFIGIMPORTREADER", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CONFIGIMPORTREADER", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CONFIGIMPORTREADER", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CONFIGIMPORTREADER", "view")]
    public override async Task<IActionResult> Search([FromBody] ConfigImportReaderSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CONFIGIMPORTREADER", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ConfigImportReaderSearchRequest request) => await base.SearchAll(request);
}

