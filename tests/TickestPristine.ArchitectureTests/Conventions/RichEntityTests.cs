using NetArchTest.Rules;
using Shouldly;
using TickestPristine.ArchitectureTests.Conventions.Fixtures;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
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
        typeof(RefreshToken),
        typeof(SlaCalendar),
        typeof(SlaCalendarHoliday),
        typeof(User),
        typeof(UserCredential),
        typeof(UserInvitation),
        typeof(UserRole)
    ];

    // Todas as entidades do Domain, descobertas por reflexão.
    internal static Type[] Entities =>
    [
        .. Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(Entity))
            .GetTypes()
            .Where(type => !type.IsAbstract)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
    ];

    // As entidades menos as anêmicas da lista. A regra do Raise fora do Domain usa as mesmas.
    internal static Type[] RichEntities => [.. Entities.Where(type => !LegacyAnemicEntities.Contains(type))];

    // Desvios do modelo de entidade que ainda existem, um item por entidade e desvio, cada um até a etapa que o remove.
    // A lista só encolhe: o desvio corrigido sai daqui, e nenhum novo entra.
    internal static readonly string[] ModelExceptions =
    [
        EntityModelChecker.WrongConstructor(typeof(AuditLogEntry)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(AuditLogEntry),
            nameof(AuditLogEntry.Action),
            nameof(AuditLogEntry.AffectedColumns),
            nameof(AuditLogEntry.EntityId),
            nameof(AuditLogEntry.EntityName),
            nameof(AuditLogEntry.Id),
            nameof(AuditLogEntry.NewValues),
            nameof(AuditLogEntry.OldValues),
            nameof(AuditLogEntry.Timestamp),
            nameof(AuditLogEntry.UserId)),
        EntityModelChecker.WithoutCreate(typeof(AuditLogEntry)),

EntityModelChecker.WrongConstructor(typeof(SlaCalendar)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(SlaCalendar),
            nameof(SlaCalendar.Id),
            nameof(SlaCalendar.Is24x7),
            nameof(SlaCalendar.Name),
            nameof(SlaCalendar.TimeZoneId),
            nameof(SlaCalendar.WorkSchedule)),
        EntityModelChecker.WithoutCreate(typeof(SlaCalendar)),

        EntityModelChecker.WrongConstructor(typeof(SlaCalendarHoliday)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(SlaCalendarHoliday),
            nameof(SlaCalendarHoliday.Date),
            nameof(SlaCalendarHoliday.Description),
            nameof(SlaCalendarHoliday.Id),
            nameof(SlaCalendarHoliday.SlaCalendarId)),
        EntityModelChecker.WithoutCreate(typeof(SlaCalendarHoliday)),

        .. EntityModelChecker.ComputedProperties(typeof(Ticket), nameof(Ticket.IsActive)),
        .. EntityModelChecker.PublicStatics(
            typeof(Ticket),
            nameof(Ticket.Active),
            nameof(Ticket.CanBeCanceled),
            nameof(Ticket.GetSlaStatus),
            nameof(Ticket.OwnerCancelDeadline)),
        .. EntityModelChecker.PublicConstants(typeof(Ticket), nameof(Ticket.OwnerCancelWindowMinutes)),

        .. EntityModelChecker.ComputedProperties(typeof(TicketSlaTimer), nameof(TicketSlaTimer.IsOpen)),

        EntityModelChecker.WrongConstructor(typeof(RefreshToken)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(RefreshToken),
            nameof(RefreshToken.ExpiresOnUtc),
            nameof(RefreshToken.Id),
            nameof(RefreshToken.PreviousTokenHash),
            nameof(RefreshToken.RotatedAtUtc),
            nameof(RefreshToken.SecurityStamp),
            nameof(RefreshToken.SessionExpiresOnUtc),
            nameof(RefreshToken.TokenHash),
            nameof(RefreshToken.UserId)),
        EntityModelChecker.WithoutCreate(typeof(RefreshToken)),

        EntityModelChecker.WrongConstructor(typeof(User)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(User),
            nameof(User.Code),
            nameof(User.CreatedAtUtc),
            nameof(User.DeactivatedAtUtc),
            nameof(User.Email),
            nameof(User.FirstName),
            nameof(User.Id),
            nameof(User.IsActive),
            nameof(User.LastName),
            nameof(User.SectorId)),
        EntityModelChecker.WithoutCreate(typeof(User)),

        EntityModelChecker.WrongConstructor(typeof(UserCredential)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(UserCredential),
            nameof(UserCredential.FailedLoginAttempts),
            nameof(UserCredential.Id),
            nameof(UserCredential.LockoutEndsAtUtc),
            nameof(UserCredential.MustChangePassword),
            nameof(UserCredential.PasswordHash),
            nameof(UserCredential.SecurityStamp),
            nameof(UserCredential.UserId)),
        EntityModelChecker.WithoutCreate(typeof(UserCredential)),

        EntityModelChecker.WrongConstructor(typeof(UserInvitation)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(UserInvitation),
            nameof(UserInvitation.CreatedAtUtc),
            nameof(UserInvitation.ExpiresAtUtc),
            nameof(UserInvitation.Id),
            nameof(UserInvitation.RevokedAtUtc),
            nameof(UserInvitation.TokenHash),
            nameof(UserInvitation.UsedAtUtc),
            nameof(UserInvitation.UserId)),
        EntityModelChecker.WithoutCreate(typeof(UserInvitation)),

        EntityModelChecker.WrongConstructor(typeof(UserRole)),
        .. EntityModelChecker.NonPrivateSetters(
            typeof(UserRole),
            nameof(UserRole.Id),
            nameof(UserRole.RoleName),
            nameof(UserRole.UserId)),
        EntityModelChecker.WithoutCreate(typeof(UserRole))
    ];

    public static TheoryData<Type, string> ModelViolations => new()
    {
        { typeof(UnsealedModelFixtureEntity), $"{typeof(UnsealedModelFixtureEntity).FullName} não é sealed (C1)." },
        { typeof(WithoutIdModelFixtureEntity), $"{typeof(WithoutIdModelFixtureEntity).FullName} não tem Id do tipo Guid (C1)." },
        {
            typeof(PublicConstructorModelFixtureEntity),
            $"{typeof(PublicConstructorModelFixtureEntity).FullName} precisa de um único construtor, privado e sem parâmetros (C2)."
        },
        {
            typeof(ComputedPropertyModelFixtureEntity),
            $"{typeof(ComputedPropertyModelFixtureEntity).FullName}.HasName é propriedade calculada: use private set ou um método de consulta (C3)."
        },
        {
            typeof(PublicSetterModelFixtureEntity),
            $"{typeof(PublicSetterModelFixtureEntity).FullName}.Name tem setter não privado: use private set e um método da entidade (C3)."
        },
        {
            typeof(InternalSetterModelFixtureEntity),
            $"{typeof(InternalSetterModelFixtureEntity).FullName}.Name tem setter não privado: use private set e um método da entidade (C3)."
        },
        { typeof(WithoutCreateModelFixtureEntity), $"{typeof(WithoutCreateModelFixtureEntity).FullName} não tem um Create estático (C4)." },
        {
            typeof(PublicStaticModelFixtureEntity),
            $"{typeof(PublicStaticModelFixtureEntity).FullName}.Matches é membro estático público além do Create (C5)."
        },
        {
            typeof(PublicConstModelFixtureEntity),
            $"{typeof(PublicConstModelFixtureEntity).FullName}.MaxNameLength é constante pública (C5)."
        }
    };

    [Fact]
    public void Entities_Should_FollowTheEntityModel()
    {
        // Arrange
        Type[] types = Entities;

        // Act
        string[] violations = [.. EntityModelChecker.Check(types).Except(ModelExceptions)];

        // Assert
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ModelExceptions_Should_StillBeViolated()
    {
        // Arrange: o desvio que a etapa corrigiu precisa sair da lista
        IReadOnlyList<string> violations = EntityModelChecker.Check(Entities);

        // Act
        string[] fixedDeviations = [.. ModelExceptions.Except(violations)];

        // Assert
        fixedDeviations.ShouldBeEmpty(string.Join(Environment.NewLine, fixedDeviations));
    }

    [Fact]
    public void ModelChecker_Should_NotReportViolation_When_EntityFollowsTheModel()
    {
        // Arrange
        Type[] types = [typeof(ModelFixtureEntity)];

        // Act
        IReadOnlyList<string> messages = EntityModelChecker.Check(types);

        // Assert
        messages.ShouldBeEmpty(string.Join(Environment.NewLine, messages));
    }

    [Theory]
    [MemberData(nameof(ModelViolations))]
    public void ModelChecker_Should_ReportViolation_When_EntityBreaksOneCriterion(Type invalidEntity, string expected)
    {
        // Arrange
        Type[] types = [typeof(ModelFixtureEntity), invalidEntity];

        // Act
        IReadOnlyList<string> messages = EntityModelChecker.Check(types);

        // Assert
        messages.ShouldHaveSingleItem(string.Join(Environment.NewLine, messages)).ShouldBe(expected);
    }

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


