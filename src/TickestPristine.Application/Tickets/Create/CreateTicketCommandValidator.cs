using FluentValidation;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty();
        RuleFor(c => c.Description).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.SectorId).NotEmpty();
    }
}
