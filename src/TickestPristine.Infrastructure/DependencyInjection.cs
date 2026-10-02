using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Infrastructure.Authentication;
using TickestPristine.Infrastructure.Authorization;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.Database.Seeding;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.Infrastructure.Storage;
using TickestPristine.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("The 'Database' connection string is required.");

        return services
            .AddServices()
            .AddDatabase(connectionString)
            .AddHealthChecks(connectionString)
            .AddAuthenticationInternal()
            .AddAuthorizationInternal();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddHybridCache();

        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        // Admin só é validado quando o seed lê os valores: a API não precisa da senha do admin para subir.
        services.AddOptions<AdminUserOptions>()
            .BindConfiguration(AdminUserOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddOptions<SeedingOptions>()
            .BindConfiguration(SeedingOptions.SectionName);

        services.AddSingleton<AdminUserSeeder>();
        services.AddSingleton<DatabaseSeeder>();

        // O seed roda junto com as migrations (Migrate/MigrateAsync e dotnet ef database update).
        services.AddDbContext<ApplicationDbContext>(
            (serviceProvider, options) => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention()
                .UseSeeding((context, _) =>
                    serviceProvider.GetRequiredService<DatabaseSeeder>().Seed(context))
                .UseAsyncSeeding((context, _, cancellationToken) =>
                    serviceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(context, cancellationToken)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, string connectionString)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(connectionString);

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IClaimsProvider, ClaimsProvider>();
        services.AddScoped<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<IPermissionProvider, PermissionProvider>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
