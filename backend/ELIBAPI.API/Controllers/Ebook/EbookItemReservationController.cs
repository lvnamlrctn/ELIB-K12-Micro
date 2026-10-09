using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookItemReservationController
    : GenericController<EbookItemReservation, EbookItemReservationSearchRequest, EbookItemReservationRequest>
{
    public EbookItemReservationController(IEbookItemReservationRepository repo) : base(repo) { }

    // ── Xem — dùng chung ModuleCode DIGITAL_DOC (bảng con của trang Tài liệu số, không có menu riêng) ──

    [HttpGet("{id:long}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookItemReservationSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIGITAL_DOC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookItemReservationSearchRequest request) => await base.SearchAll(request);
}
