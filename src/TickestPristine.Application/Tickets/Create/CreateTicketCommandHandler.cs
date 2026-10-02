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
        Guid openedByUserId = command.RequesterId ?? userContext.UserId;

        if (openedByUserId != userContext.UserId)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }
        }

        // Solicitante e responsável informados são conferidos numa única consulta; o usuário logado já é válido.
        Guid[] referencedUserIds = new[] { command.RequesterId, command.AssignedToUserId }
            .OfType<Guid>()
            .Where(id => id != userContext.UserId)
            .Distinct()
            .ToArray();

        if (referencedUserIds.Length > 0)
        {
            List<Guid> existingUserIds = await context.Users
                .Where(u => referencedUserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            Guid? missingUserId = referencedUserIds.Except(existingUserIds).Cast<Guid?>().FirstOrDefault();

            if (missingUserId is { } userId)
            {
                return Result.Failure<Guid>(UserErrors.NotFound(userId));
            }
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
