using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class UpdateTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionService);
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionService);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidStatusTransition_WhenTransitionIsNotAllowed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionService);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.InvalidStatusTransition");
    }

    [Fact]
    public async Task Handle_Should_UpdateTicket_WhenOwnerHasUpdateOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionService);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, Guid openedByUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            OpenedByUserId = openedByUserId,
            DepartmentId = Guid.NewGuid(),
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
