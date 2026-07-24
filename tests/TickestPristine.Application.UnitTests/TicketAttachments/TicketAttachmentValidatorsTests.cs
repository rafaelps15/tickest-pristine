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
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.TicketId = Guid.Empty;

        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.TicketId);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenFileNameIsEmpty()
    {
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.FileName = string.Empty;

        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.FileName);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenFileSizeIsZero()
    {
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.FileSizeBytes = 0;

        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Fact]
    public void UploadValidator_Should_HaveError_WhenContentIsNull()
    {
        UploadTicketAttachmentCommand command = ValidUploadCommand;
        command.Content = null!;

        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content);
    }

    [Fact]
    public void UploadValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        TestValidationResult<UploadTicketAttachmentCommand> result = _uploadValidator.TestValidate(ValidUploadCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenAttachmentIdIsEmpty()
    {
        var command = new DeleteTicketAttachmentCommand(Guid.Empty);

        TestValidationResult<DeleteTicketAttachmentCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.AttachmentId);
    }

    [Fact]
    public void DeleteValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new DeleteTicketAttachmentCommand(Guid.NewGuid());

        TestValidationResult<DeleteTicketAttachmentCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
