using FluentValidation;

namespace TickestPristine.Application.TicketMessages.Edit;

public class EditTicketMessageCommandValidator : AbstractValidator<EditTicketMessageCommand>
{
    public EditTicketMessageCommandValidator()
    {
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.Content).NotEmpty().MaximumLength(4000);
    }
}
