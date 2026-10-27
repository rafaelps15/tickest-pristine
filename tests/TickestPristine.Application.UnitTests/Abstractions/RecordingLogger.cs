using Microsoft.Extensions.Logging;

namespace TickestPristine.Application.UnitTests.Abstractions;

/// <summary>
/// Logger que guarda nível, mensagem e exceção de cada registro, para os testes conferirem o que foi registrado.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLogEntry> _entries = [];

    public IReadOnlyList<RecordedLogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
}

public sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception);
