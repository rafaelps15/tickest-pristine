using TickestPristine.Application.Roles;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Users;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Abstractions;

public abstract class BaseHandlerTest
{
    protected static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"tickestpristine-{Guid.NewGuid()}")
            .Options;

        return new TestDbContext(options);
    }

    // Instante padrão dos testes que não dependem de um horário: segunda-feira, 26/10/2026, 08:10 em São Paulo.
    protected static readonly DateTime DefaultUtcNow = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    protected static IDateTimeProvider CreateClock(DateTime? utcNow = null)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow ?? DefaultUtcNow);

        return clock;
    }

    protected static ISlaCalendarProvider CreateSlaCalendarProvider() => new TestSlaCalendarProvider();

    // Plano "Padrão" de um chamado Urgente, no calendário "Expediente".
    protected static TicketSlaPlan CreateSlaPlan() => TicketSla.DefaultPlan(TicketPriority.Urgent);

    // Chamado Urgente aberto em createdAtUtc com o plano de CreateSlaPlan e os
    // cronômetros do ciclo 1, como a abertura pela API grava.
    protected static Task<Ticket> SeedTicketWithSlaAsync(
        TestDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        TicketStatus status = TicketStatus.Open) =>
        SeedTicketWithSlaAsync(context, requesterId, createdAtUtc, status, resolvedAtUtc: null);

    // Chamado de SeedTicketWithSlaAsync já resolvido em resolvedAtUtc, com os dois
    // cronômetros concluídos nesse instante.
    protected static async Task<Ticket> SeedResolvedTicketWithSlaAsync(
        TestDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        DateTime resolvedAtUtc)
    {
        Ticket ticket = await SeedTicketWithSlaAsync(context, requesterId, createdAtUtc, TicketStatus.Resolved, resolvedAtUtc);

        foreach (TicketSlaTimer timer in context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id))
        {
            TicketSlaClock.Complete(timer, resolvedAtUtc, SlaCompletionCause.Resolution);
        }

        await context.SaveChangesAsync();

        return ticket;
    }

    private static async Task<Ticket> SeedTicketWithSlaAsync(
        TestDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        TicketStatus status,
        DateTime? resolvedAtUtc)
    {
        TicketSlaPlan plan = CreateSlaPlan();

        // Os prazos do chamado vêm do cronômetro de resolução; o id do chamado entra depois, como na abertura pela API.
        List<TicketSlaTimer> timers = TicketSlaClock.Start(Guid.Empty, plan, DefaultSlaCalendar.Create(), createdAtUtc);
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);

        Ticket ticket = TicketTestData.Create(
            requesterId: requesterId,
            status: status,
            createdAtUtc: createdAtUtc,
            priority: TicketPriority.Urgent,
            slaPlan: plan,
            dueAtUtc: resolution.DueOnUtc,
            slaWarningAtUtc: resolution.WarningOnUtc,
            resolvedAtUtc: resolvedAtUtc);

        timers.ForEach(timer => timer.TicketId = ticket.Id);

        context.Tickets.Add(ticket);
        context.TicketSlaTimers.AddRange(timers);
        await context.SaveChangesAsync();

        return ticket;
    }

    protected static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    // Usuário válido para os testes, com e-mail único; os demais campos podem ser ajustados depois de criado.
    protected static User CreateUser(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@example.com",
        FirstName = "Test",
        LastName = "User",
        Code = UserCodes.New(),
        IsActive = isActive,
        CreatedAtUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
    };

    // Role com qualquer nome, como as que já existem no banco além das fixas de RoleNames.
    protected static Role CreateRole(string name) => new() { Id = Guid.NewGuid(), Name = name };

    // Setor num departamento novo, gravados no banco, para os testes que leem o setor do usuário.
    protected static async Task<Sector> SeedSectorAsync(
        TestDbContext context,
        string sectorName,
        string departmentName,
        bool isActive = true)
    {
        var department = Department.Create(departmentName, departmentName, responsibleUser: null);
        var sector = Sector.Create(department, sectorName, description: null);

        if (!isActive)
        {
            sector.Deactivate();
        }

        context.Departments.Add(department);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector;
    }

    // Dá ao usuário uma role nova com as permissões informadas, gravada no banco (como a elegibilidade de atendente
    // lê, sem o cache de permissões).
    protected static async Task GrantPermissionsAsync(TestDbContext context, Guid userId, params string[] permissionCodes)
    {
        Role role = CreateRole($"Role-{Guid.NewGuid():N}");

        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleName = role.Name });
        context.RolePermissions.AddRange(permissionCodes.Select(code => new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleName = role.Name,
            PermissionCode = code
        }));

        await context.SaveChangesAsync();
    }

    protected static ISlaCalendarProvider CreateMissingSlaCalendarProvider()
    {
        ISlaCalendarProvider provider = Substitute.For<ISlaCalendarProvider>();
        provider.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Failure<BusinessCalendar>(SlaCalendarErrors.NotFound(call.Arg<Guid>())));

        return provider;
    }
}
