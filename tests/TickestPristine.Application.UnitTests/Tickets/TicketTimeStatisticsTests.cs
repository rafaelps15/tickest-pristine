using TickestPristine.Application.Tickets.GetTimes;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketTimeStatisticsTests
{
    [Fact]
    public void From_Should_ReturnAverageAndMedianOfMiddlePair_WhenSampleIsEven()
    {
        // Act: os seis tempos do AC-3 da spec 027
        TicketTimeStatisticsResponse statistics = TicketTimeStatistics.From([30, 60, 60, 90, 120, 600]);

        // Assert
        statistics.SampleSize.ShouldBe(6);
        statistics.AverageMinutes.ShouldBe(160);
        statistics.MedianMinutes.ShouldBe(75);
    }

    [Fact]
    public void From_Should_ReturnMiddleValueAsMedianAndRoundAverage_WhenSampleIsOdd()
    {
        // Act: fora de ordem, para conferir que a mediana ordena antes
        TicketTimeStatisticsResponse statistics = TicketTimeStatistics.From([100, 10, 11, 12, 13]);

        // Assert: média 29,2 e mediana 12
        statistics.SampleSize.ShouldBe(5);
        statistics.AverageMinutes.ShouldBe(29.2);
        statistics.MedianMinutes.ShouldBe(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void From_Should_ReturnOnlySampleSize_WhenSampleIsBelowMinimum(int size)
    {
        // Act
        TicketTimeStatisticsResponse statistics = TicketTimeStatistics.From([.. Enumerable.Repeat(60, size)]);

        // Assert
        statistics.SampleSize.ShouldBe(size);
        statistics.AverageMinutes.ShouldBeNull();
        statistics.MedianMinutes.ShouldBeNull();
    }
}
