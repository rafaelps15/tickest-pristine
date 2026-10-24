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
            .MaximumLength(260)
            .Must(fileName => !fileName.Any(character => char.IsControl(character) || character is '/' or '\\'))
            .WithMessage("O nome do arquivo contém caracteres inválidos.");
        RuleFor(c => c.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(100)
            .Must(TicketAttachmentLimits.IsAllowedContentType)
            .WithMessage("O tipo de arquivo '{PropertyValue}' não é suportado.");
        RuleFor(c => c.FileName)
            .Must((command, fileName) => TicketAttachmentLimits.HasExtensionOfContentType(fileName, command.ContentType))
            .When(c => !string.IsNullOrEmpty(c.FileName) && TicketAttachmentLimits.IsAllowedContentType(c.ContentType))
            .WithMessage(c => $"A extensão do arquivo não corresponde ao tipo '{c.ContentType}'.");
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(TicketAttachmentLimits.MaxFileSizeBytes)
            .WithMessage($"O arquivo excede o tamanho máximo permitido de {TicketAttachmentLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
        RuleFor(c => c.Content).NotNull();
    }
}
