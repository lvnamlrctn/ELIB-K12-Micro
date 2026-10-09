namespace ELIBAPI.Core.DTOs.Elasticsearch;

/// <summary>
/// Document của index gộp <c>library_docs</c> — chứa CẢ tài liệu in (PrintBook.Bib) và
/// tài liệu số (Ebook.Item). Hai loại có độ mịn khác nhau:
///   • Tài liệu in : 1 doc = 1 biểu ghi        → _id = "print_{bibId}"
///   • Tài liệu số : 1 doc = 1 chunk nội dung  → _id = "digital_{ebookId}_p{page:D4}_c{chunk:D2}"
/// Gộp về 1 dòng kết quả bằng <see cref="GroupId"/> (collapse).
///
/// Bộ trường CHUNG (nhóm 1) là nơi mọi tiêu chí tìm kiếm/lọc phải nằm — hai loại tài liệu đều có
/// trường thư mục tương đương, chỉ khác nguồn lấy, nên quy về MỘT tên trường để một truy vấn và
/// một facet chạy được xuyên cả 2 loại.
/// </summary>
public class LibraryDocument
{
    // ── 1. Điều phối ─────────────────────────────────────────────────────────
    /// <summary>"print_{bibId}" | "digital_{ebookId}_p{page:D4}_c{chunk:D2}" — dùng làm _id.</summary>
    public string DocId    { get; set; } = string.Empty;
    /// <summary>"print_{bibId}" | "digital_{ebookId}" — collapse để mỗi tài liệu về 1 dòng.</summary>
    public string GroupId  { get; set; } = string.Empty;
    /// <summary>"print" | "digital"</summary>
    public string DocType  { get; set; } = string.Empty;
    public Guid   PublicId { get; set; }
    public long?  TenantId { get; set; }

    // ── 2. Bộ trường CHUNG — mọi tiêu chí tìm kiếm/lọc ───────────────────────
    //  Trường          | Tài liệu in                  | Tài liệu số
    //  ----------------|------------------------------|---------------------------------
    //  Title           | BibXml.Title (245$a$b)       | ItemXml.Title
    //  Author          | BibXml.Author (100/700$a)    | ItemXml.Author
    //  Publisher       | BibXml.Publisher (264/260$b) | ItemXml.Publisher
    //  PublishDate     | BibXml.PublishDate (264$c)   | ItemXml.PublishDate
    //  Ddc             | BibXml.DDC (082$a)           | EbookSubject.DDC ?? EbookTopic.DDC
    //  Isbn            | BibData 020$a                | MetaDataValue 20 ?? 23
    //  Summary         | BibData 520$a                | MetaDataValue 27
    //  Keyword         | BibXml.Keyword (650$a)       | ItemXml.Keyword + MetaDataValue 57
    //  Language        | fixed_field_value 008/35-37  | MetaDataValue 38
    //  MaterialType    | BibType.Name                 | DigType (MetaDataValue 66)
    //  CollectionId    | Bib.CollectionId             | EbookItem.CollectionId  (CHUNG 1 bảng)
    //  Contributor     | 700$a                        | MetaDataValue 3
    public string? Title          { get; set; }
    public string? Author         { get; set; }
    public string? Publisher      { get; set; }
    /// <summary>Chuỗi tự do như trong DB ("2015", "[2015]", "2015-2016"…) — giữ nguyên để hiển thị.</summary>
    public string? PublishDate    { get; set; }
    /// <summary>Năm tách từ <see cref="PublishDate"/> — dùng để lọc khoảng năm và facet.</summary>
    public int?    PublishYear    { get; set; }
    public string? Ddc            { get; set; }
    public string? Isbn           { get; set; }
    public string? Summary        { get; set; }
    public string? Keyword        { get; set; }
    public string? Language       { get; set; }
    public string? MaterialType   { get; set; }
    public string? CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public string? Contributor    { get; set; }

    public string?   Images    { get; set; }
    public DateTime  IndexedAt { get; set; }

    // ── 3. Riêng tài liệu số ─────────────────────────────────────────────────
    public long?    EbookId     { get; set; }
    public long?    EbookFileId { get; set; }
    public int?     PageNumber  { get; set; }
    public int?     ChunkIndex  { get; set; }
    /// <summary>Nội dung trích từ file PDF — nguồn của "tìm theo nội dung". Chỉ tài liệu số có.</summary>
    public string?  Content     { get; set; }
    /// <summary>Vector 768 chiều (nomic-embed-text) cho chatbot RAG — sao chép nguyên từ index cũ.</summary>
    public float[]? Embedding   { get; set; }
    public string?  TopicId     { get; set; }
    public string?  TopicName   { get; set; }
    public string?  SubjectId   { get; set; }
    public string?  SubjectName { get; set; }
    public bool?    Free        { get; set; }
    public bool?    Share       { get; set; }

    // ── 4. Riêng tài liệu in ─────────────────────────────────────────────────
    public long?   BibId  { get; set; }
    public long?   Mfn    { get; set; }
    public string? Isbd   { get; set; }
    /// <summary>Gộp mọi BibData.Data — trường bắt-tất để tra cứu các trường MARC hiếm.</summary>
    public string? AllMarcText { get; set; }
    public long?   BibTypeId   { get; set; }
    public string? Status      { get; set; }
    public string? StatusName  { get; set; }
    public string? Url         { get; set; }
    /// <summary>Bib.EbookId — biểu ghi in có bản số tương ứng.</summary>
    public long?   LinkedEbookId { get; set; }

    /// <summary>Bản sao vật lý (nested) — phục vụ tìm theo ĐKCB/kho bằng nested query + inner_hits.</summary>
    public List<HoldingDocument> Holdings { get; set; } = [];
    public int CopyCount      { get; set; }
    public int AvailableCount { get; set; }
}

/// <summary>Một bản sao vật lý (PrintBook.Barcode) của tài liệu in.</summary>
public class HoldingDocument
{
    public long    BarcodeId  { get; set; }
    /// <summary>Số ĐKCB (cột PrintBook.Barcode.Barcode).</summary>
    public string? Barcode    { get; set; }
    public int?    StoreId    { get; set; }
    public string? StoreName  { get; set; }
    public string? Status     { get; set; }
    public string? StatusName { get; set; }
    public long?   ReceiptId  { get; set; }
    public long?   ReceiptCode { get; set; }
}
