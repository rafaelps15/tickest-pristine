namespace TickestPristine.Application.Users;

/// <summary>
/// Código público do usuário, gerado num lugar só: o cadastro pela API e o seed do primeiro administrador usam o
/// mesmo formato.
/// </summary>
public static class UserCodes
{
    public const string Prefix = "usr_";

    public static string New() => $"{Prefix}{Ulid.NewUlid()}";
}
