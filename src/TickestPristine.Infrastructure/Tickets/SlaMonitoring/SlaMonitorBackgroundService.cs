using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets.ProcessSlaBreaches;

namespace TickestPristine.Infrastructure.Tickets.SlaMonitoring;

/// <summary>
/// Serviço em background que processa, a cada volta, um lote de cronômetros de SLA vencidos, pelo
/// <see cref="ProcessSlaBreachesCommand"/>.
/// </summary>
internal sealed class SlaMonitorBackgroundService : BackgroundService
{
    // Trava consultiva do PostgreSQL: uma volta por vez em todas as instâncias; quem não consegue a trava não faz nada.
    internal const long AdvisoryLockKey = 4_006_000_001;

    private readonly ILogger<SlaMonitorBackgroundService> _logger;
    private readonly SlaMonitorSettings _slaMonitorSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="SlaMonitorBackgroundService"/>.
    /// </summary>
    /// <param name="logger">O logger.</param>
    /// <param name="slaMonitorSettingsOptions">As configurações do monitoramento do SLA.</param>
    /// <param name="serviceProvider">O provedor de serviços.</param>
    /// <param name="dataSource">A fonte de conexões do PostgreSQL, usada pela trava.</param>
    public SlaMonitorBackgroundService(
        ILogger<SlaMonitorBackgroundService> logger,
        IOptions<SlaMonitorSettings> slaMonitorSettingsOptions,
        IServiceProvider serviceProvider,
        NpgsqlDataSource dataSource)
    {
        _logger = logger;
        _slaMonitorSettings = slaMonitorSettingsOptions.Value;
        _serviceProvider = serviceProvider;
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_slaMonitorSettings.Enabled)
        {
            return;
        }

        _logger.LogDebug("SlaMonitorBackgroundService está iniciando.");

        stoppingToken.Register(() => _logger.LogDebug("A tarefa em background do SlaMonitorBackgroundService está parando."));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("A tarefa em background do SlaMonitorBackgroundService está processando.");

                await ProcessSlaBreachesAsync(stoppingToken);

                await Task.Delay(_slaMonitorSettings.SleepTimeInMilliseconds, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligamento da API: o Task.Delay foi interrompido, sem erro a registrar.
        }

        _logger.LogDebug("A tarefa em background do SlaMonitorBackgroundService está parando.");
    }

    /// <summary>
    /// Processa o próximo lote de cronômetros vencidos, se esta instância conseguir a trava.
    /// </summary>
    /// <param name="stoppingToken">O token de parada.</param>
    /// <returns>A tarefa concluída.</returns>
    internal async Task ProcessSlaBreachesAsync(CancellationToken stoppingToken)
    {
#pragma warning disable CA1031 // A falha de um lote é registrada sem derrubar o host; a volta seguinte tenta de novo.
        try
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(stoppingToken);

            if (!await TryAcquireLockAsync(connection, stoppingToken))
            {
                return;
            }

            try
            {
                using IServiceScope scope = _serviceProvider.CreateScope();

                ICommandHandler<ProcessSlaBreachesCommand, ProcessSlaBreachesResponse> handler = scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<ProcessSlaBreachesCommand, ProcessSlaBreachesResponse>>();

                await handler.Handle(new ProcessSlaBreachesCommand(_slaMonitorSettings.BatchSize), stoppingToken);
            }
            finally
            {
                await ReleaseLockAsync(connection);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "ERRO: falha ao processar o lote de cronômetros de SLA: {Message}", e.Message);
        }
#pragma warning restore CA1031
    }

    private static async Task<bool> TryAcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", AdvisoryLockKey);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // Sem o token de parada: a trava é liberada mesmo no desligamento.
    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
        command.Parameters.AddWithValue("key", AdvisoryLockKey);

        await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
