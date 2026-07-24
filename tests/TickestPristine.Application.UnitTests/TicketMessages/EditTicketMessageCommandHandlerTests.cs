using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.TicketMessages.Edit;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

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
        result.Error.Code.ShouldBe("TicketMessages.NotFound");
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
        result.Error.Code.ShouldBe("TicketMessages.Unauthorized");
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
    }

    private static async Task<Guid> SeedMessageAsync(TestDbContext context)
    {
        var ticket = Ticket.Create(
            "Printer is broken",
            "Original description",
            TicketPriority.Medium,
            AuthorId,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);
        context.Tickets.Add(ticket);

        var message = TicketMessage.Create(ticket.Id, AuthorId, "Original message", DateTime.UtcNow);
        context.TicketMessages.Add(message);

        await context.SaveChangesAsync();

        return message.Id;
    }
}
