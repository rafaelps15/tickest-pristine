using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TickestPristine.Infrastructure.Authentication;

namespace TickestPristine.Web.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails;

        if (exception is DbUpdateConcurrencyException)
        {
            // Os dados foram alterados por outra requisição ao mesmo tempo; nada foi salvo e a operação pode ser repetida.
            logger.LogWarning(exception, "Conflito de concorrência ao salvar as mudanças");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Title = "Conflito de concorrência",
                Detail = "Os dados foram alterados por outra operação ao mesmo tempo. Atualize a tela e tente novamente."
            };
        }
        else if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            // Duas requisições simultâneas passaram pela checagem de duplicidade do handler; o índice único barrou a segunda.
            logger.LogWarning(exception, "Violação de restrição de unicidade ao salvar as mudanças");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Title = "Registro duplicado",
                Detail = "Já existe um registro com os mesmos dados. Atualize a tela e tente novamente."
            };
        }
        else if (exception is BadHttpRequestException tooLarge && IsRequestBodyTooLarge(httpContext, tooLarge))
        {
            // Corpo acima do limite da rota: o Kestrel recusa com 413; o limite do multipart (o único aplicado pelo
            // TestServer) recusa com 400. Os dois são o mesmo erro do cliente. A única rota com limite próprio é o envio
            // de anexos (spec 011).
            logger.LogWarning(exception, "Corpo da requisição acima do limite da rota");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.11",
                Title = "Anexo muito grande",
                Detail = "O arquivo excede o tamanho máximo permitido para anexos. O limite é de 10 MB."
            };
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            // Corpo ou parâmetros que não puderam ser lidos (ex.: JSON malformado) são erro do cliente, não do servidor.
            logger.LogWarning(exception, "Requisição que não pôde ser lida");

            problemDetails = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Requisição inválida",
                Detail = "Não foi possível ler os dados enviados. Confira o formato da requisição."
            };
        }
        else if (exception is UserContextUnavailableException)
        {
            // Token válido, mas sem o id do usuário: a identidade não pôde ser lida, o que é 401 e não falha do servidor.
            logger.LogWarning(exception, "Requisição autenticada sem o id do usuário");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Title = "Não autenticado",
                Detail = "Não foi possível identificar o usuário. Faça login novamente."
            };
        }
        else
        {
            logger.LogError(exception, "Exceção não tratada");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                Title = "Falha no servidor"
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    /// <summary>
    /// Indica se a leitura falhou porque o corpo passou do limite declarado na própria rota
    /// (<see cref="IRequestSizeLimitMetadata"/>), pelo status 413 ou pelo tamanho informado no <c>Content-Length</c>.
    /// Rotas sem limite próprio ficam com a resposta genérica de requisição inválida. O middleware de exceções limpa o
    /// endpoint do contexto antes de chamar este handler: a rota original vem do <see cref="IExceptionHandlerFeature"/>.
    /// </summary>
    private static bool IsRequestBodyTooLarge(HttpContext httpContext, BadHttpRequestException exception)
    {
        long? routeLimit = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata
            .GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

        return routeLimit is not null &&
            (exception.StatusCode == StatusCodes.Status413PayloadTooLarge || httpContext.Request.ContentLength > routeLimit);
    }
}
