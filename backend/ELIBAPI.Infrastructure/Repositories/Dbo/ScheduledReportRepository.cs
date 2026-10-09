using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ScheduledReportRepository : BaseRepository<ScheduledReport, ScheduledReportSearchRequest, ScheduledReportRequest>
{
    public ScheduledReportRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ScheduledReport> BuildQuery(ScheduledReportSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ScheduledReportRequest r, ScheduledReport e, long userId, bool isNew)
    {
        e.Name = r.Name;
        e.ReportType = r.ReportType;
        e.ReportParamsJson = r.ReportParamsJson;
        e.FrequencyType = r.FrequencyType;
        e.DayOfWeek = r.DayOfWeek;
        e.DayOfMonth = r.DayOfMonth;
        e.TimeOfDay = r.TimeOfDay;
        e.RecipientEmails = r.RecipientEmails;
        if (r.Status.HasValue) e.Status = r.Status.Value;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ScheduledReport e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ScheduledReport e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
