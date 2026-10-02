using FluentValidation;

namespace TickestPristine.Application.Users.Register;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(UserValidationRules.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(UserValidationRules.NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(UserValidationRules.EmailMaxLength).EmailAddress();
        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a senha.")
            .StrongPassword();
    }
}
