using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Hosting;
using Elib.Media.Application;

namespace Elib.Media.Infrastructure;

/// <summary>Tải file từ URL ngoài qua HttpClient có chặn mạng nội bộ (<see cref="PublicNetwork.Handler"/>), dừng ngay khi vượt dung lượng.</summary>
public sealed class RemoteFileFetcher(HttpClient http) : IRemoteFileFetcher
{
    public async Task<byte[]> FetchAsync(Uri url, long maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(url);
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new BusinessRuleException("MEDIA_FETCH_FAILED", $"Không tải được ảnh (máy chủ trả lỗi {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength > maxBytes) throw TooLarge(maxBytes);
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes) throw TooLarge(maxBytes);
            }
            if (buffer.Length == 0) throw new BusinessRuleException("MEDIA_FETCH_FAILED", "Ảnh tải về rỗng.");
            return buffer.ToArray();
        }
        catch (BlockedAddressException ex)
        {
            throw new BusinessRuleException("MEDIA_URL_BLOCKED", ex.Message);
        }
        catch (HttpRequestException ex) when (ex.InnerException is BlockedAddressException blocked)
        {
            throw new BusinessRuleException("MEDIA_URL_BLOCKED", blocked.Message);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("MEDIA_FETCH_FAILED", "Không kết nối được máy chủ chứa ảnh.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BusinessRuleException("MEDIA_FETCH_FAILED", "Máy chủ chứa ảnh không phản hồi (quá thời gian chờ).");
        }
    }

    private static BusinessRuleException TooLarge(long maxBytes) =>
        new("MEDIA_TOO_LARGE", $"Ảnh lớn hơn {maxBytes / 1024 / 1024} MB.");
}
