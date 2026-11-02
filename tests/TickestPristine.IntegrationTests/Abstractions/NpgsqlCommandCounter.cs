using System.Diagnostics;

namespace TickestPristine.IntegrationTests;

/// <summary>
/// Conta os comandos SQL que o driver do PostgreSQL executa, enquanto estiver ativo, cujo texto atende a uma condição.
/// Lê as atividades que o Npgsql emite por comando, então conta também os do Dapper, que o <see cref="EfCommandCounter"/>
/// não vê, sem mudar a configuração da API.
/// </summary>
internal sealed class NpgsqlCommandCounter : IDisposable
{
    private readonly ActivityListener _listener;
    private int _count;

    public NpgsqlCommandCounter(Func<string, bool> matches)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (CommandText(activity) is { } commandText && matches(commandText))
                {
                    Interlocked.Increment(ref _count);
                }
            }
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public int Count => Volatile.Read(ref _count);

    public void Dispose() => _listener.Dispose();

    // O texto do comando vem no atributo das convenções atuais do OpenTelemetry ou, nas anteriores, no db.statement.
    private static string? CommandText(Activity activity) =>
        activity.GetTagItem("db.query.text") as string ?? activity.GetTagItem("db.statement") as string;
}
