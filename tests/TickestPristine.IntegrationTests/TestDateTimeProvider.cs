using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Relógio da API nos testes: o relógio real, a não ser enquanto um teste o deixa parado num instante com
/// <see cref="Freeze"/>; descartar o retorno volta ao relógio real.
/// </summary>
public sealed class TestDateTimeProvider : IDateTimeProvider
{
    private long _frozenTicks;

    public DateTime UtcNow
    {
        get
        {
            long frozenTicks = Interlocked.Read(ref _frozenTicks);

            return frozenTicks == 0 ? DateTime.UtcNow : new DateTime(frozenTicks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Para o relógio no instante UTC informado até o descarte do retorno. A autenticação vem antes: um token emitido
    /// com o relógio parado no passado já nasceria vencido para a validação, que usa o relógio real.
    /// </summary>
    public IDisposable Freeze(DateTime utcNow)
    {
        Interlocked.Exchange(ref _frozenTicks, utcNow.Ticks);

        return new Unfreeze(this);
    }

    private sealed class Unfreeze(TestDateTimeProvider clock) : IDisposable
    {
        public void Dispose() => Interlocked.Exchange(ref clock._frozenTicks, 0);
    }
}
