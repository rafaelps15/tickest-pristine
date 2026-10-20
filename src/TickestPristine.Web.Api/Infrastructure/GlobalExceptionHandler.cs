using Microsoft.AspNetCore.Diagnostics;
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
            logger.LogWarning(exception, "Concurrency conflict while saving changes");

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
            logger.LogWarning(exception, "Unique constraint violation while saving changes");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Title = "Registro duplicado",
                Detail = "Já existe um registro com os mesmos dados. Atualize a tela e tente novamente."
            };
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            // Corpo ou parâmetros que não puderam ser lidos (ex.: JSON malformado) são erro do cliente, não do servidor.
            logger.LogWarning(exception, "Unreadable request");

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
            logger.LogWarning(exception, "Authenticated request without a user id");

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
            logger.LogError(exception, "Unhandled exception occurred");

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
}
