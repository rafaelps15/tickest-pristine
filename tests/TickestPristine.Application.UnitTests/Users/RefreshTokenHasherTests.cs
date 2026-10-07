using TickestPristine.Application.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class RefreshTokenHasherTests
{
    [Fact]
    public void Hash_Should_ReturnSameHexValue_WhenTokenIsTheSame()
    {
        // Arrange
        const string token = "refresh-token";

        // Act
        string first = RefreshTokenHasher.Hash(token);
        string second = RefreshTokenHasher.Hash(token);

        // Assert
        first.ShouldBe(second);
        first.Length.ShouldBe(64);
        first.ShouldNotBe(token);
    }

    [Fact]
    public void Hash_Should_ReturnDifferentValues_WhenTokensDiffer()
    {
        // Arrange
        const string firstToken = "token-a";
        const string secondToken = "token-b";

        // Act
        string first = RefreshTokenHasher.Hash(firstToken);
        string second = RefreshTokenHasher.Hash(secondToken);

        // Assert
        first.ShouldNotBe(second);
    }
}
