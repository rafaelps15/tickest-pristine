namespace TickestPristine.Application.Abstractions.Notifications;

/// <summary>
/// E-mail a enviar, com o corpo em HTML e em texto simples (para clientes que não exibem HTML).
/// </summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);
