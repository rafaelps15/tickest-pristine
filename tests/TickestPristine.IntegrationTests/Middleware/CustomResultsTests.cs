using Microsoft.AspNetCore.Http.HttpResults;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.IntegrationTests.Middleware;

public sealed class CustomResultsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Problem, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Failure, 500)]
    public void Problem_Should_ReturnStatusOfErrorType(ErrorType type, int expectedStatus)
    {
        // Arrange
        var result = Result.Failure(new Error("Teste.Codigo", "Descrição do teste", type));

        // Act
        ProblemHttpResult problem = CustomResults.Problem(result).ShouldBeOfType<ProblemHttpResult>();

        // Assert
        problem.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void Problem_Should_MaskCodeAndDescription_When_ErrorIsFailure()
    {
        // Arrange
        var result = Result.Failure(Error.Failure("Interno.Codigo", "Descrição interna"));

        // Act
        ProblemHttpResult problem = CustomResults.Problem(result).ShouldBeOfType<ProblemHttpResult>();

        // Assert
        problem.ProblemDetails.Title.ShouldBe("Falha no servidor");
        problem.ProblemDetails.Detail.ShouldBe("Ocorreu um erro inesperado");
    }

    [Fact]
    public void Problem_Should_ExposeCodeAndDescription_When_ErrorIsExpected()
    {
        // Arrange
        var result = Result.Failure(Error.Conflict("Teste.Conflito", "Descrição do conflito"));

        // Act
        ProblemHttpResult problem = CustomResults.Problem(result).ShouldBeOfType<ProblemHttpResult>();

        // Assert
        problem.ProblemDetails.Title.ShouldBe("Teste.Conflito");
        problem.ProblemDetails.Detail.ShouldBe("Descrição do conflito");
    }
}
