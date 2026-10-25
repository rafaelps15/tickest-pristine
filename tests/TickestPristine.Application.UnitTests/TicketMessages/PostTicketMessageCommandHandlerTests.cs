using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketMessages.Post;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class PostTicketMessageCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid AssignedId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new PostTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = "Hello" };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new PostTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new PostTicketMessageCommand { TicketId = ticketId, Content = "Hello" };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_PostMessageAndRaiseDomainEvent_WhenCallerIsTicketCreator()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new PostTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new PostTicketMessageCommand { TicketId = ticketId, Content = "Hello there" };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.SingleAsync(m => m.Id == result.Value);
        message.TicketId.ShouldBe(ticketId);
        message.AuthorUserId.ShouldBe(CreatorId);
        message.Content.ShouldBe("Hello there");
        message.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketMessagePostedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == message.TicketId)).Action.ShouldBe(TicketHistoryAction.MessageAdded);
    }

    [Fact]
    public async Task Handle_Should_PostMessage_WhenCallerIsAssignedAgent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AssignedId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new PostTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new PostTicketMessageCommand { TicketId = ticketId, Content = "On it" };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.SingleAsync(m => m.Id == result.Value);
        message.TicketId.ShouldBe(ticketId);
        message.AuthorUserId.ShouldBe(AssignedId);
        message.Content.ShouldBe("On it");
    }

    [Fact]
    public async Task Handle_Should_PostMessage_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new PostTicketMessageCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new PostTicketMessageCommand { TicketId = ticketId, Content = "Escalating this" };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.SingleAsync(m => m.Id == result.Value);
        message.TicketId.ShouldBe(ticketId);
        message.AuthorUserId.ShouldBe(managerId);
        message.Content.ShouldBe("Escalating this");
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = CreatorId,
            AssignedToUserId = AssignedId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    private static readonly Guid RequesterId = Guid.NewGuid();

    // Abertura dos chamados dos testes da primeira resposta: segunda-feira, 26/10/2026, 08:10 em São Paulo.
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    private static PostTicketMessageCommandHandler CreateSlaHandler(TestDbContext context, Guid authorId, bool canManage, DateTime nowUtc)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(authorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(authorId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);

        return new PostTicketMessageCommandHandler(context, userContext, permissionProvider, CreateClock(nowUtc));
    }

    private static Task<TicketSlaTimer> GetFirstResponseAsync(TestDbContext context, Guid ticketId) =>
        context.TicketSlaTimers.AsNoTracking().SingleAsync(t => t.TicketId == ticketId && t.Metric == SlaMetric.FirstResponse);

    [Fact]
    public async Task Handle_Should_NotCompleteFirstResponse_WhenAuthorIsRequester()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);
        PostTicketMessageCommandHandler handler = CreateSlaHandler(context, RequesterId, canManage: true, OpenedAtUtc.AddMinutes(5));

        // Act
        Result<Guid> result = await handler.Handle(
            new PostTicketMessageCommand { TicketId = ticket.Id, Content = "Any news?" },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await GetFirstResponseAsync(context, ticket.Id)).CompletedOnUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_CompleteFirstResponse_WhenAgentWithManagePermissionAnswers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);
        var agentId = Guid.NewGuid();
        DateTime answeredAtUtc = OpenedAtUtc.AddMinutes(15);

        // Act
        Result<Guid> result = await CreateSlaHandler(context, agentId, canManage: true, answeredAtUtc).Handle(
            new PostTicketMessageCommand { TicketId = ticket.Id, Content = "On my way." },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketSlaTimer firstResponse = await GetFirstResponseAsync(context, ticket.Id);
        firstResponse.CompletedOnUtc.ShouldBe(answeredAtUtc);
        (firstResponse.CompletedOnUtc <= firstResponse.DueOnUtc).ShouldBeTrue();

        (await context.TicketSlaTimers.AsNoTracking().SingleAsync(t => t.TicketId == ticket.Id && t.Metric == SlaMetric.Resolution))
            .CompletedOnUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_KeepFirstCompletion_WhenAnotherAgentAnswersLater()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);
        DateTime firstAnswerUtc = OpenedAtUtc.AddMinutes(15);
        await CreateSlaHandler(context, Guid.NewGuid(), canManage: true, firstAnswerUtc).Handle(
            new PostTicketMessageCommand { TicketId = ticket.Id, Content = "On my way." },
            CancellationToken.None);

        // Act
        Result<Guid> result = await CreateSlaHandler(context, Guid.NewGuid(), canManage: true, OpenedAtUtc.AddMinutes(25)).Handle(
            new PostTicketMessageCommand { TicketId = ticket.Id, Content = "Me too." },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await GetFirstResponseAsync(context, ticket.Id)).CompletedOnUtc.ShouldBe(firstAnswerUtc);
        (await context.TicketMessages.CountAsync(m => m.TicketId == ticket.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_NotCompleteFirstResponse_WhenAuthorLacksManagePermission()
    {
        // Arrange: um responsável anterior que perdeu tickets:manage, mas ainda pode publicar.
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);
        var formerAssigneeId = Guid.NewGuid();
        ticket.AssignedToUserId = formerAssigneeId;
        await context.SaveChangesAsync();

        // Act
        Result<Guid> result = await CreateSlaHandler(context, formerAssigneeId, canManage: false, OpenedAtUtc.AddMinutes(5)).Handle(
            new PostTicketMessageCommand { TicketId = ticket.Id, Content = "I am no longer on this." },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await GetFirstResponseAsync(context, ticket.Id)).CompletedOnUtc.ShouldBeNull();
    }
}
