namespace TickestPristine.Application.Tickets;

/// <summary>
/// Limites dos campos do chamado, iguais na criação e na edição.
/// </summary>
internal static class TicketValidationRules
{
    public const int TitleMaxLength = 200;

    public const int DescriptionMinLength = 10;

    public const int DescriptionMaxLength = 500;
}
