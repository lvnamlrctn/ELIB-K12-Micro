namespace ELIBAPI.Core.DTOs.Response;

public class DicClassTreeNode
{
    public long    Id            { get; set; }
    public Guid    PublicId      { get; set; }
    public string? Code          { get; set; }
    public string? Description   { get; set; }
    public string? VnDescription { get; set; }
    public string? Type          { get; set; }
    public List<DicClassTreeNode> Children { get; set; } = new();
}

public class BibMarcSubFieldResponse
{
    public string? Code  { get; set; }
    public string? Value { get; set; }
}

public class BibMarcFieldResponse
{
    public string? Tag      { get; set; }
    public string? Ind1     { get; set; }
    public string? Ind2     { get; set; }
    public string? Value    { get; set; } // control field (Tag < "010"): Leader/001/003/005/008...
    public List<BibMarcSubFieldResponse> SubFields { get; set; } = new();
}

public class BibMarcResponse
{
    public long?   Mfn        { get; set; }
    public long    BibId      { get; set; }
    public long?   BibTypeId  { get; set; }
    public long?   CollectionId { get; set; }
    public string? Title      { get; set; }
    public string? Author     { get; set; }
    public string? Publisher  { get; set; }
    public string? PublishDate { get; set; }
    public string? Images     { get; set; }
    public List<BibMarcFieldResponse> Fields { get; set; } = new();
}

// Dùng cho CatalogueBookController.CheckIsbn — cảnh báo trùng ISBN (MARC 020$a) khi thêm biểu ghi mới.
public class IsbnDuplicateResponse
{
    public long   BibId { get; set; }
    public long?  Mfn   { get; set; }
    public string? Title { get; set; }
}

public class SaveBibRequest
{
    public long?   Mfn       { get; set; }
    public long?   BibId     { get; set; }
    public long?   BibTypeId { get; set; }
    public long?   CollectionId { get; set; }
    public string? Images    { get; set; }
    public List<BibMarcFieldResponse> Fields { get; set; } = new();
}

public class LoanReaderSnapshotResponse
{
    public long?   ReaderId   { get; set; }
    public Guid?   ReaderPublicId { get; set; }
    public string? CardNo     { get; set; }
    public string? FullName   { get; set; }
    public string? ReaderType { get; set; }
    public string? ClassName  { get; set; }
    public string? CourseName { get; set; }
    public string? OrgName    { get; set; }
    /// <summary>Số căn cước công dân.</summary>
    public string? CitizenId  { get; set; }
    public string? ExpireDate { get; set; }
    public string? IssueDate  { get; set; }
    public string? Photo      { get; set; }
    public double? Balance    { get; set; }
    public int?    Status     { get; set; }
    public int?    MaxItems   { get; set; }
    public bool    IsLocked    { get; set; }
    public bool    IsExpired   { get; set; }
    public bool    HasOverdue  { get; set; }
    public bool    CanBorrow   { get; set; }
    public string? BlockReason { get; set; }
    public List<CurrentLoanItem> CurrentLoans { get; set; } = new();
}

public class CurrentLoanItem
{
    public long    Id         { get; set; }
    public string? Barcode    { get; set; }
    public string? BibTitle   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public DateTime? DueDate   { get; set; }
    public string? Status     { get; set; }
    public double? FineValue  { get; set; }
    public int?    RenewCount { get; set; }
    public string? Location   { get; set; }
    public string? Note       { get; set; }
}

public class LoanActionRequest
{
    public long?   ReaderId   { get; set; }
    public long?   BorrowId   { get; set; }
    public string? Barcode    { get; set; }
    public string? CardNo     { get; set; }
    public int?    CircPlaceId { get; set; }
    public string? Note       { get; set; }

    /// <summary>Bắt buộc cho Renew/Note (Đợt 14) — lý do thao tác, ghi cùng UserLog. Các action khác
    /// (Checkout/Return) không dùng field này.</summary>
    public string? Reason     { get; set; }
}
