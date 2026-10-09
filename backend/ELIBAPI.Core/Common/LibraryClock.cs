namespace ELIBAPI.Core.Common;

/// <summary>
/// Đồng hồ nghiệp vụ theo GIỜ THƯ VIỆN (Asia/Ho_Chi_Minh), không phụ thuộc múi giờ máy chủ.
///
/// Dữ liệu nghiệp vụ (mượn/trả, vào/ra, phiếu phạt, ngày tạo…) được lưu theo giờ thư viện trong các cột
/// "timestamp without time zone". Trước đây mọi nơi gọi <c>DateTime.Now</c> = giờ MÁY CHỦ: đúng khi container đặt
/// TZ=Asia/Ho_Chi_Minh (DHKH/DHKT), nhưng lệch 7 giờ khi container chạy UTC (elib-demo) — "hôm nay", hạn trả,
/// quá hạn… đều sai. <see cref="Now"/>/<see cref="Today"/> thay thế trực tiếp: trên máy chủ giờ VN kết quả giống
/// hệt DateTime.Now; trên máy chủ UTC thì nay đúng giờ thư viện.
///
/// Giá trị trả về có Kind = Unspecified (giống giá trị đọc từ CSDL lên), nên so sánh/ghi xuống CSDL không bị đổi múi.
/// KHÔNG dùng cho những thứ vốn theo UTC: hạn JWT, RoomBooking (lưu UTC), bảng reader_tools (timestamptz) —
/// các chỗ đó tiếp tục dùng <c>DateTime.UtcNow</c>.
/// </summary>
public static class LibraryClock
{
    public static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    /// <summary>Nguồn thời gian — thay được trong test (vd <c>FakeTimeProvider</c>) để cố định "bây giờ".</summary>
    public static TimeProvider Provider { get; set; } = TimeProvider.System;

    /// <summary>Thời điểm hiện tại theo giờ thư viện (Kind = Unspecified).</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTime(Provider.GetUtcNow(), TimeZone).DateTime;

    /// <summary>Ngày hôm nay theo giờ thư viện (00:00, Kind = Unspecified).</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Giờ thư viện (giá trị lưu trong CSDL) → UTC, vd để xuất ISO "…Z" ra ngoài (OAI-PMH).</summary>
    public static DateTime ToUtc(DateTime libraryTime) =>
        libraryTime.Kind == DateTimeKind.Utc
            ? libraryTime
            : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(libraryTime, DateTimeKind.Unspecified), TimeZone);

    /// <summary>UTC → giờ thư viện (Kind = Unspecified).</summary>
    public static DateTime FromUtc(DateTime utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone), DateTimeKind.Unspecified);

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }
}
