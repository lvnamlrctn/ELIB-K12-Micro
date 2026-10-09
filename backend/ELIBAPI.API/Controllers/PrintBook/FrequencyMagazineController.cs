using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class FrequencyMagazineController : GenericController<FrequencyMagazine, FrequencyMagazineSearchRequest, FrequencyMagazineRequest>
{
    public FrequencyMagazineController(IGenericRepository<FrequencyMagazine, FrequencyMagazineSearchRequest, FrequencyMagazineRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("FREQUENCIES", "add")]
    public override async Task<IActionResult> Add([FromBody] FrequencyMagazineRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("FREQUENCIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] FrequencyMagazineRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("FREQUENCIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("FREQUENCIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> Search([FromBody] FrequencyMagazineSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] FrequencyMagazineSearchRequest request) => await base.SearchAll(request);
}
