using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public static class SectorErrors
{
    public static Error NotFound(Guid sectorId) => Error.NotFound(
        "Sectors.NotFound",
        $"O setor com o Id = '{sectorId}' não foi encontrado");
}
