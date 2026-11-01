# 012 — Exemplos da nova modelagem

> **Documento superado — não é referência normativa.** Estes exemplos são da versão de 2026-10-25 da spec 012, que foi desfeita. O padrão vigente está na `spec.md` desta pasta e nas skills canônicas. Únicas alterações em relação ao original: os nomes de autores externos foram trocados por `[autor externo]` e os de projeto externo por `[projeto externo]`; o texto original está preservado no backup `C:\dev\_spec012_backup.tgz`.

Exemplos do código depois da spec 012, montados a partir do código atual (commit `fe1021e`). Eles mostram a forma
e a divisão de responsabilidades; a implementação pode ajustar detalhes (nomes de parâmetros, ordem das checagens),
desde que o comportamento observável continue o mesmo (REQ-11). Erros e tipos que ainda não existem estão marcados
com `// novo`. Construtores primários (DEC-5). Sem guards nem serviços de aplicação (DEC-4).

Referências do [autor externo] usadas em cada peça: entre parênteses no título.

---

## 1. Entidade rica: `Ticket` ([projeto externo] `User`, `GroupEvent.Cancel`; artigo 1)

**Hoje:** 13 propriedades com `set` público, e as regras espalhadas em `TicketStatusTransitions`, `TicketSla`,
`TicketValidationRules` e nos handlers.

**Depois** (`Domain/Tickets/Ticket.cs`):

```csharp
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 500;

    // O chamado entra em risco ao consumir 75% da janela de resolução.
    private const double SlaWarningRatio = 0.75;

    private Ticket()
    {
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public Guid SectorId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public DateTime SlaWarningAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsActive => Status is TicketStatus.Open or TicketStatus.InProgress;

    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Guid requesterId,
        Guid? assignedToUserId,
        Guid sectorId,
        Guid openedByUserId,
        DateTime utcNow)
    {
        TimeSpan resolutionTime = ResolutionTime(priority);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedByUserId = requesterId,
            AssignedToUserId = assignedToUserId,
            SectorId = sectorId,
            CreatedAtUtc = utcNow,
            DueAtUtc = utcNow + resolutionTime,
            SlaWarningAtUtc = utcNow + resolutionTime * SlaWarningRatio
        };

        ticket.Raise(new TicketCreatedDomainEvent(ticket.Id, openedByUserId));

        return ticket;
    }

    // Regras de acesso como métodos da entidade, como o project.IsOwner(userId) do artigo do [autor externo] sobre autorização.
    public bool IsRequester(Guid userId) => CreatedByUserId == userId;

    public bool IsParticipant(Guid userId) => CreatedByUserId == userId || AssignedToUserId == userId;

    public bool CanTransitionTo(TicketStatus newStatus) =>
        newStatus != Status && Status switch
        {
            TicketStatus.Open => newStatus is TicketStatus.InProgress or TicketStatus.Canceled,
            TicketStatus.InProgress => newStatus is TicketStatus.Resolved or TicketStatus.Canceled,
            TicketStatus.Resolved => newStatus is TicketStatus.Closed or TicketStatus.Open,
            TicketStatus.Closed => newStatus is TicketStatus.Open,
            TicketStatus.Canceled => newStatus is TicketStatus.Open,
            _ => false
        };

    public Result ChangeStatus(TicketStatus newStatus, Guid changedByUserId, DateTime utcNow)
    {
        if (!CanTransitionTo(newStatus))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(Status, newStatus));
        }

        TicketStatus previousStatus = Status;
        Status = newStatus;

        // A primeira resolução decide o SLA; reabrir e resolver de novo não muda o resultado.
        if (newStatus == TicketStatus.Resolved && ResolvedAtUtc is null)
        {
            ResolvedAtUtc = utcNow;
        }

        Raise(new TicketStatusChangedDomainEvent(Id, previousStatus, newStatus, changedByUserId));

        return Result.Success();
    }

    public Result Close(Guid closedByUserId, DateTime utcNow) =>
        ChangeStatus(TicketStatus.Closed, closedByUserId, utcNow);

    public Result Reopen(Guid reopenedByUserId)
    {
        if (IsActive)
        {
            return Result.Failure(TicketErrors.AlreadyActive(Id));
        }

        if (!CanTransitionTo(TicketStatus.Open))
        {
            return Result.Failure(TicketErrors.InvalidStatusTransition(Status, TicketStatus.Open));
        }

        Status = TicketStatus.Open;

        Raise(new TicketReopenedDomainEvent(Id, reopenedByUserId));

        return Result.Success();
    }

    public Result Assign(Guid assigneeId, Guid assignedByUserId)
    {
        if (!IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(Id));
        }

        // ... mesma regra de hoje do AssignTicketCommandHandler (responsável igual ao atual), e o evento
        // TicketReassignedDomainEvent com os mesmos dados de hoje.
        AssignedToUserId = assigneeId;

        return Result.Success();
    }

    public Result Delete(DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure(TicketErrors.NotActive(Id));
        }

        DeletedAtUtc = utcNow;

        Raise(new TicketDeletedDomainEvent(Id));

        return Result.Success();
    }

    public TicketSlaStatus GetSlaStatus(DateTime utcNow) =>
        GetSlaStatus(Status, DueAtUtc, SlaWarningAtUtc, ResolvedAtUtc, utcNow);

    // Versão por colunas, para as projeções das queries, que não carregam a entidade inteira.
    public static TicketSlaStatus GetSlaStatus(
        TicketStatus status,
        DateTime dueAtUtc,
        DateTime slaWarningAtUtc,
        DateTime? resolvedAtUtc,
        DateTime utcNow)
    {
        if (resolvedAtUtc is { } resolvedAt)
        {
            return resolvedAt <= dueAtUtc ? TicketSlaStatus.Met : TicketSlaStatus.Missed;
        }

        if (status == TicketStatus.Canceled)
        {
            return TicketSlaStatus.NotApplicable;
        }

        if (utcNow >= dueAtUtc)
        {
            return TicketSlaStatus.Breached;
        }

        return utcNow >= slaWarningAtUtc ? TicketSlaStatus.AtRisk : TicketSlaStatus.OnTime;
    }

    private static TimeSpan ResolutionTime(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Urgent => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72)
        };
}
```

`Unassign`, `ChangeDescription` e os demais seguem o mesmo formato. O `ChangeDescription` devolve se o texto mudou,
como o `User.Update` do [projeto externo], para o handler saber se grava o histórico.

---

## 2. Fábricas da `TicketHistory` (artigo 2: a fábrica é o único ponto de criação)

**Hoje:** `Application/TicketHistories/TicketHistoryEntries.cs`, classe estática usada em 13 arquivos.

**Depois** (`Domain/Tickets/TicketHistory.cs`):

```csharp
public sealed class TicketHistory : Entity
{
    private TicketHistory()
    {
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public TicketHistoryAction Action { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static TicketHistory Created(Guid ticketId, Guid createdByUserId, DateTime occurredAtUtc) =>
        Create(ticketId, createdByUserId, TicketHistoryAction.Created, occurredAtUtc, newValue: TicketStatus.Open.ToString());

    public static TicketHistory StatusChanged(
        Guid ticketId, Guid? changedByUserId, TicketStatus oldStatus, TicketStatus newStatus, DateTime occurredAtUtc) =>
        Create(ticketId, changedByUserId, TicketHistoryAction.StatusChanged, occurredAtUtc,
            oldStatus.ToString(), newStatus.ToString());

    public static TicketHistory Reopened(
        Guid ticketId, Guid? changedByUserId, TicketStatus oldStatus, DateTime occurredAtUtc) =>
        Create(ticketId, changedByUserId, TicketHistoryAction.Reopened, occurredAtUtc,
            oldStatus.ToString(), TicketStatus.Open.ToString());

    // Reassigned, Unassigned, DescriptionChanged, MessageAdded, AttachmentAdded... no mesmo formato.

    private static TicketHistory Create(
        Guid ticketId,
        Guid? changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ChangedByUserId = changedByUserId,
        Action = action,
        OldValue = oldValue,
        NewValue = newValue,
        OccurredAtUtc = occurredAtUtc
    };
}
```

---

## 3. Handlers que só orquestram ([projeto externo] `CancelGroupEventCommandHandler`)

### Close

**Hoje:** testa a transição com `TicketStatusTransitions`, atribui `ticket.Status`, chama `ticket.Raise(...)` e monta
o histórico com `TicketHistoryEntries`.

**Depois:**

```csharp
internal sealed class CloseTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CloseTicketCommand>
{
    public async Task<Result> Handle(CloseTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        TicketStatus previousStatus = ticket.Status;
        DateTime utcNow = dateTimeProvider.UtcNow;

        Result result = ticket.Close(userContext.UserId, utcNow);

        if (result.IsFailure)
        {
            return result;
        }

        context.TicketHistories.Add(TicketHistory.StatusChanged(
            ticket.Id, userContext.UserId, previousStatus, ticket.Status, utcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

### Reopen (conferência de acesso no próprio handler, sem guard; DEC-4)

**Hoje:** chama `TicketAccessGuard.EnsureOwnerOrManagerAsync(...)`, testa `IsActive` e `CanTransition` com
`TicketStatusTransitions`, atribui o status, dispara o evento e monta o histórico com `TicketHistoryEntries.Create(...)`.

**Depois:**

```csharp
internal sealed class ReopenTicketCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReopenTicketCommand>
{
    public async Task<Result> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        Ticket? ticket = await context.Tickets.SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound(command.TicketId));
        }

        // O solicitante reabre com tickets:reopen-own; os demais precisam de tickets:manage.
        string requiredPermission = ticket.IsRequester(userContext.UserId)
            ? PermissionCodes.Tickets.ReopenOwn
            : PermissionCodes.Tickets.Manage;

        if (!await permissionProvider.HasPermissionAsync(userContext.UserId, requiredPermission, cancellationToken))
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        TicketStatus previousStatus = ticket.Status;

        Result result = ticket.Reopen(userContext.UserId);

        if (result.IsFailure)
        {
            return result;
        }

        context.TicketHistories.Add(TicketHistory.Reopened(
            ticket.Id, userContext.UserId, previousStatus, dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

### Create

**Hoje:** monta o `Ticket` com `new Ticket { ... }`, calcula os prazos com `TicketSla.DueAt` e `TicketSla.WarningAt`,
dispara o evento e monta o histórico com `TicketHistoryEntries.Created(...)`.

**Depois:** as checagens que dependem de consulta (solicitante e responsável existem e estão ativos; setor ativo) e a
de quem chama (abrir em nome de outra pessoa) ficam no handler, como hoje. O final passa a ser:

```csharp
DateTime utcNow = dateTimeProvider.UtcNow;

var ticket = Ticket.Create(
    command.Title,
    command.Description,
    command.Priority,
    requesterId: openedByUserId,
    command.AssignedToUserId,
    command.SectorId,
    openedByUserId: userContext.UserId,
    utcNow);

context.Tickets.Add(ticket);
context.TicketHistories.Add(TicketHistory.Created(ticket.Id, userContext.UserId, utcNow));

await context.SaveChangesAsync(cancellationToken);

return ticket.Id;
```

---

## 4. O que eram guards, escrito no handler (artigo "Authentication and Authorization in Clean Architecture"; template; [projeto externo] `CancelGroupEvent`; DEC-4)

**Hoje:** três classes estáticas em `Application/Authorization/` (`TicketAccessGuard`, `AdministratorGuard`,
`PrivilegeEscalationGuard`), chamadas pelos handlers.

**Depois:** a pasta `Application/Authorization/` deixa de existir. Cada handler escreve a conferência de que precisa,
depois de carregar os dados, como o `CancelGroupEvent` do [projeto externo] faz com o dono do evento. Os status HTTP
(403, 404, 409) não mudam (DEC-6).

**Ver um chamado e o que pertence a ele** (detalhe, mensagens, histórico, anexos), no início da query:

```csharp
var participants = await context.Tickets
    .Where(t => t.Id == query.TicketId)
    .Select(t => new { t.CreatedByUserId, t.AssignedToUserId })
    .SingleOrDefaultAsync(cancellationToken);

if (participants is null)
{
    return Result.Failure<TicketResponse>(TicketErrors.NotFound(query.TicketId));
}

// Solicitante e responsável sempre veem o chamado; os demais precisam de tickets:manage.
bool isParticipant = participants.CreatedByUserId == userContext.UserId ||
                     participants.AssignedToUserId == userContext.UserId;

if (!isParticipant &&
    !await permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, cancellationToken))
{
    return Result.Failure<TicketResponse>(UserErrors.Unauthorized());
}
```

Num command que já carregou o `Ticket`, a primeira parte vira `ticket.IsParticipant(userContext.UserId)`.

**Último administrador**, no handler que tira o acesso (desativar o usuário, trocar as funções dele):

```csharp
bool isAdministrator = await context.UserRoles.AnyAsync(
    ur => ur.UserId == user.Id && ur.RoleName == RoleNames.Administrator,
    cancellationToken);

if (isAdministrator)
{
    // A role é lida antes da checagem: de duas operações simultâneas que tirariam os dois últimos administradores,
    // só a primeira consegue salvar (token de concorrência).
    Role administratorRole = await context.Roles.SingleAsync(r => r.Name == RoleNames.Administrator, cancellationToken);

    bool anotherActiveAdministratorExists = await context.UserRoles.AnyAsync(
        ur => ur.RoleName == RoleNames.Administrator &&
              ur.UserId != user.Id &&
              context.Users.Any(u => u.Id == ur.UserId && u.IsActive),
        cancellationToken);

    if (!anotherActiveAdministratorExists)
    {
        return Result.Failure(RoleErrors.LastAdministrator);
    }

    context.Entry(administratorRole).Property<int>("Version").CurrentValue++;
}
```

**Escalada de privilégio** fica do mesmo jeito: o handler monta a lista de permissões que mudam, confere quais são
administrativas no `PermissionCatalog` e compara com as permissões de quem chama, pelo `IPermissionProvider`.

**Custo aceito (DEC-4):** a conferência de acesso ao chamado aparece em cada handler que a usa, e a do último
administrador nos handlers que tiram esse acesso. É a troca que o template faz: um pouco de repetição em vez de uma
classe auxiliar.

---

## 5. Constantes na entidade, usadas pelo validator e pelo EF ([projeto externo] `FirstName.MaxLength`; DEC-11)

**Hoje:** `Application/Tickets/TicketValidationRules.cs` no validator, e os números `200` e `500` escritos de novo no
`TicketConfiguration`.

**Depois:**

```csharp
// Application/Tickets/Create/CreateTicketCommandValidator.cs
RuleFor(c => c.Title).NotEmpty().MaximumLength(Ticket.TitleMaxLength);
RuleFor(c => c.Description)
    .NotEmpty()
    .Length(Ticket.DescriptionMinLength, Ticket.DescriptionMaxLength);
```

```csharp
// Infrastructure/Tickets/TicketConfiguration.cs
builder.Property(t => t.Title).HasMaxLength(Ticket.TitleMaxLength).HasColumnType("citext");

builder.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength);
```

Os números continuam os mesmos, então a migration não muda nada no esquema.

---

## 6. Filtros de SLA direto na query (template: `Where` no próprio handler)

**Hoje:** `Application/Tickets/TicketSlaFilters.cs`, com `WhereActive()` e `WhereSlaStatus(...)`.

**Depois:** cada query escreve o `Where`. Na listagem:

```csharp
IQueryable<Ticket> tickets = context.Tickets.AsNoTracking();

tickets = query.SlaStatus switch
{
    null => tickets,
    TicketSlaStatus.Met => tickets.Where(t => t.ResolvedAtUtc != null && t.ResolvedAtUtc <= t.DueAtUtc),
    TicketSlaStatus.Missed => tickets.Where(t => t.ResolvedAtUtc != null && t.ResolvedAtUtc > t.DueAtUtc),
    TicketSlaStatus.NotApplicable => tickets.Where(t => t.ResolvedAtUtc == null && t.Status == TicketStatus.Canceled),
    TicketSlaStatus.Breached => tickets.Where(t =>
        t.ResolvedAtUtc == null && t.Status != TicketStatus.Canceled && nowUtc >= t.DueAtUtc),
    TicketSlaStatus.AtRisk => tickets.Where(t =>
        t.ResolvedAtUtc == null && t.Status != TicketStatus.Canceled &&
        nowUtc >= t.SlaWarningAtUtc && nowUtc < t.DueAtUtc),
    _ => tickets.Where(t =>
        t.ResolvedAtUtc == null && t.Status != TicketStatus.Canceled && nowUtc < t.SlaWarningAtUtc)
};
```

E na projeção, a situação de cada chamado vem da regra da entidade, pela versão por colunas:

```csharp
SlaStatus = Ticket.GetSlaStatus(t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc, nowUtc)
```

**Atenção:** o resumo do painel usa os mesmos filtros (no prazo, em risco, violado). Sem o `TicketSlaFilters`, a regra
aparece em dois handlers e na entidade. Os testes de hoje que conferem que as versões concordam continuam e passam a
cobrir os dois handlers.

---

## 7. Value object `Email` ([projeto externo] `Email`; DEC-3)

```csharp
// Domain/Users/Email.cs
public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<Email>(UserErrors.EmailRequired); // novo
        }

        string normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<Email>(UserErrors.EmailTooLong); // novo
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
```

```csharp
// Infrastructure/Users/UserConfiguration.cs — mesma coluna de texto, sem mudar o esquema
builder.Property(u => u.Email)
    .HasConversion(email => email.Value, value => Email.Create(value).Value)
    .HasMaxLength(Email.MaxLength);
```

As buscas passam a comparar o value object: `context.Users.SingleOrDefaultAsync(u => u.Email == email, ...)`, com
`email` vindo de `Email.Create(command.Email)`.

---

## 8. Usuário: `User`, `UserCredential` e `RefreshToken` ([projeto externo] `User`; DEC-2 opção B)

### `User`

```csharp
public sealed class User : Entity, IAuditable
{
    public const int NameMaxLength = 100;
    private const string CodePrefix = "usr_";

    private User()
    {
    }

    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Code { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }

    public static User Create(Email email, string firstName, string lastName, DateTime utcNow)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Code = $"{CodePrefix}{Ulid.NewUlid()}",
            IsActive = true,
            CreatedAtUtc = utcNow
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    public Result Deactivate(DateTime utcNow) { /* IsActive, DeactivatedAtUtc e UserDeactivatedDomainEvent, como hoje */ }

    public Result Activate() { /* idem, com UserActivatedDomainEvent */ }

    public bool Rename(string firstName, string lastName) { /* devolve se mudou, como o User.Update do [projeto externo] */ }
}
```

### `UserCredential`

O [projeto externo] põe no Domain a interface de conferência de senha (`IPasswordHashChecker`) e a passa por parâmetro
para a entidade. Aqui fica igual: a `IPasswordHasher` da Application passa a estender a interface do Domain.

```csharp
// Domain/Users/IPasswordHashChecker.cs — novo
public interface IPasswordHashChecker
{
    bool Verify(string password, string passwordHash);
}
```

```csharp
// Domain/Users/UserCredential.cs
public sealed class UserCredential : Entity, IAuditable
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private UserCredential()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; }
    public bool MustChangePassword { get; private set; }
    public Guid SecurityStamp { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndsAtUtc { get; private set; }

    public static UserCredential Create(Guid userId, string passwordHash) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PasswordHash = passwordHash,
        SecurityStamp = Guid.NewGuid()
    };

    // Senha provisória (ex.: a do primeiro administrador): o login só entrega tokens depois da troca.
    public static UserCredential CreateTemporary(Guid userId, string passwordHash)
    {
        UserCredential credential = Create(userId, passwordHash);
        credential.MustChangePassword = true;

        return credential;
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndsAtUtc > utcNow;

    // Senha errada conta uma tentativa; a 5ª seguida bloqueia a conta por 15 minutos. Senha certa zera o contador.
    public Result VerifyPassword(string password, IPasswordHashChecker passwordHashChecker, DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return Result.Failure(UserErrors.LockedOut);
        }

        if (!passwordHashChecker.Verify(password, PasswordHash))
        {
            FailedLoginAttempts++;

            if (FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                FailedLoginAttempts = 0;
                LockoutEndsAtUtc = utcNow.Add(LockoutDuration);
            }

            return Result.Failure(IsLockedOut(utcNow) ? UserErrors.LockedOut : UserErrors.InvalidCredentials);
        }

        FailedLoginAttempts = 0;
        LockoutEndsAtUtc = null;

        return Result.Success();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid();

        Raise(new UserPasswordChangedDomainEvent(UserId));
    }
}
```

### `RefreshToken`

```csharp
public sealed class RefreshToken : Entity
{
    public static readonly TimeSpan MaxSessionDuration = TimeSpan.FromDays(30);

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime SessionExpiresOnUtc { get; private set; }
    public string? PreviousTokenHash { get; private set; }
    public DateTime? RotatedAtUtc { get; private set; }
    public Guid SecurityStamp { get; private set; }

    public static RefreshToken Create(
        Guid userId, string tokenHash, DateTime tokenExpiresOnUtc, Guid securityStamp, DateTime utcNow)
    {
        DateTime sessionExpiresOnUtc = utcNow.Add(MaxSessionDuration);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresOnUtc = Earliest(tokenExpiresOnUtc, sessionExpiresOnUtc),
            SessionExpiresOnUtc = sessionExpiresOnUtc,
            SecurityStamp = securityStamp
        };

        refreshToken.Raise(new RefreshTokenCreatedDomainEvent(refreshToken.Id, userId));

        return refreshToken;
    }

    public bool IsExpired(DateTime utcNow) => ExpiresOnUtc < utcNow;

    // Chamado antes de remover o token, para o evento de revogação continuar sendo disparado.
    public void Revoke() => Raise(new RefreshTokenRevokedDomainEvent(Id, UserId));

    // Troca o token para que o anterior não possa ser reutilizado; a validade nunca passa do limite da sessão.
    public void Rotate(string newTokenHash, DateTime newTokenExpiresOnUtc, DateTime utcNow)
    {
        PreviousTokenHash = TokenHash;
        RotatedAtUtc = utcNow;
        TokenHash = newTokenHash;
        ExpiresOnUtc = Earliest(newTokenExpiresOnUtc, SessionExpiresOnUtc);

        Raise(new RefreshTokenRotatedDomainEvent(Id, UserId));
    }

    private static DateTime Earliest(DateTime first, DateTime second) => first < second ? first : second;
}
```

### Login (tudo no handler, como o `LoginUserCommandHandler` do template)

**Hoje:** chama `UserCredentialsVerifier.VerifyAsync(...)` (estática, com toda a regra de bloqueio dentro) e
`UserSessions.StartAsync(...)` (estática, monta o `RefreshToken` com `new RefreshToken { ... }`).

**Depois:** as regras estão nas entidades; a busca no banco e a gravação ficam no handler.

```csharp
internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;
        Email email = Email.Create(command.Email).Value;

        User? user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Gasta o mesmo tempo de uma conferência de senha, para o tempo de resposta não revelar quais e-mails existem.
            _ = passwordHasher.Hash(command.Password);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        UserCredential? credential = await context.UserCredentials
            .SingleOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (credential is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        Result verification = credential.VerifyPassword(command.Password, passwordHasher, utcNow);

        // A tentativa (errada ou certa) é gravada na hora: as recusas abaixo não salvam.
        await context.SaveChangesAsync(cancellationToken);

        if (verification.IsFailure)
        {
            return Result.Failure<AccessTokensResponse>(verification.Error);
        }

        // Checado só depois da senha, para não revelar a situação da conta a quem não a conhece.
        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        if (credential.MustChangePassword)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.PasswordChangeRequired);
        }

        List<RefreshToken> expiredTokens = await context.RefreshTokens
            .Where(r => r.UserId == user.Id && r.ExpiresOnUtc < utcNow)
            .ToListAsync(cancellationToken);

        context.RefreshTokens.RemoveRange(expiredTokens);

        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        GeneratedRefreshToken generated = tokenProvider.GenerateRefreshToken();

        context.RefreshTokens.Add(RefreshToken.Create(
            user.Id, tokenProvider.HashRefreshToken(generated.Token), generated.ExpiresOnUtc, credential.SecurityStamp, utcNow));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, generated.Token);
    }
}
```

Hoje a tentativa só é gravada quando muda algo (erro, ou acerto depois de erros); a implementação mantém essa mesma
condição. O handler de troca da senha provisória repete a busca e a conferência, como o template faria.

### Troca de senha

**Hoje:** confere a senha atual e chama `UserPasswords.ReplaceAsync(...)`, que atribui três campos da credencial,
dispara o evento e revoga as sessões.

**Depois:**

```csharp
if (!passwordHasher.Verify(command.CurrentPassword, credential.PasswordHash))
{
    return Result.Failure(UserErrors.InvalidCurrentPassword);
}

credential.ChangePassword(passwordHasher.Hash(command.NewPassword));

// Nenhuma sessão aberta com a senha anterior continua valendo.
List<RefreshToken> sessions = await context.RefreshTokens
    .Where(r => r.UserId == credential.UserId)
    .ToListAsync(cancellationToken);

foreach (RefreshToken session in sessions)
{
    session.Revoke(); // novo: dispara o RefreshTokenRevokedDomainEvent, como hoje
}

context.RefreshTokens.RemoveRange(sessions);

await context.SaveChangesAsync(cancellationToken);
```

### Renovação de token

**Hoje:** atribui `PreviousTokenHash`, `RotatedAtUtc`, `TokenHash` e `ExpiresOnUtc` no handler, com
`RefreshTokenHasher.Hash(...)` e `UserSessions.Earliest(...)`.

**Depois:**

```csharp
string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken); // novo: o hash passa para a Infrastructure

// ... busca, detecção de reuso e conferência do security stamp como hoje ...

if (refreshToken.IsExpired(nowUtc))
{
    return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
}

GeneratedRefreshToken generated = tokenProvider.GenerateRefreshToken();

refreshToken.Rotate(tokenProvider.HashRefreshToken(generated.Token), generated.ExpiresOnUtc, nowUtc);
```

---

## 9. Calendário de SLA (DEC-7 e DEC-8)

**Hoje:** `BusinessCalendar` na Application, com um `Func<int, IReadOnlySet<DateOnly>>` no construtor;
`DefaultSlaCalendar` e `SlaCalendarIds` (Id reservado `...0001`) para o padrão em código; o provedor testa
`calendarId == SlaCalendarIds.Default`; a lista de feriados repete `new(new DateOnly(...), "...", HolidayKind.National)`.

**Depois:** a pasta `Application/SlaCalendars/` deixa de existir.

```csharp
// Domain/SlaCalendars/BusinessCalendar.cs — os laços do cálculo continuam como hoje; sai o Func
public sealed class BusinessCalendar(
    IEnumerable<SlaWorkPeriod> workSchedule,
    TimeZoneInfo timeZone,
    bool is24x7,
    IReadOnlySet<DateOnly> holidays,
    bool usesBrazilianNationalHolidays)
{
    // AddBusinessTime, SubtractBusinessTime e BusinessTimeBetween como hoje.

    private bool IsHoliday(DateOnly day) =>
        holidays.Contains(day) ||
        (usesBrazilianNationalHolidays && BrazilianNationalHolidays.For(day.Year).Any(h => h.Date == day));
}
```

```csharp
// Domain/SlaCalendars/SlaCalendar.cs — o padrão continua em código, sem linha no banco e sem seed
public sealed class SlaCalendar : Entity, IAuditable
{
    // Expediente padrão: segunda a sexta, das 8h às 18h de São Paulo, sem os feriados nacionais.
    public static BusinessCalendar Default { get; } = new(
        [.. Weekdays.Select(day => new SlaWorkPeriod(day, new TimeOnly(8, 0), new TimeOnly(18, 0)))],
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"),
        is24x7: false,
        holidays: new HashSet<DateOnly>(),
        usesBrazilianNationalHolidays: true);

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // ... propriedades da entidade
}
```

```csharp
// Infrastructure/SlaCalendars/SlaCalendarProvider.cs — sem Id reservado
public async Task<Result<BusinessCalendar>> GetAsync(Guid? calendarId, CancellationToken cancellationToken = default)
{
    if (calendarId is not { } id)
    {
        return SlaCalendar.Default;
    }

    // cópia serializável no cache, como a DEC-13 da spec 004
}
```

```csharp
// Domain/SlaCalendars/BrazilianNationalHolidays.cs — a lista vira uma tabela
DateOnly easter = EasterSunday(year);

BrazilianHoliday Fixed(int month, int day, string name) =>
    new(new DateOnly(year, month, day), name, HolidayKind.National);

BrazilianHoliday FromEaster(int days, string name, HolidayKind kind = HolidayKind.National) =>
    new(easter.AddDays(days), name, kind);

List<BrazilianHoliday> holidays =
[
    Fixed(1, 1,      "Confraternização Universal"),
    FromEaster(-48,  "Carnaval (segunda-feira)", HolidayKind.OptionalDayOff),
    FromEaster(-47,  "Carnaval (terça-feira)",   HolidayKind.OptionalDayOff),
    FromEaster(-2,   "Sexta-feira Santa"),
    Fixed(4, 21,     "Tiradentes"),
    Fixed(5, 1,      "Dia do Trabalhador"),
    FromEaster(60,   "Corpus Christi",           HolidayKind.OptionalDayOff),
    Fixed(9, 7,      "Independência do Brasil"),
    Fixed(10, 12,    "Nossa Senhora Aparecida"),
    Fixed(11, 2,     "Finados"),
    Fixed(11, 15,    "Proclamação da República"),
    Fixed(12, 25,    "Natal")
];

if (year >= BlackConsciousnessDayFirstYear)
{
    holidays.Add(Fixed(11, 20, "Dia Nacional de Zumbi e da Consciência Negra"));
}
```

---

## 10. Departamento: regra de consulta como `if` no handler ([projeto externo] `PublishEvent`/`CancelGroupEvent`)

**Hoje:** `DepartmentResponsibleUser.EnsureExistsAsync(context, responsibleUserId, ...)`, classe estática usada por 2
handlers.

**Depois**, direto no handler:

```csharp
if (command.ResponsibleUserId is { } responsibleUserId &&
    !await context.Users.AnyAsync(u => u.Id == responsibleUserId, cancellationToken))
{
    return Result.Failure(UserErrors.NotFound(responsibleUserId));
}
```

E as constantes saem de `DepartmentValidationRules` para o `Department` (`Department.NameMaxLength`,
`Department.DescriptionMaxLength`), como no exemplo 5.

---

## 11. Teste de regra direto na entidade, sem mock

```csharp
public class TicketTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reopen_Should_ReturnAlreadyActive_WhenTicketIsOpen()
    {
        Ticket ticket = CreateTicket();

        Result result = ticket.Reopen(Guid.NewGuid());

        result.Error.ShouldBe(TicketErrors.AlreadyActive(ticket.Id));
    }

    [Fact]
    public void ChangeStatus_Should_KeepFirstResolution_WhenResolvedTwice()
    {
        Ticket ticket = CreateTicket();
        ticket.ChangeStatus(TicketStatus.InProgress, Guid.NewGuid(), UtcNow);
        ticket.ChangeStatus(TicketStatus.Resolved, Guid.NewGuid(), UtcNow);
        ticket.Reopen(Guid.NewGuid());
        ticket.ChangeStatus(TicketStatus.InProgress, Guid.NewGuid(), UtcNow.AddHours(1));

        ticket.ChangeStatus(TicketStatus.Resolved, Guid.NewGuid(), UtcNow.AddHours(2));

        ticket.ResolvedAtUtc.ShouldBe(UtcNow);
    }

    private static Ticket CreateTicket() => Ticket.Create(
        "Impressora parada", "A impressora do setor não liga.", TicketPriority.High,
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), UtcNow);
}
```
