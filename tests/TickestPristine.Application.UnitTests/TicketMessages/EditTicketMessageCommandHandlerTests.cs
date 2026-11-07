using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.TicketMessages.Edit;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class EditTicketMessageCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenMessageDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new EditTicketMessageCommandHandler(context, userContext, dateTimeProvider);
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = "Updated" };

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
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new EditTicketMessageCommandHandler(context, userContext, dateTimeProvider);
        var command = new EditTicketMessageCommand { MessageId = messageId, Content = "Updated" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketMessageErrors.NotFound(messageId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotAuthor()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new EditTicketMessageCommandHandler(context, userContext, dateTimeProvider);
        var command = new EditTicketMessageCommand { MessageId = messageId, Content = "Updated" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_EditMessageAndRaiseDomainEvent_WhenCallerIsAuthor()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new EditTicketMessageCommandHandler(context, userContext, dateTimeProvider);
        var command = new EditTicketMessageCommand { MessageId = messageId, Content = "Updated content" };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketMessage message = await context.TicketMessages.SingleAsync(m => m.Id == messageId);
        message.Content.ShouldBe("Updated content");
        message.EditedAtUtc.ShouldNotBeNull();
        message.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketMessageEditedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == message.TicketId)).Action.ShouldBe(TicketHistoryAction.MessageEdited);
    }

    [Fact]
    public async Task Handle_Should_RaiseMessageEditedWithoutChangingTicket_WhenCallerIsAuthor()
    {
        // Arrange: depois da abertura dos chamados do arranjo, que usam a hora real.
        DateTime nowUtc = DateTime.UtcNow.AddHours(1);
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);
        Ticket before = await context.Tickets.AsNoTracking().SingleAsync();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AuthorId);
        var handler = new EditTicketMessageCommandHandler(context, userContext, CreateClock(nowUtc));

        // Act
        Result result = await handler.Handle(
            new EditTicketMessageCommand { MessageId = messageId, Content = "Updated" },
            CancellationToken.None);

        // Assert: a última atividade e quem o chamado aguarda ficam para a projeção, que o evento dispara depois do commit.
        result.IsSuccess.ShouldBeTrue();
        TicketMessage message = await context.TicketMessages.SingleAsync(m => m.Id == messageId);
        message.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketMessageEditedDomainEvent);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync();
        stored.LastActivityAtUtc.ShouldBe(before.LastActivityAtUtc);
        stored.WaitingOn.ShouldBe(before.WaitingOn);
    }

    [Fact]
    public async Task Handle_Should_NotRecordHistory_WhenCallerIsNotAuthor()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid messageId = await SeedMessageAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        var handler = new EditTicketMessageCommandHandler(context, userContext, CreateClock());

        // Act
        await handler.Handle(new EditTicketMessageCommand { MessageId = messageId, Content = "Updated" }, CancellationToken.None);

        // Assert
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    private static async Task<Guid> SeedMessageAsync(TestDbContext context, bool ticketDeleted = false)
    {
        Ticket ticket = TicketTestData.Create(
            requesterId: AuthorId,
            description: "Original description",
            deletedAtUtc: ticketDeleted ? DateTime.UtcNow : null);
        context.Tickets.Add(ticket);

        TicketMessage message = TicketTestData.CreateMessage(ticket.Id, AuthorId, "Original message", DateTime.UtcNow);
        context.TicketMessages.Add(message);

        await context.SaveChangesAsync();

        return message.Id;
    }
}
