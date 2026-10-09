using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public interface ICabinetCompartmentRepository : IGenericRepository<CabinetCompartment, CabinetCompartmentSearchRequest, CabinetCompartmentRequest>
{
    Task<(Cabinet? Cabinet, List<CabinetCompartment> Compartments, HashSet<long> OccupiedIds)> GetByCabinetAsync(Guid cabinetPublicId);
}

public class CabinetCompartmentRepository : BaseRepository<CabinetCompartment, CabinetCompartmentSearchRequest, CabinetCompartmentRequest>, ICabinetCompartmentRepository
{
    public CabinetCompartmentRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public async Task<(Cabinet? Cabinet, List<CabinetCompartment> Compartments, HashSet<long> OccupiedIds)> GetByCabinetAsync(Guid cabinetPublicId)
    {
        var cabinet = await _context.Cabinets.FirstOrDefaultAsync(c => c.PublicId == cabinetPublicId && c.IsDelete != 2);
        if (cabinet == null) return (null, new List<CabinetCompartment>(), new HashSet<long>());

        var compartments = await _dbSet
            .Where(x => x.CabinetId == cabinet.Id && x.IsDelete != 2)
            .OrderBy(x => x.RowIndex).ThenBy(x => x.ColIndex)
            .ToListAsync();

        var compartmentIds = compartments.Select(x => x.Id).ToList();
        var occupiedIds = (await _context.KeyOuts
            .Where(k => k.IsDelete != 2 && k.Keyid.HasValue && compartmentIds.Contains(k.Keyid.Value))
            .Select(k => k.Keyid!.Value)
            .ToListAsync())
            .ToHashSet();

        return (cabinet, compartments, occupiedIds);
    }

    protected override IQueryable<CabinetCompartment> BuildQuery(CabinetCompartmentSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.CabinetId.HasValue) q = q.Where(x => x.CabinetId == r.CabinetId);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword) || x.Code!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderBy(x => x.RowIndex).ThenBy(x => x.ColIndex);
    }

    protected override void MapRequestToEntity(CabinetCompartmentRequest r, CabinetCompartment e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CabinetCompartment e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CabinetCompartment e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
