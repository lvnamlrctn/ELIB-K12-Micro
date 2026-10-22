using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>
/// Một lượt đọc trực tuyến (LRC: ReadingTracking) — trình đọc gửi nhịp (heartbeat) vài chục giây một lần với trang đang xem và tổng
/// thời gian đang đọc. Server không tin con số của máy khách: thời gian chỉ tăng tối đa bằng thời gian thực đã trôi qua kể từ nhịp trước,
/// bắt đầu lượt tính theo đồng hồ server.
/// </summary>
public sealed class EbookReadSession : TenantEntity
{
    public const int MaxPageCount = 100_000;
    public const int MaxDeltaSeconds = 120;
    public const int FirstBeatSeconds = 60;

    private EbookReadSession() { }

    public long EbookItemId { get; private set; }
    public long? EbookFileId { get; private set; }
    public Guid ReaderPublicId { get; private set; }

    /// <summary>Mã lượt do trình đọc sinh (Guid) — gửi lại cùng mã là cập nhật cùng lượt, không tạo thêm.</summary>
    public Guid SessionKey { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public int LastPage { get; private set; }
    public int MaxPage { get; private set; }
    public int PageCount { get; private set; }
    public int ActiveSeconds { get; private set; }
    public string? Device { get; private set; }

    public static EbookReadSession Start(long ebookItemId, long? ebookFileId, Guid readerPublicId, Guid sessionKey, DateTimeOffset now) =>
        new() { EbookItemId = ebookItemId, EbookFileId = ebookFileId, ReaderPublicId = readerPublicId, SessionKey = sessionKey, StartedAt = now, LastSeenAt = now };

    /// <summary>Ghi một nhịp. <paramref name="activeSeconds"/>: tổng thời gian đang đọc máy khách tự đo (chỉ làm trần, không ghi thẳng).</summary>
    public void Beat(DateTimeOffset now, int page, int pageCount, int activeSeconds, string? device)
    {
        if (pageCount is < 1 or > MaxPageCount) throw new BusinessRuleException("READ_PAGE_COUNT_INVALID", "Số trang của tài liệu không hợp lệ.");
        if (page < 1 || page > pageCount) throw new BusinessRuleException("READ_PAGE_INVALID", "Trang đang đọc nằm ngoài tài liệu.");

        var first = LastSeenAt == StartedAt && ActiveSeconds == 0 && PageCount == 0;
        var elapsed = (int)Math.Clamp((now - LastSeenAt).TotalSeconds, 0, MaxDeltaSeconds);
        var allowed = first ? FirstBeatSeconds : elapsed + 2;
        var delta = Math.Clamp(activeSeconds - ActiveSeconds, 0, Math.Min(allowed, MaxDeltaSeconds));
        ActiveSeconds += delta;
        LastPage = page;
        MaxPage = Math.Max(MaxPage, page);
        PageCount = pageCount;
        LastSeenAt = now;
        Device = TextRules.Optional(device, 40, "DEVICE_INVALID", "Thiết bị");
    }
}

/// <summary>Tiến độ đọc của bạn đọc với một tài liệu (đọc tiếp từ trang cuối) — một dòng cho mỗi (bạn đọc, tài liệu).</summary>
public sealed class EbookReadProgress : TenantEntity
{
    private EbookReadProgress() { }

    public long EbookItemId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public int LastPage { get; private set; }
    public int MaxPage { get; private set; }
    public int PageCount { get; private set; }
    public DateTimeOffset LastReadAt { get; private set; }

    public static EbookReadProgress Create(long ebookItemId, Guid readerPublicId) => new() { EbookItemId = ebookItemId, ReaderPublicId = readerPublicId };

    public void Update(int page, int pageCount, DateTimeOffset now)
    {
        LastPage = page;
        MaxPage = Math.Max(MaxPage, page);
        PageCount = pageCount;
        LastReadAt = now;
    }

    public int Percent => PageCount <= 0 ? 0 : (int)Math.Min(100, Math.Round(100.0 * MaxPage / PageCount));
}

/// <summary>Dấu trang / ghi chú cá nhân của bạn đọc trong trình đọc — một dấu cho mỗi (bạn đọc, tài liệu, trang).</summary>
public sealed class EbookBookmark : TenantEntity
{
    public const int MaxNoteLength = 300;

    private EbookBookmark() { }

    public long EbookItemId { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public int Page { get; private set; }
    public string? Note { get; private set; }

    public static EbookBookmark Create(long ebookItemId, Guid readerPublicId, int page, string? note)
    {
        if (page is < 1 or > EbookReadSession.MaxPageCount) throw new BusinessRuleException("BOOKMARK_PAGE_INVALID", "Trang đánh dấu không hợp lệ.");
        var bookmark = new EbookBookmark { EbookItemId = ebookItemId, ReaderPublicId = readerPublicId, Page = page };
        bookmark.UpdateNote(note);
        return bookmark;
    }

    public void UpdateNote(string? note) => Note = TextRules.Optional(note, MaxNoteLength, "BOOKMARK_NOTE_INVALID", "Ghi chú");
}

/// <summary>Nhật ký truy cập file (xem/tải) — bạn đọc có thể trống với tài liệu công khai đọc không đăng nhập. Không lưu IP thô.</summary>
public sealed class EbookAccessLog : TenantEntity
{
    public const string View = "view";
    public const string Download = "download";

    private EbookAccessLog() { }

    public long EbookItemId { get; private set; }
    public Guid? ReaderPublicId { get; private set; }
    public string Kind { get; private set; } = View;
    public DateTimeOffset At { get; private set; }

    public static EbookAccessLog Create(long ebookItemId, Guid? readerPublicId, bool download, DateTimeOffset at) =>
        new() { EbookItemId = ebookItemId, ReaderPublicId = readerPublicId, Kind = download ? Download : View, At = at };
}
