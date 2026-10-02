namespace TickestPristine.Domain.Tickets;

public static class TicketStatusNames
{
    /// <summary>
    /// Retorna o nome do status exibido ao usuário.
    /// </summary>
    public static string GetDisplayName(TicketStatus status) =>
        status switch
        {
            TicketStatus.Open => "Aberto",
            TicketStatus.InProgress => "Em andamento",
            TicketStatus.Resolved => "Resolvido",
            TicketStatus.Closed => "Fechado",
            TicketStatus.Canceled => "Cancelado",
            _ => status.ToString()
        };
}
