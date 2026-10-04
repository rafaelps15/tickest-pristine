using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public static class SectorErrors
{
    public static Error NotFound(Guid sectorId) => Error.NotFound(
        "Sectors.NotFound",
        $"O setor com o Id = '{sectorId}' não foi encontrado");

    public static Error NameNotUnique(string name) => Error.Conflict(
        "Sectors.NameNotUnique",
        $"Já existe um setor ativo com o nome '{name}' neste departamento.");

    public static Error HasActiveTickets() => Error.Conflict(
        "Sectors.HasActiveTickets",
        "O setor tem chamados em aberto ou em andamento. Conclua ou cancele esses chamados antes de desativá-lo.");
}
