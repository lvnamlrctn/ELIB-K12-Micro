using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class VideoController : GenericController<Video, VideoSearchRequest, VideoRequest>
{
    public VideoController(IGenericRepository<Video, VideoSearchRequest, VideoRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("VIDEO", "add")]
    public override async Task<IActionResult> Add([FromBody] VideoRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("VIDEO", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] VideoRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("VIDEO", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("VIDEO", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("VIDEO", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("VIDEO", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("VIDEO", "view")]
    public override async Task<IActionResult> Search([FromBody] VideoSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("VIDEO", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] VideoSearchRequest request) => await base.SearchAll(request);
}

