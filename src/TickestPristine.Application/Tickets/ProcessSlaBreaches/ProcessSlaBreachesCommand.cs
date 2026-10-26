using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.ProcessSlaBreaches;

/// <summary>
/// Um lote do monitoramento do SLA: até <see cref="BatchSize"/> cronômetros com ação pendente vencida, em ordem de
/// <c>NextActionOnUtc</c>. Enviado pelo serviço em background, nunca por uma rota.
/// </summary>
public sealed record ProcessSlaBreachesCommand(int BatchSize) : ICommand<ProcessSlaBreachesResponse>;
