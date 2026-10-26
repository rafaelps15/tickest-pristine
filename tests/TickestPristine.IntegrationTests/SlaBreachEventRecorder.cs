using System.Collections.Concurrent;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Guarda os <see cref="TicketSlaBreachedDomainEvent"/> publicados, para os testes conferirem quantos e quais eventos o
/// monitoramento disparou. Os testes filtram pelos próprios chamados, porque o banco é compartilhado.
/// </summary>
public sealed class SlaBreachEventRecorder
{
    private readonly ConcurrentQueue<TicketSlaBreachedDomainEvent> _events = new();

    public void Record(TicketSlaBreachedDomainEvent domainEvent) => _events.Enqueue(domainEvent);

    public List<TicketSlaBreachedDomainEvent> For(Guid ticketId) => [.. _events.Where(e => e.TicketId == ticketId)];

    public List<TicketSlaBreachedDomainEvent> For(IEnumerable<Guid> ticketIds)
    {
        var ids = ticketIds.ToHashSet();

        return [.. _events.Where(e => ids.Contains(e.TicketId))];
    }
}

/// <summary>
/// Handler registrado só nos testes de integração: a aplicação não tem handler para o evento de violação.
/// </summary>
internal sealed class RecordingSlaBreachedDomainEventHandler(SlaBreachEventRecorder recorder)
    : IDomainEventHandler<TicketSlaBreachedDomainEvent>
{
    public Task Handle(TicketSlaBreachedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(domainEvent);

        return Task.CompletedTask;
    }
}
