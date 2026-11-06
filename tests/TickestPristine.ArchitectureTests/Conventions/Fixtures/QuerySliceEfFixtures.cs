using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures.QuerySlices.Ef;

// Slice de query de exemplo do QuerySliceChecker que lê pelo contexto do EF e estende consultas de chamado: nunca é
// registrado nem executado.

internal sealed class ReadsWithContextQueryHandler(IApplicationDbContext context) : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<int>>(context.GetHashCode());
}

internal static class FixtureTicketQueryExtensions
{
    internal static IQueryable<Ticket> WhereFixture(this IQueryable<Ticket> tickets) => tickets;
}
