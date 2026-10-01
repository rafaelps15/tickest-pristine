using FluentValidation;

namespace TickestPristine.Application.Tickets.Create;

public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.SectorId).NotEmpty();
    }
}
