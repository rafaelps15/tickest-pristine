using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class LastAdministratorCheckTests : BaseHandlerTest
{
    [Fact]
    public async Task IsLastAdministratorWithLockAsync_Should_RejectSecondSave_WhenLastTwoAdministratorsAreRemovedConcurrently()
    {
        // Arrange: dois administradores ativos e duas requisições, cada uma com o seu contexto sobre o mesmo banco
        string databaseName = $"tickestpristine-{Guid.NewGuid()}";
        Guid firstAdministratorId;
        Guid secondAdministratorId;

        await using (TestDbContext seedContext = CreateDbContext(databaseName))
        {
            Role administratorRole = CreateRole(RoleNames.Administrator);
            seedContext.Roles.Add(administratorRole);
            firstAdministratorId = SeedAdministrator(seedContext, administratorRole, hasPassword: true);
            secondAdministratorId = SeedAdministrator(seedContext, administratorRole, hasPassword: true);
            await seedContext.SaveChangesAsync();
        }

        await using TestDbContext firstContext = CreateDbContext(databaseName);
        await using TestDbContext secondContext = CreateDbContext(databaseName);

        // Act: as duas checagens passam antes de qualquer gravação; a segunda requisição salva primeiro
        bool firstIsLast = await new LastAdministratorCheck(firstContext).IsLastAdministratorWithLockAsync(
            firstAdministratorId, CancellationToken.None);
        bool secondIsLast = await new LastAdministratorCheck(secondContext).IsLastAdministratorWithLockAsync(
            secondAdministratorId, CancellationToken.None);

        (await secondContext.Users.SingleAsync(u => u.Id == secondAdministratorId)).IsActive = false;
        await secondContext.SaveChangesAsync();

        (await firstContext.Users.SingleAsync(u => u.Id == firstAdministratorId)).IsActive = false;
        Func<Task> firstSave = () => firstContext.SaveChangesAsync();

        // Assert
        firstIsLast.ShouldBeFalse();
        secondIsLast.ShouldBeFalse();
        await firstSave.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task IsLastAdministratorWithLockAsync_Should_BeTrueWithoutLock_WhenOtherAdministratorIsPendingActivation()
    {
        // Arrange: o outro administrador está ativo, mas ainda não definiu a senha (convite pendente)
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = CreateRole(RoleNames.Administrator);
        context.Roles.Add(administratorRole);
        Guid leavingAdministratorId = SeedAdministrator(context, administratorRole, hasPassword: true);
        SeedAdministrator(context, administratorRole, hasPassword: false);
        await context.SaveChangesAsync();

        // Act
        bool isLast = await new LastAdministratorCheck(context).IsLastAdministratorWithLockAsync(
            leavingAdministratorId, CancellationToken.None);

        // Assert
        isLast.ShouldBeTrue();
        context.Entry(administratorRole).Property(role => role.Version).IsModified.ShouldBeFalse();
    }

    private static TestDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(databaseName).Options);

    private static Guid SeedAdministrator(TestDbContext context, Role administratorRole, bool hasPassword)
    {
        User user = CreateUser(isActive: true);
        context.Users.Add(user);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = administratorRole.Name });

        if (hasPassword)
        {
            context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "hash" });
        }

        return user.Id;
    }
}
