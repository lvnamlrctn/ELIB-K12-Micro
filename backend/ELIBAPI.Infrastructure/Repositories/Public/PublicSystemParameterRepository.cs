using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicSystemParameterRepository : IPublicGenericRepository<PublicSystemParameterResponse, PublicSearchRequest>
{
    Task<PublicSystemParameterResponse?> GetByCodeAsync(string code, Guid? TenantId);
}

public class PublicSystemParameterRepository : PublicBaseRepository<PublicSystemParameterResponse, PublicSearchRequest>, IPublicSystemParameterRepository
{
    public PublicSystemParameterRepository(ELIBAPIDbContext db, IMemoryCache cache, ICacheInvalidator invalidator) : base(db, cache, invalidator: invalidator) { }

    protected override IQueryable<PublicSystemParameterResponse> BuildQuery(PublicSearchRequest r)
    {
        return _db.SystemParameters
            .Where(x => x.IsDelete != 2)
            .Select(x => new PublicSystemParameterResponse
            {
                Code = x.Code,
                Value = x.Value,
                DescriptionVn = x.DescriptionVn,
                DescriptionEn = x.DescriptionEn
            });
    }

    public async Task<PublicSystemParameterResponse?> GetByCodeAsync(string code, Guid? TenantId)
    {
        long? depId = null;
        if (TenantId.HasValue)
        {
            depId = await _db.Tenants
                .Where(d => d.PublicId == TenantId.Value && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefaultAsync();
        }

        var codeLower = code.ToLower();
        var q = _db.SystemParameters.Where(x => x.Code!.ToLower() == codeLower && x.IsDelete != 2);
        if (depId.HasValue) q = q.Where(x => x.TenantId == depId.Value || x.TenantId == null);

        return await q.Select(x => new PublicSystemParameterResponse
        {
            Code = x.Code,
            Value = x.Value,
            DescriptionVn = x.DescriptionVn,
            DescriptionEn = x.DescriptionEn
        }).FirstOrDefaultAsync();
    }
}

