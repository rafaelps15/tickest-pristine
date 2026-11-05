using Shouldly;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

public class AuditableNamesTests : BaseTest
{
    // O entity_name da trilha é o nome do tipo: renomear um tipo auditável separa a trilha dos registros antigos.
    private static readonly string[] AuditableTypeNames =
    [
        "Department",
        "RolePermission",
        "Sector",
        "SlaCalendar",
        "SlaCalendarHoliday",
        "Ticket",
        "TicketAttachment",
        "TicketMessage",
        "TicketSlaTimer",
        "User",
        "UserCredential",
        "UserInvitation",
        "UserRole"
    ];

    // As chaves de oldValues e newValues são os nomes das propriedades: renomear uma também separa a trilha.
    private static readonly Dictionary<string, string[]> AuditedProperties = new(StringComparer.Ordinal)
    {
        ["Department"] =
        [
            "Description",
            "Id",
            "IsActive",
            "Name",
            "ResponsibleUserId"
        ],
        ["RolePermission"] =
        [
            "Id",
            "PermissionCode",
            "RoleName"
        ],
        ["Sector"] =
        [
            "DepartmentId",
            "Description",
            "Id",
            "IsActive",
            "Name"
        ],
        ["SlaCalendar"] =
        [
            "Id",
            "Is24x7",
            "Name",
            "TimeZoneId",
            "WorkSchedule"
        ],
        ["SlaCalendarHoliday"] =
        [
            "Date",
            "Description",
            "Id",
            "SlaCalendarId"
        ],
        ["Ticket"] =
        [
            "AssignedToUserId",
            "CreatedAtUtc",
            "CreatedByUserId",
            "DeletedAtUtc",
            "Description",
            "DueAtUtc",
            "Id",
            "LastActivityAtUtc",
            "Priority",
            "ResolvedAtUtc",
            "SectorId",
            "SlaPlan",
            "SlaWarningAtUtc",
            "Status",
            "Title",
            "Version",
            "WaitingOn"
        ],
        ["TicketAttachment"] =
        [
            "ContentType",
            "DeletedAtUtc",
            "FileName",
            "FileSizeBytes",
            "Id",
            "PurgedAtUtc",
            "StorageKey",
            "TicketId",
            "UploadedAtUtc",
            "UploadedByUserId"
        ],
        ["TicketMessage"] =
        [
            "AuthorUserId",
            "Content",
            "CreatedAtUtc",
            "DeletedAtUtc",
            "EditedAtUtc",
            "Id",
            "TicketId"
        ],
        ["TicketSlaTimer"] =
        [
            "BreachedOnUtc",
            "CalendarId",
            "CompletedOnUtc",
            "CompletionCause",
            "Cycle",
            "DueOnUtc",
            "Id",
            "Metric",
            "NextActionOnUtc",
            "StartedAtUtc",
            "StoppedOnUtc",
            "TargetMinutes",
            "TicketId",
            "WarningOnUtc"
        ],
        ["User"] =
        [
            "Code",
            "CreatedAtUtc",
            "DeactivatedAtUtc",
            "Email",
            "FirstName",
            "Id",
            "IsActive",
            "LastName",
            "SectorId"
        ],
        ["UserCredential"] =
        [
            "FailedLoginAttempts",
            "Id",
            "LockoutEndsAtUtc",
            "MustChangePassword",
            "UserId"
        ],
        ["UserInvitation"] =
        [
            "CreatedAtUtc",
            "ExpiresAtUtc",
            "Id",
            "RevokedAtUtc",
            "UsedAtUtc",
            "UserId"
        ],
        ["UserRole"] =
        [
            "Id",
            "RoleName",
            "UserId"
        ]
    };

    [Fact]
    public void AuditableTypeNames_Should_MatchSnapshot()
    {
        // Arrange
        string[] typeNames = [.. DomainAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IAuditable).IsAssignableFrom(type))
            .Select(type => type.Name)];

        // Act
        List<string> differences = Compare(AuditableTypeNames, typeNames, "tipo auditável");

        // Assert
        differences.ShouldBeEmpty(string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void AuditedProperties_Should_MatchSnapshot()
    {
        // Arrange
        var properties = AuditableModel.Entities.ToDictionary(
            entity => entity.Type.Name,
            entity => entity.Properties.Except(entity.NotAuditedProperties, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

        // Act
        List<string> differences = [.. properties.Keys
            .Union(AuditedProperties.Keys)
            .Order(StringComparer.Ordinal)
            .SelectMany(type => Compare(
                AuditedProperties.GetValueOrDefault(type, []),
                properties.GetValueOrDefault(type, []),
                $"propriedade auditada de {type}"))];

        // Assert
        differences.ShouldBeEmpty(string.Join(Environment.NewLine, differences));
    }

    private static List<string> Compare(IEnumerable<string> expected, IEnumerable<string> actual, string subject) =>
    [
        .. actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(name => $"{subject} fora do instantâneo: {name}"),
        .. expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(name => $"{subject} no instantâneo, mas não no código: {name}")
    ];
}
