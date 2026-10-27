namespace TickestPristine.Application.Abstractions.Notifications;

/// <summary>
/// Monta o link de ativação da conta, no endereço do front configurado, com o token no fragmento (<c>#token=</c>):
/// o fragmento não vai para o servidor, então o token não aparece em log de acesso nem em cabeçalho Referer.
/// </summary>
public interface IActivationLinkBuilder
{
    string Build(string token);
}
