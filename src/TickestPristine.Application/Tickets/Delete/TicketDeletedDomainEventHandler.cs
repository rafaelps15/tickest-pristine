using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Delete;

internal sealed class TicketDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketDeletedDomainEvent>
{
    public async Task Handle(TicketDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // O chamado já foi excluído; o histórico usa apenas os dados do evento e o usuário atual.
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.Deleted,
            TicketHistoryDescriptions.Deleted,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
