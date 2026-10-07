using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class UserCodesTests
{
    [Fact]
    public void New_Should_StartWithPrefixFollowedByUlid()
    {
        // Arrange
        string prefix = UserCodes.Prefix;

        // Act
        string code = UserCodes.New();

        // Assert
        code.ShouldStartWith(prefix);
        Ulid.TryParse(code[prefix.Length..], out _).ShouldBeTrue();
    }

    [Fact]
    public void New_Should_ReturnDifferentCodes_WhenCalledTwice()
    {
        // Arrange
        string first = UserCodes.New();

        // Act
        string second = UserCodes.New();

        // Assert
        first.ShouldNotBe(second);
    }
}
