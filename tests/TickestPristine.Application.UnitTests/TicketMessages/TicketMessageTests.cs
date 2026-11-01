using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class TicketMessageTests
{
    private static readonly Ticket Ticket = TicketTestData.Create();
    private static readonly Guid TicketId = Ticket.Id;
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly DateTime PostedAtUtc = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PostMessage_Should_CreateMessageAndRaisePostedEvent()
    {
        // Act
        TicketMessage message = Ticket.PostMessage(AuthorId, "Conteúdo", authorIsAgent: false, PostedAtUtc).Message;

        // Assert
        message.Id.ShouldNotBe(Guid.Empty);
        message.TicketId.ShouldBe(TicketId);
        message.AuthorUserId.ShouldBe(AuthorId);
        message.Content.ShouldBe("Conteúdo");
        message.CreatedAtUtc.ShouldBe(PostedAtUtc);
        message.EditedAtUtc.ShouldBeNull();
        message.DeletedAtUtc.ShouldBeNull();
        message.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new TicketMessagePostedDomainEvent(message.Id, TicketId, AuthorId));
    }

    [Fact]
    public void Edit_Should_ChangeContentRaiseEventAndRecordEdition_WhenEditorIsAuthor()
    {
        // Arrange
        TicketMessage message = CreateMessage();
        DateTime editedAtUtc = PostedAtUtc.AddMinutes(5);

        // Act
        Result<TicketHistory> result = message.Edit("Conteúdo novo", AuthorId, editedAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        message.Content.ShouldBe("Conteúdo novo");
        message.EditedAtUtc.ShouldBe(editedAtUtc);
        message.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new TicketMessageEditedDomainEvent(message.Id, TicketId, AuthorId, "Conteúdo"));
        result.Value.ChangedByUserId.ShouldBe(AuthorId);
        result.Value.Action.ShouldBe(TicketHistoryAction.MessageEdited);
        result.Value.OccurredAtUtc.ShouldBe(editedAtUtc);
    }

    [Fact]
    public void Edit_Should_ReturnUnauthorizedAndChangeNothing_WhenEditorIsNotAuthor()
    {
        // Arrange
        TicketMessage message = CreateMessage();

        // Act
        Result<TicketHistory> result = message.Edit("Conteúdo novo", Guid.NewGuid(), PostedAtUtc.AddMinutes(5));

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
        message.Content.ShouldBe("Conteúdo");
        message.EditedAtUtc.ShouldBeNull();
        message.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_Should_MarkDeletedRaiseEventAndRecordRemovalByWhoDeleted()
    {
        // Arrange
        TicketMessage message = CreateMessage();
        var managerId = Guid.NewGuid();
        DateTime deletedAtUtc = PostedAtUtc.AddMinutes(10);

        // Act
        TicketHistory history = message.Delete(managerId, canDeleteAny: true, deletedAtUtc).Value;

        // Assert
        message.DeletedAtUtc.ShouldBe(deletedAtUtc);
        message.DomainEvents.ShouldHaveSingleItem().ShouldBe(new TicketMessageDeletedDomainEvent(message.Id, TicketId));
        history.ChangedByUserId.ShouldBe(managerId);
        history.Action.ShouldBe(TicketHistoryAction.MessageRemoved);
        history.OccurredAtUtc.ShouldBe(deletedAtUtc);
    }

    [Fact]
    public void Delete_Should_AllowAuthor_WhenAuthorCannotDeleteAny()
    {
        // Arrange
        TicketMessage message = CreateMessage();

        // Act
        Result<TicketHistory> result = message.Delete(AuthorId, canDeleteAny: false, PostedAtUtc.AddMinutes(10));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ChangedByUserId.ShouldBe(AuthorId);
    }

    [Fact]
    public void Delete_Should_ReturnUnauthorizedAndChangeNothing_WhenCallerIsNotAuthorAndCannotDeleteAny()
    {
        // Arrange
        TicketMessage message = CreateMessage();

        // Act
        Result<TicketHistory> result = message.Delete(Guid.NewGuid(), canDeleteAny: false, PostedAtUtc.AddMinutes(10));

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
        message.DeletedAtUtc.ShouldBeNull();
        message.DomainEvents.ShouldBeEmpty();
    }

    private static TicketMessage CreateMessage()
    {
        TicketMessage message = Ticket.PostMessage(AuthorId, "Conteúdo", authorIsAgent: false, PostedAtUtc).Message;

        message.ClearDomainEvents();

        return message;
    }
}
