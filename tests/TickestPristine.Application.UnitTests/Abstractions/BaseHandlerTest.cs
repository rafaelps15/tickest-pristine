using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
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

    /// <summary>
    /// Instante padrão dos testes que não dependem de um horário: segunda-feira, 26/10/2026, 08:10 em São Paulo.
    /// </summary>
    protected static readonly DateTime DefaultUtcNow = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    /// <summary>
    /// Relógio parado no instante informado (ou no <see cref="DefaultUtcNow"/>).
    /// </summary>
    protected static IDateTimeProvider CreateClock(DateTime? utcNow = null)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow ?? DefaultUtcNow);

        return clock;
    }

    protected static ISlaCalendarProvider CreateSlaCalendarProvider() => new TestSlaCalendarProvider();

    /// <summary>
    /// Plano "Padrão" de um chamado Urgente, no calendário "Expediente".
    /// </summary>
    protected static TicketSlaPlan CreateSlaPlan() => TicketSla.DefaultPlan(TicketPriority.Urgent);

    /// <summary>
    /// Chamado Urgente aberto em <paramref name="createdAtUtc"/> com o plano de <see cref="CreateSlaPlan"/> e os
    /// cronômetros do ciclo 1, como a abertura pela API grava.
    /// </summary>
    protected static async Task<Ticket> SeedTicketWithSlaAsync(
        TestDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        TicketStatus status = TicketStatus.Open)
    {
        TicketSlaPlan plan = CreateSlaPlan();
        var ticketId = Guid.NewGuid();
        List<TicketSlaTimer> timers = TicketSlaClock.Start(ticketId, plan, DefaultSlaCalendar.Create(), createdAtUtc);
        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);

        var ticket = new Ticket
        {
            Id = ticketId,
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Urgent,
            Status = status,
            CreatedByUserId = requesterId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = resolution.DueOnUtc,
            SlaWarningAtUtc = resolution.WarningOnUtc,
            SlaPlan = plan
        };

        context.Tickets.Add(ticket);
        context.TicketSlaTimers.AddRange(timers);
        await context.SaveChangesAsync();

        return ticket;
    }

    /// <summary>
    /// Chamado de <see cref="SeedTicketWithSlaAsync"/> já resolvido em <paramref name="resolvedAtUtc"/>, com os dois
    /// cronômetros concluídos nesse instante.
    /// </summary>
    protected static async Task<Ticket> SeedResolvedTicketWithSlaAsync(
        TestDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        DateTime resolvedAtUtc)
    {
        Ticket ticket = await SeedTicketWithSlaAsync(context, requesterId, createdAtUtc, TicketStatus.Resolved);
        ticket.ResolvedAtUtc = resolvedAtUtc;

        foreach (TicketSlaTimer timer in context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id))
        {
            TicketSlaClock.Complete(timer, resolvedAtUtc);
        }

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

    /// <summary>
    /// Usuário válido para os testes, com e-mail único; os demais campos podem ser ajustados depois de criado.
    /// </summary>
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

    /// <summary>
    /// Role com qualquer nome, como as que já existem no banco além das fixas de <see cref="RoleNames"/>.
    /// </summary>
    protected static Role CreateRole(string name) => new() { Id = Guid.NewGuid(), Name = name };

    /// <summary>
    /// Provedor em que nenhum calendário existe, para os caminhos de falha do calendário do plano.
    /// </summary>
    protected static ISlaCalendarProvider CreateMissingSlaCalendarProvider()
    {
        ISlaCalendarProvider provider = Substitute.For<ISlaCalendarProvider>();
        provider.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Failure<BusinessCalendar>(SlaCalendarErrors.NotFound(call.Arg<Guid>())));

        return provider;
    }
}
