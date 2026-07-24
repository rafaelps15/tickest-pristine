using FluentValidation;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class EditTicketMessageCommandValidator : AbstractValidator<EditTicketMessageCommand>
{
    public EditTicketMessageCommandValidator()
    {
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.Content).NotEmpty().MaximumLength(4000);
    }
}
