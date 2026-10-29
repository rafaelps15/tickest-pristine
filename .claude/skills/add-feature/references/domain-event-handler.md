# Modelos de handler de evento de domínio

Os arquivos vão em `src/Application/{Feature}/{Event}/`, um handler por reação. Os handlers implementam `IDomainEventHandler<T>` do `SharedKernel`, são encontrados pelo Scrutor em `Application/DependencyInjection.cs` e rodam depois de o `ApplicationDbContext.SaveChangesAsync` salvar a mudança.

## Handler

`src/Application/Projects/ProjectArchived/RemoveMembersOnProjectArchivedDomainEventHandler.cs`:

```csharp
using Application.Abstractions.Data;
using Domain.Projects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Projects.ProjectArchived;

internal sealed class RemoveMembersOnProjectArchivedDomainEventHandler(IApplicationDbContext context)
    : IDomainEventHandler<ProjectArchivedDomainEvent>
{
    public async Task Handle(ProjectArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        List<ProjectMember> members = await context.ProjectMembers
            .Where(m => m.ProjectId == domainEvent.ProjectId)
            .ToListAsync(cancellationToken);

        context.ProjectMembers.RemoveRange(members);

        await context.SaveChangesAsync(cancellationToken);
    }
}
```

## Convenções

- **Uma reação por handler**, com o nome `{Action}On{Event}DomainEventHandler`; vários handlers para o mesmo evento são normais (`RemoveMembersOnProjectArchived...`, `NotifyOwnerOnProjectArchived...`).
- **Pasta com o nome do evento**, para que todas as reações a ele fiquem juntas. Um handler com o nome só do evento (`ProjectArchivedDomainEventHandler`) não comporta a segunda reação.
- **`internal sealed`, construtor primário.** Sem registro na DI.
- **O evento leva ids**, nunca a entidade; um evento de alteração leva também o valor anterior. O handler carrega o que precisa.
- **Sem `OccurredOnUtc` no evento**: um handler que precisa de hora usa o `IDateTimeProvider` ao processar o evento.
- **Roda depois do `SaveChangesAsync`**, num escopo novo, fora da transação do command: pode falhar sozinho. Um handler que altera dados chama o próprio `SaveChangesAsync`.
- **Não serve para registros que precisam existir junto com a mudança** (filho, histórico, auditoria): o método do agregado os cria e o command os salva (padrão 7 da `domain-modeling`).
- **Quando o mesmo efeito colateral está copiado em vários command handlers**, ele vai para um handler de evento e sai dos command handlers.

## Referência de nomes

| Artefato | Padrão | Exemplo |
|---|---|---|
| Evento | `{Entity}{PastTenseVerb}DomainEvent` | `ProjectArchivedDomainEvent` |
| Pasta | `src/Application/{Feature}/{Event}/` | `Projects/ProjectArchived/` |
| Handler | `{Action}On{Event}DomainEventHandler` | `RemoveMembersOnProjectArchivedDomainEventHandler` |
| Teste unitário | `{Handler}Tests` | `RemoveMembersOnProjectArchivedDomainEventHandlerTests` |
