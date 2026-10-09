namespace ELIBAPI.Core.DTOs.Response;

public class PublicZ3950ConfigResponse
{
    public Guid PublicId { get; set; }
    public string? Name { get; set; }
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Systax { get; set; }
    public long? GroupId { get; set; }
    public string? Url { get; set; }
}
