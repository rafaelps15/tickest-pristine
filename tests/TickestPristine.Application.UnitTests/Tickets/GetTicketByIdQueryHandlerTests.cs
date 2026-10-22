using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Tickets.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketByIdQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: true);
        var query = new GetTicketByIdQuery(Guid.NewGuid());

        // Act
        Result<TicketResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnTicketWithNamesAndSla_WhenCallerIsRequester()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticket.Id);
        result.Value.OpenedByName.ShouldBe("Test User");
        result.Value.SectorName.ShouldBe("Helpdesk");
        result.Value.DueAtUtc.ShouldBe(ticket.DueAtUtc);
        result.Value.SlaStatus.ShouldBe(TicketSlaStatus.AtRisk);
    }

    [Fact]
    public async Task Handle_Should_ReturnTicket_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: true);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticket.Id);
    }

    private static GetTicketByIdQueryHandler CreateHandler(TestDbContext context, Guid callerId, bool canManage)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketByIdQueryHandler(context, userContext, permissionProvider, dateTimeProvider);
    }

    /// <summary>
    /// Chamado Urgente aberto 3,5 h antes de <see cref="NowUtc"/>: já passou do alerta (3 h) e não venceu (4 h).
    /// </summary>
    private static async Task<Ticket> SeedTicketAsync(TestDbContext context)
    {
        User requester = CreateUser();
        var sector = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", DepartmentId = Guid.NewGuid(), IsActive = true };
        DateTime createdAtUtc = NowUtc.AddHours(-3.5);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Urgent,
            Status = TicketStatus.Open,
            CreatedByUserId = requester.Id,
            SectorId = sector.Id,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = TicketSla.DueAt(createdAtUtc, TicketPriority.Urgent),
            SlaWarningAtUtc = TicketSla.WarningAt(createdAtUtc, TicketPriority.Urgent)
        };

        context.Users.Add(requester);
        context.Sectors.Add(sector);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }
}
