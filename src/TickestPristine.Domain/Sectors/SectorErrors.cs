using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public static class SectorErrors
{
    public static Error NotFound(Guid sectorId) => Error.NotFound(
        "Sectors.NotFound",
        $"The sector with the Id = '{sectorId}' was not found");
}
