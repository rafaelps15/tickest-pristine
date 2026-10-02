using FluentValidation;

namespace TickestPristine.Application.Tickets.Delete;

internal sealed class DeleteTicketCommandValidator : AbstractValidator<DeleteTicketCommand>
{
    public DeleteTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
    }
}
