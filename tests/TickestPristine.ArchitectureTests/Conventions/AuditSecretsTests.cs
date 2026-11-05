using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

public class AuditSecretsTests : BaseTest
{
    [Fact]
    public void AuditableTypes_Should_ExcludeSecretLikeProperties()
    {
        // Arrange
        Type[] auditableTypes = [.. DomainAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IAuditable).IsAssignableFrom(type))];
        auditableTypes.ShouldNotBeEmpty("Nenhum tipo auditável lido no Domain: a seleção dos tipos pode estar errada.");

        // Act
        IReadOnlyList<string> violations = AuditSecretsChecker.Check(auditableTypes, AuditableModel.IsNotAudited);

        // Assert
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Checker_Should_ReportProperty_When_SecretLikeNameIsNotExcluded()
    {
        // Arrange
        Type[] auditableTypes = [typeof(AuditableFixtureWithSecret)];

        // Act
        IReadOnlyList<string> violations = AuditSecretsChecker.Check(auditableTypes, AuditableModel.IsNotAudited);

        // Assert
        violations.ShouldHaveSingleItem().ShouldStartWith("AuditableFixtureWithSecret.ApiTokenHash:");
    }

    [Fact]
    public void Exclusions_Should_KeepTodaySecretsOutOfTheTrail()
    {
        // Arrange
        (Type Type, string Property)[] secrets =
        [
            (typeof(Domain.Users.UserCredential), nameof(Domain.Users.UserCredential.PasswordHash)),
            (typeof(Domain.Users.UserCredential), nameof(Domain.Users.UserCredential.SecurityStamp)),
            (typeof(Domain.Users.UserInvitation), nameof(Domain.Users.UserInvitation.TokenHash))
        ];

        // Act
        bool[] excluded = [.. secrets.Select(secret => AuditableModel.IsNotAudited(secret.Type, secret.Property))];

        // Assert
        excluded.ShouldAllBe(isExcluded => isExcluded);
        AuditableModel.IsNotAudited(typeof(Domain.Users.User), nameof(Domain.Users.UserCredential.PasswordHash)).ShouldBeFalse();
    }
}
