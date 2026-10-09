namespace ELIBAPI.Core.Interfaces;

public interface IZaloZnsService
{
    Task<(bool Ok, string? Error)> SendAsync(
        string accessToken, string apiUrl, string phone, string templateId,
        Dictionary<string, string> templateData, CancellationToken ct = default);
}
