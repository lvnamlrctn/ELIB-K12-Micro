using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class ReaderRepository
    : BaseRepository<Reader, ReaderSearchRequest, ReaderRequest>,
      IReaderRepository
{
    public ReaderRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Lịch sử thay đổi chi tiết (Đợt 16) — cùng allowlist field nghiệp vụ đã dùng ở
    // AdminMutationGuard.DiffFields (Đợt 10, xem trước import hàng loạt), để nhất quán giữa 2 tính năng
    // diff Reader trong dự án. Field nhạy cảm che giá trị khi ghi log (vẫn phát hiện có đổi hay không).
    protected override string[] AuditedFields =>
    [
        nameof(Reader.LastName), nameof(Reader.FirstName), nameof(Reader.Cardno), nameof(Reader.CitizenId), nameof(Reader.CardUid),
        nameof(Reader.Sex), nameof(Reader.BirthDate), nameof(Reader.Email), nameof(Reader.Phone),
        nameof(Reader.Address), nameof(Reader.IssueDate), nameof(Reader.ExpireDate), nameof(Reader.ClassId),
        nameof(Reader.CourseId), nameof(Reader.OrgId), nameof(Reader.ReaderTypeId), nameof(Reader.DegreeId),
        nameof(Reader.EthenicId), nameof(Reader.ProfId), nameof(Reader.Status),
    ];

    protected override HashSet<string> MaskedAuditFields =>
    [
        nameof(Reader.CitizenId), nameof(Reader.Email), nameof(Reader.Phone), nameof(Reader.Address),
        nameof(Reader.BirthDate),
    ];

    protected override IQueryable<Reader> BuildQuery(ReaderSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))   { var kw = r.Keyword.ToLower(); q = q.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(kw) || (x.Cardno != null && x.Cardno.ToLower().Contains(kw))); }
        if (!string.IsNullOrEmpty(r.Cardno))    { var cn = r.Cardno.ToLower(); q = q.Where(x => x.Cardno != null && x.Cardno.ToLower().Contains(cn)); }
        if (!string.IsNullOrEmpty(r.FirstName)) { var fn = r.FirstName.ToLower(); q = q.Where(x => x.FirstName != null && x.FirstName.ToLower().Contains(fn)); }
        if (!string.IsNullOrEmpty(r.LastName))  { var ln = r.LastName.ToLower();  q = q.Where(x => x.LastName  != null && x.LastName.ToLower().Contains(ln)); }
        if (!string.IsNullOrEmpty(r.PortalId))  q = q.Where(x => x.PortalId  == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language))  q = q.Where(x => x.Language  == r.Language);
        if (r.ReaderTypeId.HasValue)            q = q.Where(x => x.ReaderTypeId == r.ReaderTypeId);
        if (r.OrgId.HasValue)                   q = q.Where(x => x.OrgId        == r.OrgId);
        if (r.ClassId.HasValue)                 q = q.Where(x => x.ClassId      == r.ClassId);
        if (r.CourseId.HasValue)                q = q.Where(x => x.CourseId     == r.CourseId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status       == r.Status);
        if (r.IssuedFrom.HasValue)              q = q.Where(x => x.IssueDate  >= r.IssuedFrom);
        if (r.IssuedTo.HasValue)                q = q.Where(x => x.IssueDate  <= r.IssuedTo);
        if (r.ExpiredFrom.HasValue)             q = q.Where(x => x.ExpireDate >= r.ExpiredFrom);
        if (r.ExpiredTo.HasValue)               q = q.Where(x => x.ExpireDate <= r.ExpiredTo);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ReaderRequest r, Reader e, long userId, bool isNew)
    {
        e.FirstName = r.FirstName; e.LastName = r.LastName; e.Cardno = r.Cardno;
        // Trước đây CCCD gửi từ form bị bỏ qua (chỉ nhập Excel mới lưu) — port ELIB-LRC 10-04.
        e.CitizenId = string.IsNullOrWhiteSpace(r.CitizenId) ? null : r.CitizenId.Trim();
        e.CardUid = CardUid.Normalize(r.CardUid);
        e.Email = r.Email; e.Phone = r.Phone; e.Address = r.Address; e.OrgId = r.OrgId;
        e.ReaderTypeId = r.ReaderTypeId; e.ClassId = r.ClassId; e.CourseId = r.CourseId;
        e.DegreeId = r.DegreeId; e.EthenicId = r.EthenicId; e.ProfId = r.ProfId;
        e.Blane = r.Blane; e.CreatedDate = r.CreatedDate; e.ExpireDate = r.ExpireDate;
        e.IssueDate = r.IssueDate; e.BirthDate = r.BirthDate;
        if (!string.IsNullOrEmpty(r.Password))
            e.Password = PasswordHasher.Hash(r.Password);
        e.PortalId = r.PortalId; e.Language = r.Language; e.Photo = r.Photo;
        e.Status = r.Status; e.Sex = r.Sex;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Reader e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Reader e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<Dictionary<long, string>> GetClassMapAsync(IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        return await _context.Classes
            .Where(c => idList.Contains(c.Id) && c.IsDelete != 2)
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name ?? "");
    }

    public async Task<Dictionary<long, string>> GetCourseMapAsync(IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        return await _context.Courses
            .Where(c => idList.Contains(c.Id) && c.IsDelete != 2)
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name ?? "");
    }

    public async Task<Dictionary<long, string>> GetOrgMapAsync(IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        return await _context.Orgs
            .Where(o => idList.Contains(o.Id) && o.IsDelete != 2)
            .Select(o => new { o.Id, o.Name })
            .ToDictionaryAsync(o => o.Id, o => o.Name ?? "");
    }

    public async Task<ReaderImportResult> ImportAsync(List<ReaderImportRow> rows, string? portalId, string? language, bool overwrite = false, long? readerTypeId = null, bool classByCode = false, bool courseByCode = false, bool orgByCode = false, bool previewOnly = false, bool autoCreateRefs = false)
    {
        var userId = GetCurrentUserId();
        var deptId = GetCurrentTenantId();

        // Trần số dòng khác nhau theo ngữ cảnh: nhập trực tiếp qua UI (chặn cả request HTTP) bị giới hạn
        // chặt hơn nhiều so với chạy qua tác vụ nền (Đợt 10) — _context.BackgroundActorId chỉ có giá trị
        // khi đang chạy trong AdminTaskService.RunNext.
        var rowCap = _context.BackgroundActorId.HasValue ? 10_000 : 1_000;
        if (rows.Count > rowCap)
            throw new InvalidOperationException(
                $"Vượt giới hạn {rowCap} dòng cho {(_context.BackgroundActorId.HasValue ? "1 tác vụ nền" : "nhập trực tiếp")}.");

        var result = new ReaderImportResult { TotalRows = rows.Count };
        foreach (var row in rows) row.CardUid = CardUid.Normalize(row.CardUid);

        // Tự tạo danh mục (ghi thật): mở transaction + khoá theo đơn vị TRƯỚC khi tra danh mục (port ELIB-LRC 09-29).
        // Trước đây danh mục được đọc ngoài transaction → 2 lượt nhập song song cùng thấy "K24" chưa có và cùng tạo,
        // lần nhập sau đó mọi dòng K24 bị báo "mơ hồ". Khoá giữ tới khi transaction kết thúc (commit/rollback bên dưới).
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? refTx = null;
        if (autoCreateRefs && !previewOnly)
        {
            if (_context.Database.CurrentTransaction == null)
                refTx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await LockImportRefsAsync(deptId);
        }

        var classDict  = await _context.Classes.Where(c => c.IsDelete != 2).Select(c => new { c.Id, c.Name, c.TenantId }).ToListAsync();
        var courseDict = await _context.Courses.Where(c => c.IsDelete != 2).Select(c => new { c.Id, c.Name, c.TenantId }).ToListAsync();
        var orgDict    = await _context.Orgs   .Where(o => o.IsDelete != 2).Select(o => new { o.Id, o.Name, o.TenantId }).ToListAsync();

        // Đợt 20: tra theo TÊN chỉ trong đơn vị người nhập (tài khoản hệ thống: mọi đơn vị như cũ) — trước đây tên lớp
        // trùng ở đơn vị khác có thể được gán nhầm. So không phân biệt hoa/thường, bỏ khoảng trắng 2 đầu; tên xuất
        // hiện ở >1 bản ghi trong cùng đơn vị là mơ hồ → báo lỗi dòng thay vì lấy bừa bản đầu.
        static (Dictionary<string, long> Map, HashSet<string> Ambiguous) BuildNameMap(IEnumerable<(long Id, string? Name)> items)
        {
            var groups = items.Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name!.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            return (groups.Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase),
                    groups.Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
        var (classMap,  classAmbiguous)  = BuildNameMap(classDict .Where(c => !deptId.HasValue || c.TenantId == deptId).Select(c => (c.Id, c.Name)));
        var (courseMap, courseAmbiguous) = BuildNameMap(courseDict.Where(c => !deptId.HasValue || c.TenantId == deptId).Select(c => (c.Id, c.Name)));
        var (orgMap,    orgAmbiguous)    = BuildNameMap(orgDict   .Where(o => !deptId.HasValue || o.TenantId == deptId).Select(o => (o.Id, o.Name)));

        // Tự tạo Lớp/Khóa học/Đơn vị chưa có (port ELIB-LRC 09-22, chỉ chế độ tra theo tên). Xem trước: KHÔNG ghi gì,
        // chỉ liệt kê "sẽ tạo" (kết quả xem trước/xác nhận lại phải tất định để chữ ký HMAC khớp). Ghi thật: tạo trong
        // CÙNG transaction với bạn đọc (lỗi ghi bạn đọc → rollback cả danh mục vừa tạo). Bản ghi mới gắn TenantId của
        // người nhập (LRC không gán).
        if (autoCreateRefs)
        {
            List<string> Missing(IEnumerable<string?> names, Dictionary<string, long> map, HashSet<string> ambiguous) =>
                names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(n => !map.ContainsKey(n) && !ambiguous.Contains(n)).ToList();
            var newClasses = classByCode  ? new List<string>() : Missing(rows.Select(r => r.ClassName),  classMap,  classAmbiguous);
            var newCourses = courseByCode ? new List<string>() : Missing(rows.Select(r => r.CourseName), courseMap, courseAmbiguous);
            var newOrgs    = orgByCode    ? new List<string>() : Missing(rows.Select(r => r.OrgName),    orgMap,    orgAmbiguous);

            if (previewOnly)
            {
                result.CreatedRefs.AddRange(newClasses.Select(n => $"Lớp: {n}"));
                result.CreatedRefs.AddRange(newCourses.Select(n => $"Khóa học: {n}"));
                result.CreatedRefs.AddRange(newOrgs.Select(n => $"Đơn vị: {n}"));
            }
            else if (newClasses.Count + newCourses.Count + newOrgs.Count > 0)
            {
                var now = DateTime.Now;
                var classes = newClasses.Select(n => new Class { Name = n, PortalId = portalId, Language = language, TenantId = deptId,
                    PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now }).ToList();
                var courses = newCourses.Select(n => new Course { Name = n, PortalId = portalId, Language = language, TenantId = deptId,
                    PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now }).ToList();
                var orgs = newOrgs.Select(n => new Org { Name = n, ParentId = 0, Level = 1, Status = 2, PortalId = portalId,
                    Language = language, TenantId = deptId, PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now }).ToList();
                _context.Classes.AddRange(classes);
                _context.Courses.AddRange(courses);
                _context.Orgs.AddRange(orgs);
                await _context.SaveChangesAsync();
                foreach (var c in classes) { classMap[c.Name!] = c.Id; result.CreatedRefs.Add($"Lớp: {c.Name}"); }
                foreach (var c in courses) { courseMap[c.Name!] = c.Id; result.CreatedRefs.Add($"Khóa học: {c.Name}"); }
                foreach (var o in orgs)    { orgMap[o.Name!] = o.Id;    result.CreatedRefs.Add($"Đơn vị: {o.Name}"); }
            }
        }

        // Tập Id hợp lệ dùng để xác nhận tồn tại khi tra theo Mã (byCode=true) — lọc đúng TenantId
        // của bạn đọc đang import, tránh cho phép nhập chéo Id của tenant khác.
        var classIdSet  = classDict .Where(c => !deptId.HasValue || c.TenantId == deptId).Select(c => c.Id).ToHashSet();
        var courseIdSet = courseDict.Where(c => !deptId.HasValue || c.TenantId == deptId).Select(c => c.Id).ToHashSet();
        var orgIdSet    = orgDict   .Where(o => !deptId.HasValue || o.TenantId == deptId).Select(o => o.Id).ToHashSet();

        // Tra Lớp/Khóa học/Đơn vị theo Tên (mặc định, hành vi cũ: không khớp thì bỏ qua field, không báo lỗi)
        // hoặc theo Mã (byCode=true: Id số trực tiếp — phải tồn tại thật trong CSDL đúng tenant, nếu không sẽ
        // báo lỗi rõ ràng và bỏ qua cả dòng, thay vì âm thầm để trống).
        static (bool Success, long? Id) ResolveRef(string? rawValue, bool byCode, Dictionary<string, long> nameMap,
            HashSet<string> ambiguousNames, HashSet<long> validIds, string fieldLabel, int rowNumber, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return (true, null);

            if (!byCode)
            {
                if (ambiguousNames.Contains(rawValue.Trim()))
                {
                    errors.Add($"Dòng {rowNumber}: Tên {fieldLabel} '{rawValue.Trim()}' trùng nhiều bản ghi trong đơn vị — dùng chế độ tra theo mã");
                    return (false, null);
                }
                return (true, nameMap.TryGetValue(rawValue.Trim(), out var nameId) ? nameId : null);
            }

            if (!long.TryParse(rawValue.Trim(), out var codeId))
            {
                errors.Add($"Dòng {rowNumber}: Mã {fieldLabel} '{rawValue}' không hợp lệ");
                return (false, null);
            }
            if (!validIds.Contains(codeId))
            {
                errors.Add($"Dòng {rowNumber}: Mã {fieldLabel} '{rawValue}' không tồn tại");
                return (false, null);
            }
            return (true, codeId);
        }

        // Load tất cả cardno trong batch để tránh N+1 query
        var validCardnos = rows.Select(r => r.Cardno)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .Select(c => c!)   // đảm bảo List<string> không phải List<string?>
            .ToList();
        var existingQ = _dbSet.Where(r => r.IsDelete != 2 && r.Cardno != null && validCardnos.Contains(r.Cardno!));
        if (deptId.HasValue) existingQ = existingQ.Where(r => r.TenantId == deptId);
        // Dùng ToListAsync + GroupBy để tránh lỗi duplicate key nếu DB có 2 dòng cùng cardno
        var existingList = await existingQ.ToListAsync();
        var existingMap  = existingList
            .GroupBy(r => r.Cardno!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        // UID thẻ chip (port ELIB-LRC 10-04): không trùng giữa các bạn đọc cùng đơn vị — chủ hiện tại của từng UID trong tệp.
        var importUids = rows.Select(r => r.CardUid).Where(u => u != null).Distinct().ToList();
        var uidQ = _dbSet.Where(r => r.IsDelete != 2 && r.CardUid != null && importUids.Contains(r.CardUid));
        if (deptId.HasValue) uidQ = uidQ.Where(r => r.TenantId == deptId);
        var uidOwners = (await uidQ.Select(r => new { r.CardUid, r.Cardno }).ToListAsync())
            .GroupBy(r => r.CardUid!).ToDictionary(g => g.Key, g => g.First().Cardno ?? "");

        var toInsert = new List<Reader>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Cardno))
            {
                result.Errors.Add($"Dòng {row.RowNumber}: Số thẻ không được để trống");
                result.FailedCount++;
                continue;
            }

            int? sex = row.SexText?.Trim().ToLower() switch { "nam" => 1, "nữ" or "nu" => 2, _ => null };
            string? hashedPwd = null;
            if (!string.IsNullOrWhiteSpace(row.Password))
                hashedPwd = PasswordHasher.Hash(row.Password);
            var (classOk,  classId)  = ResolveRef(row.ClassName,  classByCode,  classMap,  classAmbiguous,  classIdSet,  "lớp",       row.RowNumber, result.Errors);
            var (courseOk, courseId) = ResolveRef(row.CourseName, courseByCode, courseMap, courseAmbiguous, courseIdSet, "khóa học",  row.RowNumber, result.Errors);
            var (orgOk,    orgId)    = ResolveRef(row.OrgName,    orgByCode,    orgMap,    orgAmbiguous,    orgIdSet,    "đơn vị",    row.RowNumber, result.Errors);

            if (!classOk || !courseOk || !orgOk)
            {
                result.FailedCount++;
                continue;
            }

            if (row.CardUid != null)
            {
                if (!CardUid.IsValid(row.CardUid) || (uidOwners.TryGetValue(row.CardUid, out var owner) && !string.Equals(owner, row.Cardno, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Errors.Add($"Dòng {row.RowNumber}: UID thẻ \"{row.CardUid}\" không hợp lệ hoặc đã gán cho bạn đọc khác.");
                    result.FailedCount++;
                    continue;
                }
                uidOwners[row.CardUid] = row.Cardno!;
            }

            if (existingMap.TryGetValue(row.Cardno, out var existing))
            {
                if (!overwrite)
                {
                    result.SkippedCount++;
                    continue;
                }

                // Ghi đè thông tin bạn đọc hiện có
                existing.LastName       = row.LastName;
                existing.FirstName      = row.FirstName;
                // Chỉ ghi đè CCCD khi file có giá trị — tránh xoá trắng dữ liệu đã nhập tay
                // khi import lại bằng file cũ chưa có cột này.
                if (!string.IsNullOrWhiteSpace(row.CitizenId)) existing.CitizenId = row.CitizenId!.Trim();
                if (row.CardUid != null) existing.CardUid = row.CardUid;
                existing.Sex            = sex;
                existing.BirthDate      = ParseDate(row.BirthDateText);
                existing.Email          = row.Email;
                existing.Phone          = row.Phone;
                existing.Address        = row.Address;
                existing.IssueDate      = ParseDate(row.IssueDateText)  ?? DateTime.Now.Date;
                existing.ExpireDate     = ParseDate(row.ExpireDateText) ?? DateTime.Now.Date.AddYears(1);
                existing.ClassId        = classId;
                existing.CourseId       = courseId;
                existing.OrgId          = orgId;
                if (readerTypeId.HasValue) existing.ReaderTypeId = readerTypeId;
                if (!string.IsNullOrEmpty(hashedPwd)) existing.Password = hashedPwd;
                existing.UpdateRowBy    = userId;
                existing.UpdatedRowDate = DateTime.Now;
                result.SuccessCount++;
            }
            else
            {
                toInsert.Add(new Reader
                {
                    Cardno         = row.Cardno,
                    LastName       = row.LastName,
                    FirstName      = row.FirstName,
                    CitizenId      = string.IsNullOrWhiteSpace(row.CitizenId) ? null : row.CitizenId.Trim(),
                    CardUid        = row.CardUid,
                    Sex            = sex,
                    BirthDate      = ParseDate(row.BirthDateText),
                    Email          = row.Email,
                    Password       = hashedPwd,
                    Phone          = row.Phone,
                    Address        = row.Address,
                    IssueDate      = ParseDate(row.IssueDateText)  ?? DateTime.Now.Date,
                    ExpireDate     = ParseDate(row.ExpireDateText) ?? DateTime.Now.Date.AddYears(1),
                    ClassId        = classId,
                    CourseId       = courseId,
                    OrgId          = orgId,
                    ReaderTypeId   = readerTypeId,
                    PortalId       = portalId,
                    Language       = language,
                    Status         = 2,
                    TenantId   = deptId,
                    PublicId       = Guid.NewGuid(),
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now,
                    UpdateRowBy    = userId,
                    UpdatedRowDate = DateTime.Now,
                });
                result.SuccessCount++;
            }
        }

        if (toInsert.Count > 0) _dbSet.AddRange(toInsert);

        // Xem trước (Đợt 10, tác vụ nền): tính diff rồi HUỶ thay đổi Reader vừa stage — không ghi gì cả.
        // Đường nhập trực tiếp qua UI (previewOnly luôn false) không đổi hành vi so với trước.
        //
        // CHỈ detach entity Reader vừa stage — TUYỆT ĐỐI không gọi _context.ChangeTracker.Clear() ở đây:
        // khi chạy trong tác vụ nền, _context là CÙNG 1 instance DbContext (scoped) đang được
        // AdminTaskService.RunNext dùng để theo dõi AdminTask/AdminTaskChunk của chính lượt xử lý này —
        // Clear() sẽ xoá tracking của CẢ những entity đó, khiến các thay đổi RunNext gán sau lời gọi này
        // (chunk.Completed/Result, task.CompletedChunks...) không bao giờ được SaveChanges ghi lại (phát
        // hiện qua kiểm thử trực tiếp: log DB cho thấy hoàn toàn không có UPDATE nào cho AdminTaskChunk).
        if (previewOnly)
        {
            var previewChanges = AdminMutationGuard.BuildChanges(_context);
            foreach (var entry in _context.ChangeTracker.Entries<Reader>().ToList())
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            result.Review = new ReaderMutationReview { Changes = previewChanges };
            return result;
        }

        using var tx = _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
            : null;
        try
        {
            if (result.SuccessCount > 0)
            {
                await _context.SaveChangesAsync();
                try
                {
                    _context.UserLogs.Add(new UserLog
                    {
                        UserId       = userId,
                        ActionType   = "Import",
                        Object       = "Reader",
                        Action       = $"Import {result.SuccessCount} Reader records (overwrite={overwrite})",
                        Submited     = DateTime.Now,
                        Application  = "ELIBAPI",
                        TenantId = deptId
                    });
                    await _context.SaveChangesAsync();
                }
                catch { }
            }
            if (tx != null) await tx.CommitAsync();
            if (refTx != null) await refTx.CommitAsync();
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            if (refTx != null) await refTx.RollbackAsync();
            throw;
        }
        finally
        {
            if (refTx != null) await refTx.DisposeAsync();
        }

        return result;
    }

    // Khoá theo đơn vị (giữ tới hết transaction hiện tại): 2 lượt nhập cùng đơn vị chạy tuần tự ở bước tra/tạo danh mục,
    // khác đơn vị không chặn nhau. Khoá cố định 740212 (như LRC) + TenantId; tài khoản hệ thống dùng 0.
    private async Task LockImportRefsAsync(long? tenantId)
    {
        var key = tenantId ?? 0;
        if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(740212, {0})", (int)(key % int.MaxValue));
        else if (_context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 120000",
                $"reader-import-refs-{key}");
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value.Trim(),
            ["dd/MM/yyyy", "d/M/yyyy", "M/d/yyyy", "yyyy-MM-dd"],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt;
        }
        return DateTime.TryParse(value.Trim(), out var dt2) ? dt2 : null;
    }

    public async Task<int> BulkUpdateAsync(ReaderBulkUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        var q      = ApplyTenantFilter(_dbSet.Where(x => x.IsDelete != 2));

        if (request.PublicIds?.Count > 0)
            q = q.Where(x => request.PublicIds.Contains(x.PublicId));

        if (request.Filter != null)
        {
            var f = request.Filter;
            if (f.OrgId.HasValue)                  q = q.Where(x => x.OrgId        == f.OrgId);
            if (f.ReaderTypeId.HasValue)           q = q.Where(x => x.ReaderTypeId == f.ReaderTypeId);
            if (f.ClassId.HasValue)                q = q.Where(x => x.ClassId      == f.ClassId);
            if (f.Status.HasValue)                 q = q.Where(x => x.Status       == f.Status);
            if (!string.IsNullOrEmpty(f.PortalId)) q = q.Where(x => x.PortalId     == f.PortalId);
        }

        var entities = await q.ToListAsync();
        foreach (var e in entities)
        {
            if (request.ExpireDate.HasValue)   e.ExpireDate   = request.ExpireDate;
            if (request.IssueDate.HasValue)    e.IssueDate    = request.IssueDate;
            if (request.ClassId.HasValue)      e.ClassId      = request.ClassId;
            if (request.CourseId.HasValue)     e.CourseId     = request.CourseId;
            if (request.ReaderTypeId.HasValue) e.ReaderTypeId = request.ReaderTypeId;
            if (request.Status.HasValue)       e.Status       = request.Status;
            e.UpdateRowBy    = userId;
            e.UpdatedRowDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId       = userId,
                ActionType   = "BulkUpdate",
                Object       = "Reader",
                Action       = $"BulkUpdate {entities.Count} Reader records",
                Submited     = DateTime.Now,
                Ip           = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Application  = "ELIBAPI",
                TenantId = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }

        return entities.Count;
    }

    public async Task<int> BatchUpdateAsync(ReaderBatchUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        var q      = ApplyTenantFilter(BuildQuery(request));
        var entities = await q.ToListAsync();
        if (entities.Count == 0) return 0;

        foreach (var e in entities)
        {
            switch (request.Action.ToLower())
            {
                case "changeclass":
                    e.ClassId      = string.IsNullOrWhiteSpace(request.Value) ? null : long.Parse(request.Value);
                    break;
                case "changecourse":
                    e.CourseId     = string.IsNullOrWhiteSpace(request.Value) ? null : long.Parse(request.Value);
                    break;
                case "changereadertype":
                    e.ReaderTypeId = string.IsNullOrWhiteSpace(request.Value) ? null : long.Parse(request.Value);
                    break;
                case "changeissuedate":
                    e.IssueDate    = ParseDate(request.Value);
                    break;
                case "changeexpiredate":
                    e.ExpireDate   = ParseDate(request.Value);
                    break;
                case "changestatus":
                    e.Status       = string.IsNullOrWhiteSpace(request.Value) ? null : int.Parse(request.Value);
                    break;
                case "changepassword":
                    e.Password     = request.Value!;
                    break;
            }
            e.UpdateRowBy    = userId;
            e.UpdatedRowDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId       = userId,
                ActionType   = "BatchUpdate",
                Object       = "Reader",
                Action       = $"BatchUpdate action={request.Action} value={request.Value} on {entities.Count} records",
                Submited     = DateTime.Now,
                Ip           = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Application  = "ELIBAPI",
                TenantId = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }

        return entities.Count;
    }

    public async Task<bool> CardUidTakenAsync(string cardUid, long? tenantId, Guid? excludePublicId = null)
    {
        var uid = CardUid.Normalize(cardUid);
        if (uid == null) return false;
        return await _dbSet.AnyAsync(x => x.IsDelete != 2 && x.CardUid == uid && x.TenantId == tenantId
            && (excludePublicId == null || x.PublicId != excludePublicId));
    }

    public async Task<bool> CheckCardnoExistsAsync(string cardno, Guid? excludePublicId = null)
    {
        var deptId = GetCurrentTenantId();
        var q = _dbSet.Where(x => x.IsDelete != 2 && x.Cardno!.ToLower() == cardno.ToLower());
        if (deptId.HasValue)          q = q.Where(x => x.TenantId == deptId);
        if (excludePublicId.HasValue) q = q.Where(x => x.PublicId     != excludePublicId.Value);
        return await q.AnyAsync();
    }

    public async Task ResetPasswordAsync(Guid publicId, string hashedPassword, long userId)
    {
        var reader = await _dbSet.FirstOrDefaultAsync(r => r.PublicId == publicId && r.IsDelete != 2)
            ?? throw new KeyNotFoundException();
        reader.Password       = hashedPassword;
        reader.UpdateRowBy    = userId;
        reader.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<int> BulkResetPasswordAsync(List<Guid> publicIds, string hashedPassword, long userId)
    {
        var q = ApplyTenantFilter(_dbSet.Where(x => x.IsDelete != 2 && publicIds.Contains(x.PublicId)));
        var entities = await q.ToListAsync();
        foreach (var e in entities)
        {
            e.Password       = hashedPassword;
            e.UpdateRowBy    = userId;
            e.UpdatedRowDate = DateTime.Now;
        }
        await _context.SaveChangesAsync();

        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId      = userId,
                ActionType  = "BulkResetPassword",
                Object      = "Reader",
                Action      = $"BulkResetPassword {entities.Count} Reader records",
                Submited    = DateTime.Now,
                Ip          = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Application = "ELIBAPI",
                TenantId    = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }

        return entities.Count;
    }

    public async Task LockAsync(Guid publicId, string? reason, long? userId)
    {
        var reader = await _dbSet.FirstOrDefaultAsync(r => r.PublicId == publicId && r.IsDelete != 2)
            ?? throw new KeyNotFoundException();
        reader.Status         = 1;
        reader.LockReason     = reason;
        reader.UpdateRowBy    = userId;
        reader.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    private IQueryable<ReaderResponse> ProjectWithNames(IQueryable<Reader> q)
        => from r in q
           join rt in _context.ReaderTypes on r.ReaderTypeId equals rt.Id into rtg
           from rt in rtg.DefaultIfEmpty()
           join o in _context.Orgs on r.OrgId equals o.Id into og
           from o in og.DefaultIfEmpty()
           select new ReaderResponse
           {
               Id             = r.Id,
               FirstName      = r.FirstName,
               LastName       = r.LastName,
               Cardno         = r.Cardno,
               CitizenId      = r.CitizenId,
               CardUid        = r.CardUid,
               Email          = r.Email,
               Phone          = r.Phone,
               Address        = r.Address,
               OrgId          = r.OrgId,
               OrgName        = o.Name,
               ReaderTypeId   = r.ReaderTypeId,
               ReaderTypeName = rt.Name,
               ClassId        = r.ClassId,
               CourseId       = r.CourseId,
               Sex            = r.Sex,
               BirthDate      = r.BirthDate,
               IssueDate      = r.IssueDate,
               ExpireDate     = r.ExpireDate,
               Photo          = r.Photo,
               PortalId       = r.PortalId,
               Language       = r.Language,
               Status         = r.Status,
               IsDelete       = r.IsDelete,
               CreatedRowBy   = r.CreatedRowBy,
               UpdateRowBy    = r.UpdateRowBy,
               CreatedRowDate = r.CreatedRowDate,
               UpdatedRowDate = r.UpdatedRowDate,
               TenantId   = r.TenantId,
               PublicId       = r.PublicId,
           };

    public async Task<PagedResult<ReaderResponse>> SearchWithNamesAsync(ReaderSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilter(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await ProjectWithNames(q)
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<ReaderResponse>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize,
        };
    }

    public async Task<List<ReaderResponse>> SearchAllWithNamesAsync(ReaderSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ProjectWithNames(ApplyTenantFilter(BuildQuery(request), requestTenantId)).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }
}


