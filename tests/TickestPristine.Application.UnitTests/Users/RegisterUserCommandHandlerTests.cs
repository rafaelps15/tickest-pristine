using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Users.Register;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private static RegisterUserCommand Command =>
        new("test@example.com", "Test", "User", "Password123!");

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailIsNotUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = Command.Email,
            FirstName = "Existing",
            LastName = "User",
            Code = $"usr_{Ulid.NewUlid()}"
        });
        await context.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
    }

    [Fact]
    public async Task Handle_Should_CreateUserWithHashedPasswordAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var requesterRole = new Role { Id = Guid.NewGuid(), Name = "Colaborador", IsDefault = true };
        context.Roles.Add(requesterRole);
        await context.SaveChangesAsync();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Command.Password).Returns("hashed-password");

        var handler = new RegisterUserCommandHandler(context, passwordHasher);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe(Command.Email);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRegisteredDomainEvent);

        UserCredential credential = await context.UserCredentials.SingleAsync(c => c.UserId == user.Id);
        credential.PasswordHash.ShouldBe("hashed-password");

        UserRole userRole = await context.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
        userRole.RoleId.ShouldBe(requesterRole.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnDefaultRoleNotConfigured_WhenNoRoleIsMarkedAsDefault()
    {
        // Arrange: role chamada "Requester", mas sem a flag de role padrão
        await using TestDbContext context = CreateDbContext();
        context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Requester" });
        await context.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.DefaultRoleNotConfigured);
        (await context.Users.AnyAsync()).ShouldBeFalse();
    }
}
