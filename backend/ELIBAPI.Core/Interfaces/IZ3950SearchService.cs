using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;

namespace ELIBAPI.Core.Interfaces;

public interface IZ3950SearchService
{
    Task<Z3950BriefResult>  SearchBriefAsync (Z3950Config config, List<Z3950SearchField> fields, string op);
    Task<Z3950DetailResult> SearchDetailAsync(Z3950Config config, List<Z3950SearchField> fields, string op, int pageIndex, int pageSize);

    /// <summary>Lấy trọn bản ghi MARC (đầy đủ trường) tại vị trí `position` (1-based) của kết quả tìm kiếm.</summary>
    Task<(bool connected, List<Iso2709Reader.Field> fields, string? error)> FetchFullRecordAsync(
        Z3950Config config, List<Z3950SearchField> fields, string op, int position);
}
