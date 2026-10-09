using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

[Route("api/Evaluate/[controller]")]
public class NganhMonHocController : GenericController<NganhMonHoc, NganhMonHocSearchRequest, NganhMonHocRequest>
{
    public NganhMonHocController(IGenericRepository<NganhMonHoc, NganhMonHocSearchRequest, NganhMonHocRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("NGANHHOC", "add")]
    public override async Task<IActionResult> Add([FromBody] NganhMonHocRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("NGANHHOC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] NganhMonHocRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("NGANHHOC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("NGANHHOC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("NGANHHOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("NGANHHOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("NGANHHOC", "view")]
    public override async Task<IActionResult> Search([FromBody] NganhMonHocSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("NGANHHOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] NganhMonHocSearchRequest request) => await base.SearchAll(request);
}

