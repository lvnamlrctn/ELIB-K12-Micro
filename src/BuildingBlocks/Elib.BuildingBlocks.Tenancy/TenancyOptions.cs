namespace Elib.BuildingBlocks.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>Khoá HMAC dùng chung với gateway. Lấy từ secret (biến môi trường Tenancy__GatewaySigningKey), không để trong appsettings.</summary>
    public string GatewaySigningKey { get; set; } = "";

    public string TenantHeader { get; set; } = "X-Tenant-Id";

    public string SignatureHeader { get; set; } = "X-Gw-Signature";
}
