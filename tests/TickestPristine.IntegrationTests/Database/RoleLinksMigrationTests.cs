using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Audit;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.Database.Migrations;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Spec 036: a migration que troca os vínculos de role do nome para o id preserva todos eles na ida e na volta, e o
/// banco passa a garantir o nome único sem maiúsculas e com até 100 caracteres.
/// </summary>
public sealed class RoleLinksMigrationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Migration_Should_PreserveEveryRoleLink_WhenAppliedAndRolledBack()
    {
        // Arrange: antes da migration, uma role criada à mão, com permissões e usuários, ao lado das roles fixas
        Guid firstUserId = await RegisterUserAsync(UniqueEmail());
        Guid secondUserId = await RegisterUserAsync(UniqueEmail());
        string manualRole = $"Supervisor-{Guid.NewGuid():N}";

        List<string> userLinksBefore = [];
        List<string> permissionLinksBefore = [];
        List<string> userLinksAfterUp = [];
        List<string> permissionLinksAfterUp = [];
        List<string> userLinksAfterDown = [];
        List<string> permissionLinksAfterDown = [];

        await ExecuteAsync(async context =>
        {
            IMigrator migrator = context.GetService<IMigrator>();
            string previousMigration = PreviousMigration(context);

            try
            {
                await migrator.MigrateAsync(previousMigration);
                await context.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO public.roles (id, name) VALUES ({Guid.NewGuid()}, {manualRole});
                    INSERT INTO public.role_permissions (id, role_name, permission_code)
                    VALUES ({Guid.NewGuid()}, {manualRole}, 'tickets:manage'), ({Guid.NewGuid()}, {manualRole}, 'users:read');
                    INSERT INTO public.user_roles (id, user_id, role_name)
                    VALUES ({Guid.NewGuid()}, {firstUserId}, {manualRole}), ({Guid.NewGuid()}, {firstUserId}, 'Atendente'),
                           ({Guid.NewGuid()}, {secondUserId}, {manualRole});
                    """);
                userLinksBefore = await ReadAsync(context, UserLinksByNameSql);
                permissionLinksBefore = await ReadAsync(context, PermissionLinksByNameSql);

                // Act: aplica a migration e depois a desfaz
                await migrator.MigrateAsync(nameof(Role_Links_By_Id_And_Citext_Name));
                userLinksAfterUp = await ReadAsync(context, UserLinksByIdSql);
                permissionLinksAfterUp = await ReadAsync(context, PermissionLinksByIdSql);

                await migrator.MigrateAsync(previousMigration);
                userLinksAfterDown = await ReadAsync(context, UserLinksByNameSql);
                permissionLinksAfterDown = await ReadAsync(context, PermissionLinksByNameSql);
            }
            finally
            {
                // O banco é compartilhado pela coleção: volta à última migration e tira a role criada aqui.
                await migrator.MigrateAsync();
                await context.Database.ExecuteSqlAsync(
                    $"""
                    DELETE FROM public.user_roles WHERE role_id = (SELECT id FROM public.roles WHERE name = {manualRole});
                    DELETE FROM public.roles WHERE name = {manualRole};
                    """);
            }
        });

        // Assert: os mesmos vínculos, sem perda nem duplicata, nos três momentos — inclusive os da role criada à mão
        userLinksBefore.ShouldContain($"{firstUserId}|{manualRole}");
        userLinksBefore.ShouldContain($"{firstUserId}|Atendente");
        userLinksBefore.ShouldContain($"{secondUserId}|{manualRole}");
        permissionLinksBefore.ShouldContain($"users:read|{manualRole}");

        userLinksAfterUp.ShouldBe(userLinksBefore, ignoreOrder: true);
        permissionLinksAfterUp.ShouldBe(permissionLinksBefore, ignoreOrder: true);
        userLinksAfterDown.ShouldBe(userLinksBefore, ignoreOrder: true);
        permissionLinksAfterDown.ShouldBe(permissionLinksBefore, ignoreOrder: true);
    }

    [Fact]
    public async Task Migration_Should_FailWithoutTouchingLinks_WhenTwoRoleNamesDifferOnlyInCase()
    {
        // Arrange: antes da migration, duas roles que o índice único do citext não aceitaria juntas
        string token = Guid.NewGuid().ToString("N");
        string lowerName = $"gestor-{token}";
        string upperName = $"GESTOR-{token}";
        Exception? failure = null;
        bool roleNameStillExists = false;

        await ExecuteAsync(async context =>
        {
            IMigrator migrator = context.GetService<IMigrator>();
            string previousMigration = PreviousMigration(context);

            try
            {
                await migrator.MigrateAsync(previousMigration);
                await context.Database.ExecuteSqlAsync(
                    $"INSERT INTO public.roles (id, name) VALUES ({Guid.NewGuid()}, {lowerName}), ({Guid.NewGuid()}, {upperName})");

                // Act
                failure = await Record.ExceptionAsync(() => migrator.MigrateAsync(nameof(Role_Links_By_Id_And_Citext_Name)));
                roleNameStillExists = await RoleNameColumnExistsAsync(context);
            }
            finally
            {
                await context.Database.ExecuteSqlAsync($"DELETE FROM public.roles WHERE name IN ({lowerName}, {upperName})");
                await migrator.MigrateAsync();
            }
        });

        // Assert: a migration é desfeita inteira, com a mensagem pedindo para renomear, e os vínculos continuam pelo nome
        failure.ShouldBeOfType<PostgresException>().MessageText.ShouldContain("mesmo nome sem diferenciar maiúsculas");
        roleNameStillExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Roles_Should_RejectNameLongerThan100AndDuplicateIgnoringCase_AtTheDatabase()
    {
        // Arrange
        string longest = new('a', 100);
        string tooLong = new('a', 101);

        await ExecuteAsync(async context =>
        {
            // Act
            Exception? tooLongFailure = await Record.ExceptionAsync(() => context.Database.ExecuteSqlAsync(
                $"INSERT INTO public.roles (id, name) VALUES ({Guid.NewGuid()}, {tooLong})"));
            Exception? duplicateFailure = await Record.ExceptionAsync(() => context.Database.ExecuteSqlAsync(
                $"INSERT INTO public.roles (id, name) VALUES ({Guid.NewGuid()}, {RoleNames.Agent.ToUpperInvariant()})"));
            int insertedLongest = await context.Database.ExecuteSqlAsync(
                $"INSERT INTO public.roles (id, name) VALUES ({Guid.NewGuid()}, {longest})");
            await context.Database.ExecuteSqlAsync($"DELETE FROM public.roles WHERE name = {longest}");

            // Assert
            tooLongFailure.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            duplicateFailure.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
            insertedLongest.ShouldBe(1);
        });
    }

    [Fact]
    public async Task RemovingAnAdministrator_Should_AuditTheAdministratorRoleVersion_WhenAnotherAdministratorRemains()
    {
        // Arrange: um segundo administrador, ao lado do administrador do seed
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid userId = await RegisterUserAsync(UniqueEmail());
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = new[] { RoleNames.Administrator } }))
            .EnsureSuccessStatusCode();
        List<Guid> entriesBefore = [];
        await ExecuteAsync(async context => entriesBefore = await AdministratorRoleAuditQuery(context).Select(e => e.Id).ToListAsync());

        // Act: tira a role de administrador dele, o que passa pelo guarda do último administrador
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = Array.Empty<string>() });

        // Assert: o incremento do token Version da role Administrador fica na auditoria (DEC-14)
        response.EnsureSuccessStatusCode();
        List<AuditLogEntry> newEntries = [];
        await ExecuteAsync(async context => newEntries = await AdministratorRoleAuditQuery(context)
            .Where(e => !entriesBefore.Contains(e.Id))
            .ToListAsync());

        AuditLogEntry entry = newEntries.ShouldHaveSingleItem();
        entry.Action.ShouldBe("Modified");
        entry.AffectedColumns.ShouldBe("Version");
        entry.OldValues.ShouldNotBeNull().ShouldContain("Version");
        entry.NewValues.ShouldNotBeNull().ShouldContain("Version");
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static IQueryable<AuditLogEntry> AdministratorRoleAuditQuery(ApplicationDbContext context)
    {
        string administratorRoleId = RoleIds.Administrator.ToString();

        return context.AuditLogs.AsNoTracking().Where(e => e.EntityName == "Role" && e.EntityId == administratorRoleId);
    }

    private static string PreviousMigration(ApplicationDbContext context)
    {
        var migrations = context.Database.GetMigrations().ToList();
        int index = migrations.FindIndex(m => m.EndsWith(nameof(Role_Links_By_Id_And_Citext_Name), StringComparison.Ordinal));

        return migrations[index - 1];
    }

    // Vínculos como "chave|nome da role", lidos em SQL: antes da migration a tabela guarda o nome; depois, o id.
    private const string UserLinksByNameSql =
        "SELECT user_id::text || '|' || role_name AS \"Value\" FROM public.user_roles";
    private const string PermissionLinksByNameSql =
        "SELECT permission_code || '|' || role_name AS \"Value\" FROM public.role_permissions";
    private const string UserLinksByIdSql =
        "SELECT l.user_id::text || '|' || r.name AS \"Value\" FROM public.user_roles l JOIN public.roles r ON r.id = l.role_id";
    private const string PermissionLinksByIdSql =
        "SELECT l.permission_code || '|' || r.name AS \"Value\" FROM public.role_permissions l JOIN public.roles r ON r.id = l.role_id";

    private static Task<List<string>> ReadAsync(ApplicationDbContext context, string sql) =>
        context.Database.SqlQueryRaw<string>(sql).ToListAsync();

    private static async Task<bool> RoleNameColumnExistsAsync(ApplicationDbContext context) =>
        await context.Database
            .SqlQuery<bool>(
                $"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'user_roles' AND column_name = 'role_name') AS \"Value\"")
            .SingleAsync();
}
