using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Application.TicketMessages.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class GetTicketMessagesQueryHandlerTests : BaseHandlerTest
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

        var handler = new GetTicketMessagesQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesQuery(Guid.NewGuid(), new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
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

        var handler = new GetTicketMessagesQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNewestMessagesFirst_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketMessagesQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.Items[0].Content.ShouldBe("Second message");
        result.Value.Items[1].Content.ShouldBe("First message");
    }

    [Fact]
    public async Task Handle_Should_ExcludeDeletedMessages_WhenTicketHasDeletedMessages()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = CreatorId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Tickets.Add(ticket);

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AuthorUserId = CreatorId,
            Content = "Deleted message",
            CreatedAtUtc = DateTime.UtcNow,
            DeletedAtUtc = DateTime.UtcNow
        };
        context.TicketMessages.Add(message);

        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketMessagesQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesQuery(ticket.Id, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_WalkAllMessagesByCursor_WhenMessagesShareTheSameCreationDate()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);
        var sameDate = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        context.TicketMessages.AddRange(
            new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = CreatorId, Content = "Tie A", CreatedAtUtc = sameDate },
            new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = CreatorId, Content = "Tie B", CreatedAtUtc = sameDate },
            new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorUserId = CreatorId, Content = "Tie C", CreatedAtUtc = sameDate });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        var handler = new GetTicketMessagesQueryHandler(context, userContext, Substitute.For<IPermissionProvider>());

        // Act
        var seen = new List<Guid>();
        DateTime? cursorDate = null;
        Guid? cursorId = null;
        int pages = 0;
        bool hasNextPage;

        do
        {
            Result<CursorPagedResponse<TicketMessageResponse>> page = await handler.Handle(
                new GetTicketMessagesQuery(ticketId, new CursorPageRequest(cursorDate, cursorId, 2)),
                CancellationToken.None);

            seen.AddRange(page.Value.Items.Select(m => m.Id));
            (cursorDate, cursorId, hasNextPage) = (page.Value.NextCursorDate, page.Value.NextCursorId, page.Value.HasNextPage);
            pages++;
        }
        while (hasNextPage);

        // Assert
        pages.ShouldBe(3);
        seen.Count.ShouldBe(5);
        seen.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task Handle_Should_ReturnCursorOfLastItem_WhenThereIsANextPage()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        var handler = new GetTicketMessagesQueryHandler(context, userContext, Substitute.For<IPermissionProvider>());

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(
            new GetTicketMessagesQuery(ticketId, new CursorPageRequest(null, null, 1)),
            CancellationToken.None);

        // Assert
        TicketMessageResponse newest = result.Value.Items.ShouldHaveSingleItem();
        newest.Content.ShouldBe("Second message");
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.NextCursorDate.ShouldBe(newest.CreatedAtUtc);
        result.Value.NextCursorId.ShouldBe(newest.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnMessages_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketWithMessagesAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetTicketMessagesQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketMessagesQuery(ticketId, new CursorPageRequest(null, null, 20));

        // Act
        Result<CursorPagedResponse<TicketMessageResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
    }

    private static async Task<Guid> SeedTicketWithMessagesAsync(TestDbContext context)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = CreatorId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Tickets.Add(ticket);

        var first = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AuthorUserId = CreatorId,
            Content = "First message",
            CreatedAtUtc = DateTime.UtcNow
        };
        var second = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            AuthorUserId = CreatorId,
            Content = "Second message",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(1)
        };
        context.TicketMessages.AddRange(first, second);

        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
