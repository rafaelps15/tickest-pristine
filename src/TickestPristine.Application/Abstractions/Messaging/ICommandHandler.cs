using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Abstractions.Messaging;

/// <summary>
/// Executa o command. A validação e o log vêm dos decorators (Validation, Logging, handler), e a falha esperada
/// volta como <c>Result</c>, sem exceção.
/// </summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Executa o command que devolve um valor. A validação e o log vêm dos decorators (Validation, Logging, handler), e
/// a falha esperada volta como <c>Result</c>, sem exceção.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
