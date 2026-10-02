using FluentValidation;

namespace TickestPristine.Application.TicketMessages.Delete;

internal sealed class DeleteTicketMessageCommandValidator : AbstractValidator<DeleteTicketMessageCommand>
{
    public DeleteTicketMessageCommandValidator()
    {
        RuleFor(c => c.MessageId).NotEmpty();
    }
}
