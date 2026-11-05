using FluentValidation;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Users.ChangePassword;

internal sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(c => c.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a nova senha.")
            .Must(password => Password.Create(password).IsSuccess).WithMessage(UserErrors.WeakPassword.Description)
            .NotEqual(c => c.CurrentPassword).WithMessage("A nova senha precisa ser diferente da atual.");
    }
}
