using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Sectors.GetAll;

public sealed record GetSectorsQuery : IQuery<List<SectorResponse>>;
