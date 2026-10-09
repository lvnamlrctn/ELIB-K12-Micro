using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using EsAgg       = Elastic.Clients.Elasticsearch.Aggregations;
using EsCoreSearch = Elastic.Clients.Elasticsearch.Core.Search;
using EsQdsl      = Elastic.Clients.Elasticsearch.QueryDsl;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Elasticsearch;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

public class ElasticsearchService : IElasticsearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;
    private readonly string _chunkIndexName;
    private readonly string _unifiedIndexName;
    /// <summary>Bật khi dữ liệu tenantId đã backfill đủ — xem chú thích ở SearchUnifiedAsync.</summary>
    private readonly bool _strictTenantFilter;
    private static volatile bool _indexReady;
    private static volatile bool _chunkIndexReady;
    private static volatile bool _unifiedIndexReady;

    public ElasticsearchService(IConfiguration config)
    {
        var s      = config.GetSection("ElasticsearchSettings");
        var uri    = s["Uri"] ?? "http://localhost:9200";
        var user   = s["Username"] ?? "";
        var pass   = s["Password"] ?? "";
        _indexName        = s["IndexName"]        ?? "ebook_items";
        _chunkIndexName   = s["ChunkIndexName"]   ?? "ebook_chunks";
        _unifiedIndexName = s["UnifiedIndexName"] ?? "library_docs";
        _strictTenantFilter = bool.TryParse(s["StrictTenantFilter"], out var st) && st;

        if (pass.StartsWith("ENC:"))
            pass = AesEncryptionHelper.Decrypt(pass[4..]);

        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .DisableDirectStreaming();
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            settings = settings.Authentication(new BasicAuthentication(user, pass));

        _client = new ElasticsearchClient(settings);
    }

    // ── Legacy document-level (ebook_items) ──────────────────────────────────

    public async Task UpsertEbookItemAsync(EbookItemDocument document)
    {
        await EnsureIndexAsync();
        await _client.IndexAsync(document, i => i
            .Index(_indexName)
            .Id(document.EbookId.ToString()));
    }

    public async Task BulkUpsertEbookItemsAsync(IEnumerable<EbookItemDocument> documents)
    {
        await EnsureIndexAsync();
        const int batchSize = 500;
        var batch = new List<EbookItemDocument>(batchSize);
        foreach (var doc in documents)
        {
            batch.Add(doc);
            if (batch.Count >= batchSize)
            {
                await FlushLegacyBatchAsync(batch);
                batch.Clear();
            }
        }
        if (batch.Count > 0) await FlushLegacyBatchAsync(batch);
    }

    private Task<BulkResponse> FlushLegacyBatchAsync(List<EbookItemDocument> batch)
        => _client.BulkAsync(b => b
            .Index(_indexName)
            .IndexMany(batch, (op, doc) => op.Id(doc.EbookId.ToString())));

    // ── Chunk-based (ebook_chunks) ────────────────────────────────────────────

    public async Task UpsertChunksAsync(IEnumerable<EbookChunkDocument> chunks)
    {
        await EnsureChunkIndexAsync();
        const int batchSize = 200;
        var batch = new List<EbookChunkDocument>(batchSize);
        foreach (var chunk in chunks)
        {
            batch.Add(chunk);
            if (batch.Count >= batchSize)
            {
                await FlushChunkBatchAsync(batch);
                batch.Clear();
            }
        }
        if (batch.Count > 0) await FlushChunkBatchAsync(batch);
    }

    private async Task<BulkResponse> FlushChunkBatchAsync(List<EbookChunkDocument> batch)
    {
        var resp = await _client.BulkAsync(b => b
            .Index(_chunkIndexName)
            .IndexMany(batch, (op, doc) => op.Id(doc.ChunkId)));
        if (resp.Errors)
        {
            var firstError = resp.ItemsWithErrors.FirstOrDefault();
            throw new InvalidOperationException(
                $"Bulk index thất bại cho {_chunkIndexName}: {firstError?.Error?.Reason}");
        }
        return resp;
    }

    public async Task DeleteChunksByEbookIdAsync(long ebookId)
    {
        await EnsureChunkIndexAsync();
        await _client.DeleteByQueryAsync<EbookChunkDocument>(_chunkIndexName, q => q
            .Query(qd => qd.Term(t => t.Field("ebookId").Value(ebookId))));
    }

    public async Task UpdateChunkMetadataAsync(long ebookId, EbookChunkDocument metadata)
    {
        await EnsureChunkIndexAsync();
        const string script = @"
ctx._source.title          = params.title;
ctx._source.author         = params.author;
ctx._source.publisher      = params.publisher;
ctx._source.publish_date   = params.publish_date;
ctx._source.keyword        = params.keyword;
ctx._source.dc_subject     = params.dc_subject;
ctx._source.dc_description = params.dc_description;
ctx._source.dc_language    = params.dc_language;
ctx._source.dc_identifier  = params.dc_identifier;
ctx._source.dc_type        = params.dc_type;
ctx._source.dc_contributor = params.dc_contributor;";

        await _client.UpdateByQueryAsync<EbookChunkDocument>(_chunkIndexName, u => u
            .Query(q => q.Term(t => t.Field("ebookId").Value(ebookId)))
            .Script(s => s
                .Source(script)
                .Params(new Dictionary<string, object>
                {
                    ["title"]          = metadata.Title          ?? (object)string.Empty,
                    ["author"]         = metadata.Author         ?? (object)string.Empty,
                    ["publisher"]      = metadata.Publisher      ?? (object)string.Empty,
                    ["publish_date"]   = metadata.PublishDate    ?? (object)string.Empty,
                    ["keyword"]        = metadata.Keyword        ?? (object)string.Empty,
                    ["dc_subject"]     = metadata.DcSubject      ?? (object)string.Empty,
                    ["dc_description"] = metadata.DcDescription  ?? (object)string.Empty,
                    ["dc_language"]    = metadata.DcLanguage     ?? (object)string.Empty,
                    ["dc_identifier"]  = metadata.DcIdentifier   ?? (object)string.Empty,
                    ["dc_type"]        = metadata.DcType         ?? (object)string.Empty,
                    ["dc_contributor"] = metadata.DcContributor  ?? (object)string.Empty,
                })));
    }

    public async Task<EbookSearchResponse> SearchChunksAsync(EbookChunkSearchRequest request)
    {
        await EnsureChunkIndexAsync();
        var from     = (request.Page - 1) * request.PageSize;
        var pageSize = Math.Min(request.PageSize, 50);

        var response = await _client.SearchAsync<EbookChunkDocument>(s =>
        {
            s.Index(_chunkIndexName).From(from).Size(pageSize);

            // Query
            s.Query(q =>
            {
                var hasQ = !string.IsNullOrWhiteSpace(request.Q);
                var hasFilters = request.TenantId.HasValue
                    || !string.IsNullOrEmpty(request.CollectionId)
                    || !string.IsNullOrEmpty(request.TopicId)
                    || !string.IsNullOrEmpty(request.SubjectId)
                    || !string.IsNullOrEmpty(request.Language)
                    || request.Free.HasValue
                    || request.FromYear.HasValue || request.ToYear.HasValue;

                var filterList = BuildFilterList(request);
                if (hasQ)
                    q.Bool(b =>
                    {
                        b.Must(m => m.MultiMatch(mm => mm
                            .Query(request.Q!)
                            .Fields(new[] { "title^4", "author^3", "keyword^2",
                                            "dc_subject^2", "dc_description^1", "content^1" })
                            .Type(EsQdsl.TextQueryType.CrossFields)
                            .Operator(EsQdsl.Operator.And)));
                        b.Should(
                            sh => sh.MatchPhrase(mp => mp.Field("content").Query(request.Q!).Slop(3).Boost(2)),
                            sh => sh.MatchPhrase(mp => mp.Field("title").Query(request.Q!).Slop(3).Boost(2)));
                        if (filterList.Count > 0) b.Filter(filterList);
                    });
                else if (hasFilters)
                    q.Bool(b => { if (filterList.Count > 0) b.Filter(filterList); });
                else
                    q.MatchAll(new EsQdsl.MatchAllQuery());
            });

            // Collapse by ebookId (InnerHits does not support Highlight in 8.x)
            s.Collapse(c => c
                .Field("ebookId")
                .InnerHits(ih => ih
                    .Name("best_chunk")
                    .Size(1)));

            // Highlight at search level
            s.Highlight(new EsCoreSearch.Highlight
            {
                Fields = new Dictionary<Field, EsCoreSearch.HighlightField>
                {
                    ["content"] = new EsCoreSearch.HighlightField
                    {
                        FragmentSize      = 200,
                        NumberOfFragments = 1,
                        PreTags           = new[] { "<mark>" },
                        PostTags          = new[] { "</mark>" }
                    }
                }
            });

            // Aggregations
            s.Aggregations(a => a
                .Add("by_language",   agg => agg.Terms(t => t.Field("dc_language").Size(20)))
                .Add("by_topic",      agg => agg.Terms(t => t.Field("topic_id").Size(50)))
                .Add("by_collection", agg => agg.Terms(t => t.Field("collection_id").Size(50)))
                .Add("by_year",       agg => agg.DateHistogram(dh => dh
                    .Field("publish_date")
                    .CalendarInterval(EsAgg.CalendarInterval.Year)))
                .Add("free_only",     agg => agg.Filter(f => f
                    .Term(t => t.Field("free").Value(true)))));
        });

        return MapSearchResponse(response, request.Page, pageSize);
    }

    private static List<EsQdsl.Query> BuildFilterList(EbookChunkSearchRequest request)
    {
        var filters = new List<EsQdsl.Query>();
        if (request.TenantId.HasValue)
            filters.Add(new EsQdsl.BoolQuery
            {
                Should = new List<EsQdsl.Query>
                {
                    new EsQdsl.TermQuery { Field = "tenant_id", Value = request.TenantId.Value },
                    new EsQdsl.BoolQuery { MustNot = new List<EsQdsl.Query>
                    {
                        new EsQdsl.ExistsQuery { Field = "tenant_id" }
                    }}
                },
                MinimumShouldMatch = 1
            });
        if (!string.IsNullOrEmpty(request.CollectionId))
            filters.Add(new EsQdsl.TermQuery { Field = "collection_id", Value = request.CollectionId });
        if (!string.IsNullOrEmpty(request.TopicId))
            filters.Add(new EsQdsl.TermQuery { Field = "topic_id",    Value = request.TopicId });
        if (!string.IsNullOrEmpty(request.SubjectId))
            filters.Add(new EsQdsl.TermQuery { Field = "subject_id",  Value = request.SubjectId });
        if (!string.IsNullOrEmpty(request.Language))
            filters.Add(new EsQdsl.TermQuery { Field = "dc_language", Value = request.Language });
        if (request.Free.HasValue)
            filters.Add(new EsQdsl.TermQuery { Field = "free",        Value = (FieldValue)(bool?)request.Free.Value });
        if (request.FromYear.HasValue || request.ToYear.HasValue)
            filters.Add(new EsQdsl.DateRangeQuery
            {
                Field = "publish_date",
                Gte   = request.FromYear.HasValue ? (DateMath?)$"{request.FromYear}-01-01" : null,
                Lte   = request.ToYear.HasValue   ? (DateMath?)$"{request.ToYear}-12-31"   : null,
            });
        return filters;
    }

    public async Task<List<string>> SuggestAsync(string query, long? tenantId = null)
    {
        await EnsureChunkIndexAsync();
        if (string.IsNullOrWhiteSpace(query)) return [];

        var response = await _client.SearchAsync<EbookChunkDocument>(s =>
        {
            s.Index(_chunkIndexName).Size(8);
            if (tenantId.HasValue)
                s.Query(q => q.Bool(b => b
                    .Must(m => m.MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "title", "title._2gram", "title._3gram" })
                        .Type(EsQdsl.TextQueryType.BoolPrefix)))
                    .Filter(f => f.Term(t => t.Field("tenant_id").Value(tenantId.Value)))));
            else
                s.Query(q => q.MultiMatch(mm => mm
                    .Query(query)
                    .Fields(new[] { "title", "title._2gram", "title._3gram" })
                    .Type(EsQdsl.TextQueryType.BoolPrefix)));

            s.Collapse(c => c.Field("ebookId"));
        });

        return response.Hits
            .Select(h => h.Source?.Title)
            .Where(t => !string.IsNullOrEmpty(t))
            .Cast<string>()
            .Distinct()
            .ToList();
    }

    public async Task<EbookSearchResponse> SearchInBookAsync(string ebookId, string keyword)
    {
        await EnsureChunkIndexAsync();

        var response = await _client.SearchAsync<EbookChunkDocument>(s => s
            .Index(_chunkIndexName)
            .Size(20)
            .Query(q => q.Bool(b => b
                .Filter(f => f.Term(t => t.Field("publicId").Value(ebookId)))
                .Must(m => m.Match(mm => mm.Field("content").Query(keyword)))))
            .Sort(so => so.Field("page_number", f => f.Order(SortOrder.Asc)))
            .Highlight(new EsCoreSearch.Highlight
            {
                Fields = new Dictionary<Field, EsCoreSearch.HighlightField>
                {
                    ["content"] = new EsCoreSearch.HighlightField
                    {
                        FragmentSize      = 200,
                        NumberOfFragments = 2,
                        PreTags           = new[] { "<mark>" },
                        PostTags          = new[] { "</mark>" }
                    }
                }
            }));

        return MapSearchResponse(response, 1, 20);
    }

    // ── Public Ebook Search (field-level filters) ──────────────────────────────

    public async Task<PublicEbookElasticResponse> SearchEbooksAsync(PublicEbookElasticSearchRequest request)
    {
        await EnsureChunkIndexAsync();
        var from     = (request.Page - 1) * request.PageSize;
        var pageSize = Math.Min(request.PageSize, 50);

        var mustActions   = new List<Action<EsQdsl.QueryDescriptor<EbookChunkDocument>>>();
        var shouldActions = new List<Action<EsQdsl.QueryDescriptor<EbookChunkDocument>>>();
        var filterActions = new List<Action<EsQdsl.QueryDescriptor<EbookChunkDocument>>>();

        // ── Must: full-text & field-level match ──
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            mustActions.Add(m => m.MultiMatch(mm => mm
                .Query(request.Q!)
                .Fields(new[] { "title^4", "author^3", "keyword^2",
                                "dcSubject^2", "dcDescription^1", "content^1" })
                .Type(EsQdsl.TextQueryType.CrossFields)
                .Operator(EsQdsl.Operator.And)));
            // Xếp hạng ưu tiên tài liệu khớp gần đúng cụm từ (không bắt buộc, chỉ cộng điểm).
            shouldActions.Add(sh => sh.MatchPhrase(mp => mp.Field("content").Query(request.Q!).Slop(3).Boost(2)));
            shouldActions.Add(sh => sh.MatchPhrase(mp => mp.Field("title").Query(request.Q!).Slop(3).Boost(2)));
        }
        if (!string.IsNullOrWhiteSpace(request.Title))
            mustActions.Add(m => m.Match(mm => mm.Field("title").Query(request.Title!)));
        if (!string.IsNullOrWhiteSpace(request.Author))
            mustActions.Add(m => m.Match(mm => mm.Field("author").Query(request.Author!)));
        if (!string.IsNullOrWhiteSpace(request.Publisher))
            mustActions.Add(m => m.Match(mm => mm.Field("publisher").Query(request.Publisher!)));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            mustActions.Add(m => m.Match(mm => mm.Field("keyword").Query(request.Keyword!)));
        if (!string.IsNullOrWhiteSpace(request.DcSubject))
            mustActions.Add(m => m.Match(mm => mm.Field("dcSubject").Query(request.DcSubject!)));

        // ── Filter: exact match & range ──
        if (request.ResolvedTenantId.HasValue)
        {
            var tid = request.ResolvedTenantId.Value;
            filterActions.Add(f => f.Bool(tb => tb
                .Should(
                    s1 => s1.Term(t => t.Field("tenantId").Value(tid)),
                    s1 => s1.Bool(nb => nb.MustNot(mn => mn.Exists(e => e.Field("tenantId"))))
                )
                .MinimumShouldMatch(1)));
        }
        if (!string.IsNullOrEmpty(request.CollectionId))
            filterActions.Add(f => f.Term(t => t.Field("collectionId.keyword").Value(request.CollectionId)));
        if (!string.IsNullOrEmpty(request.TopicId))
            filterActions.Add(f => f.Term(t => t.Field("topicId.keyword").Value(request.TopicId)));
        if (!string.IsNullOrEmpty(request.SubjectId))
            filterActions.Add(f => f.Term(t => t.Field("subjectId.keyword").Value(request.SubjectId)));
        if (!string.IsNullOrEmpty(request.Language))
            filterActions.Add(f => f.Term(t => t.Field("dcLanguage.keyword").Value(request.Language)));
        if (!string.IsNullOrEmpty(request.DcType))
            filterActions.Add(f => f.Term(t => t.Field("dcType.keyword").Value(request.DcType)));
        if (request.Free.HasValue)
            filterActions.Add(f => f.Term(t => t.Field("free").Value(request.Free.Value)));
        if (request.Share.HasValue)
            filterActions.Add(f => f.Term(t => t.Field("share").Value(request.Share.Value)));
        if (!string.IsNullOrEmpty(request.PublishDateFrom) || !string.IsNullOrEmpty(request.PublishDateTo))
            filterActions.Add(f => f.Range(r => r.DateRange(dr =>
            {
                dr.Field("publishDate");
                if (!string.IsNullOrEmpty(request.PublishDateFrom)) dr.Gte(request.PublishDateFrom);
                if (!string.IsNullOrEmpty(request.PublishDateTo))   dr.Lte(request.PublishDateTo);
            })));

        var response = await _client.SearchAsync<EbookChunkDocument>(s =>
        {
            s.Index(_chunkIndexName).From(from).Size(pageSize);
            s.TrackTotalHits(new EsCoreSearch.TrackHits(true));

            if (mustActions.Count > 0 || filterActions.Count > 0)
                s.Query(q => q.Bool(b =>
                {
                    if (mustActions.Count > 0)   b.Must(mustActions.ToArray());
                    if (shouldActions.Count > 0) b.Should(shouldActions.ToArray());
                    if (filterActions.Count > 0)  b.Filter(filterActions.ToArray());
                }));
            else
                s.Query(q => q.MatchAll(new EsQdsl.MatchAllQuery()));

            s.Collapse(c => c
                .Field("ebookId")
                .InnerHits(ih => ih.Name("best_chunk").Size(1)));

            s.Highlight(new EsCoreSearch.Highlight
            {
                Fields = new Dictionary<Field, EsCoreSearch.HighlightField>
                {
                    ["content"] = new EsCoreSearch.HighlightField
                    {
                        FragmentSize      = 200,
                        NumberOfFragments = 1,
                        PreTags           = new[] { "<mark>" },
                        PostTags          = new[] { "</mark>" }
                    }
                }
            });

            s.Aggregations(a => a
                .Add("unique_ebooks", agg => agg.Cardinality(c => c.Field("ebookId")))
                .Add("by_language", agg =>
                {
                    agg.Terms(t => t.Field("dcLanguage.keyword").Size(20));
                    agg.Aggregations(sa => sa.Add("unique", sa2 => sa2.Cardinality(c => c.Field("ebookId"))));
                })
                .Add("by_topic", agg =>
                {
                    agg.Terms(t => t.Field("topicId.keyword").Size(50));
                    agg.Aggregations(sa => sa.Add("unique", sa2 => sa2.Cardinality(c => c.Field("ebookId"))));
                })
                .Add("by_collection", agg =>
                {
                    agg.Terms(t => t.Field("collectionId.keyword").Size(50));
                    agg.Aggregations(sa => sa.Add("unique", sa2 => sa2.Cardinality(c => c.Field("ebookId"))));
                })
                .Add("free_only", agg =>
                {
                    agg.Filter(f => f.Term(t => t.Field("free").Value(true)));
                    agg.Aggregations(sa => sa.Add("unique", sa2 => sa2.Cardinality(c => c.Field("ebookId"))));
                }));
        });

        if (!response.IsValidResponse)
        {
            var rootCause = response.ElasticsearchServerError?.Error?.RootCause;
            var rootMsg = rootCause != null && rootCause.Count > 0
                ? string.Join(" | ", rootCause.Select(r => $"{r.Type}: {r.Reason}"))
                : response.ElasticsearchServerError?.Error?.Reason;
            return new PublicEbookElasticResponse
            {
                Total    = -1,
                Page     = request.Page,
                PageSize = pageSize,
                DebugInfo = rootMsg ?? response.DebugInformation
            };
        }

        return MapElasticResponse(response, request.Page, pageSize);
    }

    private static PublicEbookElasticResponse MapElasticResponse(
        SearchResponse<EbookChunkDocument> response, int page, int pageSize)
    {
        var items = response.Hits.Select(hit =>
        {
            var src = hit.Source;
            string? highlight = hit.Highlight?.GetValueOrDefault("content")?.FirstOrDefault();
            if (highlight == null)
            {
                var bestHit = hit.InnerHits?
                    .GetValueOrDefault("best_chunk")?.Hits?.Hits?.FirstOrDefault();
                highlight = bestHit?.Highlight?.GetValueOrDefault("content")?.FirstOrDefault();
            }

            return new PublicEbookElasticItem
            {
                EbookId        = src?.PublicId.ToString() ?? string.Empty,
                Title          = src?.Title,
                Author         = src?.Author,
                Publisher      = src?.Publisher,
                PublishDate    = src?.PublishDate,
                Keyword        = src?.Keyword,
                Images         = src?.Images,
                CollectionId   = src?.CollectionId,
                CollectionName = src?.CollectionName,
                TopicId        = src?.TopicId,
                TopicName      = src?.TopicName,
                SubjectId      = src?.SubjectId,
                SubjectName    = src?.SubjectName,
                Free           = src?.Free ?? false,
                Share          = src?.Share ?? false,
                DcSubject      = src?.DcSubject,
                DcDescription  = src?.DcDescription,
                DcLanguage     = src?.DcLanguage,
                DcIdentifier   = src?.DcIdentifier,
                DcType         = src?.DcType,
                DcContributor  = src?.DcContributor,
                BestPageNumber = src?.PageNumber,
                Highlight      = highlight,
                Score          = hit.Score,
            };
        }).ToList();

        var facets = new EbookSearchFacets();
        if (response.Aggregations != null)
        {
            var langTerms = response.Aggregations.GetStringTerms("by_language");
            if (langTerms != null)
                facets.Languages = langTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(),
                        (long)(b.Aggregations.GetCardinality("unique")?.Value ?? b.DocCount))).ToList();

            var topicTerms = response.Aggregations.GetStringTerms("by_topic");
            if (topicTerms != null)
                facets.Topics = topicTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(),
                        (long)(b.Aggregations.GetCardinality("unique")?.Value ?? b.DocCount))).ToList();

            var colTerms = response.Aggregations.GetStringTerms("by_collection");
            if (colTerms != null)
                facets.Collections = colTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(),
                        (long)(b.Aggregations.GetCardinality("unique")?.Value ?? b.DocCount))).ToList();

            var freeFilter = response.Aggregations.GetFilter("free_only");
            if (freeFilter != null)
                facets.FreeCount = (long)(freeFilter.Aggregations.GetCardinality("unique")?.Value ?? freeFilter.DocCount);
        }

        var uniqueCount = response.Aggregations?.GetCardinality("unique_ebooks");
        var total = (long)(uniqueCount?.Value ?? response.Total);

        return new PublicEbookElasticResponse
        {
            Total    = total,
            Page     = page,
            PageSize = pageSize,
            Items    = items,
            Facets   = facets,
        };
    }

    // ── Response Mapping ──────────────────────────────────────────────────────

    private static EbookSearchResponse MapSearchResponse(
        SearchResponse<EbookChunkDocument> response, int page, int pageSize)
    {
        var items = response.Hits.Select(hit =>
        {
            var src = hit.Source;

            // Highlight từ outer hit hoặc best_chunk inner hit
            string? highlight = hit.Highlight?.GetValueOrDefault("content")?.FirstOrDefault();
            if (highlight == null)
            {
                var bestHit = hit.InnerHits?
                    .GetValueOrDefault("best_chunk")?.Hits?.Hits?.FirstOrDefault();
                highlight = bestHit?.Highlight?.GetValueOrDefault("content")?.FirstOrDefault();
            }

            return new EbookSearchItem
            {
                EbookId        = src?.PublicId.ToString() ?? string.Empty,
                Title          = src?.Title,
                Author         = src?.Author,
                Publisher      = src?.Publisher,
                PublishDate    = src?.PublishDate,
                DcLanguage     = src?.DcLanguage,
                DcIdentifier   = src?.DcIdentifier,
                Images         = src?.Images,
                CollectionName = src?.CollectionName,
                Free           = src?.Free ?? false,
                BestPageNumber = src?.PageNumber,
                Highlight      = highlight,
                Score          = hit.Score,
            };
        }).ToList();

        var facets = new EbookSearchFacets();
        if (response.Aggregations != null)
        {
            var langTerms = response.Aggregations.GetStringTerms("by_language");
            if (langTerms != null)
                facets.Languages = langTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(), b.DocCount)).ToList();

            var topicTerms = response.Aggregations.GetStringTerms("by_topic");
            if (topicTerms != null)
                facets.Topics = topicTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(), b.DocCount)).ToList();

            var colTerms = response.Aggregations.GetStringTerms("by_collection");
            if (colTerms != null)
                facets.Collections = colTerms.Buckets
                    .Select(b => new FacetItem(b.Key.ToString(), b.DocCount)).ToList();

            var yearHist = response.Aggregations.GetDateHistogram("by_year");
            if (yearHist != null)
                facets.Years = yearHist.Buckets
                    .Where(b => b.DocCount > 0)
                    .Select(b => new FacetItem(b.KeyAsString ?? b.Key.ToString(), b.DocCount))
                    .ToList();

            var freeFilter = response.Aggregations.GetFilter("free_only");
            if (freeFilter != null)
                facets.FreeCount = freeFilter.DocCount;
        }

        return new EbookSearchResponse
        {
            Total    = response.Total,
            Page     = page,
            PageSize = pageSize,
            Items    = items,
            Facets   = facets,
        };
    }

    // ── Index Setup ───────────────────────────────────────────────────────────

    private async Task EnsureIndexAsync()
    {
        if (_indexReady) return;
        var exists = await _client.Indices.ExistsAsync(_indexName);
        if (!exists.Exists)
        {
            try
            {
                await _client.Indices.CreateAsync(_indexName, c => c
                    .Settings(s => s
                        .Analysis(a => a
                            .Analyzers(an => an
                                .Custom("vn_analyzer", ca => ca
                                    .CharFilter(["vn_clean"])
                                    .Tokenizer("vi_tokenizer")
                                    .Filter(["lowercase", "vn_stop"]))
                                .Custom("vn_analyzer_no_accent", ca => ca
                                    .CharFilter(["vn_clean"])
                                    .Tokenizer("vi_tokenizer")
                                    .Filter(["lowercase", "icu_folding", "vn_stop"])))))
                    .Mappings(m => m
                        .Properties<EbookItemDocument>(p => p
                            .LongNumber(f => f.EbookId)
                            .Keyword(f => f.EbookFileId)
                            .Keyword(f => f.Isbn)
                            .Keyword(f => f.Issn)
                            .Keyword(f => f.PublishDate)
                            .LongNumber(f => f.CollectionId)
                            .LongNumber(f => f.SubjectId)
                            .LongNumber(f => f.TopicId)
                            .Text(f => f.CollectionName, t => t.Analyzer("vn_analyzer"))
                            .Text(f => f.SubjectName,    t => t.Analyzer("vn_analyzer"))
                            .Text(f => f.TopicName,      t => t.Analyzer("vn_analyzer"))
                            .Text(f => f.Content, t => t
                                .Analyzer("vn_analyzer")
                                .Fields(ff => ff
                                    .Text("no_accent", nt => nt
                                        .Analyzer("vn_analyzer_no_accent")))))));
            }
            catch { }
        }
        _indexReady = true;
    }

    private async Task EnsureChunkIndexAsync()
    {
        if (_chunkIndexReady) return;
        var exists = await _client.Indices.ExistsAsync(_chunkIndexName);
        if (!exists.Exists)
        {
            // Raw JSON creation to handle custom CharFilter/TokenFilter definitions
            // that the fluent API doesn't expose (vn_clean pattern_replace, vn_stop)
            const string stopwords = """
                "và","của","là","có","trong","được","cho","với","các","những",
                "này","đó","không","cũng","như","một","để","khi","từ","hay",
                "thì","tại","về","ra","vào","đã","sẽ","đến","đây","theo","bởi",
                "vì","nếu","nhưng","mà","tuy","nên","hoặc","bằng","chỉ","thế",
                "lại","đi","lên","xuống","qua","rồi","nữa","hơn","rất","rằng",
                "cần","phải","muốn","quyết","định","tổng","thể","toàn",
                "the","a","an","and","or","of","to","in","is","for"
                """;
            var indexBody = $$"""
                {
                  "settings": {
                    "number_of_shards": 5,
                    "number_of_replicas": 1,
                    "refresh_interval": "30s",
                    "analysis": {
                      "char_filter": {
                        "vn_clean": { "type": "pattern_replace", "pattern": "[​-‍﻿]", "replacement": "" }
                      },
                      "filter": {
                        "vn_stop": { "type": "stop", "stopwords": [{{stopwords}}] }
                      },
                      "analyzer": {
                        "vn_analyzer": {
                          "type": "custom",
                          "char_filter": ["vn_clean"],
                          "tokenizer": "vi_tokenizer",
                          "filter": ["lowercase", "vn_stop"]
                        }
                      }
                    }
                  },
                  "mappings": {
                    "properties": {
                      "chunk_id":       { "type": "keyword" },
                      "ebookId":        { "type": "long" },
                      "ebookFileId":    { "type": "long" },
                      "publicId":       { "type": "keyword" },
                      "page_number":    { "type": "integer" },
                      "chunk_index":    { "type": "integer" },
                      "file_version":   { "type": "integer" },
                      "indexed_at":     { "type": "date" },
                      "content":        { "type": "text", "analyzer": "vn_analyzer" },
                      "collection_id":  { "type": "keyword" },
                      "topic_id":       { "type": "keyword" },
                      "subject_id":     { "type": "keyword" },
                      "free":           { "type": "boolean" },
                      "share":          { "type": "boolean" },
                      "tenant_id":      { "type": "long" },
                      "dc_language":    { "type": "keyword" },
                      "dc_type":        { "type": "keyword" },
                      "dc_identifier":  { "type": "keyword" },
                      "dc_subject":     { "type": "text", "analyzer": "vn_analyzer" },
                      "dc_description": { "type": "text", "analyzer": "vn_analyzer" },
                      "title":          { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword" }, "suggest": { "type": "search_as_you_type" } } },
                      "author":         { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword" } } },
                      "keyword":        { "type": "text", "analyzer": "vn_analyzer" }
                    }
                  }
                }
                """;

            var resp = await _client.Transport.RequestAsync<DynamicResponse>(
                Elastic.Transport.HttpMethod.PUT, _chunkIndexName,
                PostData.String(indexBody));

            if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            {
                var httpCode  = resp.ApiCallDetails.HttpStatusCode;
                var debugInfo = resp.ApiCallDetails.DebugInformation ?? "";
                // 400 resource_already_exists_exception = another worker created it first → OK
                if (httpCode != 400 || !debugInfo.Contains("resource_already_exists_exception"))
                    throw new InvalidOperationException(
                        $"Tạo index {_chunkIndexName} thất bại (HTTP {httpCode}): " + debugInfo);
            }
        }
        _chunkIndexReady = true;

        // Ensure dense_vector mapping for RAG embeddings
        await EnsureEmbeddingMappingAsync();
    }

    private static volatile bool _embeddingMappingReady;

    private async Task EnsureEmbeddingMappingAsync()
    {
        if (_embeddingMappingReady) return;
        const string body = """
            {
              "properties": {
                "embedding": {
                  "type": "dense_vector",
                  "dims": 768,
                  "index": true,
                  "similarity": "dot_product"
                }
              }
            }
            """;
        await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.PUT,
            $"/{_chunkIndexName}/_mapping",
            PostData.String(body));
        _embeddingMappingReady = true;
    }

    // ── Index GỘP (library_docs): tài liệu in + tài liệu số ───────────────────

    /// <summary>
    /// Tạo index gộp với mapping khai báo TƯỜNG MINH mọi trường — khác index cũ vốn để dynamic
    /// mapping tự sinh, dẫn tới tồn tại song song 2 bộ trường snake_case (chết) và camelCase (thật).
    /// Dùng lại nguyên analyzer vn_analyzer để tài liệu in và số cùng luật tách từ tiếng Việt.
    /// </summary>
    public async Task EnsureUnifiedIndexAsync()
    {
        if (_unifiedIndexReady) return;
        var exists = await _client.Indices.ExistsAsync(_unifiedIndexName);
        if (!exists.Exists)
        {
            const string stopwords = """
                "và","của","là","có","trong","được","cho","với","các","những",
                "này","đó","không","cũng","như","một","để","khi","từ","hay",
                "thì","tại","về","ra","vào","đã","sẽ","đến","đây","theo","bởi",
                "vì","nếu","nhưng","mà","tuy","nên","hoặc","bằng","chỉ","thế",
                "lại","đi","lên","xuống","qua","rồi","nữa","hơn","rất","rằng",
                "cần","phải","muốn","quyết","định","tổng","thể","toàn",
                "the","a","an","and","or","of","to","in","is","for"
                """;
            var indexBody = $$"""
                {
                  "settings": {
                    "number_of_shards": 5,
                    "number_of_replicas": 1,
                    "refresh_interval": "30s",
                    "analysis": {
                      "char_filter": {
                        "vn_clean": { "type": "pattern_replace", "pattern": "[​-‍﻿]", "replacement": "" }
                      },
                      "filter": {
                        "vn_stop": { "type": "stop", "stopwords": [{{stopwords}}] }
                      },
                      "analyzer": {
                        "vn_analyzer": {
                          "type": "custom",
                          "char_filter": ["vn_clean"],
                          "tokenizer": "vi_tokenizer",
                          "filter": ["lowercase", "vn_stop"]
                        },
                        "vn_analyzer_no_accent": {
                          "type": "custom",
                          "char_filter": ["vn_clean"],
                          "tokenizer": "vi_tokenizer",
                          "filter": ["lowercase", "vn_stop", "icu_folding"]
                        }
                      }
                    }
                  },
                  "mappings": {
                    "dynamic": "strict",
                    "properties": {
                      "docId":          { "type": "keyword" },
                      "groupId":        { "type": "keyword" },
                      "docType":        { "type": "keyword" },
                      "publicId":       { "type": "keyword" },
                      "tenantId":       { "type": "long"    },

                      "title":          { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword", "ignore_above": 512 }, "suggest": { "type": "search_as_you_type" }, "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "author":         { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword", "ignore_above": 512 }, "suggest": { "type": "search_as_you_type" }, "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "publisher":      { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword", "ignore_above": 512 }, "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "publishDate":    { "type": "text", "fields": { "raw": { "type": "keyword", "ignore_above": 128 } } },
                      "publishYear":    { "type": "integer" },
                      "ddc":            { "type": "keyword", "ignore_above": 128 },
                      "isbn":           { "type": "keyword", "ignore_above": 128 },
                      "summary":        { "type": "text", "analyzer": "vn_analyzer", "fields": { "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "keyword":        { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword", "ignore_above": 512 }, "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "language":       { "type": "keyword", "ignore_above": 32  },
                      "materialType":   { "type": "keyword", "ignore_above": 128 },
                      "collectionId":   { "type": "keyword" },
                      "collectionName": { "type": "text", "analyzer": "vn_analyzer", "fields": { "raw": { "type": "keyword", "ignore_above": 256 } } },
                      "contributor":    { "type": "text", "analyzer": "vn_analyzer", "fields": { "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "images":         { "type": "keyword", "index": false },
                      "indexedAt":      { "type": "date" },

                      "ebookId":        { "type": "long" },
                      "ebookFileId":    { "type": "long" },
                      "pageNumber":     { "type": "integer" },
                      "chunkIndex":     { "type": "integer" },
                      "content":        { "type": "text", "analyzer": "vn_analyzer", "fields": { "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "embedding":      { "type": "dense_vector", "dims": 768, "index": true, "similarity": "dot_product" },
                      "topicId":        { "type": "keyword" },
                      "topicName":      { "type": "text", "analyzer": "vn_analyzer" },
                      "subjectId":      { "type": "keyword" },
                      "subjectName":    { "type": "text", "analyzer": "vn_analyzer" },
                      "free":           { "type": "boolean" },
                      "share":          { "type": "boolean" },

                      "bibId":          { "type": "long" },
                      "mfn":            { "type": "long" },
                      "isbd":           { "type": "text", "analyzer": "vn_analyzer" },
                      "allMarcText":    { "type": "text", "analyzer": "vn_analyzer", "fields": { "no_accent": { "type": "text", "analyzer": "vn_analyzer_no_accent" } } },
                      "bibTypeId":      { "type": "long" },
                      "status":         { "type": "keyword", "ignore_above": 32 },
                      "statusName":     { "type": "keyword", "ignore_above": 128 },
                      "url":            { "type": "keyword", "index": false },
                      "linkedEbookId":  { "type": "long" },
                      "copyCount":      { "type": "integer" },
                      "availableCount": { "type": "integer" },
                      "holdings": {
                        "type": "nested",
                        "properties": {
                          "barcodeId":   { "type": "long" },
                          "barcode":     { "type": "keyword", "ignore_above": 64, "fields": { "text": { "type": "text", "analyzer": "vn_analyzer" } } },
                          "storeId":     { "type": "integer" },
                          "storeName":   { "type": "keyword", "ignore_above": 256 },
                          "status":      { "type": "keyword", "ignore_above": 32  },
                          "statusName":  { "type": "keyword", "ignore_above": 128 },
                          "receiptId":   { "type": "long" },
                          "receiptCode": { "type": "long" }
                        }
                      }
                    }
                  }
                }
                """;

            var resp = await _client.Transport.RequestAsync<DynamicResponse>(
                Elastic.Transport.HttpMethod.PUT, _unifiedIndexName,
                PostData.String(indexBody));

            if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            {
                var httpCode  = resp.ApiCallDetails.HttpStatusCode;
                var debugInfo = resp.ApiCallDetails.DebugInformation ?? "";
                // 400 resource_already_exists_exception = worker khác tạo trước → OK
                if (httpCode != 400 || !debugInfo.Contains("resource_already_exists_exception"))
                    throw new InvalidOperationException(
                        $"Tạo index {_unifiedIndexName} thất bại (HTTP {httpCode}): " + debugInfo);
            }
        }
        _unifiedIndexReady = true;
    }

    public async Task UpsertLibraryDocumentsAsync(IEnumerable<LibraryDocument> documents)
    {
        await EnsureUnifiedIndexAsync();
        const int batchSize = 200;
        var batch = new List<LibraryDocument>(batchSize);
        foreach (var doc in documents)
        {
            batch.Add(doc);
            if (batch.Count >= batchSize)
            {
                await FlushLibraryBatchAsync(batch);
                batch.Clear();
            }
        }
        if (batch.Count > 0) await FlushLibraryBatchAsync(batch);
    }

    private async Task FlushLibraryBatchAsync(List<LibraryDocument> batch)
    {
        var resp = await _client.BulkAsync(b => b
            .Index(_unifiedIndexName)
            .IndexMany(batch, (op, doc) => op.Id(doc.DocId)));
        if (resp.Errors)
        {
            var firstError = resp.ItemsWithErrors.FirstOrDefault();
            throw new InvalidOperationException(
                $"Bulk index thất bại cho {_unifiedIndexName}: {firstError?.Error?.Reason}");
        }
    }

    /// <summary>Đợt 26 — xóa hẳn index gộp (không phải xóa từng doc) rồi tạo lại mapping rỗng — dùng cho
    /// nút "Xóa & build lại từ đầu"; caller phải tự enqueue job reindex in + số sau khi gọi xong.</summary>
    public async Task DeleteUnifiedIndexAsync()
    {
        var resp = await _client.Indices.DeleteAsync(_unifiedIndexName);
        if (!resp.IsValidResponse && resp.ApiCallDetails.HttpStatusCode != 404)
            throw new InvalidOperationException($"Không xóa được index {_unifiedIndexName}: {resp.DebugInformation}");
        _unifiedIndexReady = false;
        await EnsureUnifiedIndexAsync();
    }

    /// <summary>Xóa mọi doc của 1 tài liệu khỏi index gộp (in: 1 doc; số: nhiều chunk).</summary>
    public async Task DeleteLibraryDocsByGroupAsync(string groupId)
    {
        await EnsureUnifiedIndexAsync();
        await _client.DeleteByQueryAsync<LibraryDocument>(_unifiedIndexName, q => q
            .Query(qd => qd.Term(t => t.Field("groupId").Value(groupId))));
    }

    /// <summary>
    /// Chuyển toàn bộ tài liệu số từ index chunk cũ sang index gộp bằng ES <c>_reindex</c> —
    /// KHÔNG tải lại PDF, KHÔNG chạy lại OCR, KHÔNG sinh lại embedding (vector được sao chép nguyên).
    /// Script Painless đổi tên trường về bộ trường chung và bỏ các trường cũ (index gộp để
    /// <c>dynamic: strict</c> nên mọi trường lạ sẽ bị từ chối).
    /// Trả về task id để theo dõi qua <c>GET _tasks/{id}</c>.
    /// </summary>
    public async Task<string> MigrateDigitalToUnifiedAsync()
    {
        await EnsureUnifiedIndexAsync();

        // Không dùng regex: script.painless.regex.enabled thường tắt mặc định trên ES.
        // Quét thủ công cụm 4 chữ số liên tiếp đầu tiên trong khoảng 1000..2100.
        const string script = """
            def s = ctx._source;
            def cid = s.chunkId;
            if (cid == null) { ctx.op = 'noop'; return; }
            ctx._id    = 'digital_' + cid;
            s.docId    = 'digital_' + cid;
            s.groupId  = 'digital_' + s.ebookId;
            s.docType  = 'digital';
            if (s.dcSubject != null && s.dcSubject != '') {
              if (s.keyword == null || s.keyword == '') { s.keyword = s.dcSubject; }
              else if (!s.keyword.contains(s.dcSubject)) { s.keyword = s.keyword + '; ' + s.dcSubject; }
            }
            s.materialType = s.dcType;
            s.contributor  = s.dcContributor;
            s.summary      = s.dcDescription;
            s.language     = s.dcLanguage;
            s.isbn         = s.dcIdentifier;
            if (s.publishDate != null) {
              String pd = s.publishDate;
              for (int i = 0; i + 4 <= pd.length(); i++) {
                boolean ok = true;
                for (int j = 0; j < 4; j++) {
                  if (!Character.isDigit(pd.charAt(i + j))) { ok = false; break; }
                }
                if (ok) {
                  int y = Integer.parseInt(pd.substring(i, i + 4));
                  if (y >= 1000 && y <= 2100) { s.publishYear = y; break; }
                }
              }
            }
            s.remove('chunkId');       s.remove('fileVersion');
            s.remove('dcSubject');     s.remove('dcDescription');
            s.remove('dcLanguage');    s.remove('dcIdentifier');
            s.remove('dcType');        s.remove('dcContributor');
            """;

        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            source = new { index = _chunkIndexName },
            dest   = new { index = _unifiedIndexName },
            script = new { lang = "painless", source = script }
        });

        var resp = await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.POST,
            "/_reindex?wait_for_completion=false&slices=auto",
            PostData.String(body));

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                $"_reindex thất bại (HTTP {resp.ApiCallDetails.HttpStatusCode}): {resp.ApiCallDetails.DebugInformation}");

        return resp.Get<string>("task") ?? "";
    }

    // ── Tìm kiếm GỘP tài liệu in + tài liệu số ───────────────────────────────

    /// <summary>
    /// Truy vấn một index gộp duy nhất nên tài liệu in và số cùng một thang xếp hạng.
    /// Gộp nhiều chunk của cùng 1 tài liệu số về 1 dòng bằng collapse theo groupId;
    /// tài liệu in vốn đã 1 doc = 1 dòng.
    /// </summary>
    public async Task<UnifiedSearchResponse> SearchUnifiedAsync(UnifiedSearchRequest r)
    {
        await EnsureUnifiedIndexAsync();
        var page     = r.Page < 1 ? 1 : r.Page;
        var pageSize = Math.Clamp(r.PageSize < 1 ? 10 : r.PageSize, 1, 50);
        var from     = (page - 1) * pageSize;

        var must   = new List<object>();
        var filter = new List<object>();

        // Tìm nhanh: quét bộ trường chung + nội dung file.
        // cross_fields coi nhiều trường như một trường gộp — hợp với dữ liệu thư mục
        // vì nhan đề/tác giả/từ khóa hay chứa các phần khác nhau của cùng cụm tìm kiếm.
        if (!string.IsNullOrWhiteSpace(r.Q))
        {
            // Quét cả bản có dấu và bản bỏ dấu (.no_accent) để người dùng gõ "van hoc"
            // vẫn ra "văn học". Bản có dấu để boost cao hơn nên gõ đúng dấu vẫn xếp trên.
            must.Add(new { multi_match = new {
                query = r.Q, type = "cross_fields", @operator = "and",
                fields = new[] {
                    "title^4", "author^3", "publisher^2", "keyword^2", "summary^2",
                    "isbn^2", "ddc^1", "publishDate^1", "allMarcText^1", "content^1",
                    "title.no_accent^2", "author.no_accent^1.5", "publisher.no_accent^1",
                    "keyword.no_accent^1", "summary.no_accent^1",
                    "allMarcText.no_accent^0.5", "content.no_accent^0.5" } } });
        }

        // Như Q nhưng KHÔNG có "content"/"content.no_accent" trong danh sách trường -- dùng cho
        // trợ lý tìm tài liệu (DocumentFinderService), nơi tuyệt đối không được để nội dung trang
        // sách lẫn vào kết quả tra cứu thư mục.
        if (!string.IsNullOrWhiteSpace(r.MetaQ))
        {
            must.Add(new { multi_match = new {
                query = r.MetaQ, type = "cross_fields", @operator = "and",
                fields = new[] {
                    "title^4", "author^3", "publisher^2", "keyword^2", "summary^2",
                    "isbn^2", "ddc^1", "allMarcText^1",
                    "title.no_accent^2", "author.no_accent^1.5", "publisher.no_accent^1",
                    "keyword.no_accent^1", "summary.no_accent^1",
                    "allMarcText.no_accent^0.5" } } });
        }

        // Khớp trên cả trường gốc lẫn trường bỏ dấu (nếu có sub-field .no_accent).
        void MatchIf(string? value, string field, bool foldable = true)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!foldable)
            {
                must.Add(new { match = new Dictionary<string, object> { [field] = new { query = value } } });
                return;
            }
            must.Add(new { multi_match = new {
                query  = value,
                type   = "best_fields",
                fields = new[] { $"{field}^2", $"{field}.no_accent" } } });
        }
        MatchIf(r.Title,     "title");
        MatchIf(r.Author,    "author");
        MatchIf(r.Publisher, "publisher");
        MatchIf(r.Keyword,   "keyword");
        MatchIf(r.Summary,   "summary");
        MatchIf(r.Content,   "content");

        // ddc/isbn/language/materialType là keyword → lọc chính xác, không phân tích.
        void TermIf(string? value, string field)
        {
            if (!string.IsNullOrWhiteSpace(value))
                filter.Add(new { term = new Dictionary<string, object> { [field] = value } });
        }
        TermIf(r.Ddc,          "ddc");
        TermIf(r.Isbn,         "isbn");
        TermIf(r.DocType,      "docType");
        TermIf(r.Language,     "language");
        TermIf(r.MaterialType, "materialType");
        TermIf(r.TopicId,      "topicId");
        TermIf(r.SubjectId,    "subjectId");
        // Bộ sưu tập cha gồm cả tài liệu của các bộ sưu tập con/cháu (port ELIB-LRC 09-23).
        if (r.ResolvedCollectionIds is { Count: > 0 })
            filter.Add(new { terms = new Dictionary<string, object> { ["collectionId"] = r.ResolvedCollectionIds.Select(x => x.ToString()).ToArray() } });
        else
            TermIf(r.CollectionId, "collectionId");

        if (r.Free.HasValue)
            filter.Add(new { term = new Dictionary<string, object> { ["free"] = r.Free.Value } });

        // Bản sao vật lý nằm trong nested → phải truy vấn qua nested, không lọc phẳng được.
        // storeId/callNumber không phải facet multi-select nên vẫn ở nhóm lọc "gốc" (filter) —
        // storeName(s) tách riêng bên dưới vì cần "sticky" (xem chú thích ở khối facet sticky).
        var holdingConds = new List<object>();
        if (r.StoreId.HasValue)
            holdingConds.Add(new { term = new Dictionary<string, object> { ["holdings.storeId"] = r.StoreId.Value } });
        if (!string.IsNullOrWhiteSpace(r.CallNumber))
            holdingConds.Add(new { match = new Dictionary<string, object> { ["holdings.barcode.text"] = new { query = r.CallNumber } } });
        if (holdingConds.Count > 0)
            filter.Add(new { nested = new {
                path = "holdings",
                query = new { @bool = new { must = holdingConds } } } });

        if (r.AvailableOnly == true)
            filter.Add(new { range = new Dictionary<string, object> { ["availableCount"] = new Dictionary<string, object> { ["gte"] = 1 } } });

        // ── 3 facet "sticky" (multi-select): Kho / Tác giả / Năm xuất bản (Đợt 25) ──────────────────
        // Tách khỏi `filter` (nhóm lọc "gốc" luôn áp dụng cho mọi aggregation) và áp dụng riêng qua
        // `post_filter` (chỉ ảnh hưởng tới KẾT QUẢ, không ảnh hưởng aggregation) — để mỗi facet loại
        // trừ ĐÚNG điều kiện của chính nó khi tính số đếm: chọn Kho A vẫn thấy đúng số lượng ở Kho B/C
        // (không tụt về 0/chỉ còn Kho A), đúng kiểu "sticky facets" chuẩn của Elasticsearch.
        List<string>? storeNamesRaw = r.StoreNames is { Count: > 0 } ? r.StoreNames
            : (!string.IsNullOrWhiteSpace(r.StoreName) ? new List<string> { r.StoreName } : null);
        var storeNames = storeNamesRaw?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        object? storeFilterClause = storeNames is { Length: > 0 }
            ? new { nested = new { path = "holdings", query = new { terms = new Dictionary<string, object> { ["holdings.storeName"] = storeNames } } } }
            : null;

        List<string>? authorsRaw = r.AuthorsExact is { Count: > 0 } ? r.AuthorsExact
            : (!string.IsNullOrWhiteSpace(r.AuthorExact) ? new List<string> { r.AuthorExact } : null);
        var authorsExact = authorsRaw?.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        object? authorFilterClause = authorsExact is { Length: > 0 }
            ? new { terms = new Dictionary<string, object> { ["author.raw"] = authorsExact } }
            : null;

        object? yearFilterClause = null;
        if (r.PublishYears is { Count: > 0 })
        {
            yearFilterClause = new { terms = new Dictionary<string, object> { ["publishYear"] = r.PublishYears.ToArray() } };
        }
        else if (r.PublishYearFrom.HasValue || r.PublishYearTo.HasValue)
        {
            var range = new Dictionary<string, object>();
            if (r.PublishYearFrom.HasValue) range["gte"] = r.PublishYearFrom.Value;
            if (r.PublishYearTo.HasValue)   range["lte"] = r.PublishYearTo.Value;
            yearFilterClause = new { range = new Dictionary<string, object> { ["publishYear"] = range } };
        }

        var stickyFilters = new[] { storeFilterClause, authorFilterClause, yearFilterClause }
            .Where(x => x != null).Cast<object>().ToList();

        // ── Lọc theo đơn vị ──
        // Dữ liệu thực tế: tài liệu in có tenantId 100%, tài liệu SỐ có 0% (DB chưa gán).
        // Nếu lọc chặt (chỉ term tenantId) thì toàn bộ tài liệu số sẽ biến mất khỏi kết quả.
        // Nên mặc định chấp nhận cả doc chưa gán tenant, và cho phép siết lại bằng cấu hình
        // sau khi dữ liệu tenantId được backfill đầy đủ.
        if (r.ResolvedTenantId.HasValue)
        {
            var tid = r.ResolvedTenantId.Value;
            filter.Add(_strictTenantFilter
                ? new { term = new Dictionary<string, object> { ["tenantId"] = tid } }
                : (object)new { @bool = new {
                    should = new object[] {
                        new { term = new Dictionary<string, object> { ["tenantId"] = tid } },
                        new { @bool = new { must_not = new object[] { new { exists = new { field = "tenantId" } } } } }
                    },
                    minimum_should_match = 1 } });
        }

        // `filter` ở đây CHỈ còn nhóm lọc "gốc" (không gồm 3 facet sticky) — 3 facet đó áp dụng cho kết
        // quả qua `post_filter` bên dưới, để mỗi aggregation tự quyết định có tính nó vào hay không.
        object query = must.Count > 0 || filter.Count > 0
            ? new { @bool = new { must = must.Count > 0 ? must : [new { match_all = new { } }], filter } }
            : new { match_all = new { } };

        // Mỗi agg bọc trong 1 `filter` aggregation riêng, cộng thêm các facet sticky KHÁC (trừ chính
        // nó) — vì aggregation vốn đã nằm trong phạm vi `query` (đã có `filter` gốc), chỉ cần bổ sung
        // phần "sticky" còn thiếu. total_docs/by_doc_type/by_collection/by_material/by_ddc/by_language
        // luôn phản ánh ĐẦY ĐỦ mọi lựa chọn (giống hành vi hiện có, không sticky).
        object AggFilter(params object?[] extra) =>
            new { @bool = new { filter = extra.Where(x => x != null).Cast<object>().ToList() } };
        object Wrap(object filterQuery, object innerAgg) =>
            new { filter = filterQuery, aggs = new Dictionary<string, object> { ["inner"] = innerAgg } };

        var fullAggFilter  = AggFilter(storeFilterClause, authorFilterClause, yearFilterClause);
        var noStoreFilter  = AggFilter(authorFilterClause, yearFilterClause);
        var noAuthorFilter = AggFilter(storeFilterClause, yearFilterClause);
        var noYearFilter   = AggFilter(storeFilterClause, authorFilterClause);

        var body = new Dictionary<string, object>
        {
            ["size"]  = pageSize,
            ["from"]  = from,
            ["query"] = query,
            // Gộp các chunk cùng tài liệu; inner_hits lấy chunk khớp nhất để hiện số trang.
            ["collapse"] = new { field = "groupId", inner_hits = new { name = "best", size = 1 } },
            ["highlight"] = new { fields = new Dictionary<string, object> {
                ["content"] = new { fragment_size = 200, number_of_fragments = 1,
                                    pre_tags = new[] { "<mark>" }, post_tags = new[] { "</mark>" } } } },
            // Facet phải đếm SỐ TÀI LIỆU (cardinality groupId), không phải số chunk —
            // nếu không, 1 ebook vài trăm trang sẽ át toàn bộ thống kê.
            ["aggs"] = new Dictionary<string, object>
            {
                ["total_docs"]     = Wrap(fullAggFilter, new { cardinality = new { field = "groupId", precision_threshold = 40000 } }),
                ["by_doc_type"]    = Wrap(fullAggFilter, Facet("docType", 5)),
                ["by_year"]        = Wrap(noYearFilter,  Facet("publishYear", 30)),
                ["by_collection"]  = Wrap(fullAggFilter, Facet("collectionId", 30)),
                ["by_material"]    = Wrap(fullAggFilter, Facet("materialType", 30)),
                ["by_ddc"]         = Wrap(fullAggFilter, Facet("ddc", 30)),
                ["by_language"]    = Wrap(fullAggFilter, Facet("language", 20)),
                // Đợt 22.2 — Kho (nested trong holdings, đếm số đầu tài liệu qua reverse_nested chứ không
                // phải số bản: 1 tài liệu 5 bản ở cùng kho chỉ tính 1) và Tác giả (field .raw sẵn có).
                ["by_store"]       = Wrap(noStoreFilter, new
                {
                    nested = new { path = "holdings" },
                    aggs = new Dictionary<string, object>
                    {
                        ["stores"] = new
                        {
                            terms = new { field = "holdings.storeName", size = 30 },
                            aggs  = new Dictionary<string, object> { ["docs"] = new { reverse_nested = new { } } },
                        },
                    },
                }),
                ["by_author"]      = Wrap(noAuthorFilter, Facet("author.raw", 30)),
            },
            ["_source"] = new { excludes = new[] { "embedding", "content", "allMarcText", "isbd" } },
        };
        if (stickyFilters.Count > 0)
            body["post_filter"] = new { @bool = new { filter = stickyFilters } };

        // Chế độ nhẹ (trợ lý chat tìm tài liệu, port ELIB-LRC 09-29): không hiện facet/đoạn trích → chỉ giữ đếm tổng số tài liệu,
        // bỏ facet + highlight, và bỏ bước dfs (thêm 1 vòng hỏi mọi shard) — trả kết quả nhanh hơn rõ.
        if (r.LightMode)
        {
            body.Remove("highlight");
            body["aggs"] = new Dictionary<string, object>
            {
                ["total_docs"] = Wrap(fullAggFilter, new { cardinality = new { field = "groupId", precision_threshold = 40000 } }),
            };
        }

        if (r.SortBy is "newest" or "oldest" or "title")
            body["sort"] = new object[] { r.SortBy switch
            {
                "newest" => new Dictionary<string, object> { ["publishYear"] = new { order = "desc", missing = "_last" } },
                "oldest" => new Dictionary<string, object> { ["publishYear"] = new { order = "asc",  missing = "_last" } },
                _        => new Dictionary<string, object> { ["title.raw"]   = new { order = "asc",  missing = "_last" } },
            }};

        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var resp = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.POST,
            // dfs_query_then_fetch: điểm BM25 tính trên toàn bộ shard trước khi xếp hạng,
            // để tài liệu in (ít doc) và tài liệu số (rất nhiều chunk) so được với nhau.
            r.LightMode ? $"/{_unifiedIndexName}/_search" : $"/{_unifiedIndexName}/_search?search_type=dfs_query_then_fetch",
            PostData.String(json));

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                $"Tìm kiếm gộp thất bại (HTTP {resp.ApiCallDetails.HttpStatusCode}): {resp.ApiCallDetails.DebugInformation}");

        return MapUnified(resp.Body, page, pageSize);
    }

    /// <summary>
    /// Trả về TOÀN BỘ bibId của tài liệu in khớp bộ lọc — thay cho các truy vấn SQL LIKE '%...%'
    /// vốn không bỏ dấu và không tách từ tiếng Việt.
    ///
    /// Dùng search_after thay vì from/size vì bên gọi (báo cáo, thống kê) cần tập ID ĐẦY ĐỦ,
    /// trong khi from/size bị chặn ở max_result_window (mặc định 10.000).
    /// </summary>
    public async Task<HashSet<long>> SearchPrintBibIdsAsync(PrintBibFilter f)
    {
        await EnsureUnifiedIndexAsync();

        var must   = new List<object>();
        var filter = new List<object> { new { term = new Dictionary<string, object> { ["docType"] = "print" } } };

        // Khớp cả bản có dấu lẫn bản bỏ dấu để gõ "van hoc" vẫn ra "văn học".
        void MatchIf(string? v, string field)
        {
            if (string.IsNullOrWhiteSpace(v)) return;
            must.Add(new { multi_match = new {
                query  = v,
                type   = "best_fields",
                fields = new[] { $"{field}^2", $"{field}.no_accent" } } });
        }
        MatchIf(f.Title,     "title");
        MatchIf(f.Author,    "author");
        MatchIf(f.Publisher, "publisher");
        MatchIf(f.Keyword,   "keyword");
        MatchIf(f.Summary,   "summary");

        // PublishDate trong DB là chuỗi tự do nên bên gọi vẫn dùng kiểu "chứa chuỗi";
        // giữ nguyên ngữ nghĩa đó bằng match_phrase trên trường text.
        if (!string.IsNullOrWhiteSpace(f.PublishDate))
            must.Add(new { match_phrase = new Dictionary<string, object> { ["publishDate"] = f.PublishDate } });

        if (f.MfnFrom.HasValue || f.MfnTo.HasValue)
        {
            var range = new Dictionary<string, object>();
            if (f.MfnFrom.HasValue) range["gte"] = f.MfnFrom.Value;
            if (f.MfnTo.HasValue)   range["lte"] = f.MfnTo.Value;
            filter.Add(new { range = new Dictionary<string, object> { ["mfn"] = range } });
        }
        if (f.BibTypeId.HasValue)
            filter.Add(new { term = new Dictionary<string, object> { ["bibTypeId"] = f.BibTypeId.Value } });
        if (f.TenantId.HasValue)
        {
            if (f.TenantIncludeShared)
                filter.Add(new
                {
                    @bool = new
                    {
                        should = new object[]
                        {
                            new { term = new Dictionary<string, object> { ["tenantId"] = f.TenantId.Value } },
                            new { @bool = new { must_not = new object[] { new { exists = new { field = "tenantId" } } } } }
                        },
                        minimum_should_match = 1
                    }
                });
            else
                filter.Add(new { term = new Dictionary<string, object> { ["tenantId"] = f.TenantId.Value } });
        }

        var holdingConds = new List<object>();
        if (f.StoreId.HasValue)
            holdingConds.Add(new { term = new Dictionary<string, object> { ["holdings.storeId"] = f.StoreId.Value } });
        if (!string.IsNullOrWhiteSpace(f.CallNumber))
            holdingConds.Add(new { match = new Dictionary<string, object> { ["holdings.barcode.text"] = new { query = f.CallNumber } } });
        if (!string.IsNullOrWhiteSpace(f.BarcodeStatus))
            holdingConds.Add(new { term = new Dictionary<string, object> { ["holdings.status"] = f.BarcodeStatus } });
        if (holdingConds.Count > 0)
            filter.Add(new { nested = new { path = "holdings", query = new { @bool = new { must = holdingConds } } } });

        var result = new HashSet<long>();
        object[]? searchAfter = null;
        const int pageSize = 1000;

        while (true)
        {
            var body = new Dictionary<string, object>
            {
                ["size"]    = pageSize,
                ["query"]   = new { @bool = new { must = must.Count > 0 ? must : [new { match_all = new { } }], filter } },
                ["sort"]    = new object[] { new Dictionary<string, object> { ["bibId"] = new { order = "asc" } } },
                ["_source"] = new[] { "bibId" },
            };
            if (searchAfter != null) body["search_after"] = searchAfter;

            var resp = await _client.Transport.RequestAsync<StringResponse>(
                Elastic.Transport.HttpMethod.POST,
                $"/{_unifiedIndexName}/_search",
                PostData.String(System.Text.Json.JsonSerializer.Serialize(body)));

            if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
                throw new InvalidOperationException(
                    $"Tìm bibId thất bại (HTTP {resp.ApiCallDetails.HttpStatusCode}): {resp.ApiCallDetails.DebugInformation}");

            using var doc = System.Text.Json.JsonDocument.Parse(resp.Body);
            var hits = Prop(Prop(doc.RootElement, "hits"), "hits");
            if (hits?.ValueKind != System.Text.Json.JsonValueKind.Array || hits.Value.GetArrayLength() == 0) break;

            System.Text.Json.JsonElement last = default;
            foreach (var h in hits.Value.EnumerateArray())
            {
                var id = Num(Prop(Prop(h, "_source"), "bibId"));
                if (id.HasValue) result.Add((long)id.Value);
                last = h;
            }

            if (hits.Value.GetArrayLength() < pageSize) break;
            var sortEl = Prop(last, "sort");
            if (sortEl?.ValueKind != System.Text.Json.JsonValueKind.Array) break;
            // sort value của bibId là số → truyền lại nguyên dạng số, không bọc chuỗi
            searchAfter = sortEl.Value.EnumerateArray()
                .Select(x => x.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? (object)x.GetDouble() : x.GetString()!)
                .ToArray();
        }

        return result;
    }

    private static readonly HashSet<string> SuggestableFields = new(StringComparer.Ordinal) { "title", "author", "publisher", "keyword" };

    /// <summary>
    /// Gợi ý giá trị phân biệt theo tiền tố cho 1 trường của index gộp — khớp trên ".no_accent" nên
    /// không phân biệt dấu tiếng Việt (gõ "van hoc" vẫn ra "Văn học"). Dùng terms aggregation trên
    /// ".raw" (keyword) để gộp các bản ghi trùng giá trị thành 1 gợi ý duy nhất, xếp theo tần suất —
    /// không cần thêm trường "search_as_you_type"/mapping mới nên không cần reindex.
    /// </summary>
    public async Task<List<string>> SuggestFieldAsync(string field, string query, long? tenantId = null)
    {
        if (!SuggestableFields.Contains(field) || string.IsNullOrWhiteSpace(query)) return [];
        await EnsureUnifiedIndexAsync();

        var must = new List<object>
        {
            new { match_bool_prefix = new Dictionary<string, object> { [$"{field}.no_accent"] = new { query } } }
        };
        var filter = new List<object>();
        if (tenantId.HasValue)
            filter.Add(new { term = new Dictionary<string, object> { ["tenantId"] = tenantId.Value } });

        var body = new Dictionary<string, object>
        {
            ["size"]  = 0,
            ["query"] = new { @bool = new { must, filter } },
            ["aggs"]  = new Dictionary<string, object>
            {
                ["values"] = new { terms = new { field = $"{field}.raw", size = 8, order = new { _count = "desc" } } }
            }
        };

        var resp = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.POST,
            $"/{_unifiedIndexName}/_search",
            PostData.String(System.Text.Json.JsonSerializer.Serialize(body)));

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode) return [];

        using var doc = System.Text.Json.JsonDocument.Parse(resp.Body);
        var buckets = Prop(Prop(Prop(doc.RootElement, "aggregations"), "values"), "buckets");
        if (buckets?.ValueKind != System.Text.Json.JsonValueKind.Array) return [];

        return buckets.Value.EnumerateArray()
            .Select(b => Str(Prop(b, "key")))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Cast<string>()
            .ToList();
    }

    /// <summary>Đợt 22.2 — tách khỏi <see cref="MapUnified"/> để <see cref="MoreLikeThisAsync"/> dùng lại
    /// đúng 1 chỗ ánh xạ hit → item (trước đó lặp lại nguyên khối này).</summary>
    private static UnifiedSearchItem? MapUnifiedHit(System.Text.Json.JsonElement hit)
    {
        var s = Prop(hit, "_source");
        if (s == null) return null;

        var item = new UnifiedSearchItem
        {
            GroupId        = Str(Prop(s, "groupId")) ?? "",
            DocType        = Str(Prop(s, "docType")) ?? "",
            PublicId       = Str(Prop(s, "publicId")),
            Title          = Str(Prop(s, "title")),
            Author         = Str(Prop(s, "author")),
            Publisher      = Str(Prop(s, "publisher")),
            PublishDate    = Str(Prop(s, "publishDate")),
            PublishYear    = (int?)Num(Prop(s, "publishYear")),
            Ddc            = Str(Prop(s, "ddc")),
            Isbn           = Str(Prop(s, "isbn")),
            Summary        = Str(Prop(s, "summary")),
            Keyword        = Str(Prop(s, "keyword")),
            Language       = Str(Prop(s, "language")),
            MaterialType   = Str(Prop(s, "materialType")),
            CollectionId   = Str(Prop(s, "collectionId")),
            CollectionName = Str(Prop(s, "collectionName")),
            Images         = Str(Prop(s, "images")),
            EbookId        = (long?)Num(Prop(s, "ebookId")),
            EbookFileId    = (long?)Num(Prop(s, "ebookFileId")),
            Free           = Bool(Prop(s, "free")),
            BibId          = (long?)Num(Prop(s, "bibId")),
            Mfn            = (long?)Num(Prop(s, "mfn")),
            CopyCount      = (int?)Num(Prop(s, "copyCount")),
            AvailableCount = (int?)Num(Prop(s, "availableCount")),
            Score          = Num(Prop(hit, "_score")),
        };

        // Chunk khớp nhất của tài liệu số → số trang + đoạn trích để deep-link.
        var best = Prop(Prop(Prop(Prop(Prop(hit, "inner_hits"), "best"), "hits"), "hits"), null);
        System.Text.Json.JsonElement? bestHit = null;
        if (best?.ValueKind == System.Text.Json.JsonValueKind.Array && best.Value.GetArrayLength() > 0)
            bestHit = best.Value[0];
        if (bestHit != null)
            item.BestPageNumber = (int?)Num(Prop(Prop(bestHit, "_source"), "pageNumber"));
        item.BestPageNumber ??= (int?)Num(Prop(s, "pageNumber"));
        item.Highlight = FirstHighlight(Prop(hit, "highlight")) ?? FirstHighlight(Prop(bestHit, "highlight"));

        var hd = Prop(s, "holdings");
        if (hd?.ValueKind == System.Text.Json.JsonValueKind.Array && hd.Value.GetArrayLength() > 0)
            item.Holdings = hd.Value.EnumerateArray().Select(h => new UnifiedHoldingItem
            {
                Barcode    = Str(Prop(h, "barcode")),
                StoreId    = (int?)Num(Prop(h, "storeId")),
                StoreName  = Str(Prop(h, "storeName")),
                Status     = Str(Prop(h, "status")),
                StatusName = Str(Prop(h, "statusName")),
            }).ToList();

        return item;
    }

    /// <summary>Đợt 22.2 — "Có thể bạn quan tâm": more_like_this trên title/author/keyword của chính tài
    /// liệu <paramref name="groupId"/>, loại chính nó ra, lọc tenant giống hệt SearchUnifiedAsync (kể cả quy
    /// ước "chấp nhận doc chưa gán tenant" khi StrictTenantFilter=false — tài liệu số hiện chưa backfill
    /// tenantId, xem chú thích ở đó). Đọc trước field text của chính tài liệu (1 truy vấn theo groupId) rồi
    /// dùng "like":[{"doc":{...}}] — không cần biết _id thật trong ES, chỉ cần các field business đã có.</summary>
    public async Task<List<UnifiedSearchItem>> MoreLikeThisAsync(string groupId, long? tenantId, int size = 8)
    {
        await EnsureUnifiedIndexAsync();

        var sourceResp = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.POST, $"/{_unifiedIndexName}/_search",
            PostData.String(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["size"] = 1,
                ["query"] = new { term = new Dictionary<string, object> { ["groupId"] = groupId } },
                ["_source"] = new[] { "title", "author", "keyword" },
            })));
        if (!sourceResp.ApiCallDetails.HasSuccessfulStatusCode) return [];

        using var sourceDoc = System.Text.Json.JsonDocument.Parse(sourceResp.Body);
        var sourceHit = Prop(Prop(Prop(sourceDoc.RootElement, "hits"), "hits"), null);
        if (sourceHit?.ValueKind != System.Text.Json.JsonValueKind.Array || sourceHit.Value.GetArrayLength() == 0) return [];
        var srcSource = Prop(sourceHit.Value[0], "_source");
        var likeDoc = new Dictionary<string, object?>
        {
            ["title"]   = Str(Prop(srcSource, "title")),
            ["author"]  = Str(Prop(srcSource, "author")),
            ["keyword"] = Str(Prop(srcSource, "keyword")),
        };
        if (likeDoc.Values.All(v => string.IsNullOrWhiteSpace(v as string))) return [];

        var filter = new List<object> { new { @bool = new { must_not = new object[] { new { term = new Dictionary<string, object> { ["groupId"] = groupId } } } } } };
        if (tenantId.HasValue)
            filter.Add(_strictTenantFilter
                ? new { term = new Dictionary<string, object> { ["tenantId"] = tenantId.Value } }
                : (object)new { @bool = new {
                    should = new object[] {
                        new { term = new Dictionary<string, object> { ["tenantId"] = tenantId.Value } },
                        new { @bool = new { must_not = new object[] { new { exists = new { field = "tenantId" } } } } }
                    },
                    minimum_should_match = 1 } });

        var body = new Dictionary<string, object>
        {
            ["size"] = size,
            ["query"] = new { @bool = new {
                must = new object[] { new { more_like_this = new {
                    fields = new[] { "title^2", "author", "keyword", "title.no_accent^2", "author.no_accent" },
                    like = new object[] { new { doc = likeDoc } },
                    min_term_freq = 1, min_doc_freq = 1,
                } } },
                filter } },
            ["collapse"] = new { field = "groupId" },
            ["_source"] = new { excludes = new[] { "embedding", "content", "allMarcText", "isbd" } },
        };

        var resp = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.POST, $"/{_unifiedIndexName}/_search",
            PostData.String(System.Text.Json.JsonSerializer.Serialize(body)));
        if (!resp.ApiCallDetails.HasSuccessfulStatusCode) return [];

        using var doc = System.Text.Json.JsonDocument.Parse(resp.Body);
        var hits = Prop(Prop(Prop(doc.RootElement, "hits"), "hits"), null);
        var items = new List<UnifiedSearchItem>();
        if (hits?.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var hit in hits.Value.EnumerateArray())
            {
                var item = MapUnifiedHit(hit);
                if (item != null) items.Add(item);
            }
        return items;
    }

    private static object Facet(string field, int size) => new
    {
        terms = new { field, size },
        aggs  = new Dictionary<string, object> { ["docs"] = new { cardinality = new { field = "groupId" } } }
    };

    // Đọc thẳng bằng System.Text.Json thay vì DynamicResponse.Get(path):
    // cách kia không phân giải được đường dẫn lồng/chỉ số mảng nên trả về rỗng toàn bộ.
    private static UnifiedSearchResponse MapUnified(string rawJson, int page, int pageSize)
    {
        var result = new UnifiedSearchResponse { Page = page, PageSize = pageSize };
        using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var aggs = Prop(root, "aggregations");
        // Đợt 25 — mỗi agg giờ bọc trong 1 "filter" aggregation riêng (facet sticky), nên số đếm/
        // buckets nằm sâu thêm 1 cấp "inner" so với trước.
        result.Total = (long)(Num(Prop(Prop(Prop(aggs, "total_docs"), "inner"), "value")) ?? 0);
        result.SearchExecutionTimeMs = (long?)Num(Prop(root, "took"));

        var hits = Prop(Prop(root, "hits"), "hits");
        if (hits?.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var hit in hits.Value.EnumerateArray())
            {
                var item = MapUnifiedHit(hit);
                if (item != null) result.Items.Add(item);
            }

        result.Facets = new UnifiedSearchFacets
        {
            DocTypes      = ReadFacet(aggs, "by_doc_type"),
            Years         = ReadFacet(aggs, "by_year"),
            Collections   = ReadFacet(aggs, "by_collection"),
            MaterialTypes = ReadFacet(aggs, "by_material"),
            Ddc           = ReadFacet(aggs, "by_ddc"),
            Languages     = ReadFacet(aggs, "by_language"),
            Stores        = ReadNestedFacet(aggs, "by_store", "stores"),
            Authors       = ReadFacet(aggs, "by_author"),
        };
        return result;
    }

    private static System.Text.Json.JsonElement? Prop(System.Text.Json.JsonElement? el, string? name)
    {
        if (el == null) return null;
        if (name == null) return el;
        return el.Value.ValueKind == System.Text.Json.JsonValueKind.Object
            && el.Value.TryGetProperty(name, out var v) ? v : null;
    }

    private static string? Str(System.Text.Json.JsonElement? el)
        => el?.ValueKind == System.Text.Json.JsonValueKind.String ? el.Value.GetString() : null;

    private static double? Num(System.Text.Json.JsonElement? el)
        => el?.ValueKind == System.Text.Json.JsonValueKind.Number ? el.Value.GetDouble() : null;

    private static bool? Bool(System.Text.Json.JsonElement? el) => el?.ValueKind switch
    {
        System.Text.Json.JsonValueKind.True  => true,
        System.Text.Json.JsonValueKind.False => false,
        _ => null
    };

    private static string? FirstHighlight(System.Text.Json.JsonElement? highlight)
    {
        if (highlight?.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        foreach (var field in highlight.Value.EnumerateObject())
            if (field.Value.ValueKind == System.Text.Json.JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                return field.Value[0].GetString();
        return null;
    }

    /// <summary>Đợt 22.2 — đọc facet lồng trong 1 aggregation "nested" (vd Kho, nằm trong holdings): buckets
    /// ở path <c>{aggName}.{termsAggName}.buckets</c>, số đếm lấy từ <c>doc_count</c> của sub-agg
    /// reverse_nested tên "docs" (số TÀI LIỆU gốc, không phải số bản — xem chú thích ở nơi khai báo agg).</summary>
    private static List<FacetItem> ReadNestedFacet(System.Text.Json.JsonElement? aggs, string aggName, string termsAggName)
    {
        var list = new List<FacetItem>();
        // Đợt 25 — thêm 1 cấp "inner" do agg gốc giờ bọc trong 1 "filter" aggregation (facet sticky).
        var buckets = Prop(Prop(Prop(Prop(aggs, aggName), "inner"), termsAggName), "buckets");
        if (buckets?.ValueKind != System.Text.Json.JsonValueKind.Array) return list;
        foreach (var b in buckets.Value.EnumerateArray())
        {
            var keyEl = Prop(b, "key");
            var key = keyEl?.ValueKind == System.Text.Json.JsonValueKind.String ? keyEl.Value.GetString() : keyEl?.ToString();
            if (string.IsNullOrEmpty(key)) continue;
            var count = (long)(Num(Prop(Prop(b, "docs"), "doc_count")) ?? 0);
            list.Add(new FacetItem(key, count));
        }
        return list;
    }

    private static List<FacetItem> ReadFacet(System.Text.Json.JsonElement? aggs, string aggName)
    {
        var list = new List<FacetItem>();
        // Đợt 25 — thêm 1 cấp "inner" do agg gốc giờ bọc trong 1 "filter" aggregation (facet sticky).
        var buckets = Prop(Prop(Prop(aggs, aggName), "inner"), "buckets");
        if (buckets?.ValueKind != System.Text.Json.JsonValueKind.Array) return list;
        foreach (var b in buckets.Value.EnumerateArray())
        {
            var keyEl = Prop(b, "key_as_string") ?? Prop(b, "key");
            var key = keyEl?.ValueKind == System.Text.Json.JsonValueKind.String
                ? keyEl.Value.GetString()
                : keyEl?.ToString();
            if (string.IsNullOrEmpty(key)) continue;
            // Ưu tiên số TÀI LIỆU (sub-agg cardinality) thay vì số doc thô.
            var count = (long)(Num(Prop(Prop(b, "docs"), "value")) ?? Num(Prop(b, "doc_count")) ?? 0);
            list.Add(new FacetItem(key, count));
        }
        return list;
    }

    /// <summary>Trạng thái một task _reindex/_update_by_query đang chạy nền.</summary>
    public async Task<object> GetTaskStatusAsync(string taskId)
    {
        var resp = await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.GET, $"/_tasks/{taskId}", PostData.String(""));
        return resp.Body;
    }

    /// <summary>
    /// Bổ sung các trường chung mà index cũ KHÔNG có (ddc/summary/language/isbn/materialType/tenantId)
    /// cho tài liệu số, lấy từ DB. Chỉ cập nhật tại chỗ theo ebookId — không đụng content/embedding.
    /// </summary>
    public async Task EnrichDigitalCommonFieldsAsync(long ebookId, LibraryDocument meta)
    {
        await EnsureUnifiedIndexAsync();
        const string script = """
            ctx._source.ddc          = params.ddc;
            ctx._source.summary      = params.summary;
            ctx._source.language     = params.language;
            ctx._source.isbn         = params.isbn;
            ctx._source.materialType = params.materialType;
            ctx._source.tenantId     = params.tenantId;
            """;

        await _client.UpdateByQueryAsync<LibraryDocument>(_unifiedIndexName, u => u
            .Query(q => q.Term(t => t.Field("groupId").Value($"digital_{ebookId}")))
            .Script(s => s
                .Source(script)
                .Params(new Dictionary<string, object?>
                {
                    ["ddc"]          = meta.Ddc,
                    ["summary"]      = meta.Summary,
                    ["language"]     = meta.Language,
                    ["isbn"]         = meta.Isbn,
                    ["materialType"] = meta.MaterialType,
                    ["tenantId"]     = meta.TenantId,
                }!)));
    }

    // ── RAG: kNN vector search ────────────────────────────────────────────────

    public async Task<List<RagChunk>> RetrieveRagChunksAsync(float[] queryVector,
        long? tenantId = null, int topK = 10, string? collectionId = null,
        string? topicId = null, string? subjectId = null,
        string? language = null, bool? free = null,
        double minScore = 0, int numCandidatesMultiplier = 10, Guid? ebookPublicId = null)
    {
        await EnsureChunkIndexAsync();

        var filters = new List<string>();
        if (ebookPublicId.HasValue)
            filters.Add($$$"""{"term":{"publicId":"{{{ebookPublicId.Value}}}"}}""");
        if (tenantId.HasValue)
            filters.Add($$$"""{"bool":{"should":[{"term":{"tenantId":{{{tenantId.Value}}}}},{"bool":{"must_not":[{"exists":{"field":"tenantId"}}]}}],"minimum_should_match":1}}""");
        if (!string.IsNullOrEmpty(collectionId))
            filters.Add($$$"""{"term":{"collectionId.keyword":"{{{EscapeJson(collectionId)}}}"}}""");
        if (!string.IsNullOrEmpty(topicId))
            filters.Add($$$"""{"term":{"topicId.keyword":"{{{EscapeJson(topicId)}}}"}}""");
        if (!string.IsNullOrEmpty(subjectId))
            filters.Add($$$"""{"term":{"subjectId.keyword":"{{{EscapeJson(subjectId)}}}"}}""");
        if (!string.IsNullOrEmpty(language))
            // Field đúng là "dc_language" (keyword, không có subfield .keyword) — hầu hết tài liệu
            // chưa được nhập metadata ngôn ngữ nên fallback should+missing như tenantId, tránh loại
            // sạch tài liệu chưa gắn ngôn ngữ khi client có gửi filter language.
            filters.Add($$$"""{"bool":{"should":[{"term":{"dc_language":"{{{EscapeJson(language)}}}"}},{"bool":{"must_not":[{"exists":{"field":"dc_language"}}]}}],"minimum_should_match":1}}""");
        if (free.HasValue)
            filters.Add($$$"""{"term":{"free":{{{free.Value.ToString().ToLower()}}}}}""");

        var vectorJson = "[" + string.Join(",", queryVector.Select(v => v.ToString("G", System.Globalization.CultureInfo.InvariantCulture))) + "]";
        var filterJson = filters.Count > 0 ? string.Join(",", filters) : "";

        var numCandidates = Math.Max(topK * numCandidatesMultiplier, 100);
        var minScoreJson = minScore > 0 ? $$"""  ,"min_score": {{minScore.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}}""" : "";

        var requestBody = string.IsNullOrEmpty(filterJson)
            ? $$"""
                {
                  "knn": {
                    "field": "embedding",
                    "query_vector": {{vectorJson}},
                    "k": {{topK}},
                    "num_candidates": {{numCandidates}}
                  },
                  "_source": ["ebookId","ebookFileId","publicId","title","author","pageNumber","chunkIndex","content"],
                  "size": {{topK}}{{minScoreJson}}
                }
                """
            : $$"""
                {
                  "knn": {
                    "field": "embedding",
                    "query_vector": {{vectorJson}},
                    "k": {{topK}},
                    "num_candidates": {{numCandidates}},
                    "filter": [{{filterJson}}]
                  },
                  "_source": ["ebookId","ebookFileId","publicId","title","author","pageNumber","chunkIndex","content"],
                  "size": {{topK}}{{minScoreJson}}
                }
                """;

        var response = await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.POST,
            $"/{_chunkIndexName}/_search",
            PostData.String(requestBody));

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            return [];

        var result = new List<RagChunk>();
        using var doc = System.Text.Json.JsonDocument.Parse(response.ApiCallDetails.ResponseBodyInBytes ?? []);
        if (!doc.RootElement.TryGetProperty("hits", out var hitsOuter)) return result;
        if (!hitsOuter.TryGetProperty("hits", out var hits)) return result;

        foreach (var hit in hits.EnumerateArray())
        {
            var score  = hit.TryGetProperty("_score", out var sc) ? sc.GetDouble() : 0d;
            if (!hit.TryGetProperty("_source", out var src)) continue;

            var content = GetStr(src, "content");
            if (string.IsNullOrWhiteSpace(content)) continue;

            result.Add(new RagChunk
            {
                EbookId     = GetLong(src, "ebookId"),
                EbookFileId = GetNullableLong(src, "ebookFileId"),
                PublicId    = GetStr(src, "publicId"),
                Title       = GetStr(src, "title"),
                Author      = GetStr(src, "author"),
                PageNumber  = GetInt(src, "pageNumber"),
                ChunkIndex  = GetInt(src, "chunkIndex"),
                Content     = content,
                Score       = score
            });
        }
        return result;
    }

    /// <summary>Tìm theo từ khóa (BM25) trong nội dung — 1 tài liệu (<paramref name="ebookPublicId"/>) hoặc toàn kho. Ghép với kNN
    /// thành tìm lai (bắt đúng tên riêng/con số) và là đường dự phòng khi máy chủ embedding không phản hồi (port ELIB-LRC 09-29).
    /// Lọc đơn vị như <see cref="RetrieveRagChunksAsync"/>: chunk của đơn vị hoặc chunk dùng chung (không có tenantId).</summary>
    public async Task<List<RagChunk>> RetrieveKeywordChunksAsync(Guid? ebookPublicId, string query, long? tenantId = null, int size = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        await EnsureChunkIndexAsync();

        var filters = new List<object>();
        if (ebookPublicId.HasValue)
            filters.Add(new { term = new { publicId = ebookPublicId.Value.ToString() } });
        if (tenantId.HasValue)
            filters.Add(new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["should"] = new object[]
                    {
                        new { term = new { tenantId = tenantId.Value } },
                        new Dictionary<string, object> { ["bool"] = new { must_not = new object[] { new { exists = new { field = "tenantId" } } } } },
                    },
                    ["minimum_should_match"] = 1,
                },
            });

        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            size,
            _source = new[] { "ebookId", "ebookFileId", "publicId", "title", "author", "pageNumber", "chunkIndex", "content" },
            query = new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["must"] = new object[] { new { match = new { content = new { query } } } },
                    ["filter"] = filters,
                },
            },
        });
        var response = await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.POST, $"/{_chunkIndexName}/_search", PostData.String(body));
        if (!response.ApiCallDetails.HasSuccessfulStatusCode) return [];

        var result = new List<RagChunk>();
        using var doc = System.Text.Json.JsonDocument.Parse(response.ApiCallDetails.ResponseBodyInBytes ?? []);
        if (!doc.RootElement.TryGetProperty("hits", out var hitsOuter) || !hitsOuter.TryGetProperty("hits", out var hits)) return result;
        foreach (var hit in hits.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var src)) continue;
            var content = GetStr(src, "content");
            if (string.IsNullOrWhiteSpace(content)) continue;
            result.Add(new RagChunk
            {
                EbookId     = GetLong(src, "ebookId"),
                EbookFileId = GetNullableLong(src, "ebookFileId"),
                PublicId    = GetStr(src, "publicId"),
                Title       = GetStr(src, "title"),
                Author      = GetStr(src, "author"),
                PageNumber  = GetInt(src, "pageNumber"),
                ChunkIndex  = GetInt(src, "chunkIndex"),
                Content     = content,
                Score       = hit.TryGetProperty("_score", out var sc) ? sc.GetDouble() : 0d
            });
        }
        return result;
    }

    // Truy xuất tuần tự theo trang (không phải kNN) — dùng cho yêu cầu tóm tắt/tổng quan toàn tài liệu,
    // vì câu "tóm tắt nội dung" không tương đồng ngữ nghĩa với bất kỳ đoạn nội dung cụ thể nào.
    public async Task<List<RagChunk>> RetrieveDocumentOverviewChunksAsync(Guid ebookPublicId, long? tenantId = null, int maxPages = 300)
    {
        await EnsureChunkIndexAsync();

        var response = await _client.SearchAsync<EbookChunkDocument>(s =>
        {
            s.Index(_chunkIndexName).Size(maxPages);
            s.Query(q => q.Bool(b =>
            {
                b.Filter(f => f.Term(t => t.Field("publicId").Value(ebookPublicId.ToString())));
                if (tenantId.HasValue)
                {
                    var tid = tenantId.Value;
                    b.Filter(f => f.Bool(tb => tb
                        .Should(
                            s1 => s1.Term(t => t.Field("tenantId").Value(tid)),
                            s1 => s1.Bool(nb => nb.MustNot(mn => mn.Exists(e => e.Field("tenantId")))))
                        .MinimumShouldMatch(1)));
                }
            }));
            s.Collapse(c => c.Field("pageNumber"));
            s.Sort(so => so.Field("pageNumber", f => f.Order(SortOrder.Asc)));
        });

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Where(h => h.Source != null && !string.IsNullOrWhiteSpace(h.Source!.Content))
            .Select(h => new RagChunk
            {
                EbookId     = h.Source!.EbookId,
                EbookFileId = h.Source!.EbookFileId,
                PublicId    = h.Source!.PublicId.ToString(),
                Title       = h.Source!.Title,
                Author      = h.Source!.Author,
                PageNumber  = h.Source!.PageNumber,
                ChunkIndex  = h.Source!.ChunkIndex,
                Content     = h.Source!.Content,
                Score       = 1.0
            }).ToList();
    }

    // ── Embedding re-index support ────────────────────────────────────────────

    public async Task<(List<(string ChunkId, string Content)> Items, string? ScrollId)> ScrollChunksWithoutEmbeddingAsync(
        string? scrollId, int batchSize = 50)
    {
        string responseBody;
        if (scrollId == null)
        {
            var body = $$"""
                {
                  "query": { "bool": { "must_not": [{ "exists": { "field": "embedding" } }] } },
                  "_source": ["content"],
                  "size": {{batchSize}}
                }
                """;
            var resp = await _client.Transport.RequestAsync<DynamicResponse>(
                Elastic.Transport.HttpMethod.POST,
                $"/{_chunkIndexName}/_search?scroll=2m",
                PostData.String(body));
            if (!resp.ApiCallDetails.HasSuccessfulStatusCode) return ([], null);
            responseBody = System.Text.Encoding.UTF8.GetString(resp.ApiCallDetails.ResponseBodyInBytes ?? []);
        }
        else
        {
            var body = $$$"""{"scroll":"2m","scroll_id":"{{{scrollId}}}"}""";
            var resp = await _client.Transport.RequestAsync<DynamicResponse>(
                Elastic.Transport.HttpMethod.POST,
                "/_search/scroll",
                PostData.String(body));
            if (!resp.ApiCallDetails.HasSuccessfulStatusCode) return ([], null);
            responseBody = System.Text.Encoding.UTF8.GetString(resp.ApiCallDetails.ResponseBodyInBytes ?? []);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        var newScrollId = doc.RootElement.TryGetProperty("_scroll_id", out var sid)
            ? sid.GetString() : null;

        var items = new List<(string, string)>();
        if (doc.RootElement.TryGetProperty("hits", out var outer) &&
            outer.TryGetProperty("hits", out var hits))
        {
            foreach (var hit in hits.EnumerateArray())
            {
                var id = hit.TryGetProperty("_id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;
                var content = hit.TryGetProperty("_source", out var src) ? GetStr(src, "content") : "";
                if (!string.IsNullOrWhiteSpace(content))
                    items.Add((id!, content));
            }
        }
        return (items, items.Count > 0 ? newScrollId : null);
    }

    public async Task BulkUpdateEmbeddingsAsync(IEnumerable<(string ChunkId, float[] Embedding)> updates)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (chunkId, embedding) in updates)
        {
            var vectorJson = "[" + string.Join(",",
                embedding.Select(v => v.ToString("G", System.Globalization.CultureInfo.InvariantCulture))) + "]";
            sb.AppendLine("{\"update\":{\"_index\":\"" + _chunkIndexName + "\",\"_id\":\"" + EscapeJson(chunkId) + "\"}}");
            sb.AppendLine("{\"doc\":{\"embedding\":" + vectorJson + "}}");
        }
        if (sb.Length == 0) return;

        await _client.Transport.RequestAsync<DynamicResponse>(
            Elastic.Transport.HttpMethod.POST,
            "/_bulk",
            PostData.String(sb.ToString()));
    }

    public async Task<(long Total, long Embedded, long Pending)> GetEmbeddingStatusAsync()
    {
        static async Task<long> Count(ElasticsearchClient client, string index, string body)
        {
            var resp = await client.Transport.RequestAsync<DynamicResponse>(
                Elastic.Transport.HttpMethod.POST,
                $"/{index}/_count",
                PostData.String(body));
            if (!resp.ApiCallDetails.HasSuccessfulStatusCode) return 0;
            using var doc = System.Text.Json.JsonDocument.Parse(resp.ApiCallDetails.ResponseBodyInBytes ?? []);
            return doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt64() : 0;
        }

        var total    = await Count(_client, _chunkIndexName, "{\"query\":{\"match_all\":{}}}");
        var embedded = await Count(_client, _chunkIndexName, "{\"query\":{\"bool\":{\"must\":[{\"exists\":{\"field\":\"embedding\"}}]}}}");
        return (total, embedded, total - embedded);
    }

    private static string GetStr(System.Text.Json.JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static int GetInt(System.Text.Json.JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
            ? v.GetInt32() : 0;

    private static long GetLong(System.Text.Json.JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
            ? v.GetInt64() : 0;

    private static long? GetNullableLong(System.Text.Json.JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
            ? v.GetInt64() : null;

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public async Task<bool> PingAsync()
    {
        try { return (await _client.PingAsync()).IsValidResponse; }
        catch { return false; }
    }
}
