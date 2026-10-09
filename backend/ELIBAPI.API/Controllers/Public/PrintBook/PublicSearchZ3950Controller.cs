using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

[Route("api/public/PrintBook/SearchZ3950")]
[AllowAnonymous]
public class PublicSearchZ3950Controller(IZ3950SearchService service, ELIBAPIDbContext db) : ControllerBase
{
    // Port ELIB-LRC 10-04: API không cần đăng nhập — trước đây không giới hạn số thư viện và cỡ trang mỗi yêu cầu
    // (mỗi thư viện là 1 kết nối Z39.50 ra ngoài). Nay tối đa 10 thư viện và 50 bản ghi/trang.
    private const int MaxLibraries = 10;
    private const int MaxPageSize  = 50;

    private Task<List<ELIBAPI.Core.Entities.PrintBook.Z3950Config>> ConfigsAsync(List<Guid> ids)
    {
        var wanted = ids.Distinct().Take(MaxLibraries).ToList();
        return db.Z3950Configs.Where(x => wanted.Contains(x.PublicId) && x.IsDelete != 2).ToListAsync();
    }

    [HttpPost("SearchBrief")]
    public async Task<IActionResult> SearchBrief([FromBody] SearchZ3950Request r)
    {
        var configs = await ConfigsAsync(r.LibraryIds);

        var results = await Task.WhenAll(
            configs.Select(c => service.SearchBriefAsync(c, r.Fields, r.Operator)));

        return Ok(ApiResponse<List<Z3950BriefResult>>.Ok(results.ToList()));
    }

    [HttpPost("SearchDetail")]
    public async Task<IActionResult> SearchDetail([FromBody] SearchZ3950DetailRequest r)
    {
        var configs = await ConfigsAsync(r.LibraryIds);

        var results = await Task.WhenAll(
            configs.Select(c => service.SearchDetailAsync(c, r.Fields, r.Operator, Math.Max(r.PageIndex, 1), Math.Clamp(r.PageSize, 1, MaxPageSize))));

        return Ok(ApiResponse<List<Z3950DetailResult>>.Ok(results.ToList()));
    }
}
