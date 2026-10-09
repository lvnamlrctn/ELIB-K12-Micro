using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public record PolicyCircWithReaderTypeName(PolicyCirc Policy, string? ReaderTypeName, string? StoreName);

public interface IPolicyCircRepository : IGenericRepository<PolicyCirc, PolicyCircSearchRequest, PolicyCircRequest>
{
    Task<List<PolicyCircWithReaderTypeName>> GetByCircPlaceAsync(Guid circPlacePublicId);
}

public class PolicyCircRepository : BaseRepository<PolicyCirc, PolicyCircSearchRequest, PolicyCircRequest>, IPolicyCircRepository
{
    public PolicyCircRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Đợt 26 — truy vấn tay này KHÔNG đi qua BaseRepository.SearchAsync/SearchAllAsync (nơi luôn tự động
    // ApplyTenantFilter) nên trước đây không lọc tenant: nếu nhiều tenant cùng tạo PolicyCirc cho cùng 1
    // loại bạn đọc tại 1 CircPlace dùng chung (TenantId null), mỗi tenant có 1 dòng riêng → hiện lặp trên
    // UI. Dùng ApplyTenantFilterIncludeNull để tenant thường chỉ thấy policy của mình + dùng chung.
    //
    // Đợt 26 — PolicyCirc còn có chiều Store (Kho): CÙNG 1 loại bạn đọc có thể có 1 chính sách RIÊNG cho
    // mỗi Kho tại cùng 1 điểm lưu thông (đây là thiết kế đúng, không phải dữ liệu lỗi). Trước đây
    // GetByCircPlaceAsync bỏ hẳn Store khỏi kết quả nên cây UI hiển thị nhiều dòng CÙNG TÊN loại bạn đọc
    // mà không cách nào phân biệt — nhìn như bị "lặp". Join thêm Store để trả StoreName, cho phép UI ghi
    // rõ từng dòng thuộc Kho nào.
    public async Task<List<PolicyCircWithReaderTypeName>> GetByCircPlaceAsync(Guid circPlacePublicId)
    {
        var circPlaceId = await _context.CircPlaces.Where(x => x.PublicId == circPlacePublicId).Select(x => x.Id).FirstOrDefaultAsync();
        var baseQuery = _dbSet.Where(x => x.IsDelete != 2 && x.CircPlace == circPlaceId);
        var rows = await (
            from p in ApplyTenantFilterIncludeNull(baseQuery)
            join rt in _context.ReaderTypes on (long?)p.ReaderType equals (long?)rt.Id into rtj
            from rt in rtj.DefaultIfEmpty()
            join s in _context.Stores on (long?)p.Store equals (long?)s.Id into sj
            from s in sj.DefaultIfEmpty()
            select new { p, ReaderTypeName = rt != null ? rt.Name : null, StoreName = s != null ? s.Name : null }
        ).ToListAsync();
        return rows.Select(x => new PolicyCircWithReaderTypeName(x.p, x.ReaderTypeName, x.StoreName)).ToList();
    }

    protected override IQueryable<PolicyCirc> BuildQuery(PolicyCircSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.CircPlace.HasValue && r.CircPlace > 0) q = q.Where(x => x.CircPlace == r.CircPlace);
        if (r.ReaderType.HasValue && r.ReaderType > 0) q = q.Where(x => x.ReaderType == r.ReaderType);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PolicyCircRequest r, PolicyCirc e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PolicyCirc e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PolicyCirc e, int status, long userId) { }
}
