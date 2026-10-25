using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TickestPristine.Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserCredential> UserCredentials { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Department> Departments { get; }
    DbSet<Sector> Sectors { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketHistory> TicketHistories { get; }
    DbSet<TicketSlaTimer> TicketSlaTimers { get; }
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
