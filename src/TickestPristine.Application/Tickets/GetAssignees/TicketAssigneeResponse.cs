namespace TickestPristine.Application.Tickets.GetAssignees;

/// <summary>
/// Responsável elegível na lista de escolha. Traz só o necessário para escolher, sem e-mail nem outros dados de
/// usuário (spec 030, DEC-1). Sem setor, os dois campos de setor vêm nulos.
/// </summary>
public sealed class TicketAssigneeResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public Guid? SectorId { get; set; }
    public string? SectorName { get; set; }
}
