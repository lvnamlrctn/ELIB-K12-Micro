using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public record CircPlaceMappingResult(List<long> StoreIds, List<long> ReaderTypeIds);

public interface ICircPlaceRepository : IGenericRepository<CircPlace, CircPlaceSearchRequest, CircPlaceRequest>
{
    Task<CircPlaceMappingResult> GetMappingAsync(Guid circPlacePublicId);
    Task ReplaceStoreMappingAsync(Guid circPlacePublicId, List<long> storeIds);
    Task ReplaceReaderTypeMappingAsync(Guid circPlacePublicId, List<long> readerTypeIds);
}

public class CircPlaceRepository : BaseRepository<CircPlace, CircPlaceSearchRequest, CircPlaceRequest>, ICircPlaceRepository
{
    public CircPlaceRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public async Task<CircPlaceMappingResult> GetMappingAsync(Guid circPlacePublicId)
    {
        var circPlaceId = await _dbSet.Where(x => x.PublicId == circPlacePublicId).Select(x => x.Id).FirstOrDefaultAsync();
        var storeIds = await _context.CircPlaceStores
            .Where(x => x.CircPlaceId == circPlaceId && x.IsDelete != 2)
            .Select(x => x.StoreId).ToListAsync();
        var readerTypeIds = await _context.CircPlaceReaderTypes
            .Where(x => x.CircPlaceId == circPlaceId && x.IsDelete != 2)
            .Select(x => x.ReaderTypeId).ToListAsync();
        return new CircPlaceMappingResult(storeIds, readerTypeIds);
    }

    public async Task ReplaceStoreMappingAsync(Guid circPlacePublicId, List<long> storeIds)
    {
        var circPlaceId = await _dbSet.Where(x => x.PublicId == circPlacePublicId).Select(x => x.Id).FirstOrDefaultAsync();
        if (circPlaceId == 0) throw new KeyNotFoundException("NotFound");
        var userId = GetCurrentUserId();
        var tenantId = GetCurrentTenantId();
        var existing = await _context.CircPlaceStores.Where(x => x.CircPlaceId == circPlaceId).ToListAsync();
        _context.CircPlaceStores.RemoveRange(existing);
        foreach (var storeId in storeIds.Distinct())
        {
            _context.CircPlaceStores.Add(new CircPlaceStore
            {
                CircPlaceId = circPlaceId, StoreId = storeId, TenantId = tenantId,
                CreatedRowBy = userId, CreatedRowDate = DateTime.Now, PublicId = Guid.NewGuid()
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task ReplaceReaderTypeMappingAsync(Guid circPlacePublicId, List<long> readerTypeIds)
    {
        var circPlaceId = await _dbSet.Where(x => x.PublicId == circPlacePublicId).Select(x => x.Id).FirstOrDefaultAsync();
        if (circPlaceId == 0) throw new KeyNotFoundException("NotFound");
        var userId = GetCurrentUserId();
        var tenantId = GetCurrentTenantId();
        var existing = await _context.CircPlaceReaderTypes.Where(x => x.CircPlaceId == circPlaceId).ToListAsync();
        _context.CircPlaceReaderTypes.RemoveRange(existing);
        var distinctIds = readerTypeIds.Distinct().ToList();
        foreach (var readerTypeId in distinctIds)
        {
            _context.CircPlaceReaderTypes.Add(new CircPlaceReaderType
            {
                CircPlaceId = circPlaceId, ReaderTypeId = readerTypeId, TenantId = tenantId,
                CreatedRowBy = userId, CreatedRowDate = DateTime.Now, PublicId = Guid.NewGuid()
            });
        }

        // Mỗi loại bạn đọc được gán vào điểm lưu thông phải có sẵn 1 dòng PolicyCirc (chính sách
        // mặc định, các hạn mức để trống) để màn hình có thể mở ra cấu hình ngay — tạo nếu chưa có,
        // không đụng tới dòng đã tồn tại (tránh mất dữ liệu chính sách đã cấu hình khi bỏ rồi chọn lại).
        var existingPolicyReaderTypes = await _context.PolicyCircs
            .Where(x => x.CircPlace == circPlaceId && x.IsDelete != 2)
            .Select(x => x.ReaderType).ToListAsync();
        foreach (var readerTypeId in distinctIds)
        {
            if (existingPolicyReaderTypes.Contains((int)readerTypeId)) continue;
            _context.PolicyCircs.Add(new PolicyCirc
            {
                CircPlace = circPlaceId, ReaderType = (int)readerTypeId, TenantId = tenantId,
                CreatedRowBy = userId, CreatedRowDate = DateTime.Now, PublicId = Guid.NewGuid()
            });
        }
        await _context.SaveChangesAsync();
    }

    protected override IQueryable<CircPlace> BuildQuery(CircPlaceSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CircPlaceRequest r, CircPlace e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CircPlace e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CircPlace e, int status, long userId) { }
}
