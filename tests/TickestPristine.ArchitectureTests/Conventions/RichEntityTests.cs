using NetArchTest.Rules;
using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

public class RichEntityTests : BaseTest
{
    // Entidades ainda no modelo anêmico, até a migração de cada uma (usuários e identidade, funções e permissões,
    // calendário de SLA e log de auditoria). A lista só encolhe: a entidade migrada sai daqui.
    internal static readonly Type[] LegacyAnemicEntities =
    [
        typeof(AuditLogEntry),
        typeof(Permission),
        typeof(RefreshToken),
        typeof(Role),
        typeof(RolePermission),
        typeof(SlaCalendar),
        typeof(SlaCalendarHoliday),
        typeof(User),
        typeof(UserCredential),
        typeof(UserInvitation),
        typeof(UserRole)
    ];

    // Todas as entidades do Domain, descobertas por reflexão, menos as anêmicas da lista. A regra do Raise fora do Domain
    // usa as mesmas.
    internal static Type[] RichEntities =>
    [
        .. Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(Entity))
            .GetTypes()
            .Where(type => !type.IsAbstract && !LegacyAnemicEntities.Contains(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
    ];

    [Fact]
    public void RichEntities_Should_NotHaveNonPrivateSetters()
    {
        // Arrange
        Type[] types = RichEntities;

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Fact]
    public void LegacyAnemicEntities_Should_StillHaveNonPrivateSetters()
    {
        // Arrange: a entidade que já não tem setter público saiu do modelo anêmico e precisa sair da lista
        Type[] types = LegacyAnemicEntities;

        // Act
        Type[] alreadyRich = [.. types.Where(type => RichEntitySetterChecker.Check([type]).Count == 0)];

        // Assert
        alreadyRich.ShouldBeEmpty();
    }

    [Fact]
    public void Checker_Should_NotReportViolation_When_EntityHasOnlyPrivateSetters()
    {
        // Arrange
        Type[] types = [typeof(RichFixtureEntity)];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Theory]
    [InlineData(typeof(PublicSetterFixtureEntity))]
    [InlineData(typeof(InitSetterFixtureEntity))]
    [InlineData(typeof(InternalSetterFixtureEntity))]
    public void Checker_Should_ReportViolation_When_EntityHasNonPrivateSetter(Type invalidEntity)
    {
        // Arrange
        Type[] types = [typeof(RichFixtureEntity), invalidEntity];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldBe($"{invalidEntity.FullName}.Name tem setter não privado: use private set e um método da entidade.");
    }

    [Fact]
    public void Checker_Should_ReportViolation_When_ListedTypeIsNotAnEntity()
    {
        // Arrange
        Type[] types = [typeof(NotAnEntityFixture)];

        // Act
        IReadOnlyList<string> messages = RichEntitySetterChecker.Check(types);

        // Assert
        string message = messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages));
        message.ShouldContain("não deriva de Entity");
    }
}
