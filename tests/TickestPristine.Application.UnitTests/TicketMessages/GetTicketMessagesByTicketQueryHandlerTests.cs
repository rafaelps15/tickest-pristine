using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketMessages.GetByTicket;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class GetTicketMessagesByTicketQueryHandlerTests : BaseHandlerTest
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

        var handler = new GetTicketMessagesByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesByTicketQuery(Guid.NewGuid());

        // Act
        Result<List<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketMessagesByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesByTicketQuery(ticketId);

        // Act
        Result<List<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnMessagesOrderedByCreatedAt_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketMessagesByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesByTicketQuery(ticketId);

        // Act
        Result<List<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Content.ShouldBe("First message");
        result.Value[1].Content.ShouldBe("Second message");
    }

    [Fact]
    public async Task Handle_Should_ExcludeDeletedMessages()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var ticket = Ticket.Create(
            "Printer is broken",
            "Original description",
            TicketPriority.Medium,
            CreatorId,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);
        context.Tickets.Add(ticket);

        var message = TicketMessage.Create(ticket.Id, CreatorId, "Deleted message", DateTime.UtcNow);
        message.Delete(DateTime.UtcNow);
        context.TicketMessages.Add(message);

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketMessagesByTicketQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesByTicketQuery(ticket.Id);

        // Act
        Result<List<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    private static async Task<Guid> SeedTicketWithMessagesAsync(TestDbContext context)
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

        var first = TicketMessage.Create(ticket.Id, CreatorId, "First message", DateTime.UtcNow);
        var second = TicketMessage.Create(ticket.Id, CreatorId, "Second message", DateTime.UtcNow.AddMinutes(1));
        context.TicketMessages.AddRange(first, second);

        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
