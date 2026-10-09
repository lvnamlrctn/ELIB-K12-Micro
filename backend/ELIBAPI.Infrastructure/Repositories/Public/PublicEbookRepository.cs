using AutoMapper;
using AutoMapper.QueryableExtensions;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicEbookSearchRequest : PublicSearchRequest
{
    public Guid? CollectionId { get; set; }
    public Guid? UserId { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? PublishDate { get; set; }
    public new string? Keyword { get; set; }
    public string? SubmitedFrom { get; set; }
    public string? SubmitedTo { get; set; }
    public int? Status { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? Order { get; set; }
    public long? Id { get; set; }
    public string? SoTapChi { get; set; }
    public string? TenTapChi { get; set; }
    public string? PublishDateFrom { get; set; }
    public string? PublishDateTo { get; set; }
    public Guid? NganhhocId { get; set; }
    public Guid? MonHocId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? DigTypeId { get; set; }
    public string? ItemIdList { get; set; }
}

public class PublicEbookLatestRequest
{
    public Guid TenantId { get; set; }
    public int Top { get; set; }
}

public interface IPublicEbookRepository : IPublicGenericRepository<PublicEbookResponse, PublicEbookSearchRequest>
{
    Task<List<PublicEbookResponse>> GetLatestEbooksAsync(PublicEbookLatestRequest r);
    Task<PublicEbookResponse?> GetEbookByPublicIdAsync(Guid publicId);
    Task<List<PublicEbookFileResponse>> GetEbookFilesByEbookPublicIdAsync(Guid ebookPublicId);
    Task<(EbookFile? file, EbookItem? item)> GetEbookFileWithItemAsync(Guid filePublicId);
    Task<string?> GetSystemParameterValueAsync(string code);
    Task IncrementTotalViewAsync(long ebookItemId);
    Task AddEbookLogAsync(long bookId, Guid? readerPublicId, string? ip, int? page, long? size, long? tenantId);
    Task<FilePermissionResult> CheckFilePermissionAsync(Guid ebookPublicId, Guid? readerPublicId);
}

public class FilePermissionResult
{
    public bool    CanRead     { get; set; }
    public bool    CanDownload { get; set; }
    public int?    MaxPage     { get; set; }
    public string? Reason      { get; set; }
}

public class PublicEbookRepository : PublicBaseRepository<PublicEbookResponse, PublicEbookSearchRequest>, IPublicEbookRepository
{
    private readonly IMapper _mapper;

    public PublicEbookRepository(ELIBAPIDbContext db, IMemoryCache cache, IMapper mapper) : base(db, cache)
    {
        _mapper = mapper;
    }

    protected override IQueryable<PublicEbookResponse> BuildQuery(PublicEbookSearchRequest r)
    {
        var q = _db.EbookItems
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .Where(x => x.IsDelete != 2);

        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.Id.HasValue && r.Id > 0) q = q.Where(x => x.Id == r.Id);

        if (r.TenantId != Guid.Empty)
        {
            var depId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
            if (depId.HasValue) q = q.Where(x => x.TenantId == depId || x.TenantId == null || x.Share > 0);
        }

        if (r.UserId.HasValue && r.UserId != Guid.Empty)
        {
            var userId = _db.Users
                .Where(u => u.PublicId == r.UserId && u.IsDelete != 2)
                .Select(u => (long?)u.Id)
                .FirstOrDefault();
            if (userId.HasValue) q = q.Where(x => x.CreatedBy == userId);
        }

        if (r.CollectionId.HasValue && r.CollectionId != Guid.Empty)
        {
            var col = _db.EbookCollections.FirstOrDefault(x => x.PublicId == r.CollectionId);
            if (col != null)
            {
                var childIds = GetCollectionChildrenIds(col.Id);
                q = q.Where(x => x.CollectionId.HasValue && childIds.Contains(x.CollectionId.Value));
            }
        }

        if (r.TopicId.HasValue && r.TopicId != Guid.Empty)
        {
            var topic = _db.EbookTopics.FirstOrDefault(x => x.PublicId == r.TopicId);
            if (topic != null)
            {
                var childIds = GetTopicChildrenIds(topic.Id);
                q = q.Where(x => x.TopicId.HasValue && childIds.Contains(x.TopicId.Value));
            }
        }

        if (r.SubjectId.HasValue && r.SubjectId != Guid.Empty)
        {
            var sub = _db.EbookSubjects.FirstOrDefault(x => x.PublicId == r.SubjectId);
            if (sub != null)
            {
                var childIds = GetSubjectChildrenIds(sub.Id);
                q = q.Where(x => x.SubjectId.HasValue && childIds.Contains(x.SubjectId.Value));
            }
        }

        if (!string.IsNullOrEmpty(r.Title))       q = FilterByMetaData(q, 64, r.Title);
        if (!string.IsNullOrEmpty(r.Keyword))      q = FilterByMetaData(q, 57, r.Keyword);
        if (!string.IsNullOrEmpty(r.Author))
        {
            var authorLower = r.Author.ToLower();
            q = q.Where(x => (x.ItemXml != null && x.ItemXml.Author != null && x.ItemXml.Author.ToLower().Contains(authorLower)) ||
                             _db.MetaDataValues.Any(m => m.ItemId == x.Id && m.MetaDataFieldId == 3 && m.Value != null && m.Value.ToLower().Contains(authorLower)));
        }
        if (!string.IsNullOrEmpty(r.Publisher))    q = FilterByMetaData(q, 39, r.Publisher);
        if (!string.IsNullOrEmpty(r.PublishDate))  q = FilterByMetaData(q, 15, r.PublishDate);
        if (!string.IsNullOrEmpty(r.PublishDateFrom))
            q = q.Where(x => _db.MetaDataValues.Any(m => m.ItemId == x.Id && m.MetaDataFieldId == 15 && m.Value_UnSign != null && m.Value_UnSign.CompareTo(r.PublishDateFrom) >= 0));
        if (!string.IsNullOrEmpty(r.PublishDateTo))
            q = q.Where(x => _db.MetaDataValues.Any(m => m.ItemId == x.Id && m.MetaDataFieldId == 15 && m.Value_UnSign != null && m.Value_UnSign.CompareTo(r.PublishDateTo) <= 0));
        if (!string.IsNullOrEmpty(r.TenTapChi))   q = FilterByMetaData(q, 43, r.TenTapChi);
        if (!string.IsNullOrEmpty(r.SoTapChi))    q = FilterByMetaData(q, 43, r.SoTapChi);
        if (!string.IsNullOrEmpty(r.SubmitedFrom) && DateTime.TryParse(r.SubmitedFrom, out var dFrom))
            q = q.Where(x => x.Submited >= dFrom);
        if (!string.IsNullOrEmpty(r.SubmitedTo) && DateTime.TryParse(r.SubmitedTo, out var dTo))
            q = q.Where(x => x.Submited <= dTo);

        if (r.DigTypeId.HasValue && r.DigTypeId != Guid.Empty)
        {
            var dtId = _db.DigTypes.Where(x => x.PublicId == r.DigTypeId).Select(x => (long?)x.Id).FirstOrDefault();
            if (dtId.HasValue) q = q.Where(x => x.TypeId == dtId);
        }

        var order = r.Order?.ToLower() ?? "";
        var desc  = order.Contains("desc");
        if (order.Contains("publishdate"))
            q = desc ? q.OrderByDescending(x => x.ItemXml == null ? "" : x.ItemXml.PublishDate)
                     : q.OrderBy(x => x.ItemXml == null ? "" : x.ItemXml.PublishDate);
        else if (order.Contains("createddate"))
            q = desc ? q.OrderByDescending(x => x.CreatedRowDate)
                     : q.OrderBy(x => x.CreatedRowDate);
        else if (order.Contains("title"))
            q = desc ? q.OrderByDescending(x => x.ItemXml == null ? "" : x.ItemXml.Title)
                     : q.OrderBy(x => x.ItemXml == null ? "" : x.ItemXml.Title);
        else if (order.Contains("submited"))
            q = desc ? q.OrderByDescending(x => x.Submited)
                     : q.OrderBy(x => x.Submited);
        else
            q = q.OrderByDescending(x => x.Submited);

        return q.ProjectTo<PublicEbookResponse>(_mapper.ConfigurationProvider);
    }

    private IQueryable<EbookItem> FilterByMetaData(IQueryable<EbookItem> q, int fieldId, string value)
        => q.Where(x => _db.MetaDataValues.Any(m => m.ItemId == x.Id && m.MetaDataFieldId == fieldId && m.Value != null && m.Value.ToLower().Contains(value.ToLower())));

    private List<long> GetCollectionChildrenIds(long parentId)
    {
        var ids = new List<long> { parentId };
        var children = _db.EbookCollections.Where(x => x.ParentId == parentId && x.IsDelete != 2).Select(x => x.Id).ToList();
        foreach (var childId in children) ids.AddRange(GetCollectionChildrenIds(childId));
        return ids.Distinct().ToList();
    }

    private List<long> GetTopicChildrenIds(long parentId)
    {
        var ids = new List<long> { parentId };
        var children = _db.EbookTopics.Where(x => x.ParentId == parentId && x.IsDelete != 2).Select(x => x.Id).ToList();
        foreach (var childId in children) ids.AddRange(GetTopicChildrenIds(childId));
        return ids.Distinct().ToList();
    }

    private List<long> GetSubjectChildrenIds(long parentId)
    {
        var ids = new List<long> { parentId };
        var children = _db.EbookSubjects.Where(x => x.ParentId == parentId && x.IsDelete != 2).Select(x => x.Id).ToList();
        foreach (var childId in children) ids.AddRange(GetSubjectChildrenIds(childId));
        return ids.Distinct().ToList();
    }

    public async Task<List<PublicEbookResponse>> GetLatestEbooksAsync(PublicEbookLatestRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
            TenantId = await _db.Tenants
                .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefaultAsync();

        var q = _db.EbookItems
            .Include(x => x.ItemXml).Include(x => x.Collection).Include(x => x.Subject).Include(x => x.Topic)
            .Where(x => x.IsDelete != 2 && x.Status == 2);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null || x.Share > 0);

        return await q.OrderByDescending(x => x.Submited).Take(r.Top)
            .ProjectTo<PublicEbookResponse>(_mapper.ConfigurationProvider).ToListAsync();
    }

    public async Task<PublicEbookResponse?> GetEbookByPublicIdAsync(Guid publicId)
        => await _db.EbookItems
            .Include(x => x.ItemXml).Include(x => x.Collection).Include(x => x.Subject).Include(x => x.Topic)
            .Where(x => x.PublicId == publicId && x.IsDelete != 2 && x.Status == 2)
            .ProjectTo<PublicEbookResponse>(_mapper.ConfigurationProvider).FirstOrDefaultAsync();

    public async Task<List<PublicEbookFileResponse>> GetEbookFilesByEbookPublicIdAsync(Guid ebookPublicId)
    {
        var ebookId = await _db.EbookItems
            .Where(x => x.PublicId == ebookPublicId && x.IsDelete != 2)
            .Select(x => (long?)x.Id).FirstOrDefaultAsync();
        if (!ebookId.HasValue) return new List<PublicEbookFileResponse>();
        // Đợt 18 (port ELIB-LRC 09-23): chỉ trả file toàn văn (Type "document", không phân biệt hoa/thường) —
        // loại file tóm tắt (Brief)... khỏi API công khai dùng để tải/đọc toàn văn. Không ràng tenant ở đây vì tra
        // theo đúng GUID tài liệu, và tìm kiếm liên thư viện (/bookz3950/:id) cố ý mở tài liệu đơn vị khác.
        return await _db.EbookFiles.Where(x => x.EbookId == ebookId && x.IsDelete != 2
                && x.Type != null && x.Type.ToLower() == "document")
            .OrderBy(x => x.SortOrder).ProjectTo<PublicEbookFileResponse>(_mapper.ConfigurationProvider).ToListAsync();
    }

    public async Task<(EbookFile? file, EbookItem? item)> GetEbookFileWithItemAsync(Guid filePublicId)
    {
        var file = await _db.EbookFiles.FirstOrDefaultAsync(x => x.PublicId == filePublicId && x.IsDelete != 2);
        if (file == null) return (null, null);
        var item = await _db.EbookItems.FirstOrDefaultAsync(x => x.Id == file.EbookId && x.IsDelete != 2);
        return (file, item);
    }

    public async Task<string?> GetSystemParameterValueAsync(string code)
        => await _db.SystemParameters.Where(x => x.Code!.ToLower() == code.ToLower() && x.IsDelete != 2).Select(x => x.Value).FirstOrDefaultAsync();

    public Task IncrementTotalViewAsync(long ebookItemId)
        => _db.EbookItems
            .Where(x => x.Id == ebookItemId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalView, x => (x.TotalView ?? 0) + 1));

    public async Task AddEbookLogAsync(long bookId, Guid? readerPublicId, string? ip, int? page, long? size, long? tenantId)
    {
        long? readerId = null;
        if (readerPublicId.HasValue)
            readerId = await _db.Readers
                .Where(r => r.PublicId == readerPublicId.Value && r.IsDelete != 2)
                .Select(r => (long?)r.Id)
                .FirstOrDefaultAsync();

        _db.EbookLogs.Add(new EbookLog
        {
            Bookid   = bookId,
            ReaderId = readerId,
            Ip       = ip,
            Page     = page,
            Size     = size,
            Type     = 1,
            Submited = DateTime.Now,
            TenantId = tenantId,
            PublicId = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();
    }

    public async Task<FilePermissionResult> CheckFilePermissionAsync(Guid ebookPublicId, Guid? readerPublicId)
    {
        var item = await _db.EbookItems.FirstOrDefaultAsync(x => x.PublicId == ebookPublicId && x.IsDelete != 2);
        if (item == null)
            return new FilePermissionResult { CanRead = false, Reason = "Tài liệu không tồn tại" };

        if (item.Free == 2)
            return new FilePermissionResult { CanRead = true, CanDownload = item.AllowDownload == 2 };

        if (!readerPublicId.HasValue)
            return new FilePermissionResult { CanRead = false, Reason = "Yêu cầu đăng nhập để đọc tài liệu" };

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.PublicId == readerPublicId.Value && r.IsDelete != 2);
        if (reader == null)
            return new FilePermissionResult { CanRead = false, Reason = "Bạn đọc không tồn tại" };

        if (!item.CollectionId.HasValue || reader.ReaderTypeId == null)
            return new FilePermissionResult { CanRead = true, CanDownload = item.AllowDownload == 2 };

        var policy = await _db.Set<PolicyDigitalByCollection>()
            .FirstOrDefaultAsync(p => p.CollectionId == (int)item.CollectionId.Value
                                   && p.ReaderTypeid == (int)reader.ReaderTypeId.Value
                                   && p.IsDelete != 2);

        if (policy == null)
            return new FilePermissionResult { CanRead = true, CanDownload = item.AllowDownload == 2 };

        if (policy.Read != 2)
            return new FilePermissionResult { CanRead = false, Reason = "Không được phép đọc theo chính sách thư viện" };

        return new FilePermissionResult
        {
            CanRead     = true,
            CanDownload = policy.Download == 2,
            MaxPage     = policy.Maxpage
        };
    }
}

