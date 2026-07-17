using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class User : Entity
{
    private User(Guid id, string email, string firstName, string lastName, DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public static User Create(string email, string firstName, string lastName, DateTime createdAtUtc)
    {
        var user = new User(Guid.NewGuid(), email, firstName, lastName, createdAtUtc);

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    public void Delete(DateTime deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc;

        Raise(new UserDeletedDomainEvent(Id));
    }
}
