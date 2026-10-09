namespace ELIBAPI.Core.DTOs.Response;

public class UnifiedSearchResponse
{
    public long Total    { get; set; }
    public int  Page     { get; set; }
    public int  PageSize { get; set; }
    public List<UnifiedSearchItem> Items  { get; set; } = [];
    public UnifiedSearchFacets     Facets { get; set; } = new();
    /// <summary>true = kết quả này đến từ DB fallback (ES lỗi/chậm/đang mở circuit breaker), không có
    /// xếp hạng liên quan/facet/highlight đầy đủ như ES thật — Đợt 9.</summary>
    public bool UsedFallback { get; set; }
    /// <summary>Đợt 25 — thời gian Elasticsearch xử lý truy vấn (ms, lấy từ trường "took" của chính ES).
    /// null khi dùng DB fallback (không có khái niệm này).</summary>
    public long? SearchExecutionTimeMs { get; set; }
}

/// <summary>
/// Một tài liệu trong kết quả gộp. Các trường chung luôn có; nhóm riêng theo loại chỉ có
/// giá trị khi <see cref="DocType"/> tương ứng.
/// </summary>
public class UnifiedSearchItem
{
    public string  GroupId  { get; set; } = string.Empty;
    /// <summary>"print" | "digital"</summary>
    public string  DocType  { get; set; } = string.Empty;
    public string? PublicId { get; set; }

    // Bộ trường chung
    public string? Title          { get; set; }
    public string? Author         { get; set; }
    public string? Publisher      { get; set; }
    public string? PublishDate    { get; set; }
    public int?    PublishYear    { get; set; }
    public string? Ddc            { get; set; }
    public string? Isbn           { get; set; }
    public string? Summary        { get; set; }
    public string? Keyword        { get; set; }
    public string? Language       { get; set; }
    public string? MaterialType   { get; set; }
    public string? CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public string? Images         { get; set; }

    // Riêng tài liệu số
    public long?   EbookId        { get; set; }
    public long?   EbookFileId    { get; set; }
    /// <summary>Trang chứa đoạn nội dung khớp nhất — dùng để deep-link sang trang đọc.</summary>
    public int?    BestPageNumber { get; set; }
    public bool?   Free           { get; set; }

    // Riêng tài liệu in
    public long?   BibId          { get; set; }
    public long?   Mfn            { get; set; }
    public int?    CopyCount      { get; set; }
    public int?    AvailableCount { get; set; }
    public List<UnifiedHoldingItem>? Holdings { get; set; }

    /// <summary>Đoạn nội dung khớp, đã bọc &lt;mark&gt;.</summary>
    public string? Highlight { get; set; }
    public double? Score     { get; set; }
}

public class UnifiedHoldingItem
{
    public string? Barcode    { get; set; }
    public int?    StoreId    { get; set; }
    public string? StoreName  { get; set; }
    public string? Status     { get; set; }
    public string? StatusName { get; set; }
}

public class UnifiedSearchFacets
{
    /// <summary>Số tài liệu theo loại — "print"/"digital". Dùng cho tab lọc trên UI.</summary>
    public List<FacetItem> DocTypes     { get; set; } = [];
    public List<FacetItem> Years        { get; set; } = [];
    public List<FacetItem> Collections  { get; set; } = [];
    public List<FacetItem> MaterialTypes{ get; set; } = [];
    public List<FacetItem> Ddc          { get; set; } = [];
    public List<FacetItem> Languages    { get; set; } = [];
    /// <summary>Đợt 22.2 — số đầu tài liệu (đếm 1 lần/tài liệu, không theo số bản) đang có tại từng Kho.</summary>
    public List<FacetItem> Stores       { get; set; } = [];
    /// <summary>Đợt 22.2.</summary>
    public List<FacetItem> Authors      { get; set; } = [];
}
