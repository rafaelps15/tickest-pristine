using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Chamados de exemplo para desenvolvimento, com "Seeding:SampleData" ligado: os do <see cref="SampleTicketCatalog"/>
/// cujo setor existe e está ativo, abertos por usuários ativos. Só roda num banco sem chamados. Cada chamado nasce com
/// o histórico que a API teria gravado até o status dele e os prazos de SLA da prioridade.
/// </summary>
internal sealed class SampleDataSeeder(IOptions<SeedingOptions> seedingOptions, IDateTimeProvider dateTimeProvider)
{
    // Semente fixa: todo ambiente recebe a mesma massa de dados, o que ajuda a reproduzir bugs.
    private const int RandomSeed = 2026;

    /// <summary>
    /// Status que cada chamado percorre a partir de Aberto até o status final (cada passo é conferido em CreateHistory).
    /// </summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> StatusPaths = new()
    {
        [TicketStatus.Open] = [],
        [TicketStatus.InProgress] = [TicketStatus.InProgress],
        [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Closed] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Canceled] = [TicketStatus.Canceled]
    };

    public async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!seedingOptions.Value.SampleData || await context.Tickets.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        Dictionary<string, Guid> sectorIdsByName = await context.Sectors
            .Where(s => s.IsActive)
            .ToDictionaryAsync(s => s.Name, s => s.Id, cancellationToken);
        Guid[] userIds = await context.Users.Where(u => u.IsActive).Select(u => u.Id).ToArrayAsync(cancellationToken);

        if (userIds.Length == 0)
        {
            return;
        }

        var faker = new Faker("pt_BR") { Random = new Randomizer(RandomSeed) };
        DateTime now = dateTimeProvider.UtcNow;

        // Setores renomeados ou desativados pelo administrador ficam sem chamados de exemplo.
        foreach (SampleTicket sampleTicket in SampleTicketCatalog.Tickets)
        {
            if (!sectorIdsByName.TryGetValue(sampleTicket.SectorName, out Guid sectorId))
            {
                continue;
            }

            Ticket ticket = CreateTicket(faker, sampleTicket, sectorId, userIds, now);

            context.Tickets.Add(ticket);
            context.TicketHistories.AddRange(CreateHistory(faker, ticket, now));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Ticket CreateTicket(Faker faker, SampleTicket sampleTicket, Guid sectorId, Guid[] userIds, DateTime now)
    {
        TicketStatus status = faker.PickRandom<TicketStatus>();
        TimeSpan resolutionTime = TicketSla.ResolutionTime(sampleTicket.Priority);

        // Os ativos nascem dentro ou pouco além da janela do SLA, para o painel ter chamados no prazo, em risco e
        // atrasados. Os demais nascem no último ano, até uma semana atrás, para caberem a resolução e o fechamento.
        DateTime createdAtUtc = TicketStatusTransitions.IsActive(status)
            ? now - resolutionTime * faker.Random.Double(0.1, 1.5)
            : faker.Date.Past(1, now.AddDays(-7));

        // Resolvidos dentro ou fora do prazo, para o painel mostrar SLA cumprido e violado.
        DateTime? resolvedAtUtc = StatusPaths[status].Contains(TicketStatus.Resolved)
            ? createdAtUtc + resolutionTime * faker.Random.Double(0.3, 1.4)
            : null;

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = sampleTicket.Title,
            Description = sampleTicket.Description,
            Priority = sampleTicket.Priority,
            Status = status,
            CreatedByUserId = faker.PickRandom(userIds),
            // Chamado que passou por "Em andamento" teve alguém atendendo.
            AssignedToUserId = StatusPaths[status].Contains(TicketStatus.InProgress) ? faker.PickRandom(userIds) : null,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = TicketSla.DueAt(createdAtUtc, sampleTicket.Priority),
            SlaWarningAtUtc = TicketSla.WarningAt(createdAtUtc, sampleTicket.Priority),
            ResolvedAtUtc = resolvedAtUtc
        };
    }

    /// <summary>
    /// Histórico igual ao que os handlers de evento de domínio gravam: a criação e cada mudança de status, em ordem.
    /// Cada passo é conferido com TicketStatusTransitions, para o seed nunca gerar um histórico que a API recusaria.
    /// </summary>
    private static IEnumerable<TicketHistory> CreateHistory(Faker faker, Ticket ticket, DateTime now)
    {
        DateTime occurredAtUtc = ticket.CreatedAtUtc;

        yield return TicketHistoryEntries.Created(ticket.Id, ticket.CreatedByUserId, occurredAtUtc);

        TicketStatus oldStatus = TicketStatus.Open;

        foreach (TicketStatus newStatus in StatusPaths[ticket.Status])
        {
            if (!TicketStatusTransitions.CanTransition(oldStatus, newStatus))
            {
                throw new InvalidOperationException(
                    $"Sample status path is not allowed by TicketStatusTransitions: {oldStatus} -> {newStatus}.");
            }

            // A resolução cai na data gravada no chamado; o que vem antes dela fica entre a abertura e a resolução.
            occurredAtUtc = newStatus switch
            {
                TicketStatus.Resolved => ticket.ResolvedAtUtc!.Value,
                TicketStatus.Closed => faker.Date.Between(occurredAtUtc, occurredAtUtc.AddDays(2)),
                _ => faker.Date.Between(occurredAtUtc, ticket.ResolvedAtUtc ?? now)
            };

            yield return TicketHistoryEntries.StatusChanged(
                ticket.Id,
                ticket.AssignedToUserId ?? ticket.CreatedByUserId,
                oldStatus,
                newStatus,
                occurredAtUtc);

            oldStatus = newStatus;
        }
    }
}
