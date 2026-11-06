using TickestPristine.Application.Users.GetByEmail;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Users;
using TickestPristine.Application.Users;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Users;

public sealed class GetUserByEmailQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    [Fact]
    public async Task Handle_Should_ReturnNotFoundByEmail_WhenNoUserHasThatEmail()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var handler = new GetUserByEmailQueryHandler(DbConnectionFactory);
        var query = new GetUserByEmailQuery("missing@example.com");

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFoundByEmail);
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenEmailExists()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = UserCodes.New()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUserByEmailQueryHandler(DbConnectionFactory);
        var query = new GetUserByEmailQuery(user.Email);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
    }
}
