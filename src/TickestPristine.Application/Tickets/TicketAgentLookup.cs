using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Quem atende chamados, lido do banco, sem o cache de permissões, para a atribuição e a situação de espera do chamado
/// nunca discordarem da lista de atendentes: usuário ativo com a permissão efetiva <c>tickets:manage</c>, vinda de
/// qualquer uma das suas roles.
/// </summary>
internal sealed class TicketAgentLookup(IApplicationDbContext context)
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

    /// <summary>
    /// Indica se a última mensagem válida do chamado (a mais recente não excluída) é de quem atende e não do
    /// solicitante; sem mensagens, devolve <c>false</c>.
    /// </summary>
    public async Task<bool> IsLastMessageFromAgentAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        Guid? authorUserId = await context.TicketMessages
            .Where(m => m.TicketId == ticket.Id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Select(m => (Guid?)m.AuthorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return authorUserId is { } authorId &&
            authorId != ticket.CreatedByUserId &&
            await IsAgentAsync(authorId, cancellationToken);
    }
}
