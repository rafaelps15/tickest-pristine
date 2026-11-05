using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TickestPristine.Infrastructure.Authentication;
using TickestPristine.Web.Api.Endpoints;

namespace TickestPristine.IntegrationTests.Middleware;

public sealed class GlobalExceptionHandlerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Theory]
    [InlineData("concorrencia", 409, "Conflito de concorrência")]
    [InlineData("ciclo-de-resolucao", 409, "Conflito de concorrência")]
    [InlineData("duplicado", 409, "Registro duplicado")]
    [InlineData("texto-fora-do-tamanho", 400, "Texto fora do tamanho permitido")]
    [InlineData("coluna-estourada", 400, "Texto fora do tamanho permitido")]
    [InlineData("ciclo-de-resolucao-direto", 409, "Conflito de concorrência")]
    [InlineData("duplicado-direto", 409, "Registro duplicado")]
    [InlineData("texto-fora-do-tamanho-direto", 400, "Texto fora do tamanho permitido")]
    [InlineData("coluna-estourada-direto", 400, "Texto fora do tamanho permitido")]
    [InlineData("corpo-grande", 413, "Anexo muito grande")]
    [InlineData("requisicao-ilegivel", 400, "Requisição inválida")]
    [InlineData("sem-usuario", 401, "Não autenticado")]
    [InlineData("inesperada", 500, "Falha no servidor")]
    public async Task Exception_Should_BecomeProblemDetails_WhenEndpointThrows(string scenario, int expectedStatus, string expectedTitle)
    {
        // Arrange: a API com um endpoint de teste que lança a exceção de cada ramo
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddTransient<IEndpoint, ThrowingEndpoint>()));
        using HttpClient client = api.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"{ThrowingEndpoint.Route}/{scenario}");

        // Assert
        ((int)response.StatusCode).ShouldBe(expectedStatus);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe(expectedTitle);
    }

    [Fact]
    public async Task UnexpectedException_Should_NotExposeItsMessage_WhenEndpointThrows()
    {
        // Arrange
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddTransient<IEndpoint, ThrowingEndpoint>()));
        using HttpClient client = api.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"{ThrowingEndpoint.Route}/inesperada");

        // Assert
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(ThrowingEndpoint.InternalDetail);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Detail.ShouldBeNull();
    }

    private sealed record ProblemDto(int Status, string Title, string? Detail);
}

// Endpoint de teste que lança, para cada cenário, a exceção de um ramo do GlobalExceptionHandler.
internal sealed class ThrowingEndpoint : IEndpoint
{
    internal const string Route = "teste/excecoes";
    internal const string InternalDetail = "detalhe interno que não pode sair na resposta";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet($"{Route}/{{scenario}}", (string scenario) => Throw(scenario))
            .AllowAnonymous()
            .WithMetadata(new RequestSizeLimitAttribute(10));
    }

    private static IResult Throw(string scenario) => throw scenario switch
    {
        "concorrencia" => new DbUpdateConcurrencyException("teste"),
        "ciclo-de-resolucao" => new DbUpdateException(
            "teste",
            Postgres(PostgresErrorCodes.UniqueViolation, "ix_ticket_sla_timers_ticket_id_metric_cycle")),
        "duplicado" => new DbUpdateException("teste", Postgres(PostgresErrorCodes.UniqueViolation, "ix_qualquer")),
        "texto-fora-do-tamanho" => new DbUpdateException("teste", Postgres(PostgresErrorCodes.CheckViolation, "ck_tickets_title_length")),
        "coluna-estourada" => new DbUpdateException("teste", Postgres(PostgresErrorCodes.StringDataRightTruncation, null)),
        "ciclo-de-resolucao-direto" => Postgres(PostgresErrorCodes.UniqueViolation, "ix_ticket_sla_timers_ticket_id_metric_cycle"),
        "duplicado-direto" => Postgres(PostgresErrorCodes.UniqueViolation, "ix_qualquer"),
        "texto-fora-do-tamanho-direto" => Postgres(PostgresErrorCodes.CheckViolation, "ck_tickets_title_length"),
        "coluna-estourada-direto" => Postgres(PostgresErrorCodes.StringDataRightTruncation, null),
        "corpo-grande" => new BadHttpRequestException("Corpo grande demais.", StatusCodes.Status413PayloadTooLarge),
        "requisicao-ilegivel" => new BadHttpRequestException("Corpo ilegível."),
        "sem-usuario" => new UserContextUnavailableException(),
        _ => new InvalidOperationException(InternalDetail),
    };

    private static PostgresException Postgres(string sqlState, string? constraintName) =>
        new("teste", "ERROR", "ERROR", sqlState, constraintName: constraintName);
}
