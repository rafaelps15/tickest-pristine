using System.Diagnostics.CodeAnalysis;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures;

// Tipos de exemplo dos testes do HandlerShapeChecker: nunca são registrados nem executados.

public sealed record FixtureCommand : ICommand;

public sealed record FixtureQuery : IQuery<int>;

public sealed record FixtureDomainEvent : IDomainEvent;

internal sealed class InternalSealedCommandHandler : ICommandHandler<FixtureCommand>
{
    public Task<Result> Handle(FixtureCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class InternalSealedQueryHandler : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(0));
}

internal sealed class InternalSealedDomainEventHandler : IDomainEventHandler<FixtureDomainEvent>
{
    public Task Handle(FixtureDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class FixtureDecorators
{
    internal sealed class CommandHandler<TCommand>(ICommandHandler<TCommand> innerHandler) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            innerHandler.Handle(command, cancellationToken);
    }
}

public sealed class PublicSealedCommandHandler : ICommandHandler<FixtureCommand>
{
    public Task<Result> Handle(FixtureCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "Exemplo de handler não selado, que o verificador deve detectar.")]
internal class InternalUnsealedQueryHandler : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(0));
}

public class PublicUnsealedDomainEventHandler : IDomainEventHandler<FixtureDomainEvent>
{
    public Task Handle(FixtureDomainEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
