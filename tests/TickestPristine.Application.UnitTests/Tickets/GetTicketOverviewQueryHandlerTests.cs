using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetOverview;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

/// <summary>
/// A contagem é SQL (Dapper) e roda nos testes de integração, contra o PostgreSQL; aqui fica o caminho de falha, que
/// responde antes de abrir a conexão.
/// </summary>
public sealed class GetTicketOverviewQueryHandlerTests : BaseHandlerTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd(bool createdPeriod)
    {
        // Arrange
        IDbConnectionFactory dbConnectionFactory = Substitute.For<IDbConnectionFactory>();
        var handler = new GetTicketOverviewQueryHandler(dbConnectionFactory, CreateClock());
        var start = new DateOnly(2026, 11, 10);
        var end = new DateOnly(2026, 11, 1);
        var scope = new TicketScopeFilter(
            [],
            null,
            null,
            null,
            false,
            createdPeriod ? start : null,
            createdPeriod ? end : null,
            createdPeriod ? null : start,
            createdPeriod ? null : end);

        // Act
        Result<TicketOverviewResponse> result = await handler.Handle(new GetTicketOverviewQuery(scope), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidPeriod);
        await dbConnectionFactory.DidNotReceive().OpenConnectionAsync(Arg.Any<CancellationToken>());
    }
}
