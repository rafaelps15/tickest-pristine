using System.Text.Json;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Abstractions;

/// <summary>
/// DbContext em memória que implementa IApplicationDbContext para os testes de handlers.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<UserCredential> UserCredentials { get; set; }

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

    /// <summary>
    /// Aplica as chaves e os filtros de exclusão lógica do banco real.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().HasKey(r => r.Name);
        modelBuilder.Entity<Role>().Property<int>("Version").IsConcurrencyToken();
        modelBuilder.Entity<Permission>(permission =>
        {
            permission.HasKey(p => p.Code);
            permission.Property(p => p.Name);
            permission.Property(p => p.Description);
            permission.Property(p => p.Group);
            permission.Property(p => p.IsAdministrative);
        });
        modelBuilder.Entity<RolePermission>().HasKey(p => new { p.RoleName, p.PermissionCode });

        modelBuilder.Entity<Ticket>().HasQueryFilter(t => t.DeletedAtUtc == null);

        // O plano é jsonb no banco real; aqui vira texto, opcional para os testes que montam chamados sem plano.
        modelBuilder.Entity<Ticket>().Property(t => t.SlaPlan)
            .HasConversion(
                plan => JsonSerializer.Serialize(plan, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<TicketSlaPlan>(json, JsonSerializerOptions.Default)!)
            .IsRequired(false);
        modelBuilder.Entity<TicketMessage>().HasQueryFilter(m => m.DeletedAtUtc == null);
        modelBuilder.Entity<TicketAttachment>().HasQueryFilter(a => a.DeletedAtUtc == null);

        // A grade é jsonb no banco real; sem a conversão da Infrastructure, o EF trataria SlaWorkPeriod como entidade.
        modelBuilder.Entity<SlaCalendar>().Ignore(c => c.WorkSchedule);
    }
}
