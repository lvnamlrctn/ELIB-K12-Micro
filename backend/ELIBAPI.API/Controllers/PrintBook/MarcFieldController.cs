using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class MarcFieldController : GenericController<MarcField, MarcFieldSearchRequest, MarcFieldRequest>
{
    // Từ điển trường MARC được đọc từ 3 màn hình nghiệp vụ (biên mục biểu ghi, đơn đặt, phiếu nhập).
    // Ai làm việc được trên một trong ba màn đó thì đọc được từ điển — không bắt cấp riêng quyền MARC_FIELD.
    private const string ReadCatalogBibs   = "CATALOG_BIBS:edit";
    private const string ReadAbOrders      = "AB_ORDERS:edit";
    private const string ReadAbReceipts    = "AB_RECEIPTS:edit";
    private const string ReadOwnDictionary = "MARC_DICTIONARY:view";

    public MarcFieldController(IGenericRepository<MarcField, MarcFieldSearchRequest, MarcFieldRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("MARC_DICTIONARY", "add")]
    public override async Task<IActionResult> Add([FromBody] MarcFieldRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MARC_DICTIONARY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MarcFieldRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MARC_DICTIONARY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MARC_DICTIONARY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [PermissionAny(ReadCatalogBibs, ReadAbOrders, ReadAbReceipts, ReadOwnDictionary)]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [PermissionAny(ReadCatalogBibs, ReadAbOrders, ReadAbReceipts, ReadOwnDictionary)]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [PermissionAny(ReadCatalogBibs, ReadAbOrders, ReadAbReceipts, ReadOwnDictionary)]
    public override async Task<IActionResult> Search([FromBody] MarcFieldSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [PermissionAny(ReadCatalogBibs, ReadAbOrders, ReadAbReceipts, ReadOwnDictionary)]
    public override async Task<IActionResult> SearchAll([FromBody] MarcFieldSearchRequest request) => await base.SearchAll(request);
}
