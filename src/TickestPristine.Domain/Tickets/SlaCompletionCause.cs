namespace TickestPristine.Domain.Tickets;

/// <summary>
/// O que concluiu um cronômetro de SLA: a mensagem de quem atende (só a primeira resposta) ou a resolução do chamado.
/// </summary>
public enum SlaCompletionCause
{
    Message = 1,
    Resolution = 2
}
