using FluentValidation;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class UploadTicketAttachmentCommandValidator : AbstractValidator<UploadTicketAttachmentCommand>
{
    public UploadTicketAttachmentCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.FileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(fileName =>
                !fileName.Contains("..", StringComparison.Ordinal) &&
                !fileName.Any(character => char.IsControl(character) || character is '/' or '\\'))
            .WithMessage("O nome do arquivo contém caracteres inválidos.");
        RuleFor(c => c.ContentType).NotEmpty();
        RuleFor(c => c.FileSizeBytes).GreaterThan(0);
        RuleFor(c => c.Content).NotNull();
    }
}
