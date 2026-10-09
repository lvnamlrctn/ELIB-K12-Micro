using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Magazine/Frequency")]
public class MagazineFrequencyController : GenericController<FrequencyMagazine, FrequencyMagazineSearchRequest, FrequencyMagazineRequest>
{
    public MagazineFrequencyController(IGenericRepository<FrequencyMagazine, FrequencyMagazineSearchRequest, FrequencyMagazineRequest> repo) : base(repo) { }

    [HttpPost("Add")] [Permission("FREQUENCIES", "add")]
    public override async Task<IActionResult> Add([FromBody] FrequencyMagazineRequest r) => await base.Add(r);

    [HttpPut("Update/{publicId:guid}")] [Permission("FREQUENCIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] FrequencyMagazineRequest r) => await base.Update(publicId, r);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("FREQUENCIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("FREQUENCIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest r) => await base.ChangeStatus(r);

    [HttpGet("{id:long}")] [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")] [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> Search([FromBody] FrequencyMagazineSearchRequest r) => await base.Search(r);

    [HttpPost("SearchAll")] [Permission("FREQUENCIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] FrequencyMagazineSearchRequest r) => await base.SearchAll(r);
}
