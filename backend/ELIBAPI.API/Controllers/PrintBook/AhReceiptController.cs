using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class AhReceiptController : GenericController<AhReceipt, AhReceiptSearchRequest, AhReceiptRequest>
{
    public AhReceiptController(IGenericRepository<AhReceipt, AhReceiptSearchRequest, AhReceiptRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AH_RECEIPT", "add")]
    public override async Task<IActionResult> Add([FromBody] AhReceiptRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AH_RECEIPT", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AhReceiptRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AH_RECEIPT", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AH_RECEIPT", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AH_RECEIPT", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AH_RECEIPT", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AH_RECEIPT", "view")]
    public override async Task<IActionResult> Search([FromBody] AhReceiptSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AH_RECEIPT", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AhReceiptSearchRequest request) => await base.SearchAll(request);
}
