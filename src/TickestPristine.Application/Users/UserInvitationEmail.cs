using System.Globalization;
using System.Net;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Users;

/// <summary>
/// E-mail de convite de ativação da conta (notificação N-01 da fonte da spec 008), em HTML e em texto simples. Nunca
/// contém senha: o usuário define a dele pelo link.
/// </summary>
internal static class UserInvitationEmail
{
    public const string Subject = "Convite para acessar o Tickest";

    private const string BrasiliaTimeZoneId = "America/Sao_Paulo";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static EmailMessage Create(User user, string activationLink, DateTime expiresAtUtc)
    {
        string expiresAt = FormatBrasiliaTime(expiresAtUtc);

        string text = string.Join(
            Environment.NewLine,
            $"Olá, {user.FirstName}!",
            string.Empty,
            "Você foi cadastrado no Tickest. Para ativar a sua conta, defina a sua senha pelo link abaixo:",
            activationLink,
            string.Empty,
            $"Seu código de usuário: {user.Code}",
            $"Seu e-mail de acesso: {user.Email}",
            $"O link vale até {expiresAt} e só pode ser usado uma vez.",
            string.Empty,
            "Se você não reconhece este convite, ignore este e-mail.");

        string html = string.Concat(
            $"<p>Olá, {Encode(user.FirstName)}!</p>",
            "<p>Você foi cadastrado no Tickest. Para ativar a sua conta, defina a sua senha pelo link abaixo:</p>",
            $"<p><a href=\"{Encode(activationLink)}\">Ativar minha conta</a></p>",
            "<ul>",
            $"<li>Seu código de usuário: {Encode(user.Code)}</li>",
            $"<li>Seu e-mail de acesso: {Encode(user.Email)}</li>",
            "</ul>",
            $"<p>O link vale até {Encode(expiresAt)} e só pode ser usado uma vez.</p>",
            "<p>Se você não reconhece este convite, ignore este e-mail.</p>");

        return new EmailMessage(user.Email, Subject, html, text);
    }

    /// <summary>
    /// Data e hora no horário de Brasília, ex.: "29/10/2026 às 08:10 (horário de Brasília)".
    /// </summary>
    internal static string FormatBrasiliaTime(DateTime utc)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById(BrasiliaTimeZoneId));

        return local.ToString("dd/MM/yyyy 'às' HH:mm", PtBr) + " (horário de Brasília)";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
