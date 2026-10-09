using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class MetadataSchemaRegistryController : GenericController<MetadataSchemaRegistry, MetadataSchemaRegistrySearchRequest, MetadataSchemaRegistryRequest>
{
    public MetadataSchemaRegistryController(IGenericRepository<MetadataSchemaRegistry, MetadataSchemaRegistrySearchRequest, MetadataSchemaRegistryRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("METADATA_SCHEMA", "add")]
    public override async Task<IActionResult> Add([FromBody] MetadataSchemaRegistryRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("METADATA_SCHEMA", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MetadataSchemaRegistryRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("METADATA_SCHEMA", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("METADATA_SCHEMA", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("METADATA_SCHEMA", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("METADATA_SCHEMA", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("METADATA_SCHEMA", "view")]
    public override async Task<IActionResult> Search([FromBody] MetadataSchemaRegistrySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("METADATA_SCHEMA", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MetadataSchemaRegistrySearchRequest request) => await base.SearchAll(request);
}

