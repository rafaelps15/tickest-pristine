namespace TickestPristine.Application.Tickets.SettleDueSlaDeadlines;

/// <summary>
/// Totais de um lote: cronômetros processados (que saíram da fila) e, entre eles, os violados.
/// </summary>
public sealed record SettleDueSlaDeadlinesResponse(int Processed, int Breached);
