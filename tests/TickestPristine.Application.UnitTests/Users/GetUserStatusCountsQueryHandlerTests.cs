using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetStatusCounts;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUserStatusCountsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_CountActivePendingAndInactiveSeparately_WhenUsersHaveEverySituation()
    {
        // Arrange: administrador e Ana ativos com senha, Bruno pendente, Carla desativada depois de ativar a conta e
        // Davi desativado antes de ativar
        await using TestDbContext context = CreateDbContext();
        AddUser(context, isActive: true, hasCredential: true);
        AddUser(context, isActive: true, hasCredential: true);
        AddUser(context, isActive: true, hasCredential: false);
        AddUser(context, isActive: false, hasCredential: true);
        AddUser(context, isActive: false, hasCredential: false);
        await context.SaveChangesAsync();

        var handler = new GetUserStatusCountsQueryHandler(context);

        // Act
        Result<UserStatusCountsResponse> result = await handler.Handle(new GetUserStatusCountsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(5);
        result.Value.ActiveCount.ShouldBe(2);
        result.Value.PendingActivationCount.ShouldBe(1);
        result.Value.InactiveCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnZeros_WhenThereAreNoUsers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var handler = new GetUserStatusCountsQueryHandler(context);

        // Act
        Result<UserStatusCountsResponse> result = await handler.Handle(new GetUserStatusCountsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.ActiveCount.ShouldBe(0);
        result.Value.PendingActivationCount.ShouldBe(0);
        result.Value.InactiveCount.ShouldBe(0);
    }

    private static void AddUser(TestDbContext context, bool isActive, bool hasCredential)
    {
        User user = CreateUser(isActive);
        context.Users.Add(user);

        if (hasCredential)
        {
            context.UserCredentials.Add(new UserCredential { Id = Guid.NewGuid(), UserId = user.Id, PasswordHash = "hash" });
        }
    }
}
