namespace TickestPristine.Application.Tickets.ProcessSlaBreaches;

/// <summary>
/// Totais de um lote: cronômetros processados (que saíram da fila) e, entre eles, os violados.
/// </summary>
public sealed record ProcessSlaBreachesResponse(int Processed, int Breached);
