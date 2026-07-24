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
        var command = new PostTicketMessageCommand { TicketId = Guid.Empty, Content = "Hello" };

        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void PostValidator_Should_HaveError_WhenContentIsEmpty()
    {
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = string.Empty };

        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void PostValidator_Should_HaveError_WhenContentExceedsMaxLength()
    {
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = new string('a', 4001) };

        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void PostValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new PostTicketMessageCommand { TicketId = Guid.NewGuid(), Content = "Hello there" };

        TestValidationResult<PostTicketMessageCommand> result = _postValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EditValidator_Should_HaveError_WhenMessageIdIsEmpty()
    {
        var command = new EditTicketMessageCommand { MessageId = Guid.Empty, Content = "Updated" };

        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.MessageId);
    }

    [Fact]
    public void EditValidator_Should_HaveError_WhenContentIsEmpty()
    {
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = string.Empty };

        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void EditValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new EditTicketMessageCommand { MessageId = Guid.NewGuid(), Content = "Updated content" };

        TestValidationResult<EditTicketMessageCommand> result = _editValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenMessageIdIsEmpty()
    {
        var command = new DeleteTicketMessageCommand(Guid.Empty);

        TestValidationResult<DeleteTicketMessageCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.MessageId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new DeleteTicketMessageCommand(Guid.NewGuid());

        TestValidationResult<DeleteTicketMessageCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
