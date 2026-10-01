using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Authorization;

internal sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    /// <summary>
    /// Policies de permissão criadas sob demanda, em cache seguro para requisições simultâneas.
    /// </summary>
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _permissionPolicies = new();

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicy? policy = await base.GetPolicyAsync(policyName);

        if (policy is not null)
        {
            return policy;
        }

        return _permissionPolicies.GetOrAdd(
            policyName,
            permission => new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build());
    }
}
