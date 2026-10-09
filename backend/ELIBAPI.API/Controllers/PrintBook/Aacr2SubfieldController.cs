using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class Aacr2SubfieldController : GenericController<Aacr2Subfield, Aacr2SubfieldSearchRequest, Aacr2SubfieldRequest>
{
    public Aacr2SubfieldController(IGenericRepository<Aacr2Subfield, Aacr2SubfieldSearchRequest, Aacr2SubfieldRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AACR2_SUBFIELD", "add")]
    public override async Task<IActionResult> Add([FromBody] Aacr2SubfieldRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AACR2_SUBFIELD", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] Aacr2SubfieldRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AACR2_SUBFIELD", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AACR2_SUBFIELD", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AACR2_SUBFIELD", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AACR2_SUBFIELD", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AACR2_SUBFIELD", "view")]
    public override async Task<IActionResult> Search([FromBody] Aacr2SubfieldSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AACR2_SUBFIELD", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] Aacr2SubfieldSearchRequest request) => await base.SearchAll(request);
}
