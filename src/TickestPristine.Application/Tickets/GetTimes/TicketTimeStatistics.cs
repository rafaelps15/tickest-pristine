namespace TickestPristine.Application.Tickets.GetTimes;

/// <summary>
/// Média e mediana de uma amostra de tempos, com uma casa decimal. Abaixo de <see cref="MinimumSampleSize"/> elementos,
/// média e mediana ficam nulas (spec 027, DEC-2); com amostra de tamanho par, a mediana é a média dos dois do meio.
/// </summary>
internal static class TicketTimeStatistics
{
    internal const int MinimumSampleSize = 5;

    internal static TicketTimeStatisticsResponse From(IReadOnlyCollection<int> minutes)
    {
        if (minutes.Count < MinimumSampleSize)
        {
            return new TicketTimeStatisticsResponse { SampleSize = minutes.Count };
        }

        int[] sorted = [.. minutes.Order()];
        int middle = sorted.Length / 2;
        double median = sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;

        return new TicketTimeStatisticsResponse
        {
            AverageMinutes = Math.Round(sorted.Average(), 1),
            MedianMinutes = Math.Round(median, 1),
            SampleSize = sorted.Length
        };
    }
}
