using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Permissions;
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
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly TicketScopeFilter NoScope = new([], null, null, null, false, null, null, null, null);

    [Fact]
    public async Task Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd()
    {
        // Arrange
        IDbConnectionFactory dbConnectionFactory = Substitute.For<IDbConnectionFactory>();
        GetTicketVolumeQueryHandler handler = CreateHandler(dbConnectionFactory, Substitute.For<IPermissionProvider>());
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
        GetTicketVolumeQueryHandler handler = CreateHandler(dbConnectionFactory, Substitute.For<IPermissionProvider>());
        var query = new GetTicketVolumeQuery(TicketVolumeGrouping.Day, NoScope, new DateOnly(2024, 1, 1), new DateOnly(2025, 12, 31));

        // Act
        Result<List<TicketVolumeResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.VolumeTooManyPoints(TicketVolumePeriods.MaxPoints));
        await dbConnectionFactory.DidNotReceive().OpenConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("sector")]
    [InlineData("priority")]
    [InlineData("requester")]
    [InlineData("assignee")]
    [InlineData("unassigned")]
    public async Task Handle_Should_ReturnStatsScopeForbidden_WhenSlicingFilterIsGivenWithoutManagePermission(string filter)
    {
        // Arrange: quem chama não tem tickets:manage
        IDbConnectionFactory dbConnectionFactory = Substitute.For<IDbConnectionFactory>();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        GetTicketVolumeQueryHandler handler = CreateHandler(dbConnectionFactory, permissionProvider);
        TicketScopeFilter scope = filter switch
        {
            "sector" => NoScope with { SectorIds = [Guid.NewGuid()] },
            "priority" => NoScope with { Priority = TicketPriority.Urgent },
            "requester" => NoScope with { RequesterId = Guid.NewGuid() },
            "assignee" => NoScope with { AssignedToUserId = Guid.NewGuid() },
            _ => NoScope with { UnassignedOnly = true }
        };
        var query = new GetTicketVolumeQuery(TicketVolumeGrouping.Day, scope, null, null);

        // Act
        Result<List<TicketVolumeResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.StatsScopeForbidden);
        await permissionProvider.Received(1).HasPermissionAsync(CallerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>());
        await dbConnectionFactory.DidNotReceive().OpenConnectionAsync(Arg.Any<CancellationToken>());
    }

    private static GetTicketVolumeQueryHandler CreateHandler(IDbConnectionFactory dbConnectionFactory, IPermissionProvider permissionProvider)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CallerId);

        return new GetTicketVolumeQueryHandler(dbConnectionFactory, CreateClock(), userContext, permissionProvider);
    }
}
