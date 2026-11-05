using TickestPristine.ArchitectureTests.Conventions.Fixtures.FixtureHappened;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures;

// Handlers de exemplo fora do padrão de nome ou de pasta, para o autoteste do DomainEventHandlerNameChecker.

public sealed class FixtureHappenedHandler : IDomainEventHandler<FixtureHappenedDomainEvent>
{
    public Task Handle(FixtureHappenedDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class LogOnFixtureHappenedDomainEventHandler : IDomainEventHandler<FixtureHappenedDomainEvent>
{
    public Task Handle(FixtureHappenedDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
