namespace ELIBAPI.Core.DTOs.Response;

public class EbookSearchResponse
{
    public long Total    { get; set; }
    public int  Page     { get; set; }
    public int  PageSize { get; set; }
    public List<EbookSearchItem>  Items  { get; set; } = [];
    public EbookSearchFacets      Facets { get; set; } = new();
}

public class EbookSearchItem
{
    public string  EbookId        { get; set; } = string.Empty;
    public string? Title          { get; set; }
    public string? Author         { get; set; }
    public string? Publisher      { get; set; }
    public string? PublishDate    { get; set; }
    public string? DcLanguage     { get; set; }
    public string? DcIdentifier   { get; set; }
    public string? Images         { get; set; }
    public string? CollectionName { get; set; }
    public bool    Free           { get; set; }
    public int?    BestPageNumber { get; set; }
    public string? Highlight      { get; set; }
    public double? Score          { get; set; }
}

public class EbookSearchFacets
{
    public List<FacetItem> Languages   { get; set; } = [];
    public List<FacetItem> Topics      { get; set; } = [];
    public List<FacetItem> Collections { get; set; } = [];
    public List<FacetItem> Years       { get; set; } = [];
    public long FreeCount { get; set; }
}

public record FacetItem(string Key, long Count);

public class BulkSyncChunksResult
{
    public int Enqueued { get; set; }
    public int Skipped  { get; set; }
}

public class PublicEbookElasticResponse
{
    public long Total    { get; set; }
    public int  Page     { get; set; }
    public int  PageSize { get; set; }
    public List<PublicEbookElasticItem> Items  { get; set; } = [];
    public EbookSearchFacets           Facets { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DebugInfo { get; set; }
}

public class PublicEbookElasticItem
{
    public string  EbookId        { get; set; } = string.Empty;
    public string? Title          { get; set; }
    public string? Author         { get; set; }
    public string? Publisher      { get; set; }
    public string? PublishDate    { get; set; }
    public string? Keyword        { get; set; }
    public string? Images         { get; set; }
    public string? CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public string? TopicId        { get; set; }
    public string? TopicName      { get; set; }
    public string? SubjectId      { get; set; }
    public string? SubjectName    { get; set; }
    public bool    Free           { get; set; }
    public bool    Share          { get; set; }
    public string? DcSubject      { get; set; }
    public string? DcDescription  { get; set; }
    public string? DcLanguage     { get; set; }
    public string? DcIdentifier   { get; set; }
    public string? DcType         { get; set; }
    public string? DcContributor  { get; set; }
    public int?    BestPageNumber { get; set; }
    public string? Highlight      { get; set; }
    public double? Score          { get; set; }
}
