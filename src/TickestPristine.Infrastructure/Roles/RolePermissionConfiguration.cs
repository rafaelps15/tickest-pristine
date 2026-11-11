using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id).ValueGeneratedNever();
        builder.HasIndex(permission => new { permission.RoleName, permission.PermissionCode }).IsUnique();
        builder.Property(permission => permission.RoleName).HasMaxLength(50);
        builder.Property(permission => permission.PermissionCode).HasMaxLength(100);
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(permission => permission.PermissionCode)
            .HasPrincipalKey(permission => permission.Code);

        object[] defaultRolePermissions =
        [
            .. PermissionCatalog.All.Select(permission => Create(RoleNames.Administrator, permission)),
            Create(RoleNames.Agent, PermissionCatalog.TicketsCreate),
            Create(RoleNames.Agent, PermissionCatalog.TicketsViewOwn),
            Create(RoleNames.Agent, PermissionCatalog.TicketsUpdateOwn),
            Create(RoleNames.Agent, PermissionCatalog.TicketsReopenOwn),
            Create(RoleNames.Agent, PermissionCatalog.TicketsManage),
            Create(RoleNames.Collaborator, PermissionCatalog.TicketsCreate),
            Create(RoleNames.Collaborator, PermissionCatalog.TicketsViewOwn),
            Create(RoleNames.Collaborator, PermissionCatalog.TicketsUpdateOwn),
            Create(RoleNames.Collaborator, PermissionCatalog.TicketsReopenOwn)
        ];

        builder.HasData(defaultRolePermissions);
    }

    private static object Create(string roleName, PermissionCatalogEntry permission) => new
    {
        Id = DeterministicGuid.Create($"role-permission:{roleName}:{permission.Code}"),
        RoleName = roleName,
        PermissionCode = permission.Code
    };
}

