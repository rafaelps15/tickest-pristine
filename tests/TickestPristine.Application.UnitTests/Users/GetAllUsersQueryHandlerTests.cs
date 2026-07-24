using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAll;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetAllUsersQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnAllUsers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("user@tickestpristine.dev", "Test", "User");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(u => u.Id == user.Id && u.Email == "user@tickestpristine.dev");
    }

    [Fact]
    public async Task Handle_Should_ReturnAssignedRoles_WhenUserHasRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("user-with-role@tickestpristine.dev", "Test", "User");
        var role = Role.Create("Agent");
        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoles.Add(UserRole.Create(user.Id, role.Id));
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserSummaryResponse response = result.Value.Single(u => u.Id == user.Id);
        response.Roles.ShouldContain(r => r.Id == role.Id && r.Name == "Agent");
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyRoles_WhenUserHasNoRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("user-without-role@tickestpristine.dev", "Test", "User");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserSummaryResponse response = result.Value.Single(u => u.Id == user.Id);
        response.Roles.ShouldBeEmpty();
    }
}
