using Elib.BuildingBlocks.Domain;

namespace Elib.Search.Domain;

/// <summary>
/// Một lượt tra cứu trên OPAC (trang 1 của một câu hỏi mới — lật trang, đổi bộ lọc không tính lượt mới) để đo chất lượng tìm kiếm:
/// tỉ lệ không có kết quả, tỉ lệ bạn đọc mở một kết quả, vị trí kết quả được mở. Không lưu IP hay danh tính người tìm.
/// </summary>
public sealed class SearchQuery : Entity, ITenantOwned
{
    public const int MaxTextLength = 200;
    public const int MaxClicks = 50;

    /// <summary>Lượt mở kết quả chỉ được ghi trong khoảng này sau khi tìm (chặn ghi bừa vào lượt tìm cũ).</summary>
    public static readonly TimeSpan ClickWindow = TimeSpan.FromHours(2);

    public long TenantId { get; set; }

    /// <summary>Mã trả cho trình duyệt cùng kết quả — gửi lại khi bạn đọc mở một kết quả.</summary>
    public Guid QueryId { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Câu tìm như bạn đọc gõ (tìm nâng cao: các ô nối lại).</summary>
    public string Text { get; set; } = "";

    /// <summary>Chữ thường không dấu — gộp "Truyện Kiều" và "truyen kieu" khi thống kê.</summary>
    public string TextFold { get; set; } = "";

    public bool Advanced { get; set; }
    public int Total { get; set; }
    public int Clicks { get; set; }

    /// <summary>Vị trí (đếm từ 1 trên toàn danh sách) của kết quả đầu tiên được mở.</summary>
    public int? FirstClickPosition { get; set; }

    /// <summary>Ghi một lượt mở kết quả; false nếu quá hạn hoặc đã đủ số lượt tối đa.</summary>
    public bool RecordClick(int position, DateTimeOffset now)
    {
        if (now - At > ClickWindow || Clicks >= MaxClicks || position < 1) return false;
        Clicks++;
        FirstClickPosition ??= position;
        return true;
    }
}
