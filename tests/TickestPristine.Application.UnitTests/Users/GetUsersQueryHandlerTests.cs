using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAll;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUsersQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnAllUsers_WhenUsersExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@tickestpristine.dev",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(u => u.Id == user.Id && u.Email == "user@tickestpristine.dev" && u.IsActive);
    }

    [Fact]
    public async Task Handle_Should_ReturnAssignedRoles_WhenUserHasRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user-with-role@tickestpristine.dev",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        var role = new Role { Id = Guid.NewGuid(), Name = "Atendente" };
        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = role.Id });
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserSummaryResponse response = result.Value.Single(u => u.Id == user.Id);
        response.Roles.ShouldContain(r => r.Id == role.Id && r.Name == "Atendente");
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyRoles_WhenUserHasNoRoles()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user-without-role@tickestpristine.dev",
            FirstName = "Test",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        // Act
        Result<List<UserSummaryResponse>> result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserSummaryResponse response = result.Value.Single(u => u.Id == user.Id);
        response.Roles.ShouldBeEmpty();
    }
}
