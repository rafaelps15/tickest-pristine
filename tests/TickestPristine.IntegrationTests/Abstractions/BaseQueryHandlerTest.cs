using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Users;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Abstractions;

/// <summary>
/// Base dos testes de query handler: cada teste ganha um banco próprio, vazio e com o esquema das migrations, copiado de
/// um modelo migrado uma vez por execução no container da coleção. O cenário é gravado pelo <see cref="CreateDbContext"/>
/// sem as chaves estrangeiras (cada teste grava só o que lê), e o handler lê o banco pelo
/// <see cref="DbConnectionFactory"/> real, com o SQL e os tipos do Dapper de produção.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseQueryHandlerTest(IntegrationTestWebAppFactory factory) : IAsyncLifetime
{
    private const string TemplateDatabase = "tickest_query_template";

    private static readonly SemaphoreSlim TemplateLock = new(1, 1);
    private static string? _templateServer;

    private string _serverConnectionString = string.Empty;
    private string _connectionString = string.Empty;
    private string _databaseName = string.Empty;
    private NpgsqlDataSource? _dataSource;

    // Instante padrão dos testes que não dependem de um horário: segunda-feira, 26/10/2026, 08:10 em São Paulo.
    protected static readonly DateTime DefaultUtcNow = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

    protected IDbConnectionFactory DbConnectionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _serverConnectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Database")!;
        await EnsureTemplateAsync(_serverConnectionString);

        _databaseName = $"tickest_query_{Guid.NewGuid():N}";
        await ExecuteAsync(_serverConnectionString, $"CREATE DATABASE \"{_databaseName}\" TEMPLATE \"{TemplateDatabase}\"");

        _connectionString = WithDatabase(_serverConnectionString, _databaseName);
        _dataSource = NpgsqlDataSource.Create(_connectionString);
        DbConnectionFactory = new DbConnectionFactory(_dataSource);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await ExecuteAsync(_serverConnectionString, $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    /// <summary>
    /// Contexto do EF sobre o banco do teste, sem interceptors (nem auditoria nem eventos) e com as chaves estrangeiras
    /// desligadas na sessão, para gravar o cenário como os testes unitários faziam no banco em memória.
    /// </summary>
    protected ApplicationDbContext CreateDbContext()
    {
        var arrange = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Options = "-c session_replication_role=replica",
            Pooling = false
        };

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(arrange.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }

    protected static IDateTimeProvider CreateClock(DateTime? utcNow = null)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow ?? DefaultUtcNow);

        return clock;
    }

    protected static ISlaCalendarProvider CreateSlaCalendarProvider() => new TestSlaCalendarProvider();

    // Provedor que não encontra nenhum calendário, para os caminhos de calendário ausente.
    protected static ISlaCalendarProvider CreateMissingSlaCalendarProvider()
    {
        ISlaCalendarProvider provider = Substitute.For<ISlaCalendarProvider>();
        provider.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Failure<BusinessCalendar>(SlaCalendarErrors.NotFound(call.Arg<Guid>())));

        return provider;
    }

    // Plano "Padrão" de um chamado Urgente, no calendário "Expediente".
    protected static TicketSlaPlan CreateSlaPlan() => TicketSlaPlan.Default(TicketPriority.Urgent);

    // Chamado Urgente aberto em createdAtUtc com o plano de CreateSlaPlan e os cronômetros do ciclo 1, como a abertura
    // pela API grava.
    protected static async Task<Ticket> SeedTicketWithSlaAsync(
        ApplicationDbContext context,
        Guid requesterId,
        DateTime createdAtUtc,
        TicketStatus status = TicketStatus.Open)
    {
        TicketSlaPlan plan = CreateSlaPlan();
        BusinessCalendar calendar = DefaultSlaCalendar.Create();

        Ticket ticket = QueryTicketTestData.Create(
            requesterId: requesterId,
            status: status,
            createdAtUtc: createdAtUtc,
            priority: TicketPriority.Urgent,
            slaPlan: plan,
            calendar: calendar);

        // Os cronômetros do ciclo 1 nascem na abertura, e os prazos do chamado são os do cronômetro de resolução, como na
        // abertura pela API.
        SlaDeadlines resolution = plan.GetDeadlines(SlaMetric.Resolution, calendar, createdAtUtc)!;

        ticket
            .With(nameof(Ticket.DueAtUtc), resolution.DueOnUtc)
            .With(nameof(Ticket.SlaWarningAtUtc), resolution.WarningOnUtc);

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }

    // Usuário válido para os testes, com e-mail único; os demais campos podem ser ajustados depois de criado.
    protected static User CreateUser(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@example.com",
        FirstName = "Test",
        LastName = "User",
        Code = UserCodes.New(),
        IsActive = isActive,
        CreatedAtUtc = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
    };

    // Role com qualquer nome, como as que já existem no banco além das fixas de RoleNames.
    protected static Role CreateRole(string name) => Role.Create(name);

    // Setor num departamento novo, gravados no banco, para os testes que leem o setor do usuário.
    protected static async Task<Sector> SeedSectorAsync(
        ApplicationDbContext context,
        string sectorName,
        string departmentName,
        bool isActive = true)
    {
        var department = Department.Create(departmentName, departmentName, responsibleUser: null);
        var sector = Sector.Create(department, sectorName, description: null);

        if (!isActive)
        {
            sector.Deactivate();
        }

        context.Departments.Add(department);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector;
    }

    // Dá ao usuário uma role nova com as permissões informadas, gravada no banco (como a elegibilidade de atendente
    // lê, sem o cache de permissões).
    protected static async Task GrantPermissionsAsync(ApplicationDbContext context, Guid userId, params string[] permissionCodes)
    {
        Role role = CreateRole($"Role-{Guid.NewGuid():N}");

        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleName = role.Name });
        context.RolePermissions.AddRange(permissionCodes.Select(code => RolePermission.Create(role.Name, code)));

        await context.SaveChangesAsync();
    }

    // Convite de 72 horas criado em createdAtUtc, revogado um minuto depois se pedido.
    protected static async Task<UserInvitation> SeedInvitationAsync(
        ApplicationDbContext context,
        Guid userId,
        string token,
        DateTime createdAtUtc,
        bool revoked = false)
    {
        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = UserInvitations.HashToken(token),
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddHours(72),
            RevokedAtUtc = revoked ? createdAtUtc.AddMinutes(1) : null
        };

        context.UserInvitations.Add(invitation);
        await context.SaveChangesAsync();

        return invitation;
    }

    // O modelo é migrado uma vez por container: as migrations gravam as funções e as permissões fixas, que saem no fim,
    // para o banco de cada teste começar vazio.
    private static async Task EnsureTemplateAsync(string serverConnectionString)
    {
        await TemplateLock.WaitAsync();

        try
        {
            if (_templateServer == serverConnectionString)
            {
                return;
            }

            await ExecuteAsync(serverConnectionString, $"DROP DATABASE IF EXISTS \"{TemplateDatabase}\" WITH (FORCE)");
            await ExecuteAsync(serverConnectionString, $"CREATE DATABASE \"{TemplateDatabase}\"");

            var template = new NpgsqlConnectionStringBuilder(WithDatabase(serverConnectionString, TemplateDatabase))
            {
                Pooling = false
            };

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(template.ConnectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention()
                .Options;

            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            await ExecuteAsync(
                template.ConnectionString,
                $"""
                 DO $$
                 DECLARE tables text;
                 BEGIN
                     SELECT string_agg(format('%I.%I', schemaname, tablename), ', ') INTO tables
                     FROM pg_tables
                     WHERE schemaname = '{Schemas.Default}' AND tablename <> '{HistoryRepository.DefaultTableName}';

                     EXECUTE 'TRUNCATE TABLE ' || tables || ' CASCADE';
                 END $$;
                 """);

            _templateServer = serverConnectionString;
        }
        finally
        {
            TemplateLock.Release();
        }
    }

    private static string WithDatabase(string connectionString, string database) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = database }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // Os nomes dos bancos e das tabelas são gerados pelo próprio teste, sem entrada externa.
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }
}



