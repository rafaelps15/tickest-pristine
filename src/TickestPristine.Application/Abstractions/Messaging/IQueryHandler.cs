using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Abstractions.Messaging;

/// <summary>
/// Executa a consulta; só o decorator de log a envolve, e a falha esperada volta como <c>Result</c>, sem exceção.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
