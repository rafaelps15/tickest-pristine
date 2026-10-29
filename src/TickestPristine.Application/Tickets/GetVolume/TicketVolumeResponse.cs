namespace TickestPristine.Application.Tickets.GetVolume;

/// <summary>
/// <see cref="PeriodStart"/> é a data local (horário de Brasília) em que o período começa: o próprio dia, a segunda-feira
/// da semana ou o dia 1º do mês. <see cref="Count"/> são os chamados abertos no período e <see cref="ResolvedCount"/>, as
/// resoluções no período, inclusive as de chamados reabertos e resolvidos de novo (spec 028).
/// </summary>
public sealed class TicketVolumeResponse
{
    public DateOnly PeriodStart { get; set; }
    public int Count { get; set; }
    public int ResolvedCount { get; set; }
}
