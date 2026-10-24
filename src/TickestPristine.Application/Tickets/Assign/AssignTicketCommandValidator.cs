using FluentValidation;

namespace TickestPristine.Application.Tickets.Assign;

internal sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.AssignedToUserId).NotEmpty();
    }
}
