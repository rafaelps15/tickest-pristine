using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class UserCodesTests
{
    [Fact]
    public void New_Should_StartWithPrefixFollowedByUlid()
    {
        // Act
        string code = UserCodes.New();

        // Assert
        code.ShouldStartWith(UserCodes.Prefix);
        Ulid.TryParse(code[UserCodes.Prefix.Length..], out _).ShouldBeTrue();
    }

    [Fact]
    public void New_Should_ReturnDifferentCodes_WhenCalledTwice()
    {
        // Act
        string first = UserCodes.New();
        string second = UserCodes.New();

        // Assert
        first.ShouldNotBe(second);
    }
}
