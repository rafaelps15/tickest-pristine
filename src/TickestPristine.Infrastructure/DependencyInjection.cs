using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Infrastructure.Audit;
using TickestPristine.Infrastructure.Authentication;
using TickestPristine.Infrastructure.Authorization;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.Database.Seeding;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.Infrastructure.SlaCalendars;
using TickestPristine.Infrastructure.Storage;
using TickestPristine.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TickestPristine.SharedKernel;
using TickestPristine.Infrastructure.Tickets.SlaMonitoring;

namespace TickestPristine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("A connection string 'Database' é obrigatória.");

        return services
            .AddServices()
            .AddCaching(configuration)
            .AddDatabase(connectionString)
            .AddHealthChecks(connectionString)
            .AddAuthenticationInternal()
            .AddAuthorizationInternal();
    }

    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        // Com mais de uma instância da API, o Redis vira o segundo nível do HybridCache e a invalidação feita numa
        // instância passa a valer nas outras. Sem a connection string, o cache fica só em memória (uma instância).
        string? cacheConnectionString = configuration.GetConnectionString("Cache");

        if (!string.IsNullOrWhiteSpace(cacheConnectionString))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = cacheConnectionString);
        }

        // A cópia em memória de cada instância vale pouco tempo: mesmo com o Redis, uma instância que não tratou a
        // invalidação deixa de usar o valor antigo em no máximo um minuto.
        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(1)
        });

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<ISlaCalendarProvider, SlaCalendarProvider>();

        services.AddOptions<SlaMonitorSettings>()
            .BindConfiguration(SlaMonitorSettings.SettingsKey)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<SlaMonitorBackgroundService>();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        // Admin só é validado quando o seed cria o primeiro administrador: a API não precisa da senha dele para subir.
        services.AddOptions<AdminUserOptions>()
            .BindConfiguration(AdminUserOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddOptions<SeedingOptions>()
            .BindConfiguration(SeedingOptions.SectionName);

        services.AddSingleton<AdminUserSeeder>();
        services.AddSingleton<DefaultOrganizationSeeder>();
        services.AddSingleton<SampleDataSeeder>();
        services.AddSingleton<DatabaseSeeder>();

        // Uma fonte de conexões só, com um pool só, para o EF e para as leituras em SQL (IDbConnectionFactory).
        services.AddSingleton(new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // O seed roda junto com as migrations (Migrate/MigrateAsync e dotnet ef database update).
        services.AddDbContext<ApplicationDbContext>(
            (serviceProvider, options) => options
                .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<AuditLoggingInterceptor>())
                .UseSeeding((context, _) =>
                    serviceProvider.GetRequiredService<DatabaseSeeder>().Seed(context))
                .UseAsyncSeeding((context, _, cancellationToken) =>
                    serviceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(context, cancellationToken)));

        services.AddScoped<AuditLoggingInterceptor>();

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
        services.AddScoped<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveUserRequirement())
                .Build();

            options.AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());
        });

        services.AddScoped<IPermissionProvider, PermissionProvider>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationHandler, ActiveUserAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
