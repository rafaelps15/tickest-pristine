namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Quem o chamado ativo está aguardando: o solicitante, quando a última mensagem válida é de quem atende, ou quem
/// atende, nos demais casos (spec 029). É só informativa: não muda o status nem o SLA.
/// </summary>
public enum TicketWaitingOn
{
    Requester = 1,
    Agent = 2
}
