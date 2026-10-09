using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Evaluate;

/// <summary>Đọc gộp cây "Cơ sở môn học": DonVi (Đơn vị đào tạo) → Chương trình → Ngành → Môn học → Tài
/// liệu, cho trang quản lý dạng cây (/admin/subject-tree). DonVi là entity RIÊNG (Evaluate.DonVi) — KHÁC
/// với Org (Dbo.Org = phòng ban nội bộ hệ thống, dùng cho Reader.OrgId) dù cùng hình dạng
/// Name/ParentId/Level/Status/Order, 2 khái niệm nghiệp vụ độc lập.
/// Chỉ đọc — mọi thao tác ghi (thêm/sửa/xoá từng node) gọi thẳng CRUD sẵn có của từng entity
/// (DonViController, EvaluateProgramController, NganhHocController, MonHocController,
/// NganhMonHocController, TaiLieuController), controller này không trùng lặp logic đó.
/// Đa tenant — mọi bảng con lọc theo TenantId của nhân viên đăng nhập (super-admin xem toàn bộ, hoặc
/// đơn vị cụ thể đã chọn qua bộ lọc "Thư viện" — Đợt 24.8, gộp thêm dữ liệu dùng chung TenantId=null).</summary>
[Route("api/Evaluate/SubjectTree")]
public class SubjectTreeController(ELIBAPIDbContext db) : BaseApiController
{
    [HttpPost("GetFullTree")]
    [Permission("SUBJECT_TREE", "view")]
    public async Task<IActionResult> GetFullTree([FromBody] SubjectTreeSearchRequest? request)
    {
        var jwtTenantId     = GetTenantId();
        var isPrivileged    = IsPrivilegedRole();
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(db, request?.TenantId, jwtTenantId, isPrivileged);

        bool Match(long? entityTenantId) => isPrivileged
            ? (!requestTenantId.HasValue || entityTenantId == requestTenantId || entityTenantId == null)
            : entityTenantId == jwtTenantId;

        var donVis   = (await db.DonVis.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();
        var programs = (await db.EvaluatePrograms.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();
        var nganhs   = (await db.NganhHocs.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();
        var links    = (await db.NganhMonHocs.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();
        var monHocs  = (await db.MonHocs.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();
        var taiLieus = (await db.TaiLieus.Where(x => x.IsDelete != 2).ToListAsync()).Where(x => Match(x.TenantId)).ToList();

        var allTenantIds = donVis.Select(x => x.TenantId)
            .Concat(programs.Select(x => x.TenantId)).Concat(nganhs.Select(x => x.TenantId))
            .Concat(monHocs.Select(x => x.TenantId)).Concat(taiLieus.Select(x => x.TenantId))
            .Where(id => id.HasValue).Select(id => id!.Value);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, allTenantIds);
        string? NameOf(long? id) => id.HasValue && tenantNames.TryGetValue(id.Value, out var n) ? n : null;

        List<SubjectTreeNode> BuildTaiLieu(long monHocId) =>
            [.. taiLieus.Where(t => t.MonHocId == monHocId)
                .Select(t => new SubjectTreeNode
                {
                    NodeType    = "tailieu",
                    Id          = t.Id,
                    PublicId    = t.PublicId,
                    Name        = t.Title,
                    Author      = t.Author,
                    PublishDate = t.PublishDate,
                    LoaiTaiLieu = t.LoaiTaiLieu,
                    TenantId    = t.TenantId,
                    TenantName  = NameOf(t.TenantId)
                })];

        List<SubjectTreeNode> BuildMonHoc(long nganhId) =>
            [.. links.Where(l => l.MajorId == nganhId)
                .Join(monHocs, l => l.MonHocId, m => m.Id, (l, m) => new { l, m })
                .Select(x => new SubjectTreeNode
                {
                    NodeType     = "monhoc",
                    Id           = x.m.Id,
                    PublicId     = x.m.PublicId,
                    Name         = x.m.TenMon,
                    LinkPublicId = x.l.PublicId,
                    Code         = x.m.MaMon,
                    SoTinChi     = x.m.SoTinChi,
                    TenantId     = x.m.TenantId,
                    TenantName   = NameOf(x.m.TenantId),
                    Children     = BuildTaiLieu(x.m.Id)
                })];

        List<SubjectTreeNode> BuildNganh(long programId) =>
            [.. nganhs.Where(n => n.ProgramId == programId)
                .Select(n => new SubjectTreeNode
                {
                    NodeType   = "nganh",
                    Id         = n.Id,
                    PublicId   = n.PublicId,
                    Name       = n.MajorsName,
                    Code       = n.MajorsCode,
                    TenantId   = n.TenantId,
                    TenantName = NameOf(n.TenantId),
                    Children   = BuildMonHoc(n.Id)
                })];

        List<SubjectTreeNode> BuildProgram(long donViId) =>
            [.. programs.Where(p => p.DonViId == donViId)
                .Select(p => new SubjectTreeNode
                {
                    NodeType   = "program",
                    Id         = p.Id,
                    PublicId   = p.PublicId,
                    Name       = p.Name,
                    TenantId   = p.TenantId,
                    TenantName = NameOf(p.TenantId),
                    Children   = BuildNganh(p.Id)
                })];

        List<SubjectTreeNode> BuildDonVi(long? parentId) =>
            [.. donVis.Where(d => parentId == null ? (d.ParentId == null || d.ParentId == 0) : d.ParentId == parentId)
                .OrderBy(d => d.Order).ThenBy(d => d.Id)
                .Select(d => new SubjectTreeNode
                {
                    NodeType   = "donvi",
                    Id         = d.Id,
                    PublicId   = d.PublicId,
                    Name       = d.Name,
                    TenantId   = d.TenantId,
                    TenantName = NameOf(d.TenantId),
                    Children   = [.. BuildDonVi(d.Id), .. BuildProgram(d.Id)]
                })];

        var tree = BuildDonVi(null);
        return Ok(ApiResponse<List<SubjectTreeNode>>.Ok(tree));
    }
}
