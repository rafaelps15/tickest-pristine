using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketMessages.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class DeleteTicketMessageCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenMessageDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketMessageCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketMessageErrors.NotFound(command.MessageId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketIsDeleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        // A mensagem continua ativa, mas o chamado dela foi excluído logicamente.
        Guid messageId = await SeedMessageAsync(context, ticketDeleted: true);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketMessageCommand(messageId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketMessageErrors.NotFound(messageId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotAuthorAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketMessageCommand(messageId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteMessageAndRaiseDomainEvent_WhenCallerIsAuthor()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketMessageCommand(messageId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == messageId);
        message.DeletedAtUtc.ShouldNotBeNull();
        message.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketMessageDeletedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == message.TicketId)).Action.ShouldBe(TicketHistoryAction.MessageRemoved);
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteMessage_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketMessageCommand(messageId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == messageId);
        message.DeletedAtUtc.ShouldNotBeNull();
    }

    private static async Task<Guid> SeedMessageAsync(TestDbContext context, bool ticketDeleted = false)
    {
        Ticket ticket = TicketTestData.Create(
            requesterId: AuthorId,
            description: "Original description",
            deletedAtUtc: ticketDeleted ? DateTime.UtcNow : null);
        context.Tickets.Add(ticket);

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AuthorUserId = AuthorId,
            Content = "Original message",
            CreatedAtUtc = DateTime.UtcNow
        };
        context.TicketMessages.Add(message);

        await context.SaveChangesAsync();

        return message.Id;
    }
}
