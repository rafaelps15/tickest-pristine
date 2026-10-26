using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Assign;

/// <summary>
/// Define o responsável por um chamado ativo. Quem atende a fila (o endpoint exige <c>tickets:manage</c>) pode assumir
/// o chamado; passá-lo para outra pessoa exige também <c>tickets:assign</c>. O responsável precisa ser um atendente
/// elegível (<see cref="TicketAssigneeEligibility"/>). Atribuir ao responsável atual não muda nada.
/// </summary>
internal sealed class AssignTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssignTicketCommand>
{
    public async Task<Result> Handle(AssignTicketCommand command, CancellationToken cancellationToken)
    {
        if (command.AssignedToUserId != userContext.UserId)
        {
            bool canAssignToOthers = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Assign,
                cancellationToken);

            if (!canAssignToOthers)
            {
                return Result.Failure(TicketErrors.CannotAssignToOthers);
            }
        }

        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        if (!TicketStatusTransitions.IsActive(ticket.Status))
        {
            return Result.Failure(TicketErrors.NotActive(ticket.Id));
        }

        bool assigneeExists = await context.Users.AnyAsync(
            u => u.Id == command.AssignedToUserId && u.IsActive,
            cancellationToken);

        if (!assigneeExists)
        {
            return Result.Failure(UserErrors.NotFound(command.AssignedToUserId));
        }

        // Lida do banco, e não do cache de permissões: a lista de atendentes usa a mesma regra e não pode discordar.
        bool assigneeIsAttendant = await context.Users
            .Where(u => u.Id == command.AssignedToUserId)
            .WhereEligibleAssignee(context)
            .AnyAsync(cancellationToken);

        if (!assigneeIsAttendant)
        {
            return Result.Failure(TicketErrors.AssigneeNotAttendant(command.AssignedToUserId));
        }

        if (ticket.AssignedToUserId == command.AssignedToUserId)
        {
            return Result.Success();
        }

        Guid? previousAssignedToUserId = ticket.AssignedToUserId;

        ticket.AssignedToUserId = command.AssignedToUserId;

        ticket.Raise(new TicketReassignedDomainEvent(
            ticket.Id,
            previousAssignedToUserId,
            command.AssignedToUserId,
            userContext.UserId));

        context.TicketHistories.Add(previousAssignedToUserId is null
            ? TicketHistoryEntries.Assigned(
                ticket.Id,
                userContext.UserId,
                command.AssignedToUserId,
                dateTimeProvider.UtcNow)
            : TicketHistoryEntries.Reassigned(
                ticket.Id,
                userContext.UserId,
                previousAssignedToUserId,
                command.AssignedToUserId,
                dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
