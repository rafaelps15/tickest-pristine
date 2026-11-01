using FluentValidation;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.Description).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}
