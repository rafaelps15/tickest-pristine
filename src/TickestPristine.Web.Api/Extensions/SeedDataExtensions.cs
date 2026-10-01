using System.Data;
using Bogus;
using Dapper;
using Npgsql;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Web.Api.Extensions;

/// <summary>
/// Popula departamentos, setores e chamados com dados fictícios. Uso exclusivo em desenvolvimento.
/// </summary>
public static class SeedDataExtensions
{
    public static void SeedData(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        using IDbConnection connection = new NpgsqlConnection(configuration.GetConnectionString("Database"));

        if (connection.ExecuteScalar<bool>("SELECT EXISTS (SELECT 1 FROM public.departments)"))
        {
            return;
        }

        var faker = new Faker("pt_BR");

        List<object> departments = [];
        List<object> sectors = [];
        List<Guid> sectorIds = [];
        for (int i = 0; i < 5; i++)
        {
            var departmentId = Guid.NewGuid();

            departments.Add(new
            {
                Id = departmentId,
                Name = faker.Commerce.Department(),
                Description = faker.Company.CatchPhrase(),
                IsActive = true
            });

            for (int j = 0; j < 2; j++)
            {
                var sectorId = Guid.NewGuid();

                sectorIds.Add(sectorId);
                sectors.Add(new
                {
                    Id = sectorId,
                    Name = faker.Commerce.ProductName(),
                    Description = faker.Lorem.Sentence(),
                    IsActive = true,
                    DepartmentId = departmentId
                });
            }
        }

        Guid[] userIds = connection.Query<Guid>("SELECT id FROM public.users").ToArray();

        List<object> tickets = [];
        for (int i = 0; i < 50; i++)
        {
            tickets.Add(new
            {
                Id = Guid.NewGuid(),
                Title = faker.Lorem.Sentence(4),
                Description = faker.Lorem.Paragraph(),
                Priority = (int)faker.PickRandom<TicketPriority>(),
                Status = (int)faker.PickRandom<TicketStatus>(),
                CreatedByUserId = faker.PickRandom(userIds),
                SectorId = faker.PickRandom(sectorIds),
                CreatedAtUtc = faker.Date.Past(1, DateTime.UtcNow)
            });
        }

        const string departmentsSql = """
            INSERT INTO public.departments
            (id, "name", description, is_active)
            VALUES(@Id, @Name, @Description, @IsActive);
            """;

        const string sectorsSql = """
            INSERT INTO public.sectors
            (id, "name", description, is_active, department_id)
            VALUES(@Id, @Name, @Description, @IsActive, @DepartmentId);
            """;

        const string ticketsSql = """
            INSERT INTO public.tickets
            (id, title, description, priority, status, created_by_user_id, sector_id, created_at_utc)
            VALUES(@Id, @Title, @Description, @Priority, @Status, @CreatedByUserId, @SectorId, @CreatedAtUtc);
            """;

        connection.Execute(departmentsSql, departments);
        connection.Execute(sectorsSql, sectors);
        connection.Execute(ticketsSql, tickets);
    }
}
