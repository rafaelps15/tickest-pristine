namespace TickestPristine.Application.Abstractions.Notifications;

/// <summary>
/// Envio de e-mail. Lança exceção quando o envio falha; quem chama decide se a falha desfaz a operação.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
