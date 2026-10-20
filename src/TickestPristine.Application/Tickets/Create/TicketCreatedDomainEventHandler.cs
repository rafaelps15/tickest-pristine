using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class TicketCreatedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketCreatedDomainEvent>
{
    public async Task Handle(TicketCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets
            .SingleOrDefaultAsync(t => t.Id == domainEvent.TicketId, cancellationToken);

        if (ticket is null)
        {
            return;
        }

        context.TicketHistories.Add(TicketHistoryEntries.Created(ticket.Id, domainEvent.OpenedByUserId, dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
