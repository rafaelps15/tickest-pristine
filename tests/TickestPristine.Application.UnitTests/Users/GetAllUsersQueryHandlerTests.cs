using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetAll;
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
}
