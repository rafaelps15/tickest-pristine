using FluentValidation;

namespace TickestPristine.Application.Tickets.SettleDueSlaDeadlines;

internal sealed class SettleDueSlaDeadlinesCommandValidator : AbstractValidator<SettleDueSlaDeadlinesCommand>
{
    public SettleDueSlaDeadlinesCommandValidator()
    {
        RuleFor(c => c.BatchSize).GreaterThan(0);
    }
}
