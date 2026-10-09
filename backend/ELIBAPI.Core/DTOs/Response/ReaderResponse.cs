namespace ELIBAPI.Core.DTOs.Response;

public class ReaderResponse
{
    public long      Id             { get; set; }
    public string?   FirstName      { get; set; }
    public string?   LastName       { get; set; }
    public string?   Cardno         { get; set; }
    public string?   CitizenId      { get; set; }
    public string?   CardUid        { get; set; }
    public string?   Email          { get; set; }
    public string?   Phone          { get; set; }
    public string?   Address        { get; set; }
    public long?     OrgId          { get; set; }
    public string?   OrgName        { get; set; }
    public long?     ReaderTypeId   { get; set; }
    public string?   ReaderTypeName { get; set; }
    public long?     ClassId        { get; set; }
    public long?     CourseId       { get; set; }
    public int?      Sex            { get; set; }
    public DateTime? BirthDate      { get; set; }
    public DateTime? IssueDate      { get; set; }
    public DateTime? ExpireDate     { get; set; }
    public string?   Photo          { get; set; }
    public string?   PortalId       { get; set; }
    public string?   Language       { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId       { get; set; }
    public string?   TenantName     { get; set; }
    public Guid      PublicId       { get; set; }
}

