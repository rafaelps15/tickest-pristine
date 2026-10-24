using FluentValidation;

namespace TickestPristine.Application.Tickets.Unassign;

internal sealed class UnassignTicketCommandValidator : AbstractValidator<UnassignTicketCommand>
{
    public UnassignTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
    }
}
