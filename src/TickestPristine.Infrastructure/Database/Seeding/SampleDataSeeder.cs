using Bogus;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Dados fictícios de departamentos, setores e chamados para desenvolvimento. Só roda num banco sem departamentos.
/// </summary>
internal static class SampleDataSeeder
{
    private const int DepartmentCount = 5;
    private const int SectorsPerDepartment = 2;
    private const int TicketCount = 50;

    public static void Seed(DbContext context)
    {
        if (context.Set<Department>().Any())
        {
            return;
        }

        Guid[] userIds = context.Set<User>().Select(u => u.Id).ToArray();

        AddSampleData(context, userIds);

        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (await context.Set<Department>().AnyAsync(cancellationToken))
        {
            return;
        }

        Guid[] userIds = await context.Set<User>().Select(u => u.Id).ToArrayAsync(cancellationToken);

        AddSampleData(context, userIds);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddSampleData(DbContext context, Guid[] userIds)
    {
        var faker = new Faker("pt_BR");
        var sectorIds = new List<Guid>();

        for (int i = 0; i < DepartmentCount; i++)
        {
            var department = new Department
            {
                Id = Guid.NewGuid(),
                Name = faker.Commerce.Department(),
                Description = faker.Company.CatchPhrase(),
                IsActive = true
            };

            context.Add(department);

            for (int j = 0; j < SectorsPerDepartment; j++)
            {
                var sector = new Sector
                {
                    Id = Guid.NewGuid(),
                    Name = faker.Commerce.ProductName(),
                    Description = faker.Lorem.Sentence(),
                    IsActive = true,
                    DepartmentId = department.Id
                };

                context.Add(sector);
                sectorIds.Add(sector.Id);
            }
        }

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
