using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class SerialRepository : BaseRepository<Serial, SerialSearchRequest, SerialRequest>
{
    public SerialRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Serial> BuildQuery(SerialSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(SerialRequest r, Serial e, long userId, bool isNew)
    {
        var (lastX, lastY, lastZ) = (e.LastX, e.LastY, e.LastZ);
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        // Port ELIB-LRC 10-03. PropertyMapper bỏ qua "BibId" (coi là khoá của biểu ghi) nên đầu báo của đăng ký trước đây
        // không được lưu.
        e.BibId = r.BibId;
        // LastX/Y/Z là số của kỳ dự kiến gần nhất, do việc sinh kỳ ghi. Form không gửi các trường này nên trước đây
        // mỗi lần sửa đăng ký chúng bị đặt về null và lần sinh sau đánh số lại từ đầu (trùng số đã có).
        e.LastX = r.LastX ?? lastX; e.LastY = r.LastY ?? lastY; e.LastZ = r.LastZ ?? lastZ;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Serial e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Serial e, int status, long userId) { }
}
