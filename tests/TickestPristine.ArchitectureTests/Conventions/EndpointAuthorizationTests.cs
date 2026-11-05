using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class EndpointAuthorizationTests : BaseTest
{
    [Fact]
    public void Endpoints_Should_DeclareProtection_When_Mapped()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Web.Api/Endpoints", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        files.ShouldNotBeEmpty("Nenhum endpoint lido em src/TickestPristine.Web.Api/Endpoints: a seleção pode estar errada.");

        // Act
        IReadOnlyList<Violation> violations = EndpointAuthorizationChecker.Check(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData(".RequireAuthorization()")]
    [InlineData(".HasPermission(Permissions.TicketsManage)")]
    [InlineData(".RequireAuthenticatedUser()")]
    [InlineData(".AllowAnonymous()")]
    public void Checker_Should_NotReportViolation_When_ProtectionIsDeclared(string protection)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", Endpoint(protection));

        // Act
        IReadOnlyList<Violation> violations = EndpointAuthorizationChecker.Check([file]);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" // .AllowAnonymous()")]
    public void Checker_Should_ReportViolation_When_ProtectionIsMissing(string protection)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", Endpoint(protection));

        // Act
        IReadOnlyList<Violation> violations = EndpointAuthorizationChecker.Check([file]);

        // Assert
        violations.ShouldHaveSingleItem().Value.ShouldBe("Exemplo");
    }

    private static string Endpoint(string protection) =>
        $$"""
        internal sealed class Exemplo : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapGet("exemplo", () => Results.Ok())
                    .WithTags(Tags.Tickets){{protection}};
            }
        }
        """;
}
