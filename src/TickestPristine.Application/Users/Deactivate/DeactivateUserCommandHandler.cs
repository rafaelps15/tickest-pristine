using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (!user.IsActive)
        {
            return Result.Failure(UserErrors.AlreadyDeactivated);
        }

        Result privilegeCheck = await PrivilegeEscalationGuard.EnsureCallerCanManageUserAsync(
            context,
            permissionProvider,
            userContext.UserId,
            user.Id,
            cancellationToken);

        if (privilegeCheck.IsFailure)
        {
            return privilegeCheck;
        }

        Result administratorCheck = await AdministratorGuard.EnsureNotLastAdministratorAsync(
            context,
            user.Id,
            cancellationToken);

        if (administratorCheck.IsFailure)
        {
            return administratorCheck;
        }

        user.IsActive = false;
        user.DeactivatedAtUtc = dateTimeProvider.UtcNow;

        // Conta pendente: o convite deixa de valer. Reativar não devolve o convite; o administrador reenvia.
        await UserInvitations.RevokeAllAsync(context, user.Id, user.DeactivatedAtUtc.Value, cancellationToken);

        user.Raise(new UserDeactivatedDomainEvent(user.Id));

        // Encerra as sessões abertas: sem isso, uma reativação faria os refresh tokens anteriores voltarem a valer.
        await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken);

        await ReturnOpenTicketsToQueueAsync(user.Id, user.DeactivatedAtUtc.Value, cancellationToken);

        // Um único SaveChanges: desativação, chamados e histórico entram juntos ou nenhum entra. Se outra requisição
        // alterou um dos chamados no meio, o token de concorrência derruba tudo com 409.
        await context.SaveChangesAsync(cancellationToken);

        // Sem as permissões e a situação do usuário em cache, o access token que ele ainda tem deixa de valer na próxima
        // requisição (só users/me e logout continuam aceitando o usuário desativado). Depois do commit, a invalidação não
        // pode ser cancelada junto com a requisição.
        await permissionProvider.InvalidateAsync(user.Id, CancellationToken.None);

        return Result.Success();
    }

    /// <summary>
    /// Os chamados em aberto do usuário desativado voltam para a fila, sem responsável e com o status Aberto, para que
    /// outro atendente os assuma (spec 014). A volta de Em Andamento para Aberto só acontece aqui: as transições manuais
    /// não a permitem. Chamados resolvidos, fechados e cancelados ficam como estão, e reativar o usuário não os devolve.
    /// </summary>
    private async Task ReturnOpenTicketsToQueueAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // O filtro global de exclusão lógica deixa os chamados excluídos de fora.
        List<Ticket> openTickets = await context.Tickets
            .Where(t =>
                t.AssignedToUserId == userId &&
                (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress))
            .ToListAsync(cancellationToken);

        foreach (Ticket ticket in openTickets)
        {
            ticket.AssignedToUserId = null;
            ticket.Raise(new TicketUnassignedDomainEvent(ticket.Id, userId, userContext.UserId));
            context.TicketHistories.Add(TicketHistoryEntries.UnassignedOnUserDeactivation(
                ticket.Id,
                userContext.UserId,
                userId,
                nowUtc));

            if (ticket.Status == TicketStatus.InProgress)
            {
                ticket.Status = TicketStatus.Open;
                ticket.Raise(new TicketStatusChangedDomainEvent(
                    ticket.Id,
                    TicketStatus.InProgress,
                    TicketStatus.Open,
                    userContext.UserId));
                context.TicketHistories.Add(TicketHistoryEntries.StatusChanged(
                    ticket.Id,
                    userContext.UserId,
                    TicketStatus.InProgress,
                    TicketStatus.Open,
                    nowUtc));
            }
        }
    }
}
