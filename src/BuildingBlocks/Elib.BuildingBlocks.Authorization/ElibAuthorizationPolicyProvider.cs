using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>Sinh policy động từ tên do <see cref="PermissionAttribute"/>/<see cref="RequiresModuleAttribute"/> đặt, không cần đăng ký trước.</summary>
public sealed class ElibAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        IAuthorizationRequirement? requirement =
            policyName.StartsWith(ElibPolicyNames.PermissionPrefix, StringComparison.Ordinal)
                ? new PermissionRequirement([policyName[ElibPolicyNames.PermissionPrefix.Length..]])
            : policyName.StartsWith(ElibPolicyNames.PermissionAnyPrefix, StringComparison.Ordinal)
                ? new PermissionRequirement(policyName[ElibPolicyNames.PermissionAnyPrefix.Length..].Split('|'))
            : policyName.StartsWith(ElibPolicyNames.ModulePrefix, StringComparison.Ordinal)
                ? new ModuleRequirement(policyName[ElibPolicyNames.ModulePrefix.Length..])
            : policyName == ElibPolicyNames.SystemContext
                ? new SystemContextRequirement()
            : policyName == ElibPolicyNames.ServiceCaller
                ? new ServiceCallerRequirement()
            : null;

        if (requirement is null) return await base.GetPolicyAsync(policyName);

        var builder = new AuthorizationPolicyBuilder();
        if (requirement is PermissionRequirement or SystemContextRequirement or ServiceCallerRequirement) builder.RequireAuthenticatedUser();
        return builder.AddRequirements(requirement).Build();
    }
}
