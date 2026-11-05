using System.Text.RegularExpressions;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

/// <summary>
/// Senha em texto, conferida pela política de senha do sistema antes de virar hash. É a regra única usada no cadastro
/// da senha, na troca e no primeiro administrador do seed.
/// </summary>
public sealed partial class Password : ValueObject
{
    public const int MinimumLength = 6;

    private Password(string value) => Value = value;

    public string Value { get; }

    public static implicit operator string(Password password) => password.Value;

    /// <summary>
    /// Cria a senha, ou devolve <see cref="UserErrors.WeakPassword"/> quando ela é vazia, tem menos de
    /// <see cref="MinimumLength"/> caracteres ou não tem letra maiúscula nem caractere especial.
    /// </summary>
    public static Result<Password> Create(string? password) =>
        Result.Create(password, UserErrors.WeakPassword)
            .Ensure(p => p.Length >= MinimumLength, UserErrors.WeakPassword)
            .Ensure(p => UppercaseRegex().IsMatch(p), UserErrors.WeakPassword)
            .Ensure(p => SpecialCharacterRegex().IsMatch(p), UserErrors.WeakPassword)
            .Map(p => new Password(p));

    // Nunca o valor: um objeto de senha num log ou numa mensagem não pode expor a senha.
    public override string ToString() => "***";

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Value;
    }

    [GeneratedRegex(@"\p{Lu}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}\s]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SpecialCharacterRegex();
}
