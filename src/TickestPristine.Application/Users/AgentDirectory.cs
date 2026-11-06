using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Permissions;

namespace TickestPristine.Application.Users;

/// <summary>
/// Responde se um usuário atende chamados: conta ativa com a permissão <c>tickets:manage</c> em alguma das suas roles.
/// Lê direto das tabelas, sem o cache de permissões, para a resposta valer no mesmo instante em que a role muda.
/// </summary>
internal sealed class AgentDirectory(IApplicationDbContext context) : IAgentDirectory
{
    public Task<bool> IsAgentAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(
            user => user.Id == userId &&
                user.IsActive &&
                context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id &&
                    context.RolePermissions.Any(rolePermission =>
                        rolePermission.RoleName == userRole.RoleName &&
                        rolePermission.PermissionCode == PermissionCodes.Tickets.Manage)),
            cancellationToken);
}
