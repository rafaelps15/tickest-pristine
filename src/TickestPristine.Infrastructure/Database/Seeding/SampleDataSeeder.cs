using Bogus;
using TickestPristine.Application.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Application.Users;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Chamados de exemplo para desenvolvimento, com "Seeding:SampleData" ligado: os do <see cref="SampleTicketCatalog"/>
/// cujo setor existe e está ativo. Só roda num banco sem chamados. Cria também usuários fictícios de cada role, menos
/// Administrador (atendente1@..., colaborador1@..., com a senha "Seeding:SampleUserPassword"): os chamados são abertos
/// pelos colaboradores e atendidos pelos atendentes, para a tela pessoal do administrador não vir cheia de dados de
/// exemplo.
/// Cada chamado nasce com o histórico que a API teria gravado até o status dele e os prazos de SLA da prioridade.
/// </summary>
internal sealed class SampleDataSeeder(
    IOptions<SeedingOptions> seedingOptions,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider)
{
    // Semente fixa: todo ambiente recebe a mesma massa de dados, o que ajuda a reproduzir bugs.
    private const int RandomSeed = 2026;

    private const string SampleEmailDomain = "tickest.dev";

    /// <summary>
    /// Usuários fictícios por role, com o prefixo do e-mail e a quantidade. O Administrador fica de fora: o primeiro
    /// administrador já vem do <see cref="AdminUserSeeder"/>.
    /// </summary>
    private static readonly (string RoleName, string EmailPrefix, int Count)[] SampleUserGroups =
    [
        (RoleNames.Agent, "atendente", 3),
        (RoleNames.Collaborator, "colaborador", 6)
    ];

    /// <summary>
    /// Status que cada chamado percorre a partir de Aberto até o status final (cada passo é conferido em CreateHistory).
    /// </summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> StatusPaths = new()
    {
        [TicketStatus.Open] = [],
        [TicketStatus.InProgress] = [TicketStatus.InProgress],
        [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Closed] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Canceled] = [TicketStatus.Canceled]
    };

    public async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!seedingOptions.Value.SampleData || await context.Tickets.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        Dictionary<string, Guid> sectorIdsByName = await context.Sectors
            .Where(s => s.IsActive)
            .ToDictionaryAsync(s => s.Name, s => s.Id, cancellationToken);

        var faker = new Faker("pt_BR") { Random = new Randomizer(RandomSeed) };
        DateTime now = dateTimeProvider.UtcNow;

        Dictionary<string, Guid[]> userIdsByRole = await EnsureSampleUsersAsync(context, faker, now, cancellationToken);
        // Colaboradores abrem os chamados; atendentes atendem e resolvem.
        Guid[] requesterIds = userIdsByRole[RoleNames.Collaborator];
        Guid[] agentIds = userIdsByRole[RoleNames.Agent];

        // Setores renomeados ou desativados pelo administrador ficam sem chamados de exemplo.
        foreach (SampleTicket sampleTicket in SampleTicketCatalog.Tickets)
        {
            if (!sectorIdsByName.TryGetValue(sampleTicket.SectorName, out Guid sectorId))
            {
                continue;
            }

            Ticket ticket = CreateTicket(faker, sampleTicket, sectorId, requesterIds, agentIds, now);

            List<TicketHistory> history = [.. CreateHistory(faker, ticket, now)];

            context.Tickets.Add(ticket);
            context.TicketHistories.AddRange(history);
            context.TicketSlaTimers.Add(CreateResolutionTimer(ticket, history));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Garante os usuários fictícios de cada grupo, com a role e a credencial, e devolve os ids por role. Quem já existe
    /// com o mesmo e-mail é reaproveitado (o banco pode ter perdido só os chamados). O nome é sorteado mesmo para quem
    /// já existe, para a sequência da semente não depender do estado do banco.
    /// </summary>
    private async Task<Dictionary<string, Guid[]>> EnsureSampleUsersAsync(
        ApplicationDbContext context,
        Faker faker,
        DateTime now,
        CancellationToken cancellationToken)
    {
        string password = seedingOptions.Value.SampleUserPassword is { Length: > 0 } configuredPassword
            ? configuredPassword
            : throw new InvalidOperationException(
                $"'{SeedingOptions.SectionName}:{nameof(SeedingOptions.SampleUserPassword)}' é obrigatório quando " +
                $"'{SeedingOptions.SectionName}:{nameof(SeedingOptions.SampleData)}' está ligado. Defina-o com dotnet user-secrets.");

        string[] emails = [.. SampleUserGroups.SelectMany(group =>
            Enumerable.Range(1, group.Count).Select(number => SampleEmail(group.EmailPrefix, number)))];

        Dictionary<string, Guid> existingUserIds = await context.Users
            .Where(u => emails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email, u => u.Id, cancellationToken);

        var userIdsByRole = new Dictionary<string, Guid[]>();

        foreach ((string roleName, string emailPrefix, int count) in SampleUserGroups)
        {
            var userIds = new List<Guid>();

            for (int number = 1; number <= count; number++)
            {
                string email = SampleEmail(emailPrefix, number);
                string firstName = faker.Name.FirstName();
                string lastName = faker.Name.LastName();

                if (existingUserIds.TryGetValue(email, out Guid existingUserId))
                {
                    userIds.Add(existingUserId);
                    continue;
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Code = UserCodes.New(),
                    IsActive = true,
                    CreatedAtUtc = now
                };

                context.Users.Add(user);
                context.UserCredentials.Add(new UserCredential
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    PasswordHash = passwordHasher.Hash(password),
                    MustChangePassword = false
                });
                context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = roleName });

                userIds.Add(user.Id);
            }

            userIdsByRole[roleName] = [.. userIds];
        }

        return userIdsByRole;
    }

    private static string SampleEmail(string emailPrefix, int number) => $"{emailPrefix}{number}@{SampleEmailDomain}";

    private static Ticket CreateTicket(
        Faker faker,
        SampleTicket sampleTicket,
        Guid sectorId,
        Guid[] requesterIds,
        Guid[] agentIds,
        DateTime now)
    {
        TicketStatus status = faker.PickRandom<TicketStatus>();
        TimeSpan resolutionTime = TicketSla.ResolutionTime(sampleTicket.Priority);

        // Os ativos nascem dentro ou pouco além da janela do SLA, para o painel ter chamados no prazo, em risco e
        // atrasados. Os demais nascem no último ano, até uma semana atrás, para caberem a resolução e o fechamento.
        DateTime createdAtUtc = TicketStatusTransitions.IsActive(status)
            ? now - resolutionTime * faker.Random.Double(0.1, 1.5)
            : faker.Date.Past(1, now.AddDays(-7));

        // Resolvidos dentro ou fora do prazo, para o painel mostrar SLA cumprido e violado.
        DateTime? resolvedAtUtc = StatusPaths[status].Contains(TicketStatus.Resolved)
            ? createdAtUtc + resolutionTime * faker.Random.Double(0.3, 1.4)
            : null;

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = sampleTicket.Title,
            Description = sampleTicket.Description,
            Priority = sampleTicket.Priority,
            Status = status,
            CreatedByUserId = faker.PickRandom(requesterIds),
            // Chamado que passou por "Em andamento" teve um atendente atendendo.
            AssignedToUserId = StatusPaths[status].Contains(TicketStatus.InProgress) ? faker.PickRandom(agentIds) : null,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = TicketSla.DueAt(createdAtUtc, sampleTicket.Priority),
            SlaWarningAtUtc = TicketSla.WarningAt(createdAtUtc, sampleTicket.Priority),
            ResolvedAtUtc = resolvedAtUtc,
            SlaPlan = TicketSla.LegacyPlan(sampleTicket.Priority)
        };
    }

    /// <summary>
    /// Cronômetro de resolução do ciclo 1 nas horas corridas do plano legado, como a migration grava nos chamados
    /// existentes: concluído na resolução e parado no cancelamento sem resolução.
    /// </summary>
    private static TicketSlaTimer CreateResolutionTimer(Ticket ticket, IEnumerable<TicketHistory> history) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticket.Id,
        Metric = SlaMetric.Resolution,
        Cycle = 1,
        TargetMinutes = ticket.SlaPlan.ResolutionMinutes,
        CalendarId = ticket.SlaPlan.CalendarId,
        StartedAtUtc = ticket.CreatedAtUtc,
        WarningOnUtc = ticket.SlaWarningAtUtc,
        DueOnUtc = ticket.DueAtUtc,
        CompletedOnUtc = ticket.ResolvedAtUtc,
        StoppedOnUtc = ticket.ResolvedAtUtc is null && ticket.Status == TicketStatus.Canceled
            ? history.Last().OccurredAtUtc
            : null
    };

    /// <summary>
    /// Histórico igual ao que os handlers de evento de domínio gravam: a criação e cada mudança de status, em ordem.
    /// Cada passo é conferido com TicketStatusTransitions, para o seed nunca gerar um histórico que a API recusaria.
    /// </summary>
    private static IEnumerable<TicketHistory> CreateHistory(Faker faker, Ticket ticket, DateTime now)
    {
        DateTime occurredAtUtc = ticket.CreatedAtUtc;

        yield return TicketHistoryEntries.Created(ticket.Id, ticket.CreatedByUserId, occurredAtUtc);

        TicketStatus oldStatus = TicketStatus.Open;

        foreach (TicketStatus newStatus in StatusPaths[ticket.Status])
        {
            if (!TicketStatusTransitions.CanTransition(oldStatus, newStatus))
            {
                throw new InvalidOperationException(
                    $"O caminho de status dos dados de exemplo não é permitido por TicketStatusTransitions: {oldStatus} -> {newStatus}.");
            }

            // A resolução cai na data gravada no chamado; o que vem antes dela fica entre a abertura e a resolução.
            occurredAtUtc = newStatus switch
            {
                TicketStatus.Resolved => ticket.ResolvedAtUtc!.Value,
                TicketStatus.Closed => faker.Date.Between(occurredAtUtc, occurredAtUtc.AddDays(2)),
                _ => faker.Date.Between(occurredAtUtc, ticket.ResolvedAtUtc ?? now)
            };

            yield return TicketHistoryEntries.StatusChanged(
                ticket.Id,
                ticket.AssignedToUserId ?? ticket.CreatedByUserId,
                oldStatus,
                newStatus,
                occurredAtUtc);

            oldStatus = newStatus;
        }
    }
}
