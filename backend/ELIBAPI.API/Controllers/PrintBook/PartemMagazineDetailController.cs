using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class PartemMagazineDetailController : GenericController<PartemMagazineDetail, PartemMagazineDetailSearchRequest, PartemMagazineDetailRequest>
{
    public PartemMagazineDetailController(IGenericRepository<PartemMagazineDetail, PartemMagazineDetailSearchRequest, PartemMagazineDetailRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "add")]
    public override async Task<IActionResult> Add([FromBody] PartemMagazineDetailRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PartemMagazineDetailRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "view")]
    public override async Task<IActionResult> Search([FromBody] PartemMagazineDetailSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PARTEM_MAGAZINE_DETAIL", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PartemMagazineDetailSearchRequest request) => await base.SearchAll(request);
}
