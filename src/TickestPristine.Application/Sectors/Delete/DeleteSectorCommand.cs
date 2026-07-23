using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.Delete;

public sealed record DeleteSectorCommand(Guid SectorId) : ICommand;
