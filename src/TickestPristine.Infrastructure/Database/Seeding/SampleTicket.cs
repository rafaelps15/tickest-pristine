using TickestPristine.Domain.Tickets;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Um chamado do catálogo de exemplo: o setor é localizado pelo nome.
/// </summary>
internal sealed record SampleTicket(string SectorName, string Title, string Description, TicketPriority Priority);
