using FluentValidation;

namespace TickestPristine.Application.Users.ResendInvitation;

internal sealed class ResendUserInvitationCommandValidator : AbstractValidator<ResendUserInvitationCommand>
{
    public ResendUserInvitationCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
