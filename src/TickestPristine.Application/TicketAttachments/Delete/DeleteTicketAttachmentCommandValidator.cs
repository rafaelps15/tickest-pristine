using FluentValidation;

namespace TickestPristine.Application.TicketAttachments.Delete;

internal sealed class DeleteTicketAttachmentCommandValidator : AbstractValidator<DeleteTicketAttachmentCommand>
{
    public DeleteTicketAttachmentCommandValidator()
    {
        RuleFor(c => c.AttachmentId).NotEmpty();
    }
}
