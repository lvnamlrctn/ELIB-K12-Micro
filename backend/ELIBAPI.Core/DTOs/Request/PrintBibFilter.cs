namespace ELIBAPI.Core.DTOs.Request;

/// <summary>
/// Bộ lọc dùng chung cho việc tra bibId của tài liệu in qua Elasticsearch —
/// thay cho 3 chỗ dựng câu SQL LIKE '%...%' trùng lặp trước đây
/// (BibRepository.BuildQuery, BibFilterHelper, BookController.FilterBibs).
/// </summary>
public class PrintBibFilter
{
    public long?   MfnFrom       { get; set; }
    public long?   MfnTo         { get; set; }
    public string? Title         { get; set; }
    public string? Author        { get; set; }
    public string? Publisher     { get; set; }
    /// <summary>Năm/ngày xuất bản — chuỗi tự do, khớp theo cụm.</summary>
    public string? PublishDate   { get; set; }
    public string? Keyword       { get; set; }
    public string? Summary       { get; set; }
    public long?   BibTypeId     { get; set; }
    public long?   TenantId      { get; set; }
    /// <summary>Đợt 24.4 — true khi TenantId là 1 đơn vị cụ thể do user đặc quyền chủ động chọn: khớp
    /// đơn vị đó HOẶC bản ghi dùng chung (tenantId rỗng trong index), thay vì so khớp tuyệt đối. Mirror
    /// BaseRepository.ApplyTenantFilter (Đợt 23). false (mặc định) giữ nguyên hành vi cũ — so khớp tuyệt
    /// đối, dùng khi TenantId bị ép theo JWT của user thường.</summary>
    public bool    TenantIncludeShared { get; set; }

    // Điều kiện trên bản sao vật lý (nested holdings)
    public int?    StoreId       { get; set; }
    public string? CallNumber    { get; set; }
    public string? BarcodeStatus { get; set; }

    public bool HasAnyCriteria =>
        MfnFrom.HasValue || MfnTo.HasValue || BibTypeId.HasValue || StoreId.HasValue
        || !string.IsNullOrWhiteSpace(Title)      || !string.IsNullOrWhiteSpace(Author)
        || !string.IsNullOrWhiteSpace(Publisher)  || !string.IsNullOrWhiteSpace(PublishDate)
        || !string.IsNullOrWhiteSpace(Keyword)    || !string.IsNullOrWhiteSpace(Summary)
        || !string.IsNullOrWhiteSpace(CallNumber) || !string.IsNullOrWhiteSpace(BarcodeStatus);
}
