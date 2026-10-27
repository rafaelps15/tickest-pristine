using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Despacho dos eventos depois do commit: a falha de um handler vai para o log e não interrompe os demais nem a
/// requisição. É o que mantém a exclusão lógica de um anexo quando a remoção do arquivo falha (spec 011).
/// </summary>
public sealed class DomainEventsDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_Should_LogErrorAndRunOtherHandlers_WhenHandlerFails()
    {
        // Arrange
        var recorder = new RecordingHandler();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>, FailingHandler>()
            .AddScoped<IDomainEventHandler<TicketAttachmentDeletedDomainEvent>>(_ => recorder)
            .BuildServiceProvider();
        var logger = new CapturingLogger();
        var dispatcher = new DomainEventsDispatcher(services, logger);
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

    private sealed class CapturingLogger : ILogger<DomainEventsDispatcher>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
