using FluentValidation;

namespace TickestPristine.Application.Roles;

/// <summary>
/// Regras do nome da role, iguais na criação e na renomeação (spec 036): obrigatório e, sem os espaços das pontas, com
/// até <see cref="NameMaxLength"/> caracteres (o banco tem a mesma restrição) e sem o formato de um Guid, que a rota
/// antiga <c>roles/{roleName}/permissions</c> confundiria com a rota pelo id durante a transição (DEC-11).
/// </summary>
internal static class RoleValidationRules
{
    public const int NameMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidRoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= NameMaxLength)
            .WithMessage($"'{{PropertyName}}' deve ter no máximo {NameMaxLength} caracteres.")
            .Must(value => value is null || !Guid.TryParse(value.Trim(), out _))
            .WithMessage("'{PropertyName}' não pode ter o formato de um identificador (Guid).");
}
