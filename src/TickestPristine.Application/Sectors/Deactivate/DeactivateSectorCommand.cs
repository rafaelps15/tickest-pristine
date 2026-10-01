using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.Deactivate;

public sealed record DeactivateSectorCommand(Guid SectorId) : ICommand;
