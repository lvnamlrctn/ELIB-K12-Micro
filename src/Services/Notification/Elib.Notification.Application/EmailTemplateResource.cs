using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Notification.Application;

public sealed record EmailTemplateRequest(string Code, string Name, string Subject, string Body, int? Status);

public sealed record EmailTemplateDto(long Id, Guid PublicId, string Code, string Name, string Subject, string Body, int Status, bool IsBuiltIn);

public sealed class EmailTemplateResource(ICrudDbContext db)
    : CrudResource<EmailTemplateResource, EmailTemplate, CrudSearch, EmailTemplateRequest, EmailTemplateDto>(db)
{
    private static readonly string[] BuiltInCodes = DefaultTemplates.All.Select(d => d.Code).ToArray();

    protected override string EntityName => "Mẫu email";

    protected override string? Describe(EmailTemplate entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<EmailTemplate, EmailTemplateDto>> Projection =>
        x => new EmailTemplateDto(x.Id, x.PublicId, x.Code, x.Name, x.Subject, x.Body, x.Status, BuiltInCodes.Contains(x.Code));

    protected override EmailTemplate Create(EmailTemplateRequest request) =>
        EmailTemplate.Create(request.Code, request.Name, request.Subject, request.Body, request.Status);

    protected override void Update(EmailTemplate entity, EmailTemplateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Code) && EmailTemplate.NormalizeCode(request.Code) != entity.Code)
            throw new BusinessRuleException("TEMPLATE_CODE_IMMUTABLE", "Không đổi được mã mẫu — service nghiệp vụ gửi tin theo mã này.");
        entity.Update(request.Name, request.Subject, request.Body, request.Status);
    }

    protected override IQueryable<EmailTemplate> Filter(IQueryable<EmailTemplate> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term) || x.Subject.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<EmailTemplate> Order(IQueryable<EmailTemplate> query) => query.OrderBy(x => x.Code);

    protected override async Task ValidateAsync(EmailTemplate entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("TEMPLATE_CODE_EXISTS", $"Mã mẫu '{entity.Code}' đã có.");
    }

    /// <summary>Thêm các mẫu mặc định còn thiếu (idempotent) — dùng khi khởi tạo đơn vị và nút "Khôi phục mẫu mặc định".</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Code).ToListAsync(ct); // mẫu đã xoá được thêm lại
        var missing = DefaultTemplates.All.Where(d => !existing.Contains(d.Code)).ToList();
        foreach (var d in missing) Set.Add(EmailTemplate.Create(d.Code, d.Name, d.Subject, d.Body, IHasStatus.Active));
        if (missing.Count > 0) await Db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
