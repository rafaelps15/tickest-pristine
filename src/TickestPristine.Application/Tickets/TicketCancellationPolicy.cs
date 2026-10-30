using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Quem pode cancelar um chamado: o dono, com <c>tickets:update-own</c>, só até
/// <see cref="OwnerWindowMinutes"/> minutos depois da abertura, para desfazer um chamado aberto por engano; quem tem
/// <c>tickets:cancel</c>, a qualquer momento. Nos dois casos, só chamado ativo. A janela conta da abertura, e reabrir
/// não a reinicia. É o único lugar da regra, usado pela edição do chamado e pelas consultas que informam se o botão de
/// cancelar aparece.
/// </summary>
internal static class TicketCancellationPolicy
{
    public const int OwnerWindowMinutes = 15;

    /// <summary>
    /// Último instante em que o dono ainda pode cancelar o chamado.
    /// </summary>
    public static DateTime OwnerDeadline(DateTime createdAtUtc) => createdAtUtc.AddMinutes(OwnerWindowMinutes);

    public static bool IsWithinOwnerWindow(DateTime createdAtUtc, DateTime nowUtc) => nowUtc <= OwnerDeadline(createdAtUtc);

    /// <summary>
    /// Indica se quem chama pode cancelar o chamado agora. <paramref name="canCancelAny"/> é ter <c>tickets:cancel</c>;
    /// <paramref name="isOwnerWhoCanEdit"/> é ser o dono e ter <c>tickets:update-own</c>.
    /// </summary>
    public static bool CanCancel(
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime nowUtc,
        bool canCancelAny,
        bool isOwnerWhoCanEdit) =>
        Ticket.IsActiveStatus(status) &&
        (canCancelAny || isOwnerWhoCanEdit && IsWithinOwnerWindow(createdAtUtc, nowUtc));
}
