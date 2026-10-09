using System.Security.Claims;
using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/Dashboard")]
[Authorize]
public class DashboardController(IDbContextFactory<ELIBAPIDbContext> dbFactory) : ControllerBase
{
    [HttpGet("Summary")]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Summary()
    {
        var tenantId = GetTenantId();

        var config     = HttpContext.RequestServices.GetService<IConfiguration>();
        var roleCode   = User.FindFirstValue("RoleCode");
        var tenantCode = User.FindFirstValue("TenantCode");
        var adminRoles = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var tenantCodes = config?.GetSection("ReadOnlyPolicy:TenantCodes").Get<string[]>() ?? [];
        if ((roleCode != null && adminRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase))
            || (tenantCode != null && tenantCodes.Contains(tenantCode, StringComparer.OrdinalIgnoreCase)))
            tenantId = null;

        var now = DateTime.Now;
        var elevenMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        const int dailyTrendDays = 14;
        var since14 = DateTime.Today.AddDays(-(dailyTrendDays - 1));

        // Mỗi khối dưới đây tự tạo 1 ELIBAPIDbContext riêng qua IDbContextFactory (1 context không
        // thread-safe nếu dùng chung giữa các Task chạy song song) và chỉ TRUY VẤN THÔ — mọi tính toán
        // phụ thuộc lẫn nhau (phần trăm, ghép tên, dựng chuỗi ngày) dời xuống sau khi Task.WhenAll xong,
        // chạy tuần tự trong bộ nhớ (không còn await nào), để tránh 1 khối phải chờ khối khác.
        var statsTask = WithContext(async ctx => (
            DigitalBooks: await ctx.EbookItems.CountAsync(x => x.Status == 2 && x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId)),
            PrintTitles: await ctx.Bibs.CountAsync(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId)),
            PrintBooksInStock: await ctx.Barcodes.CountAsync(x => x.Status == "R" && x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId))
        ));

        var borrowTrendTask = WithContext(ctx => ctx.BookOuts
            .Where(x => x.IsDelete != 2 && x.BorrowDate != null && x.BorrowDate >= elevenMonthsAgo
                     && (tenantId == null || x.TenantId == tenantId))
            .GroupBy(x => new { x.BorrowDate!.Value.Year, x.BorrowDate!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync());

        // Lấy thô ReaderId+DueDate của mọi phiếu quá hạn — vừa dùng đếm "bạn đọc quá hạn" (distinct
        // ReaderId), vừa dùng bucket "tuổi nợ" (nhóm theo số ngày quá hạn), tránh 2 lượt truy vấn.
        var overdueTask = WithContext(ctx => ctx.BookOuts
            .Where(x => x.IsDelete != 2 && x.Status == "O" && x.ReaderId != null
                     && x.DueDate != null && x.DueDate < now && (tenantId == null || x.TenantId == tenantId))
            .Select(x => new { x.ReaderId, x.DueDate })
            .ToListAsync());

        var dailyTrendTask = WithContext(async ctx =>
        {
            var borrow = await ctx.BookOuts
                .Where(x => x.IsDelete != 2 && x.BorrowDate != null && x.BorrowDate >= since14
                         && (tenantId == null || x.TenantId == tenantId))
                .GroupBy(x => x.BorrowDate!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
            var ret = await ctx.BookIns
                .Where(x => x.IsDelete != 2 && x.ReturnDate != null && x.ReturnDate >= since14
                         && (tenantId == null || x.TenantId == tenantId))
                .GroupBy(x => x.ReturnDate!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
            return (Borrow: borrow, Return: ret);
        });

        var typeDistTask = WithContext(async ctx =>
        {
            var counts = await ctx.EbookItems
                .Where(x => x.Status == 2 && x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId))
                .GroupBy(x => x.TypeId)
                .Select(g => new { TypeId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
            var names = await ctx.DigTypes
                .Where(x => x.IsDelete != 2 && (tenantId == null || x.TenantId == tenantId))
                .ToDictionaryAsync(x => x.Id, x => x.DescriptionVn);
            return (Counts: counts, Names: names);
        });

        // Top mượn nhiều nhất — giới hạn cùng cửa sổ 12 tháng như borrowTrend (trước đây quét toàn bộ
        // lịch sử không giới hạn, khác biệt duy nhất so với các panel xu hướng còn lại của dashboard).
        var topBorrowedTask = WithContext(async ctx =>
        {
            var mostBorrowed = await (
                from bo in ctx.BookOuts
                where bo.IsDelete != 2 && bo.BorrowDate != null && bo.BorrowDate >= elevenMonthsAgo
                      && (tenantId == null || bo.TenantId == tenantId)
                join bc in ctx.Barcodes.Where(b => b.IsDelete != 2 && (tenantId == null || b.TenantId == tenantId))
                on new { K = bo.Barcode, T = bo.TenantId ?? 0 } equals new { K = bc.BarcodeValue, T = bc.TenantId ?? 0 } into bcj
                from bc in bcj.DefaultIfEmpty()
                where bc != null && bc.BibId != null
                group bc by bc.BibId into g
                select new { BibId = g.Key!.Value, Count = g.Count() }
            ).OrderByDescending(x => x.Count).Take(5).ToListAsync();

            var bibIds = mostBorrowed.Select(x => x.BibId).ToList();
            var bibDict = await ctx.BibXmls.Where(x => bibIds.Contains(x.BibId)).ToDictionaryAsync(x => x.BibId);
            return (MostBorrowed: mostBorrowed, BibDict: bibDict);
        });

        // Ngân sách bổ sung vs chi tiêu thực tế theo quỹ, 12 tháng gần nhất — join AbReceiptDetail →
        // AbReceipt qua Receipt_Id, thành tiền = Amount * Price (đúng công thức AcquisitionReportController
        // đang dùng), so với hạn mức Fund.Blane.
        var budgetTask = WithContext(async ctx =>
        {
            var funds = await ctx.Funds
                .Where(f => f.IsDelete != 2 && (tenantId == null || f.TenantId == tenantId))
                .Select(f => new { f.Id, f.Name, f.Blane })
                .ToListAsync();

            var spending = await (
                from d in ctx.AbReceiptDetails
                where d.IsDelete != 2 && (tenantId == null || d.TenantId == tenantId)
                join r in ctx.AbReceipts on d.Receipt_Id equals r.Id
                where r.IsDelete != 2 && r.Receipt_Date != null && r.Receipt_Date >= elevenMonthsAgo
                      && r.FundId != null
                group d by r.FundId into g
                select new { FundId = g.Key, Total = g.Sum(x => (x.Amount ?? 0) * (x.Price ?? 0)) }
            ).ToListAsync();

            return (Funds: funds, Spending: spending);
        });

        await Task.WhenAll(statsTask, borrowTrendTask, overdueTask, dailyTrendTask, typeDistTask, topBorrowedTask, budgetTask);

        var stats = statsTask.Result;
        var totalDocs = stats.DigitalBooks + stats.PrintTitles;
        var digitizationRate = totalDocs > 0 ? (int)Math.Round(stats.DigitalBooks * 100.0 / totalDocs) : 0;

        // ── Xu hướng mượn sách in theo tháng (cửa sổ trượt 12 tháng) ──
        var borrowRaw = borrowTrendTask.Result;
        var allMonths = Enumerable.Range(0, 12).Select(i => elevenMonthsAgo.AddMonths(i)).ToList();
        var borrowLabels = allMonths.Select(m => $"Th{m.Month}").ToArray();
        var borrowData = allMonths
            .Select(m => borrowRaw.FirstOrDefault(v => v.Year == m.Year && v.Month == m.Month)?.Count ?? 0)
            .ToArray();

        // ── Bạn đọc quá hạn + bucket tuổi nợ (1-7 / 8-30 / 30+ ngày) ──
        var overdueRows = overdueTask.Result;
        var overdueReaders = overdueRows.Select(x => x.ReaderId).Distinct().Count();
        int bucket1To7 = 0, bucket8To30 = 0, bucket30Plus = 0;
        foreach (var row in overdueRows)
        {
            var days = (now - row.DueDate!.Value).Days;
            if (days <= 7) bucket1To7++;
            else if (days <= 30) bucket8To30++;
            else bucket30Plus++;
        }
        var overdueBuckets = new[]
        {
            new { label = "1-7 ngày",  count = bucket1To7 },
            new { label = "8-30 ngày", count = bucket8To30 },
            new { label = "Trên 30 ngày", count = bucket30Plus }
        };

        // ── Mượn/trả theo ngày (14 ngày gần nhất) ──
        var (dailyBorrowRaw, dailyReturnRaw) = dailyTrendTask.Result;
        var dailyTrend = Enumerable.Range(0, dailyTrendDays)
            .Select(i => since14.AddDays(i))
            .Select(d => new
            {
                label    = d.ToString("dd/MM"),
                date     = d.ToString("yyyy-MM-dd"),
                borrowed = dailyBorrowRaw.FirstOrDefault(b => b.Date == d)?.Count ?? 0,
                returned = dailyReturnRaw.FirstOrDefault(r => r.Date == d)?.Count ?? 0
            })
            .ToList();

        // ── Phân bố học liệu số theo loại (DigType) ──
        var (typeCountsRaw, digTypeNames) = typeDistTask.Result;
        var typeDistribution = typeCountsRaw.Select(x => new
        {
            label = x.TypeId.HasValue && digTypeNames.TryGetValue(x.TypeId.Value, out var typeName) && !string.IsNullOrWhiteSpace(typeName)
                ? typeName
                : "Chưa phân loại",
            count = x.Count,
            percentage = stats.DigitalBooks > 0 ? (int)Math.Round(x.Count * 100.0 / stats.DigitalBooks) : 0
        }).ToList();

        // ── Top 5 tài liệu (sách in) được mượn nhiều nhất, 12 tháng gần nhất ──
        var (mostBorrowedRaw, bibDict) = topBorrowedTask.Result;
        var top5Borrowed = mostBorrowedRaw.Select(x =>
        {
            bibDict.TryGetValue(x.BibId, out var bx);
            return new
            {
                bibId = x.BibId,
                title = bx?.Title ?? "",
                author = bx?.Author ?? "",
                ddc = bx?.DDC ?? "",
                count = x.Count
            };
        }).ToList();

        // ── Ngân sách bổ sung vs chi tiêu thực tế theo quỹ ──
        var (funds, spending) = budgetTask.Result;
        var fundBudgetVsSpending = funds
            .Select(f => new
            {
                fundName = f.Name ?? "",
                budget = f.Blane ?? 0,
                spent = spending.FirstOrDefault(s => s.FundId == f.Id)?.Total ?? 0
            })
            .Where(x => x.budget > 0 || x.spent > 0)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            stats = new { totalDocs, digitalBooks = stats.DigitalBooks, printTitles = stats.PrintTitles, printBooksInStock = stats.PrintBooksInStock, digitizationRate },
            borrowTrend = new { labels = borrowLabels, data = borrowData },
            overdueReaders,
            overdueBuckets,
            dailyTrend,
            typeDistribution,
            top5Borrowed,
            fundBudgetVsSpending
        }));
    }

    private async Task<T> WithContext<T>(Func<ELIBAPIDbContext, Task<T>> query)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        return await query(ctx);
    }

    private long? GetTenantId() =>
        long.TryParse(User.FindFirstValue("TenantId"), out var tid) ? tid : null;
}
