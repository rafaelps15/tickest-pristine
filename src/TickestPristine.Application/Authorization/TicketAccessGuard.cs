using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Authorization;

internal static class TicketAccessGuard
{
    /// <summary>
    /// Confere se quem chama pode ver o chamado e o que pertence a ele (detalhe, mensagens, histórico, anexos): os
    /// participantes (solicitante e responsável) sempre podem; os demais precisam de <c>tickets:manage</c>. Falha com
    /// <see cref="TicketErrors.NotFound"/> quando o chamado não existe. Lê só os dois ids, sem carregar o chamado.
    /// </summary>
    public static async Task<Result> EnsureCanViewAsync(
        IApplicationDbContext context,
        IPermissionProvider permissionProvider,
        Guid callerId,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var participants = await context.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.CreatedByUserId, t.AssignedToUserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (participants is null)
        {
            return Result.Failure(TicketErrors.NotFound(ticketId));
        }

        if (participants.CreatedByUserId == callerId || participants.AssignedToUserId == callerId)
        {
            return Result.Success();
        }

        bool canManageTickets = await permissionProvider.HasPermissionAsync(
            callerId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        return canManageTickets ? Result.Success() : Result.Failure(UserErrors.Unauthorized());
    }
}
