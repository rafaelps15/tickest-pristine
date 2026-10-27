using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.Register;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class Register : IEndpoint
{
    /// <summary>
    /// Cadastro sem senha (spec 008): o usuário define a dele pelo convite enviado por e-mail.
    /// </summary>
    public sealed record Request(string Email, string FirstName, string LastName);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/register", async (
            Request request,
            ICommandHandler<RegisterUserCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.FirstName, request.LastName);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersCreate);
    }
}
