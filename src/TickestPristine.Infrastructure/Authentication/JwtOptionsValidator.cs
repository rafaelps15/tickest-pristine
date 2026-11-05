using FluentValidation;

namespace TickestPristine.Infrastructure.Authentication;

internal sealed class JwtOptionsValidator : AbstractValidator<JwtOptions>
{
    public JwtOptionsValidator()
    {
        RuleFor(options => options.Issuer).NotEmpty().WithMessage("Informe Jwt:Issuer.");

        RuleFor(options => options.Audience).NotEmpty().WithMessage("Informe Jwt:Audience.");

        RuleFor(options => options.SecurityKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe Jwt:SecurityKey.")
            .MinimumLength(32).WithMessage("Jwt:SecurityKey precisa de pelo menos 32 caracteres.");

        // Teto de 60 min: um valor maior impede a subida, sem ser reduzido em silêncio.
        RuleFor(options => options.AccessTokenExpirationInMinutes)
            .InclusiveBetween(1, 60).WithMessage("Jwt:AccessTokenExpirationInMinutes deve ficar entre 1 e 60.");

        RuleFor(options => options.RefreshTokenExpirationInDays)
            .InclusiveBetween(1, 365).WithMessage("Jwt:RefreshTokenExpirationInDays deve ficar entre 1 e 365.");
    }
}
