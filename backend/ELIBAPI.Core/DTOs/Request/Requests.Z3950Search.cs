namespace ELIBAPI.Core.DTOs.Request;

public class Z3950SearchField
{
    // Title | Author | Publisher | Keyword | ISBN | Subject
    public string Field { get; set; } = "Title";
    public string Value { get; set; } = "";
}

public class SearchZ3950Request
{
    public List<Z3950SearchField> Fields     { get; set; } = [];
    public string                 Operator   { get; set; } = "AND"; // AND | OR
    public List<Guid>             LibraryIds { get; set; } = [];    // Z3950Config.PublicId[]
}

public class SearchZ3950DetailRequest : SearchZ3950Request
{
    public int PageIndex { get; set; } = 1;
    public int PageSize  { get; set; } = 10;
}
