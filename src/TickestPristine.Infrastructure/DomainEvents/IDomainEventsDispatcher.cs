using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.DomainEvents;

/// <summary>
/// Entrega os eventos de domínio aos handlers depois do commit; a falha de um handler é registrada e não interrompe
/// os demais.
/// </summary>
public interface IDomainEventsDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
