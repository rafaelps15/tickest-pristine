using System.Text.Json;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Infrastructure.Tickets;

/// <summary>
/// Formato do plano de SLA na coluna jsonb do chamado, com os nomes das propriedades como no C# (ex.:
/// "ResolutionMinutes"): o mesmo para a gravação pelo EF e para a leitura pelo Dapper, e o mesmo JSON que a migration
/// dos chamados existentes monta.
/// </summary>
internal static class TicketSlaPlanJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public static string Serialize(TicketSlaPlan plan) => JsonSerializer.Serialize(plan, Options);

    public static TicketSlaPlan Deserialize(string json) => JsonSerializer.Deserialize<TicketSlaPlan>(json, Options)!;
}
