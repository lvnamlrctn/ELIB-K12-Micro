using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class LinhVucNghienCuuController : GenericController<LinhVucNghienCuu, LinhVucNghienCuuSearchRequest, LinhVucNghienCuuRequest>
{
    public LinhVucNghienCuuController(IGenericRepository<LinhVucNghienCuu, LinhVucNghienCuuSearchRequest, LinhVucNghienCuuRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("LINH_VUC_NGHIEN_CUU", "add")]
    public override async Task<IActionResult> Add([FromBody] LinhVucNghienCuuRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LINH_VUC_NGHIEN_CUU", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] LinhVucNghienCuuRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LINH_VUC_NGHIEN_CUU", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LINH_VUC_NGHIEN_CUU", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("LINH_VUC_NGHIEN_CUU", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("LINH_VUC_NGHIEN_CUU", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("LINH_VUC_NGHIEN_CUU", "view")]
    public override async Task<IActionResult> Search([FromBody] LinhVucNghienCuuSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("LINH_VUC_NGHIEN_CUU", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LinhVucNghienCuuSearchRequest request) => await base.SearchAll(request);
}
