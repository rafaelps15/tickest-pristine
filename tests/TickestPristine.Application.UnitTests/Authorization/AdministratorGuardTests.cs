using TickestPristine.Application.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class AdministratorGuardTests : BaseHandlerTest
{
    [Fact]
    public async Task EnsureNotLastAdministratorAsync_Should_RejectSecondSave_WhenLastTwoAdministratorsAreRemovedConcurrently()
    {
        // Arrange: dois administradores ativos e duas requisições, cada uma com o seu contexto sobre o mesmo banco
        string databaseName = $"tickestpristine-{Guid.NewGuid()}";
        Guid firstAdministratorId;
        Guid secondAdministratorId;

        await using (TestDbContext seedContext = CreateDbContext(databaseName))
        {
            Role administratorRole = CreateRole(RoleNames.Administrator);
            seedContext.Roles.Add(administratorRole);
            firstAdministratorId = SeedAdministrator(seedContext, administratorRole);
            secondAdministratorId = SeedAdministrator(seedContext, administratorRole);
            await seedContext.SaveChangesAsync();
        }

        await using TestDbContext firstContext = CreateDbContext(databaseName);
        await using TestDbContext secondContext = CreateDbContext(databaseName);

        // Act: as duas checagens passam antes de qualquer gravação; a segunda requisição salva primeiro
        Result firstCheck = await AdministratorGuard.EnsureNotLastAdministratorAsync(
            firstContext, firstAdministratorId, CancellationToken.None);
        Result secondCheck = await AdministratorGuard.EnsureNotLastAdministratorAsync(
            secondContext, secondAdministratorId, CancellationToken.None);

        (await secondContext.Users.SingleAsync(u => u.Id == secondAdministratorId)).IsActive = false;
        await secondContext.SaveChangesAsync();

        (await firstContext.Users.SingleAsync(u => u.Id == firstAdministratorId)).IsActive = false;
        Func<Task> firstSave = () => firstContext.SaveChangesAsync();

        // Assert
        firstCheck.IsSuccess.ShouldBeTrue();
        secondCheck.IsSuccess.ShouldBeTrue();
        await firstSave.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }

    private static TestDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(databaseName).Options);

    private static Guid SeedAdministrator(TestDbContext context, Role administratorRole)
    {
        User user = CreateUser(isActive: true);
        context.Users.Add(user);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = administratorRole.Name });

        return user.Id;
    }
}
