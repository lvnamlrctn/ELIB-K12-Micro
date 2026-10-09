namespace ELIBAPI.Core.DTOs.Response;

public class EbookMetaDataExportRow
{
    public long    ItemId      { get; set; }
    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public string? Keyword     { get; set; }
    public List<MetaDataValueItem> Metadata { get; set; } = [];
}

public class MetaDataValueItem
{
    public int     FieldId   { get; set; }
    public string? Value     { get; set; }
    public int?    SortOrder { get; set; }
}

public class DSpaceItemExportRow
{
    public long   ItemId   { get; set; }
    public Guid   PublicId { get; set; }
    public List<DSpaceMetaEntry> MetaEntries { get; set; } = [];
    public List<DSpaceFileEntry> Files       { get; set; } = [];
}

public class DSpaceMetaEntry
{
    public string  Element   { get; set; } = "";
    public string  Qualifier { get; set; } = "none";
    public string? Value     { get; set; }
}

public class DSpaceFileEntry
{
    public long    Id      { get; set; }
    public string? Url     { get; set; }
    public string? FileExt { get; set; }
    public string  Bundle  { get; set; } = "ORIGINAL";
}
