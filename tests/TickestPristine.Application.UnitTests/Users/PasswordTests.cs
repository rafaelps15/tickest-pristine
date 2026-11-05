using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class PasswordTests
{
    [Theory]
    [InlineData("Senha@1")]
    [InlineData("ÁrvoreÇ#2")]
    public void Create_Should_ReturnPassword_WhenPolicyIsMet(string value)
    {
        // Act
        Result<Password> result = Password.Create(value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab@1")]
    [InlineData("senha@1")]
    [InlineData("Senha12")]
    [InlineData("Senha 12")]
    public void Create_Should_ReturnWeakPassword_WhenPolicyIsBroken(string? value)
    {
        // Act
        Result<Password> result = Password.Create(value);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.WeakPassword);
    }

    [Fact]
    public void ToString_Should_HideValue_WhenPasswordIsPrinted()
    {
        // Arrange
        Password password = Password.Create("Senha@1").Value;

        // Act
        string text = password.ToString();

        // Assert
        text.ShouldNotContain("Senha@1");
    }

    [Fact]
    public void Equals_Should_CompareValues_WhenPasswordsAreCreated()
    {
        // Arrange
        Password first = Password.Create("Senha@1").Value;
        Password same = Password.Create("Senha@1").Value;
        Password other = Password.Create("Outra@2").Value;

        // Act
        bool equalToSame = first == same;
        bool equalToOther = first == other;

        // Assert
        equalToSame.ShouldBeTrue();
        equalToOther.ShouldBeFalse();
        first.GetHashCode().ShouldBe(same.GetHashCode());
    }
}
