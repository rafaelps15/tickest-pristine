# Modelos de autorização

Cada conferência fica onde estão os dados dela: uma permissão fixa no endpoint, uma regra sobre o próprio estado da entidade na entidade, as permissões efetivas do usuário atrás de uma abstração, e tudo o que precisa de consulta no handler. O handler coordena; a entidade nunca consulta nada.

**Adaptação do Tickest:** a permissão nova entra em `PermissionCodes` e em `PermissionCatalog`, e a constante da rota fica em `Endpoints/{Aggregate}/Permissions.cs`. Os guards atuais (`TicketAccessGuard`, `PrivilegeEscalationGuard`, `AdministratorGuard`) são legado listado em "Em migração" no `CLAUDE.md`; código novo não cria guard estático.

## Permissão fixa — no endpoint

```csharp
app.MapGet("users/{userId}", async (
    Guid userId,
    IQueryHandler<GetUserByIdQuery, UserResponse> handler,
    CancellationToken cancellationToken) =>
{
    var query = new GetUserByIdQuery(userId);

    Result<UserResponse> result = await handler.Handle(query, cancellationToken);

    return result.Match(Results.Ok, CustomResults.Problem);
})
.HasPermission(Permissions.UsersAccess)
.WithTags(Tags.Users);
```

## Regra sobre o próprio estado da entidade — na entidade

Quando a resposta depende só de dados que a entidade guarda (o dono, o status), o método da entidade confere e devolve o erro:

```csharp
/// <summary>Arquiva o projeto; só o dono pode arquivar.</summary>
public Result Archive(Guid requestedBy, DateTime utcNow)
{
    if (requestedBy != OwnerId)
    {
        return Result.Failure(ProjectErrors.OnlyOwnerCanArchive(Id));
    }

    IsArchived = true;
    ArchivedAt = utcNow;

    Raise(new ProjectArchivedDomainEvent(Id));

    return Result.Success();
}
```

- O handler passa `userContext.UserId`; a entidade não sabe nada da requisição HTTP nem de onde vêm as permissões.

## Permissões efetivas — pelo provedor de permissões

Quando a resposta depende das funções ou permissões do usuário, o handler pergunta à abstração em `Application/Abstractions/Authorization/` e repassa a resposta:

```csharp
bool canArchiveAny = await permissionProvider.HasPermissionAsync(
    userContext.UserId, Permissions.ProjectsArchiveAny, cancellationToken);

Result result = canArchiveAny
    ? project.ForceArchive(dateTimeProvider.UtcNow)
    : project.Archive(userContext.UserId, dateTimeProvider.UtcNow);
```

- As permissões vêm do `IPermissionProvider`, a mesma fonte que o endpoint usa; o handler nunca lê tabelas de funções para decidir uma permissão.

## Conferência que precisa de consulta — método privado do handler

```csharp
internal sealed class GetProjectTodosQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext)
    : IQueryHandler<GetProjectTodosQuery, List<TodoResponse>>
{
    public async Task<Result<List<TodoResponse>>> Handle(
        GetProjectTodosQuery query,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionToQueryProject(query.ProjectId, cancellationToken))
        {
            return Result.Failure<List<TodoResponse>>(UserErrors.Unauthorized());
        }

        // ... consulta projetando para TodoResponse
    }

    private async Task<bool> HasPermissionToQueryProject(Guid projectId, CancellationToken cancellationToken) =>
        await context.Projects.AnyAsync(
            p => p.Id == projectId &&
                 (p.OwnerId == userContext.UserId ||
                  context.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userContext.UserId)),
            cancellationToken);
}
```

- Chame-o de `HasPermissionTo{Action}`; ele devolve `bool` e o handler devolve o erro.
- Uma consulta nunca vai para dentro da entidade, nem por um serviço injetado.
- Comece pelo método privado. Extraia uma peça compartilhada só quando a mesma conferência se repetir comprovadamente entre handlers, e mantenha-a como instância injetada, nunca classe estática.
