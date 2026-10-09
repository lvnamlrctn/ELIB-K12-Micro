using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.EOffice;

[Route("api/EOffice/[controller]")]
public class EofficeTopicController : GenericController<EofficeTopic, EofficeTopicSearchRequest, EofficeTopicRequest>
{
    public EofficeTopicController(IGenericRepository<EofficeTopic, EofficeTopicSearchRequest, EofficeTopicRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("EOFFICE_TOPIC", "add")]
    public override async Task<IActionResult> Add([FromBody] EofficeTopicRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EOFFICE_TOPIC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EofficeTopicRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EOFFICE_TOPIC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EOFFICE_TOPIC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("EOFFICE_TOPIC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EOFFICE_TOPIC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EOFFICE_TOPIC", "view")]
    public override async Task<IActionResult> Search([FromBody] EofficeTopicSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("EOFFICE_TOPIC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EofficeTopicSearchRequest request) => await base.SearchAll(request);
}

