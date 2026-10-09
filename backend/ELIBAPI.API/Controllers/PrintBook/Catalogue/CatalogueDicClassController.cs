using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/Catalogue/Dic/DicClass")]
public class CatalogueDicClassController : GenericController<DicClass, DicClassSearchRequest, DicClassRequest>
{
    private readonly ELIBAPIDbContext _db;

    public CatalogueDicClassController(
        IGenericRepository<DicClass, DicClassSearchRequest, DicClassRequest> repo,
        ELIBAPIDbContext db) : base(repo) => _db = db;

    [HttpPost("Add")]
    [Permission("DIC_CLASSES", "add")]
    public override async Task<IActionResult> Add([FromBody] DicClassRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("DIC_CLASSES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DicClassRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("DIC_CLASSES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DIC_CLASSES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> Search([FromBody] DicClassSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("DIC_CLASSES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DicClassSearchRequest request) => await base.SearchAll(request);

    [HttpPost("GetTree")]
    [Permission("DIC_CLASSES", "view")]
    public async Task<IActionResult> GetTree()
    {
        var tenantId   = GetTenantId();
        var privileged = IsPrivilegedRole();
        var all = await _db.DicClasses
            .Where(x => x.IsDelete != 2 && (privileged || x.TenantId == null || x.TenantId == tenantId))
            .OrderBy(x => x.Code)
            .ToListAsync();

        var lookup = all.ToDictionary(x => x.Id, x => new DicClassTreeNode
        {
            Id            = x.Id,
            PublicId      = x.PublicId,
            Code          = x.Code,
            Description   = x.Description,
            VnDescription = x.VnDescription,
            Type          = x.Type
        });

        var roots = new List<DicClassTreeNode>();
        foreach (var node in lookup.Values)
        {
            var item = all.First(x => x.Id == node.Id);
            if (item.ParentId.HasValue && lookup.TryGetValue(item.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        return Ok(ELIBAPI.Core.Common.ApiResponse<List<DicClassTreeNode>>.Ok(roots));
    }
}
