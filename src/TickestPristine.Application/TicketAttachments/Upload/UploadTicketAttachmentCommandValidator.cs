using FluentValidation;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class UploadTicketAttachmentCommandValidator : AbstractValidator<UploadTicketAttachmentCommand>
{
    public UploadTicketAttachmentCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260);
        RuleFor(c => c.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(c => c.FileSizeBytes).GreaterThan(0);
        RuleFor(c => c.Content).NotNull();
    }
}
