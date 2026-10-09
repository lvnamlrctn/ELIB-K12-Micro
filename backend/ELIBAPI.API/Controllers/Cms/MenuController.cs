using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class MenuController : GenericController<Menu, MenuSearchRequest, MenuRequest>
{
    private readonly IMenuRepository _menuRepo;
    private readonly ELIBAPIDbContext _db;

    public MenuController(IMenuRepository repo, ELIBAPIDbContext db) : base(repo)
    {
        _menuRepo = repo;
        _db = db;
    }

    [HttpPost("Add")]
    [Permission("MENU_TYPES", "add")]
    public override async Task<IActionResult> Add([FromBody] MenuRequest request)
    {
        var err = await ResolveLinkAsync(request);
        if (err != null) return BadRequest(ApiResponse<object>.Fail(err));
        return await base.Add(request);
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MENU_TYPES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MenuRequest request)
    {
        var err = await ResolveLinkAsync(request);
        if (err != null) return BadRequest(ApiResponse<object>.Fail(err));
        return await base.Update(publicId, request);
    }

    [HttpDelete("Delete/{publicId:guid}")] [Permission("MENU_TYPES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("MENU_TYPES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> Search([FromBody] MenuSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MENU_TYPES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MenuSearchRequest request) => await base.SearchAll(request);

    [HttpPost("GetTree")]
    [Permission("MENU_TYPES", "view")]
    public async Task<IActionResult> GetTree([FromBody] MenuSearchRequest request)
    {
        var tree = await _menuRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<MenuTreeResponse>>.Ok(tree));
    }

    private const string UnsafeLink = "Link chỉ nhận địa chỉ http(s)://, đường dẫn bắt đầu bằng \"/\" hoặc từ khoá trang";

    /// <summary>Kiểm tra và dựng <c>Link</c> theo <c>LinkType</c> (port ELIB-LRC 10-04):
    /// <list type="bullet">
    /// <item>liên kết chỉ nhận http(s)://, đường dẫn "/…" hoặc từ khoá — trước đây nhận mọi chuỗi, kể cả "javascript:…" và
    /// "//host" (cổng OPAC hiển thị làm href);</item>
    /// <item>menu di trú từ hệ thống cũ: OuterLink = external; CategoryLink/LinkPage giữ nguyên liên kết cũ — trước đây sửa bất
    /// kỳ menu cũ nào cũng bị báo "LinkType không hợp lệ";</item>
    /// <item>chuyên mục / bộ sưu tập / trang phải tồn tại. Tenant: thuộc đơn vị người sửa hoặc dùng chung.</item>
    /// </list></summary>
    private async Task<string?> ResolveLinkAsync(MenuRequest r)
    {
        var tenantId = IsPrivilegedRole() ? null : GetTenantId();
        var lt = (r.LinkType ?? "").Trim().ToLower();
        if (lt == "outerlink") { r.LinkType = "external"; lt = "external"; }
        if (lt is "categorylink" or "linkpage")
            return string.IsNullOrWhiteSpace(r.Link) || IsSafeLink(r.Link.Trim()) ? null : UnsafeLink;

        switch (lt)
        {
            case "external":
                r.SubId = null;
                if (string.IsNullOrWhiteSpace(r.Link))
                    return "Khi LinkType=external, Link (URL) không được trống";
                r.Link = r.Link.Trim();
                if (!IsSafeLink(r.Link)) return UnsafeLink;
                break;

            case "category":
            {
                if (!Guid.TryParse(r.SubId, out var id))
                    return "Khi LinkType=category, SubId phải là GUID publicId của Category";
                if (!await _db.Categories.AnyAsync(c => c.PublicId == id && c.IsDelete != 2
                        && (!tenantId.HasValue || c.TenantId == null || c.TenantId == tenantId)))
                    return "Không tìm thấy chuyên mục với SubId đã cho";
                r.Link = $"/category/{r.SubId}";
                break;
            }

            case "collection":
            {
                if (!Guid.TryParse(r.SubId, out var id))
                    return "Khi LinkType=collection, SubId phải là GUID publicId của Collection";
                if (!await _db.EbookCollections.AnyAsync(c => c.PublicId == id && c.IsDelete != 2
                        && (!tenantId.HasValue || c.TenantId == null || c.TenantId == tenantId)))
                    return "Không tìm thấy bộ sưu tập với SubId đã cho";
                r.Link = $"/search?collection={r.SubId}";
                break;
            }

            case "page":
                if (!Guid.TryParse(r.SubId, out var pageGuid))
                    return "Khi LinkType=page, SubId phải là GUID publicId của Page";
                var pageCode = await _db.Set<Page>()
                    .Where(p => p.PublicId == pageGuid && p.IsDelete != 2
                             && (!tenantId.HasValue || p.TenantId == null || p.TenantId == tenantId))
                    .Select(p => p.PageCode)
                    .FirstOrDefaultAsync();
                if (string.IsNullOrEmpty(pageCode))
                    return "Không tìm thấy trang với SubId đã cho";
                r.Link = $"/{pageCode}";
                break;

            case "":
                if (!string.IsNullOrWhiteSpace(r.Link) && !IsSafeLink(r.Link.Trim())) return UnsafeLink;
                break;

            default:
                return $"LinkType '{r.LinkType}' không hợp lệ. Giá trị hợp lệ: external, page, category, collection";
        }
        return null;
    }

    /// <summary>http(s) tuyệt đối, đường dẫn trong site ("/…", không phải "//host"), hoặc từ khoá không có dấu ":".</summary>
    internal static bool IsSafeLink(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        || link.StartsWith('/') && !link.StartsWith("//") && !link.StartsWith("/\\")
        || !link.Contains(':') && !link.Contains('/') && !link.Any(char.IsWhiteSpace);
}
