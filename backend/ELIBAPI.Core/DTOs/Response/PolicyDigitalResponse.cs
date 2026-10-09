namespace ELIBAPI.Core.DTOs.Response;

public class PolicyDigitalResponse
{
    public int       Id             { get; set; }
    public int?      ReaderTypeid   { get; set; }
    public string?   ReaderTypeName { get; set; }
    public int?      Maxpage        { get; set; }
    public double?   Maxsize        { get; set; }
    public int?      Maxdocument    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public string?   TenantName     { get; set; }
    public Guid      PublicId       { get; set; }
}

