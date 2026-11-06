using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Quem pode ver o chamado e o que pertence a ele (detalhe, SLA, mensagens, histórico e anexos), a conferência comum
/// às consultas do chamado: os participantes (solicitante e responsável) sempre podem; os demais precisam de
/// <c>tickets:manage</c>. Lê só os dois ids, sem carregar o chamado, pela conexão que o handler já abriu.
/// </summary>
internal sealed class TicketViewAccess(IPermissionProvider permissionProvider)
{
    /// <summary>
    /// Falha com <see cref="TicketErrors.NotFound"/> quando o chamado não existe ou foi excluído, antes de olhar a
    /// permissão, e com <see cref="UserErrors.Unauthorized"/> quando quem chama não participa nem gerencia chamados.
    /// </summary>
    public async Task<Result> EnsureCanViewAsync(
        DbConnection connection,
        Guid callerId,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        const string sql =
            $"""
             SELECT
                 t.created_by_user_id AS {nameof(TicketParticipants.CreatedByUserId)},
                 t.assigned_to_user_id AS {nameof(TicketParticipants.AssignedToUserId)}
             FROM public.tickets t
             WHERE t.id = @TicketId AND t.deleted_at_utc IS NULL
             """;

        TicketParticipants? participants = await connection.QuerySingleOrDefaultAsync<TicketParticipants>(
            new CommandDefinition(sql, new { TicketId = ticketId }, cancellationToken: cancellationToken));

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

    internal sealed class TicketParticipants
    {
        public Guid CreatedByUserId { get; init; }
        public Guid? AssignedToUserId { get; init; }
    }
}
