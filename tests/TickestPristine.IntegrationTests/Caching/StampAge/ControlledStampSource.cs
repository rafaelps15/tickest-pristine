using System.Collections.Concurrent;

namespace TickestPristine.IntegrationTests.Caching.StampAge;

/// <summary>
/// Fonte do carimbo nos testes do protótipo: guarda o carimbo atual de cada usuário e pode prender as próximas leituras
/// em portões. Cada leitura pega o valor no momento em que começa, como uma consulta ao banco, e só devolve quando o
/// portão dela é liberado.
/// </summary>
public sealed class ControlledStampSource
{
    private readonly ConcurrentDictionary<Guid, Guid> _stamps = new();
    private readonly ConcurrentQueue<StampReadGate> _gates = new();

    public void SetStamp(Guid userId, Guid stamp) => _stamps[userId] = stamp;

    /// <summary>
    /// Prende a próxima leitura que começar, de qualquer usuário, até <see cref="StampReadGate.Release"/>. Portões
    /// pedidos em sequência prendem as leituras seguintes, na mesma ordem.
    /// </summary>
    public StampReadGate HoldNextRead()
    {
        var gate = new StampReadGate();
        _gates.Enqueue(gate);

        return gate;
    }

    public async Task<Guid> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid stamp = _stamps[userId];

        if (_gates.TryDequeue(out StampReadGate? gate))
        {
            await gate.HoldAsync(cancellationToken);
        }

        return stamp;
    }
}

/// <summary>
/// Portão de uma leitura da <see cref="ControlledStampSource"/>: avisa quando a leitura começou e a solta em
/// <see cref="Release"/>.
/// </summary>
public sealed class StampReadGate
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public void Release() => _released.TrySetResult();

    internal async Task HoldAsync(CancellationToken cancellationToken)
    {
        _started.TrySetResult();

        await _released.Task.WaitAsync(cancellationToken);
    }
}
