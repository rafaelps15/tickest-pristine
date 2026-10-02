using TickestPristine.Application.Abstractions.Authentication;
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

        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = ticket.CreatedByUserId,
            Action = TicketHistoryAction.Created,
            Description = "Chamado criado",
            OldValue = null,
            NewValue = ticket.Status.ToString(),
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
