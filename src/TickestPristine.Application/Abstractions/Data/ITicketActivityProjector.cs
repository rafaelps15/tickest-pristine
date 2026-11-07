namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Projeção da última atividade e da situação de espera de um chamado, recalculadas pelo estado gravado depois do commit
/// de uma ação sobre uma mensagem ou um anexo, com as regras do chamado. Atualiza só essas duas colunas, sem conferir a
/// versão do chamado, mas o UPDATE muda a versão da linha: um command no mesmo chamado que leu a versão anterior recebe
/// conflito.
/// </summary>
public interface ITicketActivityProjector
{
    /// <summary>
    /// Trava a linha do chamado e só então lê o estado gravado: a projeção seguinte do mesmo chamado espera esta
    /// terminar, e a última sempre lê a última mensagem válida. A última atividade nunca diminui. O chamado excluído não
    /// muda, e nada é gravado quando as duas colunas já estão certas.
    /// </summary>
    Task ProjectAsync(Guid ticketId, CancellationToken cancellationToken);
}
