using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class BarcodeStatusController : GenericController<BarcodeStatus, BarcodeStatusSearchRequest, BarcodeStatusRequest>
{
    // Danh mục trạng thái ĐKCB là từ điển dùng chung: biên mục biểu ghi, đơn đặt, phiếu nhập đều cần đọc,
    // ngoài ra còn dùng để hiển thị nhãn trạng thái ở tìm kiếm tài liệu và lý do phạt.
    private static class Read
    {
        public const string CatalogBibs   = "CATALOG_BIBS:edit";
        public const string AbOrders      = "AB_ORDERS:edit";
        public const string AbReceipts    = "AB_RECEIPTS:edit";
        public const string DocSearch     = "DOC_SEARCH:view";
        public const string FineReasons   = "FINE_REASONS:view";
        public const string OwnDictionary = "AB_RECEIPTS:view";
    }

    public BarcodeStatusController(IGenericRepository<BarcodeStatus, BarcodeStatusSearchRequest, BarcodeStatusRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AB_RECEIPTS", "add")]
    public override async Task<IActionResult> Add([FromBody] BarcodeStatusRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] BarcodeStatusRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_RECEIPTS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_RECEIPTS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [PermissionAny(Read.CatalogBibs, Read.AbOrders, Read.AbReceipts, Read.DocSearch, Read.FineReasons, Read.OwnDictionary)]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [PermissionAny(Read.CatalogBibs, Read.AbOrders, Read.AbReceipts, Read.DocSearch, Read.FineReasons, Read.OwnDictionary)]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [PermissionAny(Read.CatalogBibs, Read.AbOrders, Read.AbReceipts, Read.DocSearch, Read.FineReasons, Read.OwnDictionary)]
    public override async Task<IActionResult> Search([FromBody] BarcodeStatusSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [PermissionAny(Read.CatalogBibs, Read.AbOrders, Read.AbReceipts, Read.DocSearch, Read.FineReasons, Read.OwnDictionary)]
    public override async Task<IActionResult> SearchAll([FromBody] BarcodeStatusSearchRequest request) => await base.SearchAll(request);
}
