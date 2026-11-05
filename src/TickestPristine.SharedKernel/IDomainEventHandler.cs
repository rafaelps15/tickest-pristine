namespace TickestPristine.SharedKernel;

/// <summary>
/// Trata um evento de domínio depois do commit da mudança que o disparou; a falha é registrada, sem desfazer a
/// mudança nem derrubar a requisição.
/// </summary>
public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task Handle(T domainEvent, CancellationToken cancellationToken);
}
