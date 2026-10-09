using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="ISerialBindingService"/> (tách từ MagazineBindingController, port ELIB-LRC 10-04). Tenant: trước đây
/// chỉ tìm kiếm lọc đơn vị — xem, sửa, xoá tập theo PublicId và chọn số báo không kiểm tra đơn vị.</summary>
public class SerialBindingService(ELIBAPIDbContext db) : ISerialBindingService
{
    private const int Received = 1;
    private const string NotFoundMessage = "Không tìm thấy bộ đóng tập";

    private IQueryable<SerialBinding> Bindings(long? tenantId) =>
        db.SerialBindings.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

    public async Task<(List<SerialBindingView> Items, int Total)> SearchAsync(string? accessionNo, string? volumeTitle, long? storeId,
        int? pageIndex, int? pageSize, long? scopeTenantId, bool includeShared, bool all)
    {
        var query = db.SerialBindings.AsNoTracking().Where(x => x.IsDelete != 2
            && (all || x.TenantId == scopeTenantId || (includeShared && x.TenantId == null)));
        if (!string.IsNullOrWhiteSpace(accessionNo))
        {
            var a = accessionNo.Trim().ToLower();
            query = query.Where(x => x.AccessionNo != null && x.AccessionNo.ToLower().Contains(a));
        }
        if (!string.IsNullOrWhiteSpace(volumeTitle))
        {
            var t = volumeTitle.Trim().ToLower();
            query = query.Where(x => (x.VolumeTitle != null && x.VolumeTitle.ToLower().Contains(t))
                                  || (x.SubscriptionTitle != null && x.SubscriptionTitle.ToLower().Contains(t)));
        }
        if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId);

        var total = await query.CountAsync();
        var page = Math.Max(pageIndex ?? 1, 1);
        var size = Math.Clamp(pageSize ?? 20, 1, 1000);
        // Trước đây lấy id theo trang rồi truy vấn lại bằng Contains — mất thứ tự sắp xếp.
        var rows = await query.OrderByDescending(x => x.BindingDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return (await ViewsAsync(rows), total);
    }

    public async Task<SerialBindingView?> GetAsync(Guid publicId, long? tenantId)
    {
        var binding = await Bindings(tenantId).AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId);
        return binding == null ? null : (await ViewsAsync([binding]))[0];
    }

    /// <summary>Kèm danh sách số (nhãn gáy tập in khoảng kỳ đầu–cuối), tên kho và tên đơn vị.</summary>
    private async Task<List<SerialBindingView>> ViewsAsync(List<SerialBinding> bindings)
    {
        var ids = bindings.Select(b => b.Id).ToList();
        var items = (await db.SerialBindingItems.AsNoTracking()
                .Where(i => i.BindingId != null && ids.Contains(i.BindingId.Value) && i.IsDelete != 2)
                .OrderBy(i => i.PublishedDate).ThenBy(i => i.Id)
                .Select(i => new { i.BindingId, i.IssueId, i.SerialSeq, i.PublishedDate }).ToListAsync())
            .ToLookup(i => i.BindingId!.Value);
        var storeIds = bindings.Where(b => b.StoreId.HasValue).Select(b => b.StoreId!.Value).Distinct().ToList();
        var stores = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
        var tenants = await TenantScopeHelper.GetTenantNamesAsync(db, bindings.Where(b => b.TenantId.HasValue).Select(b => b.TenantId!.Value));

        return bindings.Select(b =>
        {
            var list = items[b.Id].Select(i => new SerialBindingIssue(i.IssueId ?? 0, i.SerialSeq, i.PublishedDate)).ToList();
            return new SerialBindingView(b.Id, b.PublicId, b.AccessionNo, b.StoreId, b.StoreId.HasValue ? stores.GetValueOrDefault(b.StoreId.Value) : null,
                b.VolumeTitle, b.SubscriptionId, b.SubscriptionTitle, b.BindingDate, b.Note, list.Count, list,
                b.TenantId.HasValue ? tenants.GetValueOrDefault(b.TenantId.Value) : null);
        }).ToList();
    }

    public async Task<ServiceResult<SerialBindingView>> SaveAsync(Guid? publicId, SerialBindingInput input, long? tenantId, long? userId)
    {
        SerialBinding? binding = null;
        if (publicId.HasValue)
        {
            binding = await Bindings(tenantId).FirstOrDefaultAsync(x => x.PublicId == publicId);
            if (binding == null) return ServiceResult<SerialBindingView>.NotFound(NotFoundMessage);
        }

        var subscriptionId = input.SubscriptionId ?? binding?.SubscriptionId;
        List<SerialItem>? issues = null;
        if (input.IssueIds != null)
        {
            var issueIds = input.IssueIds.Distinct().ToList();
            issues = await db.SerialItems.AsNoTracking().Where(x => issueIds.Contains(x.ID) && x.IsDelete != 2).ToListAsync();
            if (issues.Count != issueIds.Count) return ServiceResult<SerialBindingView>.BadRequest("Có số báo không tồn tại hoặc đã xoá");
            if (issues.FirstOrDefault(x => x.STATUS != Received) is { } notReceived)
                return ServiceResult<SerialBindingView>.BadRequest($"Số {notReceived.SERIAL_SEQ} chưa nhận, không đóng tập được");
            var subscriptions = issues.Select(x => x.SUBSCRIPTION_ID).Distinct().ToList();
            if (subscriptions.Count > 1 || (subscriptionId.HasValue && subscriptions.Count == 1 && subscriptions[0] != subscriptionId))
                return ServiceResult<SerialBindingView>.BadRequest("Các số trong một tập phải cùng một đơn đặt");
            subscriptionId ??= subscriptions.FirstOrDefault();
        }

        // Tenant: đơn đặt phải thuộc phạm vi người gọi; tập thuộc đơn vị của đơn đặt (tập cũ không gắn đơn đặt thì giữ đơn vị cũ).
        long? bindingTenantId = binding?.TenantId ?? tenantId;
        if (subscriptionId.HasValue)
        {
            var serial = await db.Serials.Where(x => x.Id == subscriptionId && x.IsDelete != 2
                    && (!tenantId.HasValue || x.TenantId == tenantId))
                .Select(x => new { x.TenantId }).FirstOrDefaultAsync();
            if (serial == null) return ServiceResult<SerialBindingView>.BadRequest("Đơn đặt không tồn tại hoặc thuộc đơn vị khác");
            if (binding != null && binding.TenantId != serial.TenantId)
                return ServiceResult<SerialBindingView>.BadRequest("Đơn đặt thuộc đơn vị khác với tập");
            bindingTenantId = serial.TenantId;
        }

        var accession = (input.AccessionNo ?? binding?.AccessionNo)?.Trim();
        if (string.IsNullOrEmpty(accession)) return ServiceResult<SerialBindingView>.BadRequest("Nhập số ĐKCB của tập");
        var lowerAccession = accession.ToLower();
        var selfId = binding?.Id ?? 0;
        if (await db.SerialBindings.AnyAsync(x => x.Id != selfId && x.IsDelete != 2 && x.TenantId == bindingTenantId
                                                && x.AccessionNo != null && x.AccessionNo.Trim().ToLower() == lowerAccession))
            return ServiceResult<SerialBindingView>.BadRequest($"Số ĐKCB '{accession}' đã dùng cho tập khác");

        if (issues != null)
        {
            var issueIds = issues.Select(x => x.ID).ToList();
            // Trước đây một số báo đóng được vào nhiều tập (màn chọn số chỉ loại các số của tập đang mở).
            var taken = await (from i in db.SerialBindingItems
                               join b in db.SerialBindings on i.BindingId equals b.Id
                               where i.IsDelete != 2 && b.IsDelete != 2 && b.Id != selfId && i.IssueId != null && issueIds.Contains(i.IssueId.Value)
                               select new { i.SerialSeq, b.AccessionNo }).FirstOrDefaultAsync();
            if (taken != null) return ServiceResult<SerialBindingView>.BadRequest($"Số {taken.SerialSeq} đã đóng trong tập {taken.AccessionNo}");
        }

        var now = LibraryClock.Now;
        await using var tx = await db.Database.BeginTransactionAsync();
        if (binding == null)
        {
            binding = new SerialBinding
            {
                PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now,
                BindingDate = input.BindingDate ?? LibraryClock.Today, TenantId = bindingTenantId
            };
            db.SerialBindings.Add(binding);
        }
        else
        {
            binding.UpdateRowBy = userId;
            binding.UpdatedRowDate = now;
            binding.BindingDate = input.BindingDate ?? binding.BindingDate;
        }
        binding.AccessionNo       = accession;
        binding.StoreId           = input.StoreId ?? binding.StoreId;
        binding.VolumeTitle       = input.VolumeTitle ?? binding.VolumeTitle;
        binding.SubscriptionId    = subscriptionId;
        binding.SubscriptionTitle = input.SubscriptionTitle ?? binding.SubscriptionTitle;
        binding.Note              = input.Note ?? binding.Note;
        await db.SaveChangesAsync();

        if (issues != null)
        {
            var existing = await db.SerialBindingItems.Where(x => x.BindingId == binding.Id && x.IsDelete != 2).ToListAsync();
            foreach (var e in existing) { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = now; }
            foreach (var issue in issues)
                db.SerialBindingItems.Add(new SerialBindingItem
                {
                    BindingId = binding.Id, IssueId = issue.ID, SerialSeq = issue.SERIAL_SEQ, PublishedDate = issue.PUBLISHED_DATE,
                    PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now
                });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return ServiceResult<SerialBindingView>.Ok((await GetAsync(binding.PublicId, null))!);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid publicId, long? tenantId, long? userId)
    {
        var binding = await Bindings(tenantId).FirstOrDefaultAsync(x => x.PublicId == publicId);
        if (binding == null) return ServiceResult<bool>.NotFound(NotFoundMessage);
        var now = LibraryClock.Now;
        binding.IsDelete = 2; binding.UpdateRowBy = userId; binding.UpdatedRowDate = now;
        foreach (var i in await db.SerialBindingItems.Where(x => x.BindingId == binding.Id && x.IsDelete != 2).ToListAsync())
        { i.IsDelete = 2; i.UpdateRowBy = userId; i.UpdatedRowDate = now; }
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }
}
