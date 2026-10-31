using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Quem pode ser responsável por um chamado, escrito como filtro que o EF traduz para SQL: usuário ativo com a
/// permissão efetiva <c>tickets:manage</c>, vinda de qualquer uma das suas roles. As consultas de atendentes usam este
/// filtro, e a atribuição, a mesma regra do <see cref="TicketAgentLookup"/>, lida sempre do banco (sem o cache de
/// permissões), para as duas nunca discordarem.
/// </summary>
internal static class TicketAssigneeEligibility
{
    internal static IQueryable<User> WhereEligibleAssignee(this IQueryable<User> users, IApplicationDbContext context) =>
        users.Where(user => user.IsActive &&
            context.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                context.RolePermissions.Any(rolePermission =>
                    rolePermission.RoleName == userRole.RoleName &&
                    rolePermission.PermissionCode == PermissionCodes.Tickets.Manage)));
}
