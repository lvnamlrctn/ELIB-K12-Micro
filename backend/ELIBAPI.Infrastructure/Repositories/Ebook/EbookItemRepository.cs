using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Jobs;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookItemRepository
    : BaseRepository<EbookItem, EbookItemSearchRequest, EbookItemRequest>,
      IEbookItemRepository
{
    private readonly IElasticsearchService _elastic;

    public EbookItemRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http,
        IElasticsearchService elastic) : base(ctx, http)
    {
        _elastic = elastic;
    }

    // Lịch sử thay đổi chi tiết (Đợt 17) — UpdateEbookAsync/DeleteAsync bỏ qua đường generic
    // BaseRepository.UpdateAsync nên không dùng được hook AuditedFields, gọi EntityAuditService thủ công.
    private static readonly string[] ItemAuditedFields =
    [
        nameof(EbookItem.CollectionId), nameof(EbookItem.Status), nameof(EbookItem.AllowDownload),
        nameof(EbookItem.Free), nameof(EbookItem.SubjectId), nameof(EbookItem.TypeId), nameof(EbookItem.TopicId),
        nameof(EbookItem.Show), nameof(EbookItem.IndexContent), nameof(EbookItem.Share),
        nameof(EbookItem.PrintCopies), nameof(EbookItem.OfflineDays),
    ];
    private static readonly HashSet<string> EmptyMasked = [];

    // Đúng ID metaDataFieldId dùng trong khối rebuild EbookItemXml ngay bên dưới (First/Joined 64/65/3/2/39/15/57).
    private static string MetaDataFieldLabel(int? id) => id switch
    {
        64 => "Title", 65 => "OtherTitle", 3 => "Author", 2 => "OldAuthor",
        39 => "Publisher", 15 => "PublishDate", 57 => "Keyword",
        _ => $"Metadata #{id}",
    };

    public async Task SyncElasticAsync(Guid publicId)
    {
        var item = await _dbSet
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2);
        if (item == null) return;

        var meta = await _context.MetaDataValues
            .Where(m => m.ItemId == item.Id && m.IsDelete != 2
                     && (m.MetaDataFieldId == 20 || m.MetaDataFieldId == 23))
            .ToListAsync();

        var pdfFile = await _context.EbookFiles
            .Where(f => f.EbookId == item.Id && f.IsDelete != 2
                     && (f.FileExt == "pdf" || f.FileExt == ".pdf" || f.FileType == "application/pdf"))
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.Id)
            .FirstOrDefaultAsync();

        var title     = item.ItemXml?.Title;
        var author    = item.ItemXml?.Author;
        var publisher = item.ItemXml?.Publisher;
        var keyword   = item.ItemXml?.Keyword;
        var colName   = item.Collection?.Name;
        var subName   = item.Subject?.Name;
        var topName   = item.Topic?.Name;

        var doc = new EbookItemDocument
        {
            EbookId        = item.Id,
            Title          = title,
            Author         = author,
            PublishDate    = item.ItemXml?.PublishDate,
            Publisher      = publisher,
            Keyword        = keyword,
            Isbn           = meta.FirstOrDefault(m => m.MetaDataFieldId == 20)?.Value,
            Issn           = meta.FirstOrDefault(m => m.MetaDataFieldId == 23)?.Value,
            CollectionId   = item.CollectionId,
            CollectionName = colName,
            SubjectId      = item.SubjectId,
            SubjectName    = subName,
            TopicId        = item.TopicId,
            TopicName      = topName,
            Images         = item.Images,
            Content        = string.Join(" ", new[] { title, author, publisher, keyword, colName, subName, topName }
                                 .Where(s => !string.IsNullOrEmpty(s))),
            EbookFileId    = pdfFile?.PublicId.ToString(),
        };

        await _elastic.UpsertEbookItemAsync(doc);
    }

    public async Task<int> BulkMoveCollectionAsync(EbookItemBulkMoveCollectionRequest request)
    {
        var collectionExists = await _context.EbookCollections.AnyAsync(x => x.Id == request.CollectionId && x.IsDelete != 2);
        if (!collectionExists) throw new Exception("Bộ sưu tập đích không tồn tại");

        var userId = GetCurrentUserId();
        var q = ApplyTenantFilter(_dbSet.Where(x => x.IsDelete != 2 && request.PublicIds.Contains(x.PublicId)));
        var entities = await q.ToListAsync();
        foreach (var e in entities)
        {
            e.CollectionId   = request.CollectionId;
            e.UpdateRowBy    = userId;
            e.UpdatedRowDate = DateTime.Now;
        }
        await _context.SaveChangesAsync();

        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId      = userId,
                ActionType  = "BulkMoveCollection",
                Object      = "EbookItem",
                Action      = $"Move {entities.Count} EbookItem to CollectionId={request.CollectionId}",
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

    public async Task<int> BulkSyncElasticAsync()
    {
        var items = await _dbSet
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .Where(x => x.IsDelete != 2)
            .AsNoTracking()
            .ToListAsync();

        if (items.Count == 0) return 0;

        var itemIds = items.Select(x => x.Id).ToList();
        var metaLookup = (await _context.MetaDataValues
            .Where(m => m.ItemId.HasValue && itemIds.Contains(m.ItemId.Value) && m.IsDelete != 2
                     && (m.MetaDataFieldId == 20 || m.MetaDataFieldId == 23))
            .AsNoTracking()
            .ToListAsync())
            .GroupBy(m => m.ItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pdfFileLookup = (await _context.EbookFiles
            .Where(f => itemIds.Contains(f.EbookId ?? 0) && f.IsDelete != 2
                     && (f.FileExt == "pdf" || f.FileExt == ".pdf" || f.FileType == "application/pdf"))
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.Id)
            .AsNoTracking()
            .ToListAsync())
            .GroupBy(f => f.EbookId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var docs = items.Select(item =>
        {
            var meta   = metaLookup.GetValueOrDefault(item.Id) ?? [];
            var title  = item.ItemXml?.Title;
            var author = item.ItemXml?.Author;
            var pub    = item.ItemXml?.Publisher;
            var kw     = item.ItemXml?.Keyword;
            var pdfFile = pdfFileLookup.GetValueOrDefault(item.Id);
            return new EbookItemDocument
            {
                EbookId        = item.Id,
                Title          = title,
                Author         = author,
                PublishDate    = item.ItemXml?.PublishDate,
                Publisher      = pub,
                Keyword        = kw,
                Isbn           = meta.FirstOrDefault(m => m.MetaDataFieldId == 20)?.Value,
                Issn           = meta.FirstOrDefault(m => m.MetaDataFieldId == 23)?.Value,
                CollectionId   = item.CollectionId,
                CollectionName = item.Collection?.Name,
                SubjectId      = item.SubjectId,
                SubjectName    = item.Subject?.Name,
                TopicId        = item.TopicId,
                TopicName      = item.Topic?.Name,
                Images         = item.Images,
                Content        = string.Join(" ", new[] { title, author, pub, kw,
                                     item.Collection?.Name, item.Subject?.Name, item.Topic?.Name }
                                     .Where(s => !string.IsNullOrEmpty(s))),
                EbookFileId    = pdfFile?.PublicId.ToString(),
            };
        });

        await _elastic.BulkUpsertEbookItemsAsync(docs);
        return items.Count;
    }

    public async Task<BulkSyncChunksResult> BulkSyncChunksAsync(BulkSyncChunksRequest request)
    {
        var forceReindex = request.ForceReindex;
        // Đợt 20: chỉ tài liệu của đơn vị người gọi (tài khoản hệ thống: tất cả) — trước đây admin 1 đơn vị kích hoạt
        // được việc index lại tài liệu số của MỌI đơn vị (ELIB không có global query filter theo tenant).
        var callerTenantId = GetCurrentTenantId();
        var query = _dbSet.Where(x => x.IsDelete != 2 && (!callerTenantId.HasValue || x.TenantId == callerTenantId));
        if (!forceReindex)
            query = query.Where(x => x.IndexedAt == null);
        if (request.IdFrom.HasValue)
            query = query.Where(x => x.Id >= request.IdFrom.Value);
        if (request.IdTo.HasValue)
            query = query.Where(x => x.Id < request.IdTo.Value);

        var publicIds = await query
            .Select(x => x.PublicId)
            .ToListAsync();

        foreach (var pid in publicIds)
            BackgroundJob.Enqueue<EbookIndexingJob>(x => x.RunAsync(pid));

        var totalQuery = _dbSet.Where(x => x.IsDelete != 2 && (!callerTenantId.HasValue || x.TenantId == callerTenantId));
        if (request.IdFrom.HasValue)
            totalQuery = totalQuery.Where(x => x.Id >= request.IdFrom.Value);
        if (request.IdTo.HasValue)
            totalQuery = totalQuery.Where(x => x.Id < request.IdTo.Value);

        var totalCount = forceReindex
            ? publicIds.Count
            : await totalQuery.CountAsync();

        return new BulkSyncChunksResult
        {
            Enqueued = publicIds.Count,
            Skipped  = totalCount - publicIds.Count,
        };
    }

    public async Task<EbookItem> AddEbookAsync(EbookItemAddRequest request)
    {
        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var userId = GetCurrentUserId();
            var deptId = GetCurrentTenantId();
            var now    = DateTime.Now;

            // 1. Insert EbookItem
            var item = new EbookItem
            {
                CollectionId = request.CollectionId, Images       = request.Images,
                Status        = request.IsPublished.HasValue   ? (byte?)(request.IsPublished.Value   ? 2 : 1) : request.Status,
                AllowDownload = request.AllowDownload.HasValue ? (request.AllowDownload.Value ? 2 : 1) : null,
                Free          = request.IsFree.HasValue        ? (request.IsFree.Value        ? 2 : 1) : null,
                SubjectId    = request.SubjectId,   TypeId       = request.TypeId,
                TopicId      = request.TopicId,
                PortalId     = request.PortalId,     Language     = request.Language,
                Show         = request.Show,         IndexContent = request.IndexContent,
                Share        = request.Share,        Submited     = now,
                PrintCopies  = request.PrintCopies,   OfflineDays  = request.OfflineDays,
                CreatedRowBy = userId, CreatedRowDate = now,
                UpdateRowBy  = userId, UpdatedRowDate = now,
                TenantId = deptId, PublicId = Guid.NewGuid()
            };
            _dbSet.Add(item);
            await _context.SaveChangesAsync();

            // 2. Insert MetaDataValues
            var metas     = new List<MetaDataValue>();
            var submitted = now.ToString("yyyy-MM-ddTHH:mm:ssZ");

            void AddMeta(int fieldId, string? value, int sort = 1)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                metas.Add(new MetaDataValue
                {
                    MetaDataFieldId = fieldId, Value = value,
                    Language = "vi", ItemId = item.Id, SortOrder = sort,
                    CreatedRowBy = userId, CreatedRowDate = now,
                    UpdateRowBy  = userId, UpdatedRowDate = now,
                    TenantId = deptId, PublicId = Guid.NewGuid()
                });
            }

            AddMeta(64, request.Title);
            for (int i = 0; i < (request.OtherTitles?.Count ?? 0); i++) AddMeta(65, request.OtherTitles![i], i + 1);
            for (int i = 0; i < (request.Authors?.Count    ?? 0); i++) AddMeta(3,  request.Authors![i],     i + 1);
            for (int i = 0; i < (request.Advisors?.Count   ?? 0); i++) AddMeta(2,  request.Advisors![i],    i + 1);
            AddMeta(39, request.Publisher);
            if (request.TypeId.HasValue)
            {
                var typeCode = await _context.DigTypes
                    .Where(d => d.Id == request.TypeId && d.IsDelete != 2)
                    .Select(d => d.Code).FirstOrDefaultAsync();
                AddMeta(66, typeCode);
            }
            AddMeta(15, request.PublishDate);
            for (int i = 0; i < (request.Keywords?.Count ?? 0); i++) AddMeta(57, request.Keywords![i], i + 1);
            AddMeta(18, request.Citation);
            AddMeta(25, request.Uri);
            for (int i = 0; i < (request.Series?.Count ?? 0); i++) AddMeta(43, request.Series![i], i + 1);
            AddMeta(20, request.Isbn);
            AddMeta(23, request.Issn);
            AddMeta(38, request.DocLanguage);
            AddMeta(27, request.Abstract);
            AddMeta(29, request.Sponsor);
            AddMeta(26, request.Description);
            AddMeta(11, submitted); AddMeta(82, submitted);
            AddMeta(12, submitted); AddMeta(74, submitted);
            if (request.ExtraMetadata != null)
                foreach (var x in request.ExtraMetadata) AddMeta(x.MetaDataFieldId, x.Value, x.SortOrder);

            _context.MetaDataValues.AddRange(metas);
            await _context.SaveChangesAsync();

            // 3. Insert EbookItemXml (Id = EbookItem.Id — 1:1 shared PK)
            var xml = new EbookItemXml
            {
                Id          = item.Id,
                Title       = request.Title,
                OtherTitle  = request.OtherTitles?.Count > 0 ? string.Join("; ", request.OtherTitles) : null,
                Author      = request.Authors?.Count    > 0 ? string.Join("; ", request.Authors)    : null,
                OldAuthor   = request.Advisors?.Count   > 0 ? string.Join("; ", request.Advisors)   : null,
                Publisher   = request.Publisher,
                PublishDate = request.PublishDate,
                Keyword     = request.Keywords?.Count   > 0 ? string.Join("; ", request.Keywords)   : null,
                CreatedRowBy = userId, CreatedRowDate = now,
                UpdateRowBy  = userId, UpdatedRowDate = now,
                TenantId = deptId, PublicId = Guid.NewGuid()
            };
            _context.EbookItemXmls.Add(xml);
            await _context.SaveChangesAsync();

            // 4. Ghi nhận biên mục
            _context.TheodoiBienmucEbooks.Add(new TheodoiBienmucEbook
            {
                UserId       = (int?)userId, DigId  = item.Id,
                Status       = "Add",        Submited = now,
                CreatedRowBy = userId, CreatedRowDate = now,
                UpdateRowBy  = userId, UpdatedRowDate = now,
                TenantId = deptId, PublicId = Guid.NewGuid()
            });
            await _context.SaveChangesAsync();

            await tx.CommitAsync();

            try { await SyncElasticAsync(item.PublicId); } catch { }

            try
            {
                _context.UserLogs.Add(new UserLog
                {
                    UserId = userId, ActionType = "Add", Object = nameof(EbookItem),
                    Action = $"Add EbookItem ID={item.Id}", Submited = now,
                    Ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Application = "ELIBAPI", TenantId = deptId
                });
                await _context.SaveChangesAsync();
            }
            catch { }

            return item;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<EbookItem> UpdateEbookAsync(Guid publicId, EbookItemRequest request)
    {
        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var userId = GetCurrentUserId();
            var deptId = GetCurrentTenantId();
            var now    = DateTime.Now;

            // 1. Load item & check ownership
            var item = await _dbSet
                .FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2)
                ?? throw new KeyNotFoundException("NotFound");

            await CheckTenantOwnershipAsync(item);

            // 2. Update EbookItem fields
            MapRequestToEntity(request, item, userId, false);
            // Lịch sử thay đổi chi tiết (Đợt 17) — PHẢI diff trước SaveChangesAsync (ChangeTracker mất
            // OriginalValue sau khi lưu). Không dùng hook AuditedFields của BaseRepository.UpdateAsync vì
            // hàm này bỏ qua đường generic đó hoàn toàn — gọi thủ công, gộp cùng metadata/xml bên dưới rồi
            // ghi 1 sự kiện atomic trong transaction `tx` đã bọc sẵn toàn hàm.
            var itemChanges = EntityAuditService.DiffTrackedEntity(_context, item, ItemAuditedFields, EmptyMasked);
            await _context.SaveChangesAsync();

            // 3. Update / Insert MetaDataValues (chỉ khi client gửi entries)
            if (request.MetaDataEntries.Count > 0)
            {
                foreach (var entry in request.MetaDataEntries)
                {
                    if (entry.Id > 0)
                    {
                        var meta = await _context.MetaDataValues
                            .FirstOrDefaultAsync(m => m.Id == entry.Id && m.ItemId == item.Id && m.IsDelete != 2);
                        if (meta != null)
                        {
                            meta.Value          = entry.Value;
                            meta.SortOrder      = entry.SortOrder;
                            meta.UpdateRowBy    = userId;
                            meta.UpdatedRowDate = now;
                        }
                    }
                    else if (entry.MetaDataFieldId.HasValue)
                    {
                        _context.MetaDataValues.Add(new MetaDataValue
                        {
                            MetaDataFieldId = entry.MetaDataFieldId.Value,
                            Value           = entry.Value,
                            Language        = "vi",
                            ItemId          = item.Id,
                            SortOrder       = entry.SortOrder,
                            CreatedRowBy    = userId, CreatedRowDate = now,
                            UpdateRowBy     = userId, UpdatedRowDate = now,
                            TenantId    = deptId, PublicId = Guid.NewGuid()
                        });
                    }
                }
                // Diff MetaDataValue qua ChangeTracker (Added/Modified) — TRƯỚC SaveChangesAsync cùng lý
                // do ở bước 2. Field = tên thân thiện theo MetaDataFieldId (đúng ID dùng ở bước rebuild
                // Xml ngay dưới, không suy đoán).
                var metaChanges = new List<EntityAuditService.FieldChange>();
                foreach (var entry in _context.ChangeTracker.Entries<MetaDataValue>())
                {
                    if (entry.Entity.ItemId != item.Id) continue;
                    if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
                    {
                        var prop = entry.Property(nameof(MetaDataValue.Value));
                        if (!prop.IsModified) continue;
                        metaChanges.Add(new EntityAuditService.FieldChange
                        {
                            Field = MetaDataFieldLabel(entry.Entity.MetaDataFieldId),
                            OldValue = prop.OriginalValue as string,
                            NewValue = prop.CurrentValue as string,
                        });
                    }
                    else if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                    {
                        metaChanges.Add(new EntityAuditService.FieldChange
                        {
                            Field = MetaDataFieldLabel(entry.Entity.MetaDataFieldId),
                            OldValue = null,
                            NewValue = entry.Entity.Value,
                        });
                    }
                }
                await _context.SaveChangesAsync();

                // 4. Rebuild EbookItemXml từ toàn bộ MetaDataValues hiện tại
                var allMeta = await _context.MetaDataValues
                    .Where(m => m.ItemId == item.Id && m.IsDelete != 2)
                    .OrderBy(m => m.SortOrder)
                    .ToListAsync();

                string? First(int fieldId) =>
                    allMeta.FirstOrDefault(m => m.MetaDataFieldId == fieldId)?.Value;

                string? Joined(int fieldId)
                {
                    var vals = allMeta
                        .Where(m => m.MetaDataFieldId == fieldId && !string.IsNullOrEmpty(m.Value))
                        .Select(m => m.Value!)
                        .ToList();
                    return vals.Count > 0 ? string.Join("; ", vals) : null;
                }

                var xml = await _context.EbookItemXmls.FirstOrDefaultAsync(x => x.Id == item.Id);
                if (xml != null)
                {
                    xml.Title       = First(64);
                    xml.OtherTitle  = Joined(65);
                    xml.Author      = Joined(3);
                    xml.OldAuthor   = Joined(2);
                    xml.Publisher   = First(39);
                    xml.PublishDate = First(15);
                    xml.Keyword     = Joined(57);
                    xml.UpdateRowBy    = userId;
                    xml.UpdatedRowDate = now;
                }
                else
                {
                    xml = new EbookItemXml
                    {
                        Id          = item.Id,
                        Title       = First(64),
                        OtherTitle  = Joined(65),
                        Author      = Joined(3),
                        OldAuthor   = Joined(2),
                        Publisher   = First(39),
                        PublishDate = First(15),
                        Keyword     = Joined(57),
                        CreatedRowBy = userId, CreatedRowDate = now,
                        UpdateRowBy  = userId, UpdatedRowDate = now,
                        TenantId = deptId, PublicId = Guid.NewGuid()
                    };
                    _context.EbookItemXmls.Add(xml);
                }
                // Không diff riêng EbookItemXml: nó chỉ là bản dựng lại thuần từ MetaDataValue ngay phía
                // trên (không phải nội dung người dùng tự sửa độc lập) — diff riêng sẽ tạo dòng trùng lặp
                // với chính field metadata vừa ghi (ví dụ "Title" xuất hiện 2 lần cùng giá trị đổi). `xml`
                // vẫn cần gán/tạo như cũ để giữ đúng hành vi hiện có, chỉ không đưa vào sự kiện audit.
                await _context.SaveChangesAsync();

                itemChanges.AddRange(metaChanges);
            }

            // Lịch sử thay đổi chi tiết (Đợt 17) — gộp Item + MetaDataValue thành 1 sự kiện, queue trước
            // SaveChangesAsync cuối cùng của hàm (dòng dưới) — vẫn nằm trong `tx` đã bọc sẵn toàn hàm nên
            // atomic thật sự với tx.CommitAsync() (khác PrintBib — xem CatalogueBookController).
            if (itemChanges.Count > 0)
            {
                var actorName = await _context.Users.AsNoTracking().Where(u => u.Id == userId)
                    .Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync();
                var reason = EntityAuditService.ReadReasonHeader(_http.HttpContext);
                EntityAuditService.QueueEntityChangeLog(_context, "DigitalDocument", item.PublicId, userId,
                    actorName, deptId, "Update", reason, itemChanges,
                    _http.HttpContext?.Connection.RemoteIpAddress?.ToString());
            }

            // Ghi nhận biên mục
            _context.TheodoiBienmucEbooks.Add(new TheodoiBienmucEbook
            {
                UserId       = (int?)userId, DigId  = item.Id,
                Status       = "Update",     Submited = now,
                CreatedRowBy = userId, CreatedRowDate = now,
                UpdateRowBy  = userId, UpdatedRowDate = now,
                TenantId = deptId, PublicId = Guid.NewGuid()
            });
            await _context.SaveChangesAsync();

            await tx.CommitAsync();

            // 5. Sync Elasticsearch
            try { await SyncElasticAsync(item.PublicId); } catch { }

            // 6. [MỚI] Nếu đã indexed chunks: update metadata trong ebook_chunks (không re-extract PDF)
            if (item.IndexedAt != null && request.MetaDataEntries.Count > 0)
            {
                try
                {
                    var allMeta = await _context.MetaDataValues
                        .Where(m => m.ItemId == item.Id && m.IsDelete != 2)
                        .AsNoTracking().ToListAsync();
                    string? First(int fid)  => allMeta.FirstOrDefault(m => m.MetaDataFieldId == fid)?.Value;
                    string? Joined(int fid) => string.Join("; ", allMeta
                        .Where(m => m.MetaDataFieldId == fid && !string.IsNullOrEmpty(m.Value))
                        .OrderBy(m => m.SortOrder).Select(m => m.Value!));

                    var xml = await _context.EbookItemXmls.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == item.Id);
                    var metadata = new ELIBAPI.Core.DTOs.Elasticsearch.EbookChunkDocument
                    {
                        EbookId       = item.Id,
                        PublicId      = item.PublicId,
                        Title         = xml?.Title,
                        Author        = xml?.Author,
                        Publisher     = xml?.Publisher,
                        PublishDate   = xml?.PublishDate,
                        Keyword       = xml?.Keyword,
                        DcSubject     = Joined(57),
                        DcDescription = First(27),
                        DcLanguage    = First(38),
                        DcIdentifier  = First(20) ?? First(23),
                        DcType        = First(66),
                        DcContributor = Joined(3) is { Length: > 0 } contrib ? contrib : xml?.Author,
                    };
                    await _elastic.UpdateChunkMetadataAsync(item.Id, metadata);
                }
                catch { }
            }

            // 6. UserLog
            try
            {
                _context.UserLogs.Add(new UserLog
                {
                    UserId      = userId, ActionType = "Update", Object = nameof(EbookItem),
                    Action      = $"Update EbookItem ID={item.Id}", Submited = now,
                    Ip          = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Application = "ELIBAPI", TenantId = deptId
                });
                await _context.SaveChangesAsync();
            }
            catch { }

            return item;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<EbookItemDetailResponse?> GetDetailAsync(Guid publicId)
    {
        var item = await _dbSet
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .Include(x => x.Files.Where(f => f.IsDelete != 2))
            .FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2);

        if (item == null) return null;

        var metaData = await _context.MetaDataValues
            .Where(m => m.ItemId == item.Id && m.IsDelete != 2)
            .OrderBy(m => m.MetaDataFieldId)
            .ThenBy(m => m.SortOrder)
            .ToListAsync();

        return new EbookItemDetailResponse { Item = item, MetaData = metaData };
    }

    public Task<List<EbookItem>> GetItemsWithOldImagesAsync(string minioPublicBaseUrl)
        => _dbSet
            .Where(i => i.IsDelete != 2 && i.Images != null
                && !i.Images.StartsWith(minioPublicBaseUrl)
                && !EF.Functions.Like(i.Images, "[0-9][0-9][0-9][0-9]/%"))
            .AsNoTracking()
            .ToListAsync();

    public async Task<bool> UpdateItemImagesAsync(long id, string? images)
    {
        var affected = await _dbSet
            .Where(i => EF.Property<long>(i, "Id") == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Images, images));
        return affected > 0;
    }

    protected override IQueryable<EbookItem> BuildQuery(EbookItemSearchRequest r)
    {
        var q = _dbSet
            .Include(x => x.ItemXml)
            .Include(x => x.Collection)
            .Include(x => x.Subject)
            .Include(x => x.Topic)
            .Include(x => x.Files)
            .Where(x => x.IsDelete != 2);

        if (!string.IsNullOrEmpty(r.PortalId))     q = q.Where(x => x.PortalId     == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language))     q = q.Where(x => x.Language     == r.Language);
        if (r.CollectionId.HasValue)               q = q.Where(x => x.CollectionId == r.CollectionId);
        if (r.SubjectId.HasValue)                  q = q.Where(x => x.SubjectId    == r.SubjectId);
        if (r.TypeId.HasValue)                     q = q.Where(x => x.TypeId       == r.TypeId);
        if (r.TopicId.HasValue)                    q = q.Where(x => x.TopicId      == r.TopicId);
        if (r.Status.HasValue && r.Status > 0)     q = q.Where(x => x.Status       == (byte)r.Status);
        if (r.SubmitedFrom.HasValue)               q = q.Where(x => x.Submited     >= r.SubmitedFrom);
        if (r.SubmitedTo.HasValue)                 q = q.Where(x => x.Submited     <= r.SubmitedTo);

        if (!string.IsNullOrEmpty(r.Title))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.Title != null && x.ItemXml.Title.ToLower().Contains(r.Title.ToLower()));
        if (!string.IsNullOrEmpty(r.Author))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.Author != null && x.ItemXml.Author.ToLower().Contains(r.Author.ToLower()));
        if (!string.IsNullOrEmpty(r.Publisher))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.Publisher != null && x.ItemXml.Publisher.ToLower().Contains(r.Publisher.ToLower()));
        if (!string.IsNullOrEmpty(r.Keyword))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.Keyword != null && x.ItemXml.Keyword.ToLower().Contains(r.Keyword.ToLower()));
        if (!string.IsNullOrEmpty(r.PublishDateFrom))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.PublishDate != null
                           && string.Compare(x.ItemXml.PublishDate, r.PublishDateFrom) >= 0);
        if (!string.IsNullOrEmpty(r.PublishDateTo))
            q = q.Where(x => x.ItemXml != null && x.ItemXml.PublishDate != null
                           && string.Compare(x.ItemXml.PublishDate, r.PublishDateTo) <= 0);

        return q.OrderByDescending(x => x.Id);
    }

    public async Task<List<EbookMetaDataExportRow>> ExportMetaDataAsync(EbookItemSearchRequest request)
    {
        var items   = await BuildQuery(request).ToListAsync();
        var itemIds = items.Select(x => x.Id).ToList();

        var allMeta = await _context.MetaDataValues
            .Where(m => m.ItemId != null && itemIds.Contains(m.ItemId.Value) && m.IsDelete != 2)
            .ToListAsync();

        var metaLookup = allMeta.ToLookup(m => m.ItemId!.Value);

        return items.Select(item => new EbookMetaDataExportRow
        {
            ItemId      = item.Id,
            Title       = item.ItemXml?.Title,
            Author      = item.ItemXml?.Author,
            Publisher   = item.ItemXml?.Publisher,
            PublishDate = item.ItemXml?.PublishDate,
            Keyword     = item.ItemXml?.Keyword,
            Metadata    = metaLookup[item.Id].Select(m => new MetaDataValueItem
            {
                FieldId   = m.MetaDataFieldId ?? 0,
                Value     = m.Value,
                SortOrder = m.SortOrder
            }).ToList()
        }).ToList();
    }

    public async Task<List<DSpaceItemExportRow>> ExportDSpaceDataAsync(EbookItemSearchRequest request, bool includeFiles)
    {
        var query = BuildQuery(request);
        if (includeFiles) query = query.Include(x => x.Files);
        var items   = await query.AsNoTracking().ToListAsync();
        var itemIds = items.Select(x => x.Id).ToList();

        var allMeta = await _context.MetaDataValues
            .Where(m => m.ItemId != null && itemIds.Contains(m.ItemId.Value) && m.IsDelete != 2)
            .OrderBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        var fieldIds = allMeta
            .Where(m => m.MetaDataFieldId.HasValue)
            .Select(m => (long)m.MetaDataFieldId!.Value)
            .Distinct().ToList();

        var fields = await _context.MetaDataFieldRegisteries
            .Where(f => fieldIds.Contains(f.MetaDataFieldId) && f.IsDelete != 2)
            .AsNoTracking()
            .ToDictionaryAsync(f => (int)f.MetaDataFieldId);

        var metaByItem = allMeta.ToLookup(m => m.ItemId!.Value);

        return items.Select(item =>
        {
            var entries = metaByItem[item.Id]
                .Where(m => m.MetaDataFieldId.HasValue
                         && fields.ContainsKey(m.MetaDataFieldId.Value)
                         && !string.IsNullOrWhiteSpace(m.Value))
                .Select(m =>
                {
                    var f = fields[m.MetaDataFieldId!.Value];
                    return new DSpaceMetaEntry
                    {
                        Element   = f.Field ?? "description",
                        Qualifier = string.IsNullOrEmpty(f.Subfield) ? "none" : f.Subfield,
                        Value     = m.Value
                    };
                }).ToList();

            var files = includeFiles
                ? item.Files
                    .Where(f => f.IsDelete != 2 && !string.IsNullOrEmpty(f.Url))
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new DSpaceFileEntry
                    {
                        Id      = f.Id,
                        Url     = f.Url,
                        FileExt = string.IsNullOrEmpty(f.FileExt) ? "bin" : f.FileExt,
                        Bundle  = "ORIGINAL"
                    }).ToList()
                : (List<DSpaceFileEntry>)[];

            return new DSpaceItemExportRow
            {
                ItemId      = item.Id,
                PublicId    = item.PublicId,
                MetaEntries = entries,
                Files       = files
            };
        }).ToList();
    }

    protected override void MapRequestToEntity(EbookItemRequest r, EbookItem e, long userId, bool isNew)
    {
        e.CollectionId = r.CollectionId; if (r.Images != null) e.Images = r.Images;
        e.Status       = r.IsPublished.HasValue   ? (byte?)(r.IsPublished.Value   ? 2 : 1) : r.Status;
        e.AllowDownload = r.AllowDownload.HasValue ? (r.AllowDownload.Value ? 2 : 1) : null;
        e.Free          = r.IsFree.HasValue        ? (r.IsFree.Value        ? 2 : 1) : null;
        e.SubjectId = r.SubjectId; e.TypeId = r.TypeId; e.TopicId = r.TopicId;
        e.PortalId = r.PortalId; e.Language = r.Language;
        e.Show = r.Show; e.IndexContent = r.IndexContent; e.Share = r.Share;
        e.PrintCopies = r.PrintCopies; e.OfflineDays = r.OfflineDays;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; e.Submited = DateTime.Now; }
    }

    protected override void SoftDelete(EbookItem e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookItem e, int status, long userId)
    { e.Status = (byte)status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public override async Task DeleteAsync(Guid publicId)
    {
        var item = await _dbSet.FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2)
                   ?? throw new KeyNotFoundException("NotFound");

        var userId = GetCurrentUserId();
        var now    = DateTime.Now;

        item.IsDelete = 2; item.UpdateRowBy = userId; item.UpdatedRowDate = now;

        // Lịch sử thay đổi chi tiết (Đợt 17) — chỉ ghi 1 sự kiện Delete ở cấp Item. Không diff riêng
        // MetaDataValue/EbookFile con vì 2 nhánh đó dùng ExecuteUpdateAsync (raw SQL, bỏ qua
        // ChangeTracker) — đơn giản hoá có chủ đích, không diff được theo cơ chế hiện có.
        var actorName = await _context.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync();
        EntityAuditService.QueueEntityChangeLog(_context, "DigitalDocument", item.PublicId, userId, actorName,
            GetCurrentTenantId(), "Delete", reason: null,
            changes: [new EntityAuditService.FieldChange { Field = nameof(EbookItem.IsDelete), OldValue = null, NewValue = "2" }],
            ip: _http.HttpContext?.Connection.RemoteIpAddress?.ToString());

        // Cascade: ItemXml (1:1, cùng Id)
        var xml = await _context.EbookItemXmls.FindAsync(item.Id);
        if (xml != null) { xml.IsDelete = 2; xml.UpdateRowBy = userId; xml.UpdatedRowDate = now; }

        // Cascade: MetaDataValue
        await _context.MetaDataValues
            .Where(m => m.ItemId == item.Id && m.IsDelete != 2)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsDelete, 2)
                .SetProperty(m => m.UpdateRowBy, (long?)userId)
                .SetProperty(m => m.UpdatedRowDate, (DateTime?)now));

        // Cascade: EbookFile
        await _context.EbookFiles
            .Where(f => f.EbookId == item.Id && f.IsDelete != 2)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.IsDelete, 2)
                .SetProperty(f => f.UpdateRowBy, (long?)userId)
                .SetProperty(f => f.UpdatedRowDate, (DateTime?)now));

        await _context.SaveChangesAsync();
        try
        {
            _context.UserLogs.Add(new ELIBAPI.Core.Entities.Dbo.UserLog
            {
                UserId     = userId,
                ActionType = "Delete",
                Object     = nameof(EbookItem),
                Action     = $"Delete EbookItem #{item.Id} (cascade: ItemXml, MetaDataValue, EbookFile)",
                Submited   = now,
                Application = "ELIBAPI",
                TenantId   = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }
    }
}


