using FluentValidation;

namespace TickestPristine.Application.Tickets.ProcessSlaBreaches;

internal sealed class ProcessSlaBreachesCommandValidator : AbstractValidator<ProcessSlaBreachesCommand>
{
    public ProcessSlaBreachesCommandValidator()
    {
        RuleFor(c => c.BatchSize).GreaterThan(0);
    }
}
