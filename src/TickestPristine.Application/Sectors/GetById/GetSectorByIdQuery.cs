using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.GetById;

public sealed record GetSectorByIdQuery(Guid SectorId) : IQuery<SectorResponse>;
