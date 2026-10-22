using FluentValidation;

namespace TickestPristine.Application.Tickets.Close;

internal sealed class CloseTicketCommandValidator : AbstractValidator<CloseTicketCommand>
{
    public CloseTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
    }
}
