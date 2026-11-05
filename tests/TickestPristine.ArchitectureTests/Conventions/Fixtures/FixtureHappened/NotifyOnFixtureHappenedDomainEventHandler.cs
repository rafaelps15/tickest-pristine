using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures.FixtureHappened;

// Evento e handlers de exemplo dos testes do DomainEventHandlerNameChecker: nunca são registrados nem usados fora dos testes.

public sealed record FixtureHappenedDomainEvent : IDomainEvent;

public sealed class NotifyOnFixtureHappenedDomainEventHandler : IDomainEventHandler<FixtureHappenedDomainEvent>
{
    public Task Handle(FixtureHappenedDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
