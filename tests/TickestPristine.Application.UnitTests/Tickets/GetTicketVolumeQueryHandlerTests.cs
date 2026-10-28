using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetVolume;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

/// <summary>
/// A contagem é SQL (Dapper) e roda nos testes de integração, contra o PostgreSQL; aqui ficam os caminhos de falha, que
/// respondem antes de abrir a conexão.
/// </summary>
public sealed class GetTicketVolumeQueryHandlerTests : BaseHandlerTest
{
    private static readonly TicketScopeFilter NoScope = new([], null, null, null, false, null, null, null, null);

    [Fact]
    public async Task Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd()
    {
        // Arrange
        IDbConnectionFactory dbConnectionFactory = Substitute.For<IDbConnectionFactory>();
        var handler = new GetTicketVolumeQueryHandler(dbConnectionFactory, CreateClock());
        var query = new GetTicketVolumeQuery(TicketVolumeGrouping.Day, NoScope, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1));

        // Act
        Result<List<TicketVolumeResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidPeriod);
        await dbConnectionFactory.DidNotReceive().OpenConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnVolumeTooManyPoints_WhenPeriodGeneratesMoreThanTheMaximum()
    {
        // Arrange: dois anos agrupados por dia
        IDbConnectionFactory dbConnectionFactory = Substitute.For<IDbConnectionFactory>();
        var handler = new GetTicketVolumeQueryHandler(dbConnectionFactory, CreateClock());
        var query = new GetTicketVolumeQuery(TicketVolumeGrouping.Day, NoScope, new DateOnly(2024, 1, 1), new DateOnly(2025, 12, 31));

        // Act
        Result<List<TicketVolumeResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.VolumeTooManyPoints(TicketVolumePeriods.MaxPoints));
        await dbConnectionFactory.DidNotReceive().OpenConnectionAsync(Arg.Any<CancellationToken>());
    }
}
