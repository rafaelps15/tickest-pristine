using FluentValidation;

namespace TickestPristine.Application.Users.UpdateProfile;

internal sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(UserValidationRules.NameMaxLength);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(UserValidationRules.NameMaxLength);
    }
}
