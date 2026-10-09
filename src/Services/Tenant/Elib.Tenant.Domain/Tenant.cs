using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Tenant.Domain;

public enum TenantState
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    ProvisioningFailed = 3,
}

public enum StepStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}

/// <summary>
/// Đơn vị (thư viện của một trường/phòng/sở). Dữ liệu cấp hệ thống — KHÔNG thuộc đơn vị nào (không ITenantOwned).
/// <see cref="Version"/> tăng ở mỗi thay đổi, dùng làm SourceVersion cho bản sao ở các service khác.
/// </summary>
public sealed partial class Tenant : AuditableEntity
{
    private static readonly HashSet<string> ReservedSubdomains =
        ["www", "api", "admin", "app", "static", "cdn", "mail", "ops", "identity", "auth", "status"];

    private readonly List<ProvisioningStep> _steps = [];

    private Tenant() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Subdomain { get; private set; } = "";
    public string TimeZone { get; private set; } = "Asia/Ho_Chi_Minh";
    public long? ParentOrgId { get; private set; }
    public TenantState Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset? ProvisioningStartedAt { get; private set; }
    public string? ProvisioningError { get; private set; }

    /// <summary>Đường dẫn logo trên kho file công khai (/s3/media-public/{Id}/tenant-logo/…), do service media cấp. null = logo mặc định.</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>Tên ngắn hiện cạnh logo/khi cài OPAC như ứng dụng (monolith: LogoText). null = dùng tên đơn vị.</summary>
    public string? LogoText { get; private set; }

    public IReadOnlyList<ProvisioningStep> ProvisioningSteps => _steps;

    public static Tenant Create(string code, string name, string subdomain, string? timeZone, long? parentOrgId)
    {
        var tenant = new Tenant { Code = NormalizeCode(code), Status = TenantState.Provisioning };
        tenant.SetDetails(name, subdomain, timeZone, parentOrgId);
        return tenant;
    }

    public void Update(string name, string subdomain, string? timeZone, long? parentOrgId)
    {
        SetDetails(name, subdomain, timeZone, parentOrgId);
        Version++;
    }

    /// <summary>
    /// Bắt đầu (hoặc chạy lại) khởi tạo: mỗi service trong danh sách phải báo TenantSeeded. Danh sách rỗng → hoạt động ngay.
    /// Chạy lại tái sử dụng bước cũ (đặt về Pending) để không tạo trùng khoá (TenantId, Service).
    /// </summary>
    public void StartProvisioning(IEnumerable<string> services, DateTimeOffset now)
    {
        if (Status is not (TenantState.Provisioning or TenantState.ProvisioningFailed))
            throw new ConflictException("TENANT_ALREADY_PROVISIONED", "Đơn vị đã khởi tạo xong; không chạy lại khởi tạo.");

        var wanted = services.Select(s => s.Trim().ToLowerInvariant()).Distinct().Order().ToList();
        _steps.RemoveAll(s => !wanted.Contains(s.Service));
        foreach (var service in wanted)
        {
            var existing = _steps.FirstOrDefault(s => s.Service == service);
            if (existing is null) _steps.Add(new ProvisioningStep(Id, service, now));
            else existing.Reset(now);
        }

        ProvisioningStartedAt = now;
        ProvisioningError = null;
        Status = _steps.Count == 0 ? TenantState.Active : TenantState.Provisioning;
        Version++;
    }

    /// <summary>Ghi nhận phản hồi của một service. Trả về true nếu trạng thái đơn vị thay đổi (để phát TenantUpdated).</summary>
    public bool RecordSeeded(string service, bool succeeded, string? error, DateTimeOffset now)
    {
        if (Status != TenantState.Provisioning) return false; // phản hồi muộn sau khi đã kết thúc/timeout
        var step = _steps.FirstOrDefault(s => string.Equals(s.Service, service.Trim(), StringComparison.OrdinalIgnoreCase));
        if (step is null) return false; // service không nằm trong danh sách chờ

        step.Complete(succeeded, error, now);
        if (_steps.Any(s => s.Status == StepStatus.Failed))
        {
            Status = TenantState.ProvisioningFailed;
            ProvisioningError = string.Join("; ", _steps.Where(s => s.Status == StepStatus.Failed).Select(s => $"{s.Service}: {s.Error}"));
        }
        else if (_steps.All(s => s.Status == StepStatus.Succeeded))
        {
            Status = TenantState.Active;
        }
        else
        {
            return false;
        }

        Version++;
        return true;
    }

    public bool TimeOutProvisioning(DateTimeOffset now, TimeSpan timeout)
    {
        if (Status != TenantState.Provisioning || ProvisioningStartedAt is null || now - ProvisioningStartedAt < timeout) return false;
        Status = TenantState.ProvisioningFailed;
        ProvisioningError = "Quá thời gian chờ: " + string.Join(", ", _steps.Where(s => s.Status == StepStatus.Pending).Select(s => s.Service));
        Version++;
        return true;
    }

    public void Suspend()
    {
        if (Status != TenantState.Active) throw new ConflictException("TENANT_NOT_ACTIVE", "Chỉ khoá được đơn vị đang hoạt động.");
        Status = TenantState.Suspended;
        Version++;
    }

    public void Activate()
    {
        if (Status != TenantState.Suspended) throw new ConflictException("TENANT_NOT_SUSPENDED", "Chỉ mở lại được đơn vị đang bị khoá.");
        Status = TenantState.Active;
        Version++;
    }

    public void BumpVersion() => Version++;

    /// <summary>
    /// Đổi logo/tên ngắn. Logo chỉ nhận file media đã kiểm tra của chính đơn vị này: đường dẫn phải nằm dưới
    /// <c>{publicPrefix}{Id}/tenant-logo/</c> — không nhận URL ngoài (tránh nhúng ảnh theo dõi/nội dung lạ vào trang của trường).
    /// </summary>
    public void SetBranding(string? logoText, string? logoUrl, string publicPrefix)
    {
        var text = string.IsNullOrWhiteSpace(logoText) ? null : logoText.Trim();
        if (text is { Length: > 50 })
            throw new BusinessRuleException("TENANT_LOGO_TEXT_INVALID", "Tên ngắn tối đa 50 ký tự.");

        var url = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        if (url is not null)
        {
            var prefix = $"{publicPrefix.TrimEnd('/')}/{Id}/tenant-logo/";
            if (!url.StartsWith(prefix, StringComparison.Ordinal) || url.Length > 500 || !LogoPathPattern().IsMatch(url[prefix.Length..]))
                throw new BusinessRuleException("TENANT_LOGO_INVALID", "Logo phải là ảnh đã upload cho chính đơn vị này.");
        }

        LogoText = text;
        LogoUrl = url;
        Version++;
    }

    private void SetDetails(string name, string subdomain, string? timeZone, long? parentOrgId)
    {
        Name = string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            ? throw new BusinessRuleException("TENANT_NAME_INVALID", "Tên đơn vị bắt buộc, tối đa 200 ký tự.")
            : name.Trim();
        Subdomain = NormalizeSubdomain(subdomain);
        TimeZone = ValidateTimeZone(timeZone);
        ParentOrgId = parentOrgId;
    }

    public static string NormalizeCode(string code)
    {
        var value = (code ?? "").Trim().ToUpperInvariant();
        return CodePattern().IsMatch(value)
            ? value
            : throw new BusinessRuleException("TENANT_CODE_INVALID", "Mã đơn vị gồm 2–30 ký tự A-Z, 0-9, '_' hoặc '-'.");
    }

    public static string NormalizeSubdomain(string subdomain)
    {
        var value = (subdomain ?? "").Trim().ToLowerInvariant();
        if (!SubdomainPattern().IsMatch(value))
            throw new BusinessRuleException("TENANT_SUBDOMAIN_INVALID", "Tên miền con gồm 2–63 ký tự a-z, 0-9, '-' và không bắt đầu/kết thúc bằng '-'.");
        if (ReservedSubdomains.Contains(value))
            throw new BusinessRuleException("TENANT_SUBDOMAIN_RESERVED", $"Tên miền con '{value}' được hệ thống giữ lại.");
        return value;
    }

    private static string ValidateTimeZone(string? timeZone)
    {
        var value = string.IsNullOrWhiteSpace(timeZone) ? "Asia/Ho_Chi_Minh" : timeZone.Trim();
        return TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
            ? value
            : throw new BusinessRuleException("TENANT_TIMEZONE_INVALID", $"Múi giờ '{value}' không hợp lệ.");
    }

    [GeneratedRegex("^[A-Z0-9_-]{2,30}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,61}[a-z0-9]$")]
    private static partial Regex SubdomainPattern();

    /// <summary>Phần sau tiền tố của key media: yyyy/MM/{guid N}.{png|jpg|webp}.</summary>
    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.(png|jpg|webp)$")]
    private static partial Regex LogoPathPattern();
}

/// <summary>Tiến độ khởi tạo của một service cho đơn vị.</summary>
public sealed class ProvisioningStep
{
    private ProvisioningStep() { }

    internal ProvisioningStep(long tenantId, string service, DateTimeOffset now)
    {
        TenantId = tenantId;
        Service = service;
        Status = StepStatus.Pending;
        UpdatedAt = now;
    }

    public long TenantId { get; private set; }
    public string Service { get; private set; } = "";
    public StepStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Reset(DateTimeOffset now)
    {
        Status = StepStatus.Pending;
        Error = null;
        UpdatedAt = now;
    }

    internal void Complete(bool succeeded, string? error, DateTimeOffset now)
    {
        Status = succeeded ? StepStatus.Succeeded : StepStatus.Failed;
        Error = succeeded ? null : (string.IsNullOrWhiteSpace(error) ? "Không rõ lỗi" : error.Trim());
        UpdatedAt = now;
    }
}
