namespace Elib.Patron.Application;

/// <summary>Nơi lưu thông đang dùng một loại bạn đọc (dạng trả về của circulation /internal/tenants/{id}/reader-types/{id}/usage).</summary>
public sealed record CirculationReaderTypeUsage(IReadOnlyList<string> Places, int Policies)
{
    public bool InUse => Places.Count > 0 || Policies > 0;

    /// <summary>"điểm lưu thông QUAY — Quầy mượn trả, TV — Thư viện; 2 chính sách lưu thông" (tối đa 3 điểm).</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Places.Count > 0)
            parts.Add("điểm lưu thông " + string.Join(", ", Places.Take(3)) + (Places.Count > 3 ? $" và {Places.Count - 3} điểm khác" : ""));
        if (Policies > 0) parts.Add($"{Policies} chính sách lưu thông");
        return string.Join("; ", parts);
    }
}

/// <summary>
/// Hỏi service khác loại bạn đọc có đang được dùng không — trước khi xoá (ELIB-LRC kiểm trong cùng DB). Lưu thông: gọi /internal bằng service
/// token, trong đơn vị hiện tại.
/// </summary>
public interface IReaderTypeUsage
{
    Task<CirculationReaderTypeUsage> CirculationAsync(long readerTypeId, CancellationToken ct);
}
