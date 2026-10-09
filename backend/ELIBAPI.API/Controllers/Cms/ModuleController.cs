using System.Security.Claims;
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
public class ModuleController : GenericController<Module, ModuleSearchRequest, ModuleRequest>
{
    private readonly IModuleRepository _moduleRepo;
    private readonly ELIBAPIDbContext _db;

    public ModuleController(IModuleRepository repo, ELIBAPIDbContext db) : base(repo)
    {
        _moduleRepo = repo;
        _db = db;
    }

    [HttpPost("Add")]    [Permission("MODULES", "add")]
    public override Task<IActionResult> Add([FromBody] ModuleRequest request) => base.Add(request);

    [InvalidatePermissionStamps] [HttpPut("Update/{publicId:guid}")] [Permission("MODULES", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] ModuleRequest request) => base.Update(publicId, request);

    [InvalidatePermissionStamps] [HttpDelete("Delete/{publicId:guid}")] [Permission("MODULES", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [InvalidatePermissionStamps] [HttpPut("ChangeStatus")] [Permission("MODULES", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]               [Permission("MODULES", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("MODULES", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("MODULES", "view")]
    public override Task<IActionResult> Search([FromBody] ModuleSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")] [Permission("MODULES", "view")]
    public override Task<IActionResult> SearchAll([FromBody] ModuleSearchRequest request) => base.SearchAll(request);

    // ── GetTree ───────────────────────────────────────────────────────────────

    [HttpPost("GetTree")]
    public async Task<IActionResult> GetTree([FromBody] ModuleSearchRequest request)
    {
        var tree = await _moduleRepo.GetTreeAsync(request);

        var config         = HttpContext.RequestServices.GetService<IConfiguration>();
        var roleCode       = User.FindFirstValue("RoleCode");
        var adminRoleCodes = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var hiddenCodes    = config?.GetSection("ReadOnlyPolicy:AdminModuleCodesHidden").Get<string[]>() ?? [];
        var isAdmin        = roleCode != null && adminRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
        if (!isAdmin && hiddenCodes.Length > 0)
            tree = FilterHiddenModules(tree, hiddenCodes);

        return Ok(ApiResponse<List<ModuleTreeResponse>>.Ok(tree));
    }

    private static List<ModuleTreeResponse> FilterHiddenModules(List<ModuleTreeResponse> nodes, string[] hiddenCodes)
        => nodes
            .Where(n => !hiddenCodes.Contains(n.ModuleCode, StringComparer.OrdinalIgnoreCase))
            .Select(n => { n.Children = FilterHiddenModules(n.Children, hiddenCodes); return n; })
            .ToList();

    // ── UpdateOrder ───────────────────────────────────────────────────────────

    [HttpPut("UpdateOrder")]
    [Permission("MODULES", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _moduleRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    [HttpPut("Move/{publicId:guid}")]
    [Permission("MODULES", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _moduleRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<Module>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<Module>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<Module>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── DeleteWithChildren ────────────────────────────────────────────────────

    [InvalidatePermissionStamps] [HttpDelete("DeleteWithChildren/{publicId:guid}")]
    [Permission("MODULES", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _moduleRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── BuildModuleCms (existing) ─────────────────────────────────────────────

    [HttpGet("BuildModuleCms")]
    public async Task<IActionResult> BuildModuleCms()
    {
        var modules  = await _repo.SearchAllAsync(new ModuleSearchRequest { Status = 2 });

        var config         = HttpContext.RequestServices.GetService<IConfiguration>();
        var roleCode       = User.FindFirstValue("RoleCode");
        var adminRoleCodes = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var hiddenCodes    = config?.GetSection("ReadOnlyPolicy:AdminModuleCodesHidden").Get<string[]>() ?? [];
        var isAdmin        = roleCode != null && adminRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
        if (!isAdmin && hiddenCodes.Length > 0)
            modules = modules.Where(m => !hiddenCodes.Contains(m.ModuleCode, StringComparer.OrdinalIgnoreCase)).ToList();

        var byParent = modules.GroupBy(x => x.ParentId ?? 0).ToDictionary(g => g.Key, g => g.ToList());

        List<ModuleItemResponse> BuildNodes(long parentId) =>
            byParent.TryGetValue(parentId, out var children)
                ? children.ConvertAll(m =>
                  {
                      var childNodes = BuildNodes(m.Id);
                      return new ModuleItemResponse
                      {
                          Title    = m.Name,
                          Icon     = m.Icon,
                          Link     = m.Link,
                          Expanded = childNodes.Count > 0 ? true : null,
                          Children = childNodes.Count > 0 ? childNodes : null
                      };
                  })
                : [];

        return Ok(ApiResponse<List<ModuleItemResponse>>.Ok(BuildNodes(0)));
    }

    // ── SyncDiff / SyncApply ──────────────────────────────────────────────────
    // So sánh cms.Module hiện có với danh sách chuẩn ModuleCatalog (khớp menu.ts) — chỉ báo thiếu/thừa,
    // KHÔNG tự xoá gì. Đây là thao tác an toàn, có thể bấm lại nhiều lần không hại gì.

    [HttpGet("SyncDiff")]
    [Permission("MODULES", "view")]
    public async Task<IActionResult> SyncDiff()
    {
        var existingCodes = await _db.Modules
            .Where(x => x.IsDelete != 2 && x.ModuleCode != null)
            .Select(x => x.ModuleCode!).ToListAsync();
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalogSet  = ModuleCatalog.All.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing  = ModuleCatalog.All.Where(x => !existingSet.Contains(x.Code))
            .Select(x => new { code = x.Code, name = x.Name, parentCode = x.ParentCode }).ToList();
        var orphaned = existingCodes.Where(c => !catalogSet.Contains(c)).Distinct().OrderBy(c => c).ToList();

        return Ok(ApiResponse<object>.Ok(new { missing, orphaned }));
    }

    [InvalidatePermissionStamps] [HttpPost("SyncApply")]
    [Permission("MODULES", "add")]
    public async Task<IActionResult> SyncApply()
    {
        var existingCodes = await _db.Modules
            .Where(x => x.IsDelete != 2 && x.ModuleCode != null)
            .Select(x => x.ModuleCode!).ToListAsync();
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = ModuleCatalog.All.Where(x => !existingSet.Contains(x.Code)).ToList();
        if (missing.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { created = 0 }, "Không có module nào cần tạo thêm."));

        // Map ModuleCode -> Id để gán ParentId đúng, kể cả khi cha vừa được tạo trong cùng lần chạy này
        // (ModuleCatalog.All đã được liệt kê theo thứ tự cha trước con).
        var codeToId = await _db.Modules
            .Where(x => x.IsDelete != 2 && x.ModuleCode != null)
            .ToDictionaryAsync(x => x.ModuleCode!, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        foreach (var entry in missing)
        {
            long? parentId = entry.ParentCode != null && codeToId.TryGetValue(entry.ParentCode, out var pid) ? pid : null;
            var module = new Module
            {
                Name       = entry.Name,
                Link       = entry.Link,
                Icon       = entry.Icon,
                ParentId   = parentId,
                SortOrder  = 0,
                ModuleCode = entry.Code,
                Status     = 2,
                IsDelete   = 1,
                TenantId   = null,
                PublicId   = Guid.NewGuid(),
                CreatedRowDate = DateTime.Now
            };
            _db.Modules.Add(module);
            await _db.SaveChangesAsync();
            codeToId[entry.Code] = module.Id;
            created++;
        }

        return Ok(ApiResponse<object>.Ok(new { created }, $"Đã tạo {created} module còn thiếu."));
    }
}
