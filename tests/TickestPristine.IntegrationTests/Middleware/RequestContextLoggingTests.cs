namespace TickestPristine.IntegrationTests.Middleware;

/// <summary>
/// Valida o CorrelationId devolvido no header de resposta: o do cliente é mantido quando está no formato aceito e
/// trocado pelo id do servidor quando não está.
/// </summary>
public sealed class RequestContextLoggingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";

    [Fact]
    public async Task Request_Should_EchoCorrelationId_WhenClientSendsValidValue()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Add(CorrelationIdHeaderName, "front-123_abc.def");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues(CorrelationIdHeaderName).ShouldBe(["front-123_abc.def"]);
    }

    [Theory]
    [InlineData("valor com espacos")]
    [InlineData("<script>")]
    public async Task Request_Should_ReplaceCorrelationId_WhenClientValueHasInvalidCharacters(string correlationId)
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.TryAddWithoutValidation(CorrelationIdHeaderName, correlationId);

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        string returned = response.Headers.GetValues(CorrelationIdHeaderName).ShouldHaveSingleItem();
        returned.ShouldNotBe(correlationId);
        returned.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Request_Should_ReplaceCorrelationId_WhenClientValueIsTooLong()
    {
        // Arrange
        string correlationId = new('a', 65);
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Add(CorrelationIdHeaderName, correlationId);

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues(CorrelationIdHeaderName).ShouldHaveSingleItem().ShouldNotBe(correlationId);
    }

    [Fact]
    public async Task Request_Should_ReturnGeneratedCorrelationId_WhenClientSendsNone()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request);

        // Assert
        response.Headers.GetValues(CorrelationIdHeaderName).ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
    }
}
