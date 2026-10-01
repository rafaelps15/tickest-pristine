using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class CreateTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTicketCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        Guid openedByUserId = userContext.UserId;

        if (command.RequesterId is { } requesterId && requesterId != userContext.UserId)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }

            openedByUserId = requesterId;
        }

        bool sectorExists = await context.Sectors.AnyAsync(s => s.Id == command.SectorId, cancellationToken);

        if (!sectorExists)
        {
            return Result.Failure<Guid>(SectorErrors.NotFound(command.SectorId));
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            Description = command.Description,
            Priority = command.Priority,
            Status = TicketStatus.Open,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = command.AssignedToUserId,
            SectorId = command.SectorId,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id));

        context.Tickets.Add(ticket);

        await context.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
