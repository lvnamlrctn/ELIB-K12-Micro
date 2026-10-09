using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class CustomerRepository : BaseRepository<Customer, CustomerSearchRequest, CustomerRequest>
{
    public CustomerRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Customer> BuildQuery(CustomerSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CustomerRequest r, Customer e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Address = r.Address; e.Phone = r.Phone;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Customer e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Customer e, int status, long userId) { }
}
