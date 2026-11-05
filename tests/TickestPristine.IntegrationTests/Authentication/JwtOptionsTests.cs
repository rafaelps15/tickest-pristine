using System.IdentityModel.Tokens.Jwt;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using TickestPristine.Infrastructure.Authentication;
using TickestPristine.IntegrationTests.Tickets;

namespace TickestPristine.IntegrationTests.Authentication;

public sealed class JwtOptionsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public void Startup_Should_FailValidation_WhenExpirationExceedsSixtyMinutes()
    {
        // Arrange
        using WebApplicationFactoryWithSetting invalidFactory = new(_factory, "Jwt:AccessTokenExpirationInMinutes", "61");

        // Act
        Exception exception = Should.Throw<Exception>(() => invalidFactory.CreateClient());

        // Assert
        Exception? current = exception;

        while (current is not null and not OptionsValidationException)
        {
            current = current.InnerException;
        }

        current.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain("AccessTokenExpirationInMinutes");
    }

    [Fact]
    public async Task Login_Should_IssueTokenExpiringInSixtyMinutes_WhenExpirationIsSixty()
    {
        // Arrange: a fábrica dos testes usa Jwt:AccessTokenExpirationInMinutes = 60
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert: iat e exp têm resolução de segundos, e o iat sai um instante depois do relógio que calcula o exp
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        (token.ValidTo - token.IssuedAt).TotalSeconds.ShouldBeInRange(3599, 3600);
    }

    [Fact]
    public void Validate_Should_Succeed_WhenEveryValueFollowsTheRules()
    {
        // Arrange
        JwtOptions options = CreateOptions("tickestpristine", "developers", 32, 60, 7);

        // Act
        TestValidationResult<JwtOptions> result = new JwtOptionsValidator().TestValidate(options);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(" ", "developers", 32, 60, 7, nameof(JwtOptions.Issuer))]
    [InlineData("tickestpristine", "", 32, 60, 7, nameof(JwtOptions.Audience))]
    [InlineData("tickestpristine", "developers", 31, 60, 7, nameof(JwtOptions.SecurityKey))]
    [InlineData("tickestpristine", "developers", 32, 0, 7, nameof(JwtOptions.AccessTokenExpirationInMinutes))]
    [InlineData("tickestpristine", "developers", 32, 61, 7, nameof(JwtOptions.AccessTokenExpirationInMinutes))]
    [InlineData("tickestpristine", "developers", 32, 60, 0, nameof(JwtOptions.RefreshTokenExpirationInDays))]
    [InlineData("tickestpristine", "developers", 32, 60, 366, nameof(JwtOptions.RefreshTokenExpirationInDays))]
    public void Validate_Should_FailCitingTheKey_WhenValueBreaksTheRule(
        string issuer,
        string audience,
        int securityKeyLength,
        int accessTokenExpirationInMinutes,
        int refreshTokenExpirationInDays,
        string property)
    {
        // Arrange
        JwtOptions options = CreateOptions(
            issuer,
            audience,
            securityKeyLength,
            accessTokenExpirationInMinutes,
            refreshTokenExpirationInDays);

        // Act
        TestValidationResult<JwtOptions> result = new JwtOptionsValidator().TestValidate(options);

        // Assert
        result.ShouldHaveValidationErrorFor(property)
            .ShouldAllBe(failure => failure.ErrorMessage.Contains($"Jwt:{property}", StringComparison.Ordinal));
    }

    private static JwtOptions CreateOptions(
        string issuer,
        string audience,
        int securityKeyLength,
        int accessTokenExpirationInMinutes,
        int refreshTokenExpirationInDays) =>
        new()
        {
            Issuer = issuer,
            Audience = audience,
            SecurityKey = new string('k', securityKeyLength),
            AccessTokenExpirationInMinutes = accessTokenExpirationInMinutes,
            RefreshTokenExpirationInDays = refreshTokenExpirationInDays
        };
}
