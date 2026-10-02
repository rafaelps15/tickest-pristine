using FluentValidation;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class UploadTicketAttachmentCommandValidator : AbstractValidator<UploadTicketAttachmentCommand>
{
    public UploadTicketAttachmentCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260);
        RuleFor(c => c.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(100)
            .Must(contentType => TicketAttachmentLimits.AllowedContentTypes.Contains(contentType))
            .WithMessage("O tipo de arquivo '{PropertyValue}' não é suportado.");
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(TicketAttachmentLimits.MaxFileSizeBytes)
            .WithMessage($"O arquivo excede o tamanho máximo permitido de {TicketAttachmentLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
        RuleFor(c => c.Content).NotNull();
    }
}
