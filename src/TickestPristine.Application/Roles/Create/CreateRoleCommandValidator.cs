using FluentValidation;

namespace TickestPristine.Application.Roles.Create;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}
