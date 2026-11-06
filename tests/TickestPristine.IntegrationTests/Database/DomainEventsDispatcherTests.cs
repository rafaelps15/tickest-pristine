using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TickestPristine.Application.Abstractions.Telemetry;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketAttachments;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Despacho dos eventos depois do commit: a falha de um handler vai para o log e não interrompe os demais nem a
/// requisição. É o que mantém a exclusão lógica de um anexo quando a remoção do arquivo falha (spec 011).
/// </summary>
public sealed class DomainEventsDispatcherTests
{
    private const string HandlerFailures = "tickest.domain_event.handler_failures";

    [Fact]
    public async Task DispatchAsync_Should_LogErrorAndRunOtherHandlers_WhenHandlerFails()
    {
        // Arrange
        var recorder = new RecordingHandler();
        await using ServiceProvider services = ServicesWithTelemetry()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>, FailingHandler>()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>>(_ => recorder)
            .BuildServiceProvider();
        var logger = new TestLogger<DomainEventsDispatcher>();
        DomainEventsDispatcher dispatcher = CreateDispatcher(services, logger);
        var domainEvent = new TicketAttachmentDeletedDomainEvent(Guid.NewGuid(), Guid.NewGuid());

        // Act
        Exception? exception = await Record.ExceptionAsync(() => dispatcher.DispatchAsync([domainEvent]));

        // Assert
        exception.ShouldBeNull();
        recorder.Handled.ShouldHaveSingleItem().ShouldBe(domainEvent);
        (LogLevel level, string message, Exception? error) = logger.Entries.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Error);
        message.ShouldContain(nameof(FailingHandler));
        message.ShouldContain(nameof(TicketAttachmentDeletedDomainEvent));
        error.ShouldBeOfType<IOException>();
    }

    [Fact]
    public async Task DispatchAsync_Should_IncrementHandlerFailureCounter_When_HandlerThrows()
    {
        // Arrange
        await using ServiceProvider services = ServicesWithTelemetry()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>, FailingHandler>()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>, RecordingHandler>()
            .BuildServiceProvider();
        using var measurements = new MeasurementRecorder(services.GetRequiredService<IMeterFactory>());
        DomainEventsDispatcher dispatcher = CreateDispatcher(services, new TestLogger<DomainEventsDispatcher>());

        // Act
        await dispatcher.DispatchAsync([new TicketAttachmentDeletedDomainEvent(Guid.NewGuid(), Guid.NewGuid())]);

        // Assert: só o handler que lançou conta, com o evento e o nome dele
        measurements.Sum(HandlerFailures).ShouldBe(1);
        measurements.Sum(HandlerFailures, "tickest.domain_event.handler", nameof(FailingHandler)).ShouldBe(1);
        measurements.Sum(HandlerFailures, "tickest.domain_event.name", nameof(TicketAttachmentDeletedDomainEvent)).ShouldBe(1);
    }

    private static IServiceCollection ServicesWithTelemetry() =>
        new ServiceCollection()
            .AddMetrics()
            .AddSingleton<ApplicationTelemetry>();

    private static DomainEventsDispatcher CreateDispatcher(ServiceProvider services, TestLogger<DomainEventsDispatcher> logger) =>
        new(services, services.GetRequiredService<ApplicationTelemetry>(), logger);

    private sealed class FailingHandler : IDomainEventHandler<TicketAttachmentDeletedDomainEvent>
    {
        public Task Handle(TicketAttachmentDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
            throw new IOException("Disco indisponível");
    }

    private sealed class RecordingHandler : IDomainEventHandler<TicketAttachmentDeletedDomainEvent>
    {
        public List<TicketAttachmentDeletedDomainEvent> Handled { get; } = [];

        public Task Handle(TicketAttachmentDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            Handled.Add(domainEvent);

            return Task.CompletedTask;
        }
    }
}
