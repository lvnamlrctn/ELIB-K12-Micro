using System.Globalization;
using System.Text;
using Elib.BuildingBlocks.Domain;

namespace Elib.Serials.Domain;

/// <summary>Ngày lịch Việt Nam (UTC+7) — ngày nhận, hạn khiếu nại mặc định theo giờ VN.</summary>
public static class VietnamDate
{
    public static DateOnly Of(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));

    /// <summary>0 giờ ngày <paramref name="date"/> giờ VN, biểu diễn ở UTC (Npgsql chỉ nhận timestamptz offset 0).</summary>
    public static DateTimeOffset Start(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}

internal static class TextRules
{
    public static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }

    public static string Required(string? value, int max, string code, string label)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} bắt buộc, tối đa {max} ký tự.");
    }
}

/// <summary>Loại ấn phẩm (monolith: MagazineType, quyền MAGAZINE_TYPES) — Báo, Tạp chí…</summary>
public sealed class PublicationType : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Đơn vị chu kỳ của <see cref="Frequency"/>.</summary>
public static class FrequencyUnit
{
    public const int Day = 1;
    public const int Week = 2;
    public const int Month = 3;
    public const int Quarter = 4;
    public const int Year = 5;

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [Day] = "Ngày", [Week] = "Tuần", [Month] = "Tháng", [Quarter] = "Quý", [Year] = "Năm",
    };
}

public sealed record FrequencyData(string Name, int Unit, int? IssuesPerUnit = null, int? UnitsPerIssue = null, int? ReleaseDay = null, int SortOrder = 0);

/// <summary>
/// Tần suất xuất bản (monolith: FrequencyMagazine, quyền FREQUENCIES). Khoảng cách giữa hai kỳ = <see cref="UnitsPerIssue"/> đơn vị nếu &gt; 0, ngược lại
/// 1 / <see cref="IssuesPerUnit"/> đơn vị — "Nửa tháng" = tháng, 2 số mỗi tháng.
/// </summary>
public sealed class Frequency : TenantEntity
{
    private const double DaysPerMonth = 365.25 / 12;

    private Frequency() { }

    public string Name { get; private set; } = "";
    public int Unit { get; private set; } = FrequencyUnit.Month;
    public int? IssuesPerUnit { get; private set; }
    public int? UnitsPerIssue { get; private set; }

    /// <summary>Ngày phát hành trong tuần/tháng (thông tin, không dùng để tính).</summary>
    public int? ReleaseDay { get; private set; }

    public int SortOrder { get; private set; }

    public static Frequency Create(FrequencyData data)
    {
        var f = new Frequency();
        f.Update(data);
        return f;
    }

    public void Update(FrequencyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Name = TextRules.Required(data.Name, 250, "NAME_INVALID", "Tên tần suất");
        if (!FrequencyUnit.Names.ContainsKey(data.Unit))
            throw new BusinessRuleException("FREQUENCY_UNIT_INVALID", "Đơn vị chu kỳ: 1 ngày, 2 tuần, 3 tháng, 4 quý, 5 năm.");
        Unit = data.Unit;
        IssuesPerUnit = Positive(data.IssuesPerUnit, "Số kỳ mỗi đơn vị");
        UnitsPerIssue = Positive(data.UnitsPerIssue, "Số đơn vị mỗi kỳ");
        if (IssuesPerUnit is null && UnitsPerIssue is null)
            throw new BusinessRuleException("FREQUENCY_RATE_REQUIRED", "Nhập số kỳ mỗi đơn vị hoặc số đơn vị mỗi kỳ.");
        ReleaseDay = data.ReleaseDay is null or (>= 1 and <= 31) ? data.ReleaseDay : throw new BusinessRuleException("RELEASE_DAY_INVALID", "Ngày phát hành 1–31.");
        SortOrder = data.SortOrder;
    }

    private static int? Positive(int? value, string label) =>
        value is null or 0 ? null : value > 0 ? value : throw new BusinessRuleException("FREQUENCY_RATE_INVALID", $"{label} phải lớn hơn 0.");

    /// <summary>Khoảng cách giữa hai kỳ, tính bằng đơn vị chu kỳ.</summary>
    public double Distance => UnitsPerIssue is > 0 ? UnitsPerIssue.Value : 1d / (IssuesPerUnit ?? 1);

    /// <summary>
    /// Ngày dự kiến của kỳ thứ <paramref name="step"/> kể từ <paramref name="anchor"/> (step 0 = đúng mốc). Tính thẳng từ mốc nhân số bước (không cộng dồn
    /// nên không trôi ngày cuối tháng); khoảng cách lẻ (vd. nửa tháng) quy ra ngày với 365,25/12 ngày mỗi tháng.
    /// </summary>
    public DateOnly PlannedDate(DateOnly anchor, int step)
    {
        var total = Distance * step;
        var whole = Math.Abs(total - Math.Round(total)) < 1e-9;
        var n = (int)Math.Round(total);
        return Unit switch
        {
            FrequencyUnit.Day => anchor.AddDays(n),
            FrequencyUnit.Week => anchor.AddDays(whole ? 7 * n : (int)Math.Round(7 * total)),
            FrequencyUnit.Month => whole ? anchor.AddMonths(n) : anchor.AddDays((int)Math.Round(DaysPerMonth * total)),
            FrequencyUnit.Quarter => whole ? anchor.AddMonths(3 * n) : anchor.AddDays((int)Math.Round(3 * DaysPerMonth * total)),
            _ => whole ? anchor.AddYears(n) : anchor.AddDays((int)Math.Round(12 * DaysPerMonth * total)),
        };
    }
}

public sealed record PatternData(
    string Name, string? Description = null, string? LabelTemplate = null, string? NameX = null, string? NameY = null, string? NameZ = null,
    int StepX = 1, int StepY = 1, int StepZ = 1, int? MaxX = null, int? MaxY = null, int? MaxZ = null, int ResetX = 1, int ResetY = 1, int ResetZ = 1, int SortOrder = 0);

/// <summary>Số kỳ (X/Y/Z): ví dụ X = số trong năm, Y = năm/tập, Z = bộ.</summary>
public readonly record struct IssueNumber(int X, int? Y, int? Z);

/// <summary>
/// Mẫu đánh số (monolith: PatternMagazine + PartemMagazineDetail, quyền PATTERNS). <see cref="LabelTemplate"/> dạng "Số {X}/{Y}". X tăng theo
/// <see cref="StepX"/>; vượt <see cref="MaxX"/> thì X về <see cref="ResetX"/> và Y tăng <see cref="StepY"/>; Y vượt <see cref="MaxY"/> thì Y về
/// <see cref="ResetY"/> và Z tăng <see cref="StepZ"/>.
/// </summary>
public sealed class NumberingPattern : TenantEntity
{
    public const string DefaultTemplate = "Số {X}";

    private NumberingPattern() { }

    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public string LabelTemplate { get; private set; } = DefaultTemplate;
    public string? NameX { get; private set; }
    public string? NameY { get; private set; }
    public string? NameZ { get; private set; }
    public int StepX { get; private set; } = 1;
    public int StepY { get; private set; } = 1;
    public int StepZ { get; private set; } = 1;
    public int? MaxX { get; private set; }
    public int? MaxY { get; private set; }
    public int? MaxZ { get; private set; }
    public int ResetX { get; private set; } = 1;
    public int ResetY { get; private set; } = 1;
    public int ResetZ { get; private set; } = 1;
    public int SortOrder { get; private set; }

    public static NumberingPattern Create(PatternData data)
    {
        var p = new NumberingPattern();
        p.Update(data);
        return p;
    }

    public void Update(PatternData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Name = TextRules.Required(data.Name, 250, "NAME_INVALID", "Tên mẫu đánh số");
        Description = TextRules.Optional(data.Description, 500, "DESCRIPTION_INVALID", "Mô tả");
        LabelTemplate = TextRules.Optional(data.LabelTemplate, 250, "LABEL_TEMPLATE_INVALID", "Mẫu nhãn") ?? DefaultTemplate;
        NameX = TextRules.Optional(data.NameX, 50, "NAME_X_INVALID", "Tên cấp X");
        NameY = TextRules.Optional(data.NameY, 50, "NAME_Y_INVALID", "Tên cấp Y");
        NameZ = TextRules.Optional(data.NameZ, 50, "NAME_Z_INVALID", "Tên cấp Z");
        StepX = Step(data.StepX);
        StepY = Step(data.StepY);
        StepZ = Step(data.StepZ);
        MaxX = Max(data.MaxX);
        MaxY = Max(data.MaxY);
        MaxZ = Max(data.MaxZ);
        ResetX = data.ResetX;
        ResetY = data.ResetY;
        ResetZ = data.ResetZ;
        SortOrder = data.SortOrder;
    }

    private static int Step(int value) => value >= 1 ? value : throw new BusinessRuleException("STEP_INVALID", "Bước tăng phải từ 1 trở lên.");

    private static int? Max(int? value) => value is null or >= 1 ? value : throw new BusinessRuleException("MAX_INVALID", "Giá trị tối đa phải từ 1 trở lên.");

    public bool UsesY => LabelTemplate.Contains("{Y}", StringComparison.Ordinal);

    public bool UsesZ => LabelTemplate.Contains("{Z}", StringComparison.Ordinal);

    /// <summary>Số kỳ đầu của đăng ký chưa nhập số bắt đầu: X = 1, Y/Z = giá trị đặt lại nếu mẫu nhãn dùng đến.</summary>
    public IssueNumber Initial(int? x, int? y, int? z) => new(x ?? ResetX, y ?? (UsesY ? ResetY : null), z ?? (UsesZ ? ResetZ : null));

    /// <summary>Số kỳ kế tiếp.</summary>
    public IssueNumber Next(IssueNumber current)
    {
        var x = current.X + StepX;
        var y = current.Y;
        var z = current.Z;
        if (MaxX is { } maxX && x > maxX)
        {
            x = ResetX;
            y = (y ?? ResetY) + StepY;
            if (MaxY is { } maxY && y > maxY)
            {
                y = ResetY;
                z = (z ?? ResetZ) + StepZ;
                if (MaxZ is { } maxZ && z > maxZ) z = ResetZ;
            }
        }
        return new IssueNumber(x, y, z);
    }

    /// <summary>Nhãn hiển thị của kỳ: thay {X}, {Y}, {Z} trong mẫu.</summary>
    public string Label(IssueNumber n) => LabelTemplate
        .Replace("{X}", n.X.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{Y}", n.Y?.ToString(CultureInfo.InvariantCulture) ?? "", StringComparison.Ordinal)
        .Replace("{Z}", n.Z?.ToString(CultureInfo.InvariantCulture) ?? "", StringComparison.Ordinal);
}

/// <summary>Bản sao biểu ghi thư mục (BibSnapshot): ghi từ event BibChanged của catalog, thiếu thì hỏi catalog.</summary>
public sealed class BibSnapshot : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? Publisher { get; set; }

    /// <summary>ISSN — event BibChanged không mang ISSN nên để trống; ISSN của đăng ký nhập tay ở <see cref="Subscription"/>.</summary>
    public string? Issn { get; set; }

    public string TitleKey { get; set; } = "";
    public bool Deleted { get; set; }
    public long Version { get; set; }

    /// <summary>Chữ thường không dấu (đ → d), gộp khoảng trắng — "bao tuoi tre" khớp "Báo Tuổi Trẻ".</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
