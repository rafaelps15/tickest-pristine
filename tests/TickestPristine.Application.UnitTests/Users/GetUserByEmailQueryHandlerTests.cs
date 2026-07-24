using TickestPristine.Application.Users.GetByEmail;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUserByEmailQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFoundByEmail_WhenNoUserHasThatEmail()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetUserByEmailQueryHandler(context);
        var query = new GetUserByEmailQuery("missing@example.com");

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFoundByEmail");
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenEmailExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("someone@example.com", "Some", "One");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUserByEmailQueryHandler(context);
        var query = new GetUserByEmailQuery(user.Email);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
    }
}
