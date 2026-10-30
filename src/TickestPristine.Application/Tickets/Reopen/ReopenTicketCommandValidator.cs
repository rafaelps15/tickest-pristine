using FluentValidation;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class ReopenTicketCommandValidator : AbstractValidator<ReopenTicketCommand>
{
    public ReopenTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
    }
}
