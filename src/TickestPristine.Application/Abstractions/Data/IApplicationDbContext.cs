using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.Abstractions.Data;

/// <summary>
/// Acesso da Application ao banco pelo EF Core: os conjuntos das entidades e o <c>SaveChangesAsync</c>, que grava a
/// auditoria na mesma transação e publica os eventos de domínio depois do commit.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserCredential> UserCredentials { get; }
    DbSet<UserInvitation> UserInvitations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Department> Departments { get; }
    DbSet<Sector> Sectors { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketHistoryEntry> TicketHistories { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditLogEntry> AuditLogs { get; }
    DbSet<SlaCalendar> SlaCalendars { get; }
    DbSet<SlaCalendarHoliday> SlaCalendarHolidays { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
