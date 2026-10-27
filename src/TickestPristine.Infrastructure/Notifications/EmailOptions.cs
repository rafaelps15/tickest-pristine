using System.ComponentModel.DataAnnotations;

namespace TickestPristine.Infrastructure.Notifications;

/// <summary>
/// Configuração da seção "Email": servidor SMTP e remetente dos e-mails do sistema. Sem servidor ou remetente, a API
/// não sobe.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    /// <summary>
    /// Conexão com TLS: <c>true</c> usa STARTTLS ou TLS direto, conforme o servidor; <c>false</c> só serve para o
    /// servidor local de desenvolvimento (Mailpit).
    /// </summary>
    public bool UseTls { get; init; } = true;

    /// <summary>
    /// Usuário do SMTP; sem ele, o envio não autentica.
    /// </summary>
    public string? UserName { get; init; }

    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    [Required]
    public string FromName { get; init; } = "Tickest";
}
