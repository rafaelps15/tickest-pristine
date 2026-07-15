using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class CreateTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static CreateTicketCommand Command => new()
    {
        Title = "Printer is broken",
        Description = "The printer on the 3rd floor is not working",
        Priority = TicketPriority.Medium,
        DepartmentId = Guid.NewGuid(),
        SectorId = Guid.NewGuid()
    };

    [Fact]
    public async Task Handle_Should_OpenTicketForCurrentUser_WhenRequesterIdIsNotProvided()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTicketCommandHandler(context, userContext, permissionService, dateTimeProvider);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.OpenedByUserId.ShouldBe(UserId);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenOpeningOnBehalfOfAnotherUserWithoutManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CreateTicketCommandHandler(context, userContext, permissionService, dateTimeProvider);
        CreateTicketCommand command = Command;
        command.RequesterId = Guid.NewGuid();

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_OpenTicketForRequester_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var requesterId = Guid.NewGuid();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTicketCommandHandler(context, userContext, permissionService, dateTimeProvider);
        CreateTicketCommand command = Command;
        command.RequesterId = requesterId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.OpenedByUserId.ShouldBe(requesterId);
    }
}
