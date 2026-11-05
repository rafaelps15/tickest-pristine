using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TickestPristine.Application.UnitTests.Abstractions;

public sealed class TestDbContextInventoryTests : BaseHandlerTest
{
    // O modelo de hoje do TestDbContext, congelado até ele sair do projeto, quando o último handler deixar o EF. Só entra
    // mudança que uma fronteira aprovada exija; regra que depende do banco (unicidade, token, filtro, check, citext,
    // tradução de SQL) vai para a integração.
    private static readonly string[] ApprovedInventory =
    [
        "DbSets: AuditLogs=AuditLogEntry, Departments=Department, Permissions=Permission, RefreshTokens=RefreshToken, RolePermissions=RolePermission, Roles=Role, Sectors=Sector, SlaCalendarHolidays=SlaCalendarHoliday, SlaCalendars=SlaCalendar, TicketAttachments=TicketAttachment, TicketHistories=TicketHistory, TicketMessages=TicketMessage, TicketSlaTimers=TicketSlaTimer, Tickets=Ticket, UserCredentials=UserCredential, UserInvitations=UserInvitation, UserRoles=UserRole, Users=User",
        "AuditLogEntry: key(Id); props(Action,AffectedColumns,EntityId,EntityName,Id,NewValues,OldValues,Timestamp,UserId)",
        "Department: key(Id); props(Description,Id,IsActive,Name,ResponsibleUserId)",
        "Permission: alt(Code); key(Id); props(Code,Description,Group,Id,IsAdministrative,Name)",
        "RefreshToken: key(Id); props(ExpiresOnUtc,Id,PreviousTokenHash,RotatedAtUtc,SecurityStamp,SessionExpiresOnUtc,TokenHash,UserId)",
        "Role: alt(Name); key(Id); props(Id,Name,Version); token(Version)",
        "RolePermission: key(Id); props(Id,PermissionCode,RoleName)",
        "Sector: key(Id); props(DepartmentId,Description,Id,IsActive,Name)",
        "SlaCalendar: key(Id); props(Id,Is24x7,Name,TimeZoneId)",
        "SlaCalendarHoliday: key(Id); props(Date,Description,Id,SlaCalendarId)",
        "Ticket: conversion(SlaPlan); filter; key(Id); props(AssignedToUserId,CreatedAtUtc,CreatedByUserId,DeletedAtUtc,Description,DueAtUtc,Id,LastActivityAtUtc,Priority,ResolvedAtUtc,SectorId,SlaPlan,SlaWarningAtUtc,Status,Title,WaitingOn)",
        "TicketAttachment: filter; key(Id); props(ContentType,DeletedAtUtc,FileName,FileSizeBytes,Id,PurgedAtUtc,StorageKey,TicketId,UploadedAtUtc,UploadedByUserId)",
        "TicketHistory: key(Id); props(Action,ChangedByUserId,Id,NewValue,OccurredAtUtc,OldValue,TicketId)",
        "TicketMessage: filter; key(Id); props(AuthorUserId,Content,CreatedAtUtc,DeletedAtUtc,EditedAtUtc,Id,TicketId)",
        "TicketSlaTimer: key(Id); props(BreachedOnUtc,CalendarId,CompletedOnUtc,CompletionCause,Cycle,DueOnUtc,Id,Metric,NextActionOnUtc,StartedAtUtc,StoppedOnUtc,TargetMinutes,TicketId,WarningOnUtc)",
        "User: key(Id); props(Code,CreatedAtUtc,DeactivatedAtUtc,Email,FirstName,Id,IsActive,LastName,SectorId)",
        "UserCredential: key(Id); props(FailedLoginAttempts,Id,LockoutEndsAtUtc,MustChangePassword,PasswordHash,SecurityStamp,UserId)",
        "UserInvitation: key(Id); props(CreatedAtUtc,ExpiresAtUtc,Id,RevokedAtUtc,TokenHash,UsedAtUtc,UserId)",
        "UserRole: key(Id); props(Id,RoleName,UserId)",
    ];

    [Fact]
    public void TestDbContext_Should_KeepCurrentModel_When_ModelIsBuilt()
    {
        // Arrange
        using TestDbContext context = CreateDbContext();

        // Act
        string[] inventory = Inventory(context);

        // Assert
        inventory.ShouldBe(ApprovedInventory);
    }

    private static string[] Inventory(TestDbContext context)
    {
        var lines = new List<string>
        {
            "DbSets: " + string.Join(", ", typeof(TestDbContext).GetProperties()
                .Where(property => property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(property => $"{property.Name}={property.PropertyType.GetGenericArguments()[0].Name}")
                .Order(StringComparer.Ordinal))
        };

        foreach (IEntityType entity in context.Model.GetEntityTypes().OrderBy(e => e.DisplayName(), StringComparer.Ordinal))
        {
            var parts = new List<string> { $"key({Names(entity.FindPrimaryKey()?.Properties)})" };
            parts.AddRange(entity.GetKeys().Where(key => !key.IsPrimaryKey()).Select(key => $"alt({Names(key.Properties)})"));
            parts.AddRange(entity.GetForeignKeys()
                .Select(foreignKey => $"fk({Names(foreignKey.Properties)}->{foreignKey.PrincipalEntityType.DisplayName()})"));
            parts.AddRange(entity.GetIndexes()
                .Select(index => $"index({Names(index.Properties)}{(index.IsUnique ? " unique" : string.Empty)})"));

            if (entity.GetDeclaredQueryFilters().Count > 0)
            {
                parts.Add("filter");
            }

            parts.AddRange(entity.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => $"token({p.Name})"));
            parts.AddRange(entity.GetProperties().Where(p => p.GetValueConverter() is not null).Select(p => $"conversion({p.Name})"));
            parts.Add($"props({Names(entity.GetProperties())})");

            lines.Add($"{entity.DisplayName()}: {string.Join("; ", parts.Order(StringComparer.Ordinal))}");
        }

        return [.. lines];
    }

    private static string Names(IEnumerable<IReadOnlyProperty>? properties) =>
        properties is null ? "-" : string.Join(",", properties.Select(property => property.Name).Order(StringComparer.Ordinal));
}
