using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TickestPristine.Infrastructure.Authentication;

namespace TickestPristine.IntegrationTests.Authentication;

public sealed class JwtValidationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("hs512")]
    [InlineData("alg-none")]
    [InlineData("tampered-signature")]
    public async Task GetCurrent_Should_ReturnUnauthorized_WhenTokenIsInvalid(string kind)
    {
        // Arrange: o token do login, alterado só no aspecto do caso
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(InvalidToken(kind, tokens.AccessToken));

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnOk_WhenTokenComesFromLogin()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnOk_WhenLoginClaimsAreSignedAgainWithHs256()
    {
        // Arrange: a mesma montagem dos casos inválidos, sem alteração; prova que só o aspecto de cada caso é recusado
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(Sign("HS256", Claims(tokens.AccessToken.Split('.')[1]), Secret()));

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private string InvalidToken(string kind, string loginToken)
    {
        string[] parts = loginToken.Split('.');
        JsonObject claims = Claims(parts[1]);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        switch (kind)
        {
            case "expired":
                claims["iat"] = now - 600;
                claims["nbf"] = now - 600;
                claims["exp"] = now - 300;
                return Sign("HS256", claims, Secret());
            case "wrong-issuer":
                claims["iss"] = "outro-emissor";
                return Sign("HS256", claims, Secret());
            case "wrong-audience":
                claims["aud"] = "outra-audiencia";
                return Sign("HS256", claims, Secret());
            case "hs512":
                return Sign("HS512", claims, Secret());
            case "alg-none":
                return Sign("none", claims, Secret());
            case "tampered-signature":
                // O primeiro caractere da assinatura muda os bits mais altos do primeiro byte.
                string signature = parts[2];
                char first = signature[0] == 'A' ? 'B' : 'A';
                return $"{parts[0]}.{parts[1]}.{first}{signature[1..]}";
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Caso de token desconhecido.");
        }
    }

    private string Secret() => Services.GetRequiredService<IOptions<JwtOptions>>().Value.SecurityKey;

    private static JsonObject Claims(string payload) => JsonNode.Parse(Base64UrlEncoder.Decode(payload))!.AsObject();

    private static string Sign(string algorithm, JsonObject claims, string secret)
    {
        string header = Base64UrlEncoder.Encode(new JsonObject { ["alg"] = algorithm, ["typ"] = "JWT" }.ToJsonString());
        string payload = Base64UrlEncoder.Encode(claims.ToJsonString());
        string unsigned = $"{header}.{payload}";
        byte[] key = Encoding.UTF8.GetBytes(secret);
        byte[] data = Encoding.ASCII.GetBytes(unsigned);

        string signature = algorithm switch
        {
            "HS256" => Base64UrlEncoder.Encode(HMACSHA256.HashData(key, data)),
            "HS512" => Base64UrlEncoder.Encode(HMACSHA512.HashData(key, data)),
            _ => string.Empty,
        };

        return $"{unsigned}.{signature}";
    }
}
