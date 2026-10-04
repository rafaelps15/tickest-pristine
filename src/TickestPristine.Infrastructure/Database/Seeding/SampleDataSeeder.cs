using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Chamados fictícios para desenvolvimento, com "Seeding:SampleData" ligado, distribuídos pelos usuários e setores
/// ativos. Só roda num banco sem chamados. Cada chamado nasce com o histórico que a API teria gravado até o status dele.
/// </summary>
internal sealed class SampleDataSeeder(IOptions<SeedingOptions> seedingOptions, IDateTimeProvider dateTimeProvider)
{
    private const int TicketCount = 50;

    // Semente fixa: todo ambiente recebe a mesma massa de dados, o que ajuda a reproduzir bugs.
    private const int RandomSeed = 2026;

    /// <summary>
    /// Status que cada chamado percorre a partir de Aberto até o status final, seguindo TicketStatusTransitions.
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

        Guid[] sectorIds = await context.Sectors.Where(s => s.IsActive).Select(s => s.Id).ToArrayAsync(cancellationToken);
        Guid[] userIds = await context.Users.Where(u => u.IsActive).Select(u => u.Id).ToArrayAsync(cancellationToken);

        if (sectorIds.Length == 0 || userIds.Length == 0)
        {
            return;
        }

        var faker = new Faker("pt_BR") { Random = new Randomizer(RandomSeed) };
        DateTime now = dateTimeProvider.UtcNow;

        for (int i = 0; i < TicketCount; i++)
        {
            Ticket ticket = CreateTicket(faker, sectorIds, userIds, now);

            context.Tickets.Add(ticket);
            context.TicketHistories.AddRange(CreateHistory(faker, ticket, now));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Ticket CreateTicket(Faker faker, Guid[] sectorIds, Guid[] userIds, DateTime now)
    {
        TicketStatus status = faker.PickRandom<TicketStatus>();

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = faker.Lorem.Sentence(4),
            Description = faker.Lorem.Sentences(2),
            Priority = faker.PickRandom<TicketPriority>(),
            Status = status,
            CreatedByUserId = faker.PickRandom(userIds),
            // Chamado que passou por "Em andamento" teve alguém atendendo.
            AssignedToUserId = StatusPaths[status].Contains(TicketStatus.InProgress) ? faker.PickRandom(userIds) : null,
            SectorId = faker.PickRandom(sectorIds),
            CreatedAtUtc = faker.Date.Past(1, now)
        };
    }

    /// <summary>
    /// Histórico igual ao que os handlers de evento de domínio gravam: a criação e cada mudança de status, em ordem.
    /// </summary>
    private static IEnumerable<TicketHistory> CreateHistory(Faker faker, Ticket ticket, DateTime now)
    {
        DateTime occurredAtUtc = ticket.CreatedAtUtc;

        yield return new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ChangedByUserId = ticket.CreatedByUserId,
            Action = TicketHistoryAction.Created,
            Description = TicketHistoryDescriptions.Created,
            NewValue = TicketStatus.Open.ToString(),
            OccurredAtUtc = occurredAtUtc
        };

        TicketStatus oldStatus = TicketStatus.Open;

        foreach (TicketStatus newStatus in StatusPaths[ticket.Status])
        {
            occurredAtUtc = faker.Date.Between(occurredAtUtc, now);

            yield return new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ChangedByUserId = ticket.AssignedToUserId ?? ticket.CreatedByUserId,
                Action = TicketHistoryAction.StatusChanged,
                Description = TicketHistoryDescriptions.StatusChanged(oldStatus, newStatus),
                OldValue = oldStatus.ToString(),
                NewValue = newStatus.ToString(),
                OccurredAtUtc = occurredAtUtc
            };

            oldStatus = newStatus;
        }
    }
}
