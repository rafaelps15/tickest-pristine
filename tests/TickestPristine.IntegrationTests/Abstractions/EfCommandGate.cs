using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Interceptor do EF Core que conta os comandos cujo texto atende a uma condição e pode prender o próximo deles, depois de
/// executado no banco, até o teste liberar. Só entra nos hosts de teste que precisam dele, pelo <c>ConfigureDbContext</c>.
/// </summary>
internal sealed class EfCommandGate(Func<string, bool> matches) : DbCommandInterceptor
{
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;
    private int _matchedCount;

    public int MatchedCount => Volatile.Read(ref _matchedCount);

    /// <summary>
    /// Prende o próximo comando que atender à condição; os seguintes passam direto. O portão prende um comando só.
    /// </summary>
    public void HoldNextMatch() => Interlocked.Exchange(ref _armed, 1);

    /// <summary>
    /// Termina quando o comando preso já executou no banco e está esperando a liberação; falha depois do prazo.
    /// </summary>
    public Task WaitUntilHeldAsync(TimeSpan timeout) => _held.Task.WaitAsync(timeout);

    public void Release() => _released.TrySetResult();

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        await HoldIfMatchesAsync(command, cancellationToken);

        return result;
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        await HoldIfMatchesAsync(command, cancellationToken);

        return result;
    }

    private async Task HoldIfMatchesAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (!matches(command.CommandText))
        {
            return;
        }

        Interlocked.Increment(ref _matchedCount);

        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            _held.TrySetResult();

            await _released.Task.WaitAsync(cancellationToken);
        }
    }
}
