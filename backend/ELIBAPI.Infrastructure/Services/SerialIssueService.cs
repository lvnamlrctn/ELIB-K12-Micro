using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Port ELIB-LRC 10-03 (tách từ <c>MagazineSerialController</c>), thêm phạm vi đơn vị: trước đây chỉ danh sách kỳ
/// lọc đơn vị — duyệt, nhận/sửa, khiếu nại, sinh kỳ, thống kê, xoá kỳ thao tác được trên đăng ký của đơn vị khác theo Id.</summary>
public class SerialIssueService(ELIBAPIDbContext db) : ISerialIssueService
{
    private const int Received = 1, Claimed = 2, Expected = 0;
    public const int MaxPredict = 366;
    private const string NoSubscription = "Không tìm thấy đăng ký";
    private const string NoIssue = "Không tìm thấy kỳ báo";

    private IQueryable<Serial> Serials(long? tenantId) =>
        db.Serials.Where(x => x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

    /// <summary>Kỳ ấn phẩm thuộc đăng ký trong phạm vi đơn vị.</summary>
    private IQueryable<SerialItem> Items(long? tenantId)
    {
        var subscriptionIds = Serials(tenantId).Select(s => (long?)s.Id);
        return db.SerialItems.Where(x => x.IsDelete != 2 && subscriptionIds.Contains(x.SUBSCRIPTION_ID));
    }

    public Task<bool> IsApprovedAsync(Guid subscriptionPublicId, long? tenantId) =>
        Serials(tenantId).AnyAsync(x => x.PublicId == subscriptionPublicId && x.Approved == true);

    public async Task<ServiceResult<SerialApproval>> ApproveAsync(Guid subscriptionPublicId, long? tenantId, long? userId)
    {
        var serial = await Serials(tenantId).FirstOrDefaultAsync(x => x.PublicId == subscriptionPublicId);
        if (serial == null) return ServiceResult<SerialApproval>.NotFound(NoSubscription);
        if (serial.Approved == true) return ServiceResult<SerialApproval>.BadRequest("Đăng ký đã được duyệt trước đó.");

        var now = LibraryClock.Now;
        serial.Approved       = true;
        serial.ApprovedDate   = now;
        serial.UpdateRowBy    = userId;
        serial.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        return ServiceResult<SerialApproval>.Ok(new SerialApproval(serial.PublicId, true, serial.ApprovedDate));
    }

    public async Task<ServiceResult<SerialIssuePage>> SearchAsync(SerialReceiptSearchRequest r, long? tenantId)
    {
        if (!await Serials(tenantId).AnyAsync(x => x.Id == r.SubscriptionId))
            return ServiceResult<SerialIssuePage>.NotFound(NoSubscription);

        var page = Math.Max(1, r.PageIndex ?? 1);
        var size = Math.Clamp(r.PageSize ?? 20, 1, 500);
        var query = db.SerialItems.Where(x => x.SUBSCRIPTION_ID == r.SubscriptionId && x.IsDelete != 2);
        if (r.Status.HasValue) query = query.Where(x => x.STATUS == r.Status);

        var total = await query.CountAsync();
        // Kỳ mới nhất trước; kỳ không có ngày dự kiến xuống cuối (Postgres mặc định đưa NULL lên đầu khi sắp giảm dần).
        var items = await query
            .OrderBy(x => x.PLANNED_DATE == null).ThenByDescending(x => x.PLANNED_DATE).ThenByDescending(x => x.ID)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync();
        return ServiceResult<SerialIssuePage>.Ok(new SerialIssuePage(items.ConvertAll(View), total, page, size));
    }

    public async Task<ServiceResult<SerialIssueView>> SaveAsync(SerialIssueSaveRequest r, long? tenantId, long? userId)
    {
        if (r.SubscriptionId is not > 0) return ServiceResult<SerialIssueView>.BadRequest("Thiếu đăng ký của kỳ ấn phẩm");
        var serial = await Serials(tenantId).FirstOrDefaultAsync(x => x.Id == r.SubscriptionId);
        if (serial == null) return ServiceResult<SerialIssueView>.NotFound(NoSubscription);

        SerialItem? entity = null;
        if (r.Id is > 0 || r.PublicId.HasValue)
        {
            entity = r.Id is > 0
                ? await db.SerialItems.FirstOrDefaultAsync(x => x.ID == r.Id && x.IsDelete != 2)
                : await db.SerialItems.FirstOrDefaultAsync(x => x.PublicId == r.PublicId && x.IsDelete != 2);
            if (entity == null) return ServiceResult<SerialIssueView>.NotFound(NoIssue);
            if (entity.SUBSCRIPTION_ID != r.SubscriptionId) return ServiceResult<SerialIssueView>.BadRequest("Kỳ báo không thuộc đăng ký này");
        }
        else if (r.IsMerged != true && (r.SerialSeqX is > 0 || r.SerialSeqY is > 0 || r.SerialSeqZ is > 0))
        {
            // Nhập tay đúng số của một kỳ đã dự kiến thì cập nhật kỳ đó, không sinh bản trùng.
            entity = await db.SerialItems.FirstOrDefaultAsync(x =>
                x.SUBSCRIPTION_ID == r.SubscriptionId && x.SERIAL_SEQ_X == r.SerialSeqX && x.SERIAL_SEQ_Y == r.SerialSeqY
                && x.SERIAL_SEQ_Z == r.SerialSeqZ && x.IsDelete != 2);
        }

        var isNew = entity == null;
        entity ??= new SerialItem { PublicId = Guid.NewGuid() };
        var now = LibraryClock.Now;
        entity.SUBSCRIPTION_ID = r.SubscriptionId;
        entity.SERIAL_SEQ      = r.SerialSeq ?? entity.SERIAL_SEQ;
        entity.SERIAL_SEQ_X    = r.SerialSeqX ?? entity.SERIAL_SEQ_X;
        entity.SERIAL_SEQ_Y    = r.SerialSeqY ?? entity.SERIAL_SEQ_Y;
        entity.SERIAL_SEQ_Z    = r.SerialSeqZ ?? entity.SERIAL_SEQ_Z;
        entity.IS_SPECIAL      = r.IsSpecial ?? entity.IS_SPECIAL;
        entity.STATUS          = r.Status ?? entity.STATUS;
        entity.QUANTITY        = r.Quantity ?? entity.QUANTITY;
        entity.PLANNED_DATE    = r.PlannedDate ?? entity.PLANNED_DATE;
        entity.NOTE            = r.Note ?? entity.NOTE;
        entity.CLAIM_DATE      = r.ClaimDate ?? entity.CLAIM_DATE;
        entity.CLAIM_COUNT     = r.ClaimCount ?? entity.CLAIM_COUNT;
        entity.IsMerged        = r.IsMerged ?? entity.IsMerged;
        entity.SortOrder       = r.SortOrder ?? entity.SortOrder;
        // Ngày nhận chỉ có nghĩa với kỳ đã nhận: lấy ngày gửi lên (cho phép sửa lại), chưa có thì lấy hôm nay.
        if (entity.STATUS == Received)
            entity.PUBLISHED_DATE = r.PublishedDate ?? entity.PUBLISHED_DATE ?? now;

        entity.UpdateRowBy    = userId;
        entity.UpdatedRowDate = now;
        if (isNew)
        {
            entity.TenantId       = serial.TenantId;
            entity.CreatedRowBy   = userId;
            entity.CreatedRowDate = now;
            db.SerialItems.Add(entity);
        }
        await db.SaveChangesAsync();
        return ServiceResult<SerialIssueView>.Ok(View(entity));
    }

    public async Task<ServiceResult<SerialIssueView>> ClaimAsync(long id, long? tenantId, long? userId)
    {
        var item = await Items(tenantId).FirstOrDefaultAsync(x => x.ID == id);
        if (item == null) return ServiceResult<SerialIssueView>.NotFound(NoIssue);
        // Khiếu nại kỳ đã nhận sẽ xoá mất trạng thái "đã nhận" (và làm sai thống kê) — không cho phép.
        if (item.STATUS == Received) return ServiceResult<SerialIssueView>.BadRequest("Kỳ báo đã nhận, không thể khiếu nại");

        var now = LibraryClock.Now;
        item.STATUS         = Claimed;
        item.CLAIM_DATE     = now;
        item.CLAIM_COUNT    = (item.CLAIM_COUNT ?? 0) + 1;
        item.UpdateRowBy    = userId;
        item.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        return ServiceResult<SerialIssueView>.Ok(View(item));
    }

    public async Task<ServiceResult<List<SerialIssueView>>> PredictAsync(PredictIssuesRequest r, long? tenantId, long? userId)
    {
        var serial = await Serials(tenantId).FirstOrDefaultAsync(x => x.Id == r.SubscriptionId);
        if (serial == null) return ServiceResult<List<SerialIssueView>>.NotFound(NoSubscription);
        if (!serial.FrequencyId.HasValue || !serial.PatternId.HasValue)
            return ServiceResult<List<SerialIssueView>>.BadRequest("Serial chưa gán FrequencyId hoặc PatternId");

        var freq    = await db.FrequencyMagazines.FirstOrDefaultAsync(x => x.Id == serial.FrequencyId.Value);
        var pattern = await db.PatternMagazines.FirstOrDefaultAsync(x => x.Id == serial.PatternId.Value);
        var detail  = await db.PartemMagazineDetails.FirstOrDefaultAsync(x => x.PatternId == serial.PatternId.Value && x.IsDelete != 2);
        if (freq == null || detail == null)
            return ServiceResult<List<SerialIssueView>>.BadRequest("Không tìm thấy tần suất hoặc pattern");

        int curX = serial.LastX ?? 0, curY = serial.LastY ?? 0, curZ = serial.LastZ ?? 0;
        // Chưa sinh kỳ nào và đăng ký có "số bắt đầu" → kỳ đầu mang đúng số đó, các kỳ sau tăng tiếp từ đây.
        var startAt = serial.LastX == null && serial.LastY == null && serial.LastZ == null && serial.StartX.HasValue
            ? (serial.StartX.Value, serial.StartY ?? 0, serial.StartZ ?? 0)
            : ((int, int, int)?)null;
        var count = Math.Clamp(r.Count ?? 10, 1, MaxPredict);

        // Mốc ngày: kỳ dự kiến gần nhất đã có → kỳ kế tiếp cách 1 chu kỳ. Chưa có kỳ nào → kỳ đầu đúng vào
        // "ngày phát hành kỳ đầu" của đăng ký (không có thì 1 chu kỳ sau hôm nay).
        var lastPlanned = await db.SerialItems
            .Where(x => x.SUBSCRIPTION_ID == serial.Id && x.IsDelete != 2 && x.PLANNED_DATE != null)
            .MaxAsync(x => x.PLANNED_DATE);
        var anchor = (lastPlanned ?? serial.FirstTime ?? LibraryClock.Today).Date;
        var firstStep = lastPlanned == null && serial.FirstTime.HasValue ? 0 : 1;

        var now = LibraryClock.Now;
        var generated = new List<SerialItem>();
        for (var i = 0; i < count; i++)
        {
            (curX, curY, curZ) = i == 0 && startAt.HasValue ? startAt.Value : Increment(curX, curY, curZ, detail);
            var item = new SerialItem
            {
                SUBSCRIPTION_ID = serial.Id,
                SERIAL_SEQ      = (pattern?.Function ?? "Số {X}").Replace("{X}", curX.ToString()).Replace("{Y}", curY.ToString()).Replace("{Z}", curZ.ToString()),
                SERIAL_SEQ_X    = curX,
                SERIAL_SEQ_Y    = curY,
                SERIAL_SEQ_Z    = curZ,
                STATUS          = Expected,
                PLANNED_DATE    = PlannedDate(anchor, firstStep + i, freq),
                PublicId        = Guid.NewGuid(),
                CreatedRowBy    = userId,
                CreatedRowDate  = now,
                TenantId        = serial.TenantId,
            };
            db.SerialItems.Add(item);
            generated.Add(item);
        }

        serial.LastX = curX; serial.LastY = curY; serial.LastZ = curZ;
        serial.UpdateRowBy    = userId;
        serial.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        return ServiceResult<List<SerialIssueView>>.Ok(generated.ConvertAll(View));
    }

    public async Task<ServiceResult<SerialIssueStats>> StatisticsAsync(long subscriptionId, long? tenantId)
    {
        if (!await Serials(tenantId).AnyAsync(x => x.Id == subscriptionId))
            return ServiceResult<SerialIssueStats>.NotFound(NoSubscription);
        var items = await db.SerialItems
            .Where(x => x.SUBSCRIPTION_ID == subscriptionId && x.IsDelete != 2)
            .Select(x => new { x.STATUS, x.PLANNED_DATE, x.PUBLISHED_DATE })
            .ToListAsync();

        var today = LibraryClock.Today;
        return ServiceResult<SerialIssueStats>.Ok(new SerialIssueStats(
            items.Count,
            items.Count(x => x.STATUS == Received),
            items.Count(x => x.STATUS == Claimed),
            items.Count(x => x.STATUS == Expected && x.PLANNED_DATE < today),
            // So theo NGÀY: ngày nhận ghi kèm giờ, nhận đúng ngày dự kiến không phải là trễ.
            items.Count(x => x.STATUS == Received && x.PUBLISHED_DATE?.Date > x.PLANNED_DATE?.Date)));
    }

    public async Task<ServiceResult<SerialIssueView?>> LastReceivedAsync(long subscriptionId, long? tenantId)
    {
        if (!await Serials(tenantId).AnyAsync(x => x.Id == subscriptionId))
            return ServiceResult<SerialIssueView?>.NotFound(NoSubscription);
        var last = await db.SerialItems
            .Where(x => x.SUBSCRIPTION_ID == subscriptionId && x.STATUS == Received && x.IsDelete != 2 && x.PUBLISHED_DATE != null)
            .OrderByDescending(x => x.PUBLISHED_DATE).ThenByDescending(x => x.ID)
            .FirstOrDefaultAsync();
        return ServiceResult<SerialIssueView?>.Ok(last == null ? null : View(last));
    }

    public async Task<ServiceResult<string>> DeleteAsync(long? id, Guid? publicId, long? tenantId, long? userId)
    {
        var item = id.HasValue
            ? await Items(tenantId).FirstOrDefaultAsync(x => x.ID == id)
            : await Items(tenantId).FirstOrDefaultAsync(x => x.PublicId == publicId);
        if (item == null) return ServiceResult<string>.NotFound(NoIssue);
        item.IsDelete       = 2;
        item.UpdateRowBy    = userId;
        item.UpdatedRowDate = LibraryClock.Now;
        await db.SaveChangesAsync();
        return ServiceResult<string>.Ok("Đã xóa kỳ báo");
    }

    private static SerialIssueView View(SerialItem x) => new(
        x.ID, x.SUBSCRIPTION_ID, x.SERIAL_SEQ, x.SERIAL_SEQ_X, x.SERIAL_SEQ_Y, x.SERIAL_SEQ_Z, x.IS_SPECIAL, x.STATUS,
        x.QUANTITY, x.PLANNED_DATE, x.PUBLISHED_DATE, x.CLAIM_DATE, x.CLAIM_COUNT, x.NOTE, x.IsMerged, x.SortOrder, x.PublicId);

    internal static (int x, int y, int z) Increment(int x, int y, int z, PartemMagazineDetail p)
    {
        x += p.StepX ?? 1;
        if (p.MaxX.HasValue && x > p.MaxX.Value)
        {
            x = p.ResetX ?? 1;
            y += p.StepY ?? 1;
            if (p.MaxY.HasValue && y > p.MaxY.Value)
            {
                y = p.ResetY ?? 1;
                z += p.StepZ ?? 1;
            }
        }
        return (x, y, z);
    }

    /// <summary>
    /// Ngày dự kiến của kỳ thứ <paramref name="step"/> tính từ <paramref name="anchor"/>. Đơn vị (DV): 1 ngày, 2 tuần,
    /// 3 tháng, 4 quý, 5 năm; mỗi kỳ cách DVTrenSo đơn vị, hoặc 1/SoTrenDV đơn vị khi tần suất là "n số mỗi đơn vị".
    /// Tính thẳng từ mốc (không cộng dồn từng kỳ) nên không trôi ngày cuối tháng (31/1 → 28/2 → 28/3…), và chu kỳ lẻ
    /// tháng/quý/năm (vd 2 số/tháng) quy ra ngày — trước đây làm tròn thành 0 tháng nên mọi kỳ trùng một ngày.
    /// </summary>
    public static DateTime PlannedDate(DateTime anchor, int step, FrequencyMagazine freq)
    {
        var unit  = freq.DV ?? 3;
        var every = freq.DVTrenSo is > 0 ? freq.DVTrenSo.Value : freq.SoTrenDV is > 0 ? 1.0 / freq.SoTrenDV.Value : 1.0;
        var units = every * step;

        DateTime ByMonths(double months, double daysPerMonth) =>
            Math.Abs(months - Math.Round(months)) < 1e-9 ? anchor.AddMonths((int)Math.Round(months)) : anchor.AddDays(Math.Round(months * daysPerMonth));

        return unit switch
        {
            1 => anchor.AddDays(Math.Round(units)),
            2 => anchor.AddDays(Math.Round(units * 7)),
            3 => ByMonths(units, 365.25 / 12),
            4 => ByMonths(units * 3, 365.25 / 12),
            5 => ByMonths(units * 12, 365.25 / 12),
            _ => anchor.AddMonths(step)
        };
    }
}
