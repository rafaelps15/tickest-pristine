using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Delete;

internal sealed class TicketMessageDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessageDeletedDomainEvent>
{
    public async Task Handle(TicketMessageDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // A mensagem já foi excluída; o histórico usa apenas os dados do evento e o usuário atual.
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.MessageRemoved,
            TicketHistoryDescriptions.MessageRemoved,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
