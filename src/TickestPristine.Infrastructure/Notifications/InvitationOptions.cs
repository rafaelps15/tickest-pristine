namespace TickestPristine.Infrastructure.Notifications;

/// <summary>
/// Configuração da seção "Invitations": endereço da tela de ativação da conta no front (ex.:
/// <c>https://tickest.exemplo/ativar-conta</c>). O token vai no fragmento do link.
/// </summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    public string ActivationUrl { get; init; } = string.Empty;
}
