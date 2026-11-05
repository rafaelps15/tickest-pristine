using System.Diagnostics;
using System.Diagnostics.Metrics;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Abstractions.Telemetry;

/// <summary>
/// Spans e medidas da Application, publicados com o nome <see cref="Name"/>, que o OpenTelemetry da API registra: um
/// span por command e por query e os contadores de falhas por tipo de erro, de falhas dos handlers de evento e das
/// voltas do monitoramento de SLA.
/// </summary>
public sealed class ApplicationTelemetry
{
    public const string Name = "TickestPristine.Application";

    private const string ResultTag = "tickest.request.result";
    private const string RequestNameTag = "tickest.request.name";
    private const string ErrorTypeTag = "error.type";
    private const string DomainEventTag = "tickest.domain_event.name";
    private const string HandlerTag = "tickest.domain_event.handler";
    private const string SuccessResult = "success";

    // O ActivitySource não tem fábrica no contêiner: uma instância por processo. O Meter vem do IMeterFactory, que o
    // descarta com o contêiner e separa as medidas de cada host nos testes.
    private static readonly ActivitySource Source = new(Name);

    private readonly Counter<long> _requestFailures;
    private readonly Counter<long> _domainEventHandlerFailures;
    private readonly Counter<long> _slaTimersProcessed;
    private readonly Counter<long> _slaBreaches;
    private readonly Counter<long> _slaBatchesRolledBack;

    public ApplicationTelemetry(IMeterFactory meterFactory)
    {
#pragma warning disable CA2000 // O Meter pertence ao IMeterFactory, que o descarta junto com o contêiner.
        Meter meter = meterFactory.Create(Name);
#pragma warning restore CA2000

        _requestFailures = meter.CreateCounter<long>(
            "tickest.request.failures",
            "{failure}",
            "Commands e queries concluídos com falha, por tipo de erro.");
        _domainEventHandlerFailures = meter.CreateCounter<long>(
            "tickest.domain_event.handler_failures",
            "{failure}",
            "Handlers de evento de domínio que falharam.");
        _slaTimersProcessed = meter.CreateCounter<long>(
            "tickest.sla.timers_processed",
            "{timer}",
            "Cronômetros de SLA vencidos apurados pelo monitoramento.");
        _slaBreaches = meter.CreateCounter<long>(
            "tickest.sla.breaches",
            "{breach}",
            "Violações de SLA gravadas pelo monitoramento.");
        _slaBatchesRolledBack = meter.CreateCounter<long>(
            "tickest.sla.batches_rolled_back",
            "{batch}",
            "Lotes do monitoramento de SLA desfeitos por uma falha.");
    }

    /// <summary>
    /// Abre o span do command ou da query com o nome do tipo; devolve <see langword="null"/> quando nada escuta a fonte.
    /// </summary>
    public static Activity? StartRequest(string requestName) => Source.StartActivity(requestName);

    /// <summary>
    /// Marca o span como erro e guarda nele a exceção lançada pelo handler; quem chama relança a exceção.
    /// </summary>
    public static void RecordException(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        activity?.AddException(exception);
    }

    /// <summary>
    /// Grava no span o resultado (<c>success</c> ou o código do erro) e, na falha, soma 1 no contador do tipo do erro.
    /// Só a falha inesperada (<c>Failure</c>) marca o span como erro, como no nível do log.
    /// </summary>
    public void RecordOutcome(Activity? activity, string requestName, Result result)
    {
        if (result.IsSuccess)
        {
            activity?.SetTag(ResultTag, SuccessResult);

            return;
        }

        string errorType = result.Error.Type.ToString();

        activity?.SetTag(ResultTag, result.Error.Code);
        activity?.SetTag(ErrorTypeTag, errorType);

        if (result.Error.Type == ErrorType.Failure)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
        }

        _requestFailures.Add(
            1,
            new KeyValuePair<string, object?>(RequestNameTag, requestName),
            new KeyValuePair<string, object?>(ErrorTypeTag, errorType));
    }

    /// <summary>
    /// Soma 1 nas falhas de handler do evento, com o nome do evento e o do handler que falhou.
    /// </summary>
    public void RecordDomainEventHandlerFailure(string domainEventName, string handlerName) =>
        _domainEventHandlerFailures.Add(
            1,
            new KeyValuePair<string, object?>(DomainEventTag, domainEventName),
            new KeyValuePair<string, object?>(HandlerTag, handlerName));

    public void RecordSlaBatch(int processed, int breached)
    {
        _slaTimersProcessed.Add(processed);
        _slaBreaches.Add(breached);
    }

    public void RecordSlaBatchRolledBack() => _slaBatchesRolledBack.Add(1);
}
