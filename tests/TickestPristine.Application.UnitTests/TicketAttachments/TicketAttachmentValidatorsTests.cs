using TickestPristine.Application.TicketAttachments.Delete;
using TickestPristine.Application.TicketAttachments.Upload;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentValidatorsTests
{
    private readonly UploadTicketAttachmentCommandValidator _uploadValidator = new();
    private readonly DeleteTicketAttachmentCommandValidator _deleteValidator = new();

    private static UploadTicketAttachmentCommand ValidUploadCommand => new()
    {
        TicketId = Guid.NewGuid(),
        FileName = "report.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 1024,
        Content = new MemoryStream([1, 2, 3])
    };

    [Fact]
    public void UploadValidator_Should_HaveError_WhenTicketIdIsEmpty()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.TicketId = Guid.Empty;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenFileNameIsEmpty()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.FileName = string.Empty;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FileName);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenFileSizeIsZero()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.FileSizeBytes = 0;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenContentIsNull()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.Content = null!;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void UploadValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenAttachmentIdIsEmpty()
    {
        // Arrange
        var command = new DeleteTicketAttachmentCommand(Guid.Empty);

        // Act
        TestValidationResult<DeleteTicketAttachmentCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.AttachmentId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeleteTicketAttachmentCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeleteTicketAttachmentCommand> result = _deleteValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenFileNameExceedsMaxLength()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.FileName = new string('a', 261);

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FileName);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenContentTypeIsEmpty()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.ContentType = string.Empty;

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ContentType);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenContentTypeExceedsMaxLength()
    {
        // Arrange
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.ContentType = new string('a', 101);

        // Act
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ContentType);
    }
}
