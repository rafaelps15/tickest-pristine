namespace TickestPristine.Application.Abstractions.Messaging;

/// <summary>
/// Command que altera o estado e devolve só sucesso ou falha.
/// </summary>
public interface ICommand;

/// <summary>
/// Command que altera o estado e, no sucesso, devolve um valor, como o id criado.
/// </summary>
public interface ICommand<TResponse>;
