using Shouldly;

namespace TickestPristine.ArchitectureTests.Conventions;

public class EndpointTagTests : BaseTest
{
    private const string EndpointWithTag = """
        internal sealed class Create : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets", () => Results.Ok()).WithTags(Tags.Tickets);
            }
        }
        """;

    private const string EndpointWithTagOnOwnLine = """
        internal sealed class Register : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("users/register", () => Results.Ok())
                    .AllowAnonymous()
                    .WithTags(Tags.Users);
            }
        }
        """;

    private const string NonEndpointWithoutTag = """
        internal sealed class Helper
        {
            public void Map(IEndpointRouteBuilder app)
            {
                app.MapGet("helper", () => Results.Ok());
            }
        }
        """;

    private const string EndpointWithoutTag = """
        internal sealed class Close : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/close", () => Results.Ok());
            }
        }
        """;

    private const string EndpointWithLiteralTag = """
        internal sealed class Close : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/close", () => Results.Ok()).WithTags("Tickets");
            }
        }
        """;

    private const string EndpointWithTagInLineComment = """
        internal sealed class Close : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/close", () => Results.Ok()); // .WithTags(Tags.Tickets)
            }
        }
        """;

    private const string EndpointWithTagInBlockComment = """
        internal sealed class Close : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/close", () => Results.Ok()) /* .WithTags(Tags.Tickets) */;
            }
        }
        """;

    private const string TwoEndpointsOneWithoutTag = """
        internal sealed class Open : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/open", () => Results.Ok()).WithTags(Tags.Tickets);
            }
        }

        internal sealed class Close : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                app.MapPost("tickets/close", () => Results.Ok());
            }
        }
        """;

    [Fact]
    public void Endpoints_Should_DeclareTagWithTagsConstant()
    {
        // Arrange
        IReadOnlyList<SourceFile> files = RepositoryFiles.Read(
            "src/TickestPristine.Web.Api", name => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        files.SelectMany(EndpointTagChecker.FindEndpoints)
            .ShouldNotBeEmpty("Nenhum endpoint (IEndpoint) encontrado: a seleção dos arquivos pode estar errada.");
        // Act
        IReadOnlyList<Violation> violations = EndpointTagChecker.Check(files);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData(EndpointWithTag)]
    [InlineData(EndpointWithTagOnOwnLine)]
    [InlineData(NonEndpointWithoutTag)]
    public void Checker_Should_NotReportViolation_When_EndpointHasTagsConstant(string content)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = EndpointTagChecker.Check(file);

        // Assert
        violations.ShouldBeEmpty(Violation.Describe(violations));
    }

    [Theory]
    [InlineData(EndpointWithoutTag, "sem tag")]
    [InlineData(EndpointWithLiteralTag, "tag literal")]
    [InlineData(EndpointWithTagInLineComment, "sem tag")]
    [InlineData(EndpointWithTagInBlockComment, "sem tag")]
    public void Checker_Should_ReportViolation_When_EndpointHasNoTagsConstant(string content, string expectedReason)
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", content);

        // Act
        IReadOnlyList<Violation> violations = EndpointTagChecker.Check(file);

        // Assert
        Violation violation = violations.ShouldHaveSingleItem(Violation.Describe(violations));
        string message = violation.ToString();
        message.ShouldStartWith("Exemplo.cs:1:");
        message.ShouldContain(expectedReason);
        violation.Value.ShouldBe("Close");
    }

    [Fact]
    public void Checker_Should_ReportOnlyEndpointWithoutTag_When_FileHasTwoEndpoints()
    {
        // Arrange
        var file = new SourceFile("Exemplo.cs", TwoEndpointsOneWithoutTag);

        // Act
        IReadOnlyList<Violation> violations = EndpointTagChecker.Check(file);

        // Assert
        Violation violation = violations.ShouldHaveSingleItem(Violation.Describe(violations));
        violation.Value.ShouldBe("Close");
        violation.Line.ShouldBe(9);
        violation.Reason.ShouldContain("sem tag");
    }
}
