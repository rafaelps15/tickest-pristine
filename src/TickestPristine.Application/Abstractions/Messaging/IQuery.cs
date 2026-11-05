namespace TickestPristine.Application.Abstractions.Messaging;

/// <summary>
/// Consulta que só lê e devolve um valor; não passa pela validação dos decorators.
/// </summary>
public interface IQuery<TResponse>;
