using System.Collections.Concurrent;
using TickestPristine.Application.Abstractions.Notifications;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Substituto do envio de e-mail nos testes: guarda as mensagens em vez de enviá-las. Para destinatários que contêm
/// <see cref="FailingAddressMarker"/>, simula uma falha do servidor SMTP.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    /// <summary>
    /// Trecho do e-mail do destinatário que faz o envio falhar.
    /// </summary>
    public const string FailingAddressMarker = "smtp-falha";

    private const string TokenMarker = "#token=";

    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);

        if (message.To.Contains(FailingAddressMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Servidor SMTP indisponível (simulado nos testes).");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Mensagens recebidas para o e-mail informado, na ordem de envio.
    /// </summary>
    public List<EmailMessage> MessagesTo(string email) =>
        [.. _messages.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Token do link de ativação do último e-mail enviado ao endereço informado.
    /// </summary>
    public string LatestTokenFor(string email)
    {
        EmailMessage message = MessagesTo(email).LastOrDefault()
            ?? throw new InvalidOperationException($"Nenhum e-mail enviado para {email}.");

        return ExtractToken(message.TextBody);
    }

    /// <summary>
    /// Token do link de ativação (o que vem depois de <c>#token=</c>) no corpo do e-mail.
    /// </summary>
    public static string ExtractToken(string body)
    {
        int start = body.IndexOf(TokenMarker, StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException("O e-mail não tem link de ativação.");
        }

        start += TokenMarker.Length;
        int end = body.IndexOfAny(['\r', '\n'], start);

        return Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
    }
}
