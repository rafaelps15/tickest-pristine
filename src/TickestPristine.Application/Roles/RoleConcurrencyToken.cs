namespace TickestPristine.Application.Roles;

/// <summary>
/// Nome da propriedade sombra (só no banco) usada como token de concorrência da role de administrador, num lugar só
/// para a configuração do EF, o <see cref="LastAdministratorCheck"/> e os testes.
/// </summary>
public static class RoleConcurrencyToken
{
    public const string PropertyName = "Version";
}
