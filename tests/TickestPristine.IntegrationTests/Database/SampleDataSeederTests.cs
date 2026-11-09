using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

public sealed class SampleDataSeederTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly IntegrationTestWebAppFactory _factory = new(enableSampleData: true);
    private bool _disposed;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => DisposeFactoryAsync();

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeFactoryAsync());

    [Fact]
    public async Task Migrate_ShouldCreateSampleTicketsWithCycleOneTimers_WhenSampleDataIsEnabled()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<Ticket> tickets = await context.Tickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(ticket => ticket.SlaTimers)
            .ToListAsync();

        tickets.ShouldNotBeEmpty();
        tickets.ShouldAllBe(ticket => ticket.SlaTimers.Any(timer => timer.Cycle == 1));
        tickets.ShouldAllBe(ticket => ticket.SlaTimers.Any(
            timer => timer.Cycle == 1 && timer.Metric == SlaMetric.Resolution));
    }

    private async Task DisposeFactoryAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _factory.DisposeAsync();
    }
}
