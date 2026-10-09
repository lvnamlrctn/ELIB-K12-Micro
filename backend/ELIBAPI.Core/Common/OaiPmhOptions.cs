namespace ELIBAPI.Core.Common;

public class OaiPmhOptions
{
    public string RepositoryName    { get; set; } = "ELIB";
    public string AdminEmail        { get; set; } = "admin@example.org";
    public int    PageSize          { get; set; } = 100;
    public long?  FallbackTenantId  { get; set; }
}
