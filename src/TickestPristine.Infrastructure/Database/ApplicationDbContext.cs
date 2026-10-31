using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Infrastructure.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<UserCredential> UserCredentials { get; set; }

    public DbSet<UserInvitation> UserInvitations { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Department> Departments { get; set; }

    public DbSet<Sector> Sectors { get; set; }

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<TicketAttachment> TicketAttachments { get; set; }

    public DbSet<TicketMessage> TicketMessages { get; set; }

    public DbSet<TicketHistory> TicketHistories { get; set; }

    public DbSet<TicketSlaTimer> TicketSlaTimers { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Permission> Permissions { get; set; }

    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<UserRole> UserRoles { get; set; }

    public DbSet<AuditLogEntry> AuditLogs { get; set; }

    public DbSet<SlaCalendar> SlaCalendars { get; set; }

    public DbSet<SlaCalendarHoliday> SlaCalendarHolidays { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Texto que compara sem diferenciar maiúsculas de minúsculas (usado em nome e e-mail do usuário).
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }
}
