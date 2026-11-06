using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures.QuerySlices.Dapper;

// Slice de query de exemplo do QuerySliceChecker que lê pela fábrica de conexões: nunca é registrado nem executado.

internal sealed class ReadsWithConnectionQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<int>>(dbConnectionFactory.GetHashCode());
}
