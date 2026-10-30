using FluentValidation;

namespace TickestPristine.Application.Sectors.Create;

internal sealed class CreateSectorCommandValidator : AbstractValidator<CreateSectorCommand>
{
    public CreateSectorCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.DepartmentId).NotEmpty();
    }
}
