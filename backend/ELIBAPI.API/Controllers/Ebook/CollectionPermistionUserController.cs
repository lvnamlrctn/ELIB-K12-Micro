using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class CollectionPermistionUserController : GenericController<CollectionPermistionUser, CollectionPermistionUserSearchRequest, CollectionPermistionUserRequest>
{
    public CollectionPermistionUserController(IGenericRepository<CollectionPermistionUser, CollectionPermistionUserSearchRequest, CollectionPermistionUserRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EBOOK_COLLECTION", "add")]
    public override async Task<IActionResult> Add([FromBody] CollectionPermistionUserRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOK_COLLECTION", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CollectionPermistionUserRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOK_COLLECTION", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOK_COLLECTION", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> Search([FromBody] CollectionPermistionUserSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EBOOK_COLLECTION", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CollectionPermistionUserSearchRequest request) => await base.SearchAll(request);
}

