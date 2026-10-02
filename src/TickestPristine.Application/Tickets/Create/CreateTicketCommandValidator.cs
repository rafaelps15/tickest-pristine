using FluentValidation;

namespace TickestPristine.Application.Tickets.Create;

internal sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(TicketValidationRules.TitleMaxLength);
        RuleFor(c => c.Description)
            .NotEmpty()
            .Length(TicketValidationRules.DescriptionMinLength, TicketValidationRules.DescriptionMaxLength);
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.SectorId).NotEmpty();
    }
}
