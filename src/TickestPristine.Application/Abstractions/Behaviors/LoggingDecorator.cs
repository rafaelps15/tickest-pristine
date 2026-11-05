using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Telemetry;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Abstractions.Behaviors;

internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ApplicationTelemetry telemetry,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            using Activity? activity = ApplicationTelemetry.StartRequest(commandName);

            logger.LogInformation("Processando o comando {Command}", commandName);

            long started = Stopwatch.GetTimestamp();

            Result<TResponse> result = await Traced(activity, () => innerHandler.Handle(command, cancellationToken));

            telemetry.RecordOutcome(activity, commandName, result);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Comando {Command} concluído em {ElapsedMilliseconds} ms",
                    commandName,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object?> { ["@Error"] = result.Error }))
                {
                    logger.Log(
                        LevelFor(result.Error),
                        "Comando {Command} concluído com o erro {ErrorCode} em {ElapsedMilliseconds} ms",
                        commandName,
                        result.Error.Code,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
            }

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ApplicationTelemetry telemetry,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            using Activity? activity = ApplicationTelemetry.StartRequest(commandName);

            logger.LogInformation("Processando o comando {Command}", commandName);

            long started = Stopwatch.GetTimestamp();

            Result result = await Traced(activity, () => innerHandler.Handle(command, cancellationToken));

            telemetry.RecordOutcome(activity, commandName, result);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Comando {Command} concluído em {ElapsedMilliseconds} ms",
                    commandName,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object?> { ["@Error"] = result.Error }))
                {
                    logger.Log(
                        LevelFor(result.Error),
                        "Comando {Command} concluído com o erro {ErrorCode} em {ElapsedMilliseconds} ms",
                        commandName,
                        result.Error.Code,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
            }

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ApplicationTelemetry telemetry,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            string queryName = typeof(TQuery).Name;

            using Activity? activity = ApplicationTelemetry.StartRequest(queryName);

            logger.LogInformation("Processando a consulta {Query}", queryName);

            long started = Stopwatch.GetTimestamp();

            Result<TResponse> result = await Traced(activity, () => innerHandler.Handle(query, cancellationToken));

            telemetry.RecordOutcome(activity, queryName, result);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Consulta {Query} concluída em {ElapsedMilliseconds} ms",
                    queryName,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            else
            {
                using (logger.BeginScope(new Dictionary<string, object?> { ["@Error"] = result.Error }))
                {
                    logger.Log(
                        LevelFor(result.Error),
                        "Consulta {Query} concluída com o erro {ErrorCode} em {ElapsedMilliseconds} ms",
                        queryName,
                        result.Error.Code,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
            }

            return result;
        }
    }

    // A exceção do handler marca o span como erro e segue adiante, sem log aqui: quem a recebe a registra.
    private static async Task<TResult> Traced<TResult>(Activity? activity, Func<Task<TResult>> handle)
    {
        try
        {
            return await handle();
        }
        catch (Exception exception)
        {
            ApplicationTelemetry.RecordException(activity, exception);

            throw;
        }
    }

    /// <summary>
    /// Nível do registro da falha esperada: só a falha inesperada (<c>Failure</c>) é <c>Error</c>; a recusa por permissão
    /// (<c>Forbidden</c>) é <c>Warning</c>, e as demais (validação de negócio, não encontrado, conflito) são <c>Information</c>,
    /// para que os alertas por nível não disparem com o uso normal da API.
    /// </summary>
    private static LogLevel LevelFor(Error error) => error.Type switch
    {
        ErrorType.Failure => LogLevel.Error,
        ErrorType.Forbidden => LogLevel.Warning,
        _ => LogLevel.Information
    };
}
