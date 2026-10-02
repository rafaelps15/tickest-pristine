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
        // Act
        string first = RefreshTokenHasher.Hash("token-a");
        string second = RefreshTokenHasher.Hash("token-b");

        // Assert
        first.ShouldNotBe(second);
    }
}
