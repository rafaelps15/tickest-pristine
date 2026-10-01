using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.Update;

public sealed class UpdateSectorCommand : ICommand
{
    public Guid SectorId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
}
