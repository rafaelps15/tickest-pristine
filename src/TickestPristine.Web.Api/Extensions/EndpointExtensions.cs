using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TickestPristine.Infrastructure.Authorization;
using TickestPristine.Web.Api.Endpoints;

namespace TickestPristine.Web.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(
        this WebApplication app,
        RouteGroupBuilder? routeGroupBuilder = null)
    {
        IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }

    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder app, string permission)
    {
        return app.RequireAuthorization(new HasPermissionAttribute(permission));
    }

    /// <summary>
    /// Só exige o token válido, sem conferir se o usuário continua ativo; as demais rotas usam
    /// <c>RequireAuthorization()</c>, que barra o usuário desativado.
    /// </summary>
    public static RouteHandlerBuilder RequireAuthenticatedUser(this RouteHandlerBuilder app)
    {
        return app.RequireAuthorization(AuthorizationPolicies.AuthenticatedUser);
    }
}
