using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Corpo ou parâmetros que não puderam ser lidos viram exceção em todos os ambientes, e não só em Development:
        // assim chegam ao GlobalExceptionHandler e respondem em Problem Details (spec 011, DEC-6).
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        return services;
    }
}
