namespace ELIBAPI.Core.Common;

public static class FaceImage
{
    /// <summary>Chấp nhận cả dạng "data:image/jpeg;base64,...." — cắt bỏ prefix, trả base64 thuần (port ELIB-LRC 09-30).</summary>
    public static string StripDataUrl(string? value)
    {
        var s = (value ?? "").Trim();
        var comma = s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? s.IndexOf(',') : -1;
        return comma >= 0 ? s[(comma + 1)..] : s;
    }
}
