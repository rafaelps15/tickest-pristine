# Templates de slice de query

Os arquivos ficam em `src/Application/{Feature}/{UseCase}/`. Queries são leituras: sem validator, sem eventos de domínio, sem `SaveChangesAsync`.

## Query

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.GetOverdue;

public sealed record GetOverdueTodosQuery : IQuery<List<TodoResponse>>;
```

Com parâmetros: `public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;`

## DTO de resposta

Fica ao lado da query. É plano, fácil de serializar e nunca é uma entidade de domínio.

```csharp
namespace Application.Todos.GetOverdue;

public sealed class TodoResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

Cada pasta de caso de uso tem o seu próprio `TodoResponse` — não compartilhe DTOs entre slices, mesmo que hoje pareçam iguais.

## Handler

Restrinja ao usuário atual e projete com `.Select` direto no DTO:

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.GetOverdue;

internal sealed class GetOverdueTodosQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOverdueTodosQuery, List<TodoResponse>>
{
    public async Task<Result<List<TodoResponse>>> Handle(
        GetOverdueTodosQuery query,
        CancellationToken cancellationToken)
    {
        List<TodoResponse> todos = await context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId &&
                               !todoItem.IsCompleted &&
                               todoItem.DueDate < dateTimeProvider.UtcNow)
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                UserId = todoItem.UserId,
                Description = todoItem.Description,
                DueDate = todoItem.DueDate,
                Labels = todoItem.Labels,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt,
                CompletedAt = todoItem.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return todos;
    }
}
```

Em queries de um único item, retorne `Result.Failure<TodoResponse>(TodoItemErrors.NotFound(id))` quando nada for encontrado.

## Listagem paginada

Nenhuma coleção que cresce sem limite volta inteira. Use as peças de `Application/Abstractions/Pagination` — não crie paginação própria. Catálogos pequenos e fixos podem devolver `List<T>`.

Escolha a estratégia pela tela:

| Situação | Estratégia |
|---|---|
| Tela administrativa, pular para qualquer página, conjunto moderado | Offset (`PagedResponse<T>`) |
| Feed, rolagem infinita, tabela grande, dados que mudam enquanto o cliente navega | Keyset/cursor (`CursorPagedResponse<T>`) |

Regras das duas:
- Filtre antes de paginar.
- Ordene de forma estável, com a chave por último (`.ThenBy(x => x.Id)`), para nenhum item repetir ou sumir entre páginas.
- Crie na configuração do EF um índice que acompanhe a ordenação (e o filtro principal), ex.: `builder.HasIndex(t => new { t.IsCompleted, t.CreatedAt })`.
- Só conte o total quando a tela mostra o número de páginas: o `COUNT` numa tabela grande pode custar mais que a página.

### Offset

A query termina com `PageRequest Paging` (nunca `Page`/`PageSize` soltos) e devolve `PagedResponse<T>`:

```csharp
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.GetAll;

public sealed record GetTodosQuery(
    bool? IsCompleted,
    string? Search,
    PageRequest Paging) : IQuery<PagedResponse<TodoSummaryResponse>>;
```

O endpoint só repassa página e tamanho (veja [endpoint.md](endpoint.md)). O handler filtra, ordena, projeta no DTO e termina com `ToPagedListAsync`, que ajusta os valores inválidos (página mínima 1, tamanho padrão 20, máximo 100) e faz a contagem e a página — não repita esses ajustes nem crie validação para eles:

```csharp
internal sealed class GetTodosQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetTodosQuery, PagedResponse<TodoSummaryResponse>>
{
    public async Task<Result<PagedResponse<TodoSummaryResponse>>> Handle(
        GetTodosQuery query,
        CancellationToken cancellationToken)
    {
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        PagedResponse<TodoSummaryResponse> todos = await context.TodoItems
            .Where(todoItem => todoItem.UserId == userContext.UserId &&
                               (query.IsCompleted == null || todoItem.IsCompleted == query.IsCompleted) &&
                               (search == null || todoItem.Description.Contains(search)))
            .OrderByDescending(todoItem => todoItem.CreatedAt)
            .ThenByDescending(todoItem => todoItem.Id)
            .Select(todoItem => new TodoSummaryResponse
            {
                Id = todoItem.Id,
                Description = todoItem.Description,
                IsCompleted = todoItem.IsCompleted
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return todos;
    }
}
```

Campo calculado por uma regra do Domain que não se traduz para SQL entra no próprio `.Select`, chamando o método do Domain com as colunas (ex.: `IsOverdue = TodoRules.IsOverdue(todoItem.DueDate, nowUtc)`): o EF lê as colunas e calcula na projeção final, também com paginação. Não preencha o DTO num segundo passo depois da consulta.

### Keyset/cursor

O cliente pede a primeira página sem cursor e, para as seguintes, devolve o cursor da página anterior. Datas quase sempre podem repetir, então o cursor é **composto** (data + chave). O handler aplica os filtros próprios e chama `AfterCursor`, que filtra depois do cursor (em vez de pular linhas) e ordena do mais novo para o mais antigo com os mesmos seletores; depois projeta no DTO e termina com `ToCursorPagedListAsync`, que ajusta o tamanho, busca um item a mais para saber se há próxima página e monta o cursor do último item. Nada disso se repete no handler — nunca escreva o filtro `data < cursor || ...` à mão. Não há total. A resposta é o `CursorPagedResponse<T>` compartilhado:

```csharp
public sealed record GetTodoCommentsQuery(
    Guid TodoItemId,
    CursorPageRequest Paging) : IQuery<CursorPagedResponse<TodoCommentResponse>>;

internal sealed class GetTodoCommentsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetTodoCommentsQuery, CursorPagedResponse<TodoCommentResponse>>
{
    public async Task<Result<CursorPagedResponse<TodoCommentResponse>>> Handle(
        GetTodoCommentsQuery query,
        CancellationToken cancellationToken)
    {
        CursorPagedResponse<TodoCommentResponse> page = await context.TodoComments
            .Where(c => c.TodoItemId == query.TodoItemId)
            .AfterCursor(query.Paging, c => c.CreatedAt, c => c.Id)
            .Select(c => new TodoCommentResponse { Id = c.Id, Content = c.Content, CreatedAt = c.CreatedAt })
            .ToCursorPagedListAsync(query.Paging, c => c.CreatedAt, c => c.Id, cancellationToken);

        return page;
    }
}
```

O índice acompanha o filtro e a ordenação: `builder.HasIndex(c => new { c.TodoItemId, c.CreatedAt, c.Id }).IsDescending(false, true, true);`.

Quando a leitura depende de o usuário poder ver o item pai (ex.: comentários de um item que só os participantes ou quem gerencia podem ver), a checagem vem de um guard em `Application/Authorization` chamado no começo do handler, e não copiada em cada handler:

```csharp
Result access = await TodoAccessGuard.EnsureCanViewAsync(context, permissionProvider, userContext.UserId, query.TodoItemId, cancellationToken);

if (access.IsFailure)
{
    return Result.Failure<CursorPagedResponse<TodoCommentResponse>>(access.Error);
}
```


## Cache (opcional, só para leituras frequentes)

Envolva a consulta ao banco em `HybridCache.GetOrCreateAsync`, com uma chave vinda da classe de chaves de cache da feature (o `GetTodoByIdQueryHandler` é o exemplo real):

```csharp
namespace Application.Todos;

internal static class TodoCacheKeys
{
    internal static string ById(Guid userId, Guid todoItemId) => $"todos-{userId}-{todoItemId}";
}
```

```csharp
TodoResponse? todo = await cache.GetOrCreateAsync(
    TodoCacheKeys.ById(userId, query.TodoItemId),
    async cancellation => await context.TodoItems
        .Where(...)
        .Select(...)
        .SingleOrDefaultAsync(cancellation),
    cancellationToken: cancellationToken);
```

Todo command que altera o dado em cache precisa invalidar a mesma chave com `cache.RemoveAsync(...)`. Se não for possível listar as chaves afetadas, não use cache.
