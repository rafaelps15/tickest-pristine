using Bogus;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Chamados fictícios para desenvolvimento, distribuídos pelos setores ativos que já existem.
/// Só roda num banco sem chamados e com pelo menos um setor ativo.
/// </summary>
internal static class SampleDataSeeder
{
    private const int TicketCount = 50;

    public static void Seed(DbContext context)
    {
        if (context.Set<Ticket>().IgnoreQueryFilters().Any())
        {
            return;
        }

        Guid[] sectorIds = context.Set<Sector>().Where(s => s.IsActive).Select(s => s.Id).ToArray();
        Guid[] userIds = context.Set<User>().Select(u => u.Id).ToArray();

        if (sectorIds.Length == 0 || userIds.Length == 0)
        {
            return;
        }

        AddSampleTickets(context, sectorIds, userIds);

        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (await context.Set<Ticket>().IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        Guid[] sectorIds = await context.Set<Sector>().Where(s => s.IsActive).Select(s => s.Id).ToArrayAsync(cancellationToken);
        Guid[] userIds = await context.Set<User>().Select(u => u.Id).ToArrayAsync(cancellationToken);

        if (sectorIds.Length == 0 || userIds.Length == 0)
        {
            return;
        }

        AddSampleTickets(context, sectorIds, userIds);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddSampleTickets(DbContext context, Guid[] sectorIds, Guid[] userIds)
    {
        var faker = new Faker("pt_BR");

        for (int i = 0; i < TicketCount; i++)
        {
            context.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                Title = faker.Lorem.Sentence(4),
                Description = faker.Lorem.Sentences(2),
                Priority = faker.PickRandom<TicketPriority>(),
                Status = faker.PickRandom<TicketStatus>(),
                CreatedByUserId = faker.PickRandom(userIds),
                SectorId = faker.PickRandom(sectorIds),
                CreatedAtUtc = faker.Date.Past(1, DateTime.UtcNow)
            });
        }
    }
}
