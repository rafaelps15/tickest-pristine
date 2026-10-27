namespace TickestPristine.Application.Tickets.GetOwn;

/// <summary>
/// Grupo de status de "Meus chamados": <see cref="Active"/> são os chamados em aberto (Aberto e Em Andamento), e
/// <see cref="Finished"/>, os encerrados (Resolvido, Fechado e Cancelado).
/// </summary>
public enum OwnTicketStatusGroup
{
    Active = 1,
    Finished = 2
}
