using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories.GetByTicket;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketHistories;

public sealed class GetTicketHistoryByTicketQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketHistoryByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryByTicketQuery(Guid.NewGuid());

        // Act
        Result<List<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketHistoryByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryByTicketQuery(ticketId);

        // Act
        Result<List<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnHistoryOrderedByOccurredAt_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketHistoryByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryByTicketQuery(ticketId);

        // Act
        Result<List<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Action.ShouldBe(TicketHistoryAction.Created);
        result.Value[1].Action.ShouldBe(TicketHistoryAction.MessageAdded);
    }

    [Fact]
    public async Task Handle_Should_ReturnHistory_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithHistoryAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetTicketHistoryByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketHistoryByTicketQuery(ticketId);

        // Act
        Result<List<TicketHistoryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
    }

    private static async Task<Guid> SeedTicketWithHistoryAsync(TestDbContext context)
    {
        var ticket = Ticket.Create(
            "Printer is broken",
            "Original description",
            TicketPriority.Medium,
            CreatorId,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);
        context.Tickets.Add(ticket);

        var created = TicketHistory.Create(
            ticket.Id,
            CreatorId,
            TicketHistoryAction.Created,
            "Ticket criado",
            null,
            null,
            DateTime.UtcNow);
        var messageAdded = TicketHistory.Create(
            ticket.Id,
            CreatorId,
            TicketHistoryAction.MessageAdded,
            "Nova mensagem adicionada ao ticket",
            null,
            null,
            DateTime.UtcNow.AddMinutes(1));
        context.TicketHistories.AddRange(created, messageAdded);

        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
