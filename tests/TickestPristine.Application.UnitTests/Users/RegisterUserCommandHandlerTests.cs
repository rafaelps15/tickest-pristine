using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Application.Users.Register;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickestPristine.Application.Users;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private static RegisterUserCommand Command => new("test@example.com", "Test", "User");

    [Fact]
    public async Task Handle_Should_ReturnEmailNotUnique_WhenEmailIsNotUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = Command.Email,
            FirstName = "Existing",
            LastName = "User",
            Code = UserCodes.New()
        });
        await context.SaveChangesAsync();

        IEmailSender emailSender = Substitute.For<IEmailSender>();
        RegisterUserCommandHandler handler = CreateHandler(context, emailSender);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        await emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_CreatePendingUserWithInvitationAndRaiseDomainEvents_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IEmailSender>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe(Command.Email);
        user.IsActive.ShouldBeTrue();
        user.CreatedAtUtc.ShouldBe(DefaultUtcNow);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRegisteredDomainEvent);

        (await context.UserCredentials.AnyAsync(c => c.UserId == user.Id)).ShouldBeFalse();
        (await context.UserRoles.SingleAsync(ur => ur.UserId == user.Id)).RoleName.ShouldBe(RoleNames.Collaborator);

        UserInvitation invitation = await context.UserInvitations.SingleAsync(i => i.UserId == user.Id);
        invitation.CreatedAtUtc.ShouldBe(DefaultUtcNow);
        invitation.ExpiresAtUtc.ShouldBe(DefaultUtcNow.AddHours(72));
        invitation.UsedAtUtc.ShouldBeNull();
        invitation.RevokedAtUtc.ShouldBeNull();
        invitation.TokenHash.Length.ShouldBe(64);
        invitation.DomainEvents.OfType<UserInvitationIssuedDomainEvent>().ShouldHaveSingleItem().UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Handle_Should_SendInvitationEmailWithLinkWhoseTokenMatchesStoredHash()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        RegisterUserCommandHandler handler = CreateHandler(context, emailSender);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        var message = (EmailMessage)emailSender.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0]!;
        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        UserInvitation invitation = await context.UserInvitations.SingleAsync(i => i.UserId == user.Id);

        message.To.ShouldBe(Command.Email);
        message.Subject.ShouldBe(UserInvitationEmail.Subject);
        message.TextBody.ShouldContain("Olá, Test!");
        message.TextBody.ShouldContain(user.Code);
        message.TextBody.ShouldContain(Command.Email);
        message.TextBody.ShouldContain(UserInvitationEmail.FormatBrasiliaTime(invitation.ExpiresAtUtc));
        message.HtmlBody.ShouldContain("Ativar minha conta");
        message.TextBody.ShouldNotContain(invitation.TokenHash);

        string token = ExtractToken(message.TextBody);
        UserInvitations.HashToken(token).ShouldBe(invitation.TokenHash);
    }

    [Fact]
    public async Task Handle_Should_KeepUserAndLogErrorWithoutToken_WhenEmailSendingFails()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IEmailSender emailSender = Substitute.For<IEmailSender>();
        emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP indisponível")));
        var logger = new RecordingLogger<RegisterUserCommandHandler>();
        RegisterUserCommandHandler handler = CreateHandler(context, emailSender, logger);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.AnyAsync(u => u.Id == result.Value)).ShouldBeTrue();
        (await context.UserInvitations.AnyAsync(i => i.UserId == result.Value)).ShouldBeTrue();

        var message = (EmailMessage)emailSender.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0]!;
        string token = ExtractToken(message.TextBody);
        RecordedLogEntry error = logger.Entries.ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Message.ShouldContain(result.Value.ToString());
        error.Message.ShouldNotContain(token);
    }

    /// <summary>
    /// Token do link de ativação do e-mail (o que vem depois de <c>#token=</c>).
    /// </summary>
    internal static string ExtractToken(string body)
    {
        const string marker = "#token=";
        int start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = body.IndexOfAny(['\r', '\n'], start);

        return Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
    }

    /// <summary>
    /// Montador de link igual ao da Infrastructure, com um endereço fixo.
    /// </summary>
    internal static IActivationLinkBuilder CreateLinkBuilder()
    {
        IActivationLinkBuilder linkBuilder = Substitute.For<IActivationLinkBuilder>();
        linkBuilder.Build(Arg.Any<string>())
            .Returns(call => $"https://tickest.exemplo/ativar-conta#token={Uri.EscapeDataString(call.Arg<string>())}");

        return linkBuilder;
    }

    private static RegisterUserCommandHandler CreateHandler(
        TestDbContext context,
        IEmailSender emailSender,
        ILogger<RegisterUserCommandHandler>? logger = null) =>
        new(
            context,
            emailSender,
            CreateLinkBuilder(),
            CreateClock(),
            logger ?? new RecordingLogger<RegisterUserCommandHandler>());
}
