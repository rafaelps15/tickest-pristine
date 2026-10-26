using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.ProcessSlaBreaches;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.Tickets.SlaMonitoring;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Tickets;

/// <summary>
/// Laço do serviço em background do monitoramento do SLA: espera entre as voltas, falha isolada, desligamento,
/// registros e configuração.
/// </summary>
public sealed class SlaMonitorBackgroundServiceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    private SlaMonitorBackgroundService CreateService(
        IServiceProvider serviceProvider,
        TestLogger<SlaMonitorBackgroundService> logger,
        bool enabled = true) =>
        new(
            logger,
            Options.Create(new SlaMonitorSettings { Enabled = enabled, BatchSize = 1, SleepTimeInMilliseconds = 1000 }),
            serviceProvider,
            Services.GetRequiredService<NpgsqlDataSource>());

    private static ServiceProvider CreateProviderWith(ScriptedProcessSlaBreachesHandlerState state)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<ICommandHandler<ProcessSlaBreachesCommand, ProcessSlaBreachesResponse>, ScriptedProcessSlaBreachesHandler>();

        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Should_LogCriticalAndKeepRunning_WhenABatchFails_AndStopWithoutErrors()
    {
        // Arrange: a primeira volta falha; a segunda funciona.
        var state = new ScriptedProcessSlaBreachesHandlerState(failingCalls: 1);
        await using ServiceProvider provider = CreateProviderWith(state);
        var logger = new TestLogger<SlaMonitorBackgroundService>();
        using SlaMonitorBackgroundService service = CreateService(provider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => state.Calls >= 2);
        await service.StopAsync(CancellationToken.None);

        // Assert
        state.Calls.ShouldBeGreaterThanOrEqualTo(2);

        TestLogEntry critical = logger.Entries.Where(e => e.Level >= LogLevel.Error).ShouldHaveSingleItem();
        critical.Level.ShouldBe(LogLevel.Critical);
        critical.Exception.ShouldBeOfType<InvalidOperationException>();

        logger.Entries.ShouldContain(e => e.Level == LogLevel.Debug && e.Message.Contains("está parando", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Should_LogOnlyDebugMessagesOfTheLoop_WhenBatchesSucceed()
    {
        // Arrange
        var state = new ScriptedProcessSlaBreachesHandlerState(failingCalls: 0);
        await using ServiceProvider provider = CreateProviderWith(state);
        var logger = new TestLogger<SlaMonitorBackgroundService>();
        using SlaMonitorBackgroundService service = CreateService(provider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => state.Calls >= 2);
        await service.StopAsync(CancellationToken.None);

        // Assert: partida, cada volta e parada, em Debug; nada por cronômetro nem acima de Debug.
        logger.Entries.ShouldAllBe(e => e.Level == LogLevel.Debug);
        logger.Entries.Select(e => e.Message).Distinct().Order().ShouldBe(
        [
            "A tarefa em background do SlaMonitorBackgroundService está parando.",
            "A tarefa em background do SlaMonitorBackgroundService está processando.",
            "SlaMonitorBackgroundService está iniciando."
        ]);
        logger.Entries.Count(e => e.Message.Contains("processando", StringComparison.Ordinal)).ShouldBe(state.Calls);
    }

    [Fact]
    public void Startup_Should_Fail_WhenBatchSizeIsZero()
    {
        // Arrange
        using WebApplicationFactoryWithSetting invalidFactory = new(_factory, "SlaMonitor:BatchSize", "0");

        // Act
        Exception exception = Should.Throw<Exception>(() => invalidFactory.CreateClient());

        // Assert
        Exception? current = exception;

        while (current is not null and not OptionsValidationException)
        {
            current = current.InnerException;
        }

        current.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain("BatchSize");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProcessNothing_WhenDisabled()
    {
        // Arrange: um cronômetro vencido de verdade, no banco.
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid requesterId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync();
        Guid sectorId = await context.Sectors.Select(s => s.Id).FirstAsync();
        DateTime createdAtUtc = DateTime.UtcNow.AddHours(-5);
        var ticketId = Guid.NewGuid();

        context.Tickets.Add(new Ticket
        {
            Id = ticketId,
            Title = "Overdue ticket",
            Description = "Opened five hours ago",
            Priority = TicketPriority.Urgent,
            Status = TicketStatus.Open,
            CreatedByUserId = requesterId,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = createdAtUtc.AddHours(4),
            SlaWarningAtUtc = createdAtUtc.AddHours(3),
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Urgent)
        });
        context.TicketSlaTimers.Add(new TicketSlaTimer
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Metric = SlaMetric.Resolution,
            Cycle = 1,
            TargetMinutes = 240,
            CalendarId = SlaCalendarIds.Continuous,
            StartedAtUtc = createdAtUtc,
            WarningOnUtc = createdAtUtc.AddHours(3),
            DueOnUtc = createdAtUtc.AddHours(4),
            NextActionOnUtc = createdAtUtc.AddHours(4)
        });
        await context.SaveChangesAsync();

        var logger = new TestLogger<SlaMonitorBackgroundService>();
        using SlaMonitorBackgroundService service = CreateService(Services, logger, enabled: false);

        try
        {
            // Act: duas esperas inteiras com o serviço desligado.
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(2.5));
            await service.StopAsync(CancellationToken.None);

            // Assert
            TicketSlaTimer timer = await context.TicketSlaTimers.AsNoTracking().SingleAsync(t => t.TicketId == ticketId);
            timer.BreachedOnUtc.ShouldBeNull();
            timer.NextActionOnUtc.ShouldBe(timer.DueOnUtc);
            logger.Entries.ShouldBeEmpty();
        }
        finally
        {
            // Tira o cronômetro da fila, para não sobrar trabalho para os outros testes.
            await context.TicketSlaTimers
                .Where(t => t.TicketId == ticketId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.NextActionOnUtc, (DateTime?)null));
        }
    }
}

/// <summary>
/// Fábrica derivada com uma configuração a mais, para conferir a subida da API com um valor inválido.
/// </summary>
internal sealed class WebApplicationFactoryWithSetting(IntegrationTestWebAppFactory factory, string key, string value) : IDisposable
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _derived =
        factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

    public HttpClient CreateClient() => _derived.CreateClient();

    public void Dispose() => _derived.Dispose();
}

/// <summary>
/// Estado do handler de teste: quantas voltas o chamaram e quantas das primeiras devem falhar.
/// </summary>
public sealed class ScriptedProcessSlaBreachesHandlerState(int failingCalls)
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public int NextCall() => Interlocked.Increment(ref _calls);

    public bool ShouldFail(int call) => call <= failingCalls;
}

/// <summary>
/// Handler de teste do <see cref="ProcessSlaBreachesCommand"/>, para exercitar o laço do serviço sem banco.
/// </summary>
internal sealed class ScriptedProcessSlaBreachesHandler(ScriptedProcessSlaBreachesHandlerState state)
    : ICommandHandler<ProcessSlaBreachesCommand, ProcessSlaBreachesResponse>
{
    public Task<Result<ProcessSlaBreachesResponse>> Handle(ProcessSlaBreachesCommand command, CancellationToken cancellationToken)
    {
        int call = state.NextCall();

        if (state.ShouldFail(call))
        {
            throw new InvalidOperationException($"Falha simulada na volta {call}.");
        }

        return Task.FromResult(Result.Success(new ProcessSlaBreachesResponse(Processed: 1, Breached: 0)));
    }
}
