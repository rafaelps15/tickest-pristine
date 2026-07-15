using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class CreateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionService permissionService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTicketCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        Guid openedByUserId = userContext.UserId;

        if (command.RequesterId is { } requesterId && requesterId != userContext.UserId)
        {
            bool canManageTickets = await permissionService.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }

            openedByUserId = requesterId;
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            Description = command.Description,
            Priority = command.Priority,
            Status = TicketStatus.Open,
            OpenedByUserId = openedByUserId,
            AssignedToUserId = command.ResponsibleId,
            DepartmentId = command.DepartmentId,
            SectorId = command.SectorId,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id));

        context.Tickets.Add(ticket);

        await context.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
