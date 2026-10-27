using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Conta os comandos SQL que o EF Core executa, enquanto estiver ativo, cujo texto atende a uma condição. Lê os eventos
/// do <see cref="DiagnosticListener"/> do EF, sem mudar a configuração da API.
/// </summary>
internal sealed class EfCommandCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly Func<string, bool> _matches;
    private readonly IDisposable _allListenersSubscription;
    private readonly List<IDisposable> _listenerSubscriptions = [];
    private int _count;

    public EfCommandCounter(Func<string, bool> matches)
    {
        _matches = matches;
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public int Count => Volatile.Read(ref _count);

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == DbLoggerCategory.Name)
        {
            lock (_listenerSubscriptions)
            {
                _listenerSubscriptions.Add(value.Subscribe(this));
            }
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == RelationalEventId.CommandExecuting.Name &&
            value.Value is CommandEventData data &&
            _matches(data.Command.CommandText))
        {
            Interlocked.Increment(ref _count);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        _allListenersSubscription.Dispose();

        lock (_listenerSubscriptions)
        {
            _listenerSubscriptions.ForEach(subscription => subscription.Dispose());
        }
    }
}
