using TickestPristine.Application.TicketMessages.Delete;
using TickestPristine.Application.TicketMessages.Edit;
using TickestPristine.Application.TicketMessages.Post;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class TicketMessageValidatorsTests
{
    private readonly PostTicketMessageCommandValidator _postValidator = new();
    private readonly EditTicketMessageCommandValidator _editValidator = new();
    private readonly DeleteTicketMessageCommandValidator _deleteValidator = new();

    [Fact]
    public void PostValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        // Arrange
        var command = new PostTicketMessageCommand { TicketId = Guid.Empty, Content = "Hello" };

        // Act
        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void PostValidator_Should_HaveError_WhenContentIsEmpty()
    {
        // Arrange
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = string.Empty };

        // Act
        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void PostValidator_Should_HaveError_WhenContentExceedsMaxLength()
    {
        // Arrange
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = new string('a', 4001) };

        // Act
        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void PostValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = "Hello there" };

        // Act
        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EditValidator_Should_HaveError_WhenMessageIdIsEmpty()
    {
        // Arrange
        var command = new EditTicketMessageCommand { MessageId = Guid.Empty, Content = "Updated" };

        // Act
        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.MessageId);
    }

    [Fact]
    public void EditValidator_Should_HaveError_WhenContentIsEmpty()
    {
        // Arrange
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = string.Empty };

        // Act
        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void EditValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = "Updated content" };

        // Act
        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenMessageIdIsEmpty()
    {
        // Arrange
        var command = new DeleteTicketMessageCommand(Guid.Empty);

        // Act
        TestValidationResult<DeleteTicketMessageCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.MessageId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeleteTicketMessageCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeleteTicketMessageCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EditValidator_Should_HaveError_WhenContentExceedsMaxLength()
    {
        // Arrange
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = new string('a', 4001) };

        // Act
        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }
}
