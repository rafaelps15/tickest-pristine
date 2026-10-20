using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Authorization;

/// <summary>
/// Policies registradas vêm do provedor padrão; qualquer outro nome é um código de permissão e vira uma policy com
/// <see cref="PermissionRequirement"/>. A policy é montada a cada pedido, sem gravar nada nas opções compartilhadas,
/// que não são seguras para escrita entre requisições simultâneas.
/// </summary>
internal sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        await base.GetPolicyAsync(policyName)
        ?? new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
}
