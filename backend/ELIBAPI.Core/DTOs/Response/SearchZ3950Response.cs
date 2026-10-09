namespace ELIBAPI.Core.DTOs.Response;

public class Z3950BriefResult
{
    public Guid    LibraryId   { get; set; }
    public string  LibraryName { get; set; } = "";
    public string  Systax      { get; set; } = "";
    public bool    Connected   { get; set; }
    public int     Count       { get; set; }
    /// <summary>Tổng thực tế vượt <c>Z3950SearchService.MaxResults</c> (Count đã bị cắt).</summary>
    public bool    Truncated   { get; set; }
    public string? Error       { get; set; }
}

public class Z3950Record
{
    public Guid?   PublicId    { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public string? Keyword     { get; set; }
    public string? OtherTitle  { get; set; }
}

public class Z3950DetailResult
{
    public Guid              LibraryId   { get; set; }
    public string            LibraryName { get; set; } = "";
    public string            Systax      { get; set; } = "";
    public bool              Connected   { get; set; }
    public int               TotalCount  { get; set; }
    public bool              Truncated   { get; set; }
    public int               PageIndex   { get; set; }
    public int               PageSize    { get; set; }
    public List<Z3950Record> Records     { get; set; } = [];
    public string?           Error       { get; set; }
}
