# Modelos de teste

Pilha: xUnit + Shouldly + NSubstitute (unitários), FluentValidation.TestHelper (validators), WebApplicationFactory + Testcontainers (integração). Todo método de entidade e todo caso de uso novo ganham os seus testes.

## Testes da entidade

`tests/Application.UnitTests/{Feature}/{Entity}Tests.cs`. Sem banco e sem substitutos: a entidade é criada pelo próprio `Create` e exercitada diretamente.

Cubra: as invariantes conferidas na construção, cada transição válida (estado novo e evento) e cada transição inválida (o erro, com o estado e os eventos inalterados).

```csharp
using Domain.Todos;

namespace Application.UnitTests.Todos;

public sealed class TodoItemTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_ReturnFailure_WhenDueDateIsBeforeCreation()
    {
        // Arrange
        DateTime dueDate = CreatedAt.AddDays(-1);

        // Act
        Result<TodoItem> result = TodoItem.Create(Guid.NewGuid(), "Write tests", Priority.Low, dueDate, [], CreatedAt);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.DueDateBeforeCreation);
    }

    [Fact]
    public void Create_Should_Throw_WhenUserIdIsEmpty()
    {
        // Arrange
        Guid emptyUserId = Guid.Empty;

        // Act
        Action create = () => TodoItem.Create(emptyUserId, "Write tests", Priority.Low, null, [], CreatedAt);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Complete_Should_CompleteAndRaiseEvent_WhenNotCompleted()
    {
        // Arrange
        TodoItem todoItem = CreateTodoItem();
        DateTime completedAt = CreatedAt.AddHours(1);

        // Act
        Result result = todoItem.Complete(completedAt);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.CompletedAt.ShouldBe(completedAt);
        todoItem.DomainEvents.ShouldContain(e => e is TodoItemCompletedDomainEvent);
    }

    [Fact]
    public void Complete_Should_ReturnFailureAndKeepState_WhenAlreadyCompleted()
    {
        // Arrange
        TodoItem todoItem = CreateTodoItem();
        DateTime firstCompletion = CreatedAt.AddHours(1);
        todoItem.Complete(firstCompletion);
        int eventsBefore = todoItem.DomainEvents.Count;

        // Act
        Result result = todoItem.Complete(CreatedAt.AddHours(2));

        // Assert
        result.Error.ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
        todoItem.CompletedAt.ShouldBe(firstCompletion);
        todoItem.DomainEvents.Count.ShouldBe(eventsBefore);
    }

    private static TodoItem CreateTodoItem() =>
        TodoItem.Create(Guid.NewGuid(), "Write tests", Priority.Low, null, [], CreatedAt).Value;
}
```

- Nomeie os métodos `{Method}_Should_{Outcome}_When{Condition}`.
- Uma transição inválida confere três coisas: o erro exato, o estado como antes e nenhum evento novo.

## Testes unitários de handler

`tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Herde `BaseHandlerTest` — ele fornece `CreateDbContext()` (um `TestDbContext` em memória que implementa `IApplicationDbContext`) e `CreateCache()` (um `HybridCache` real). Substitua só interfaces (`IUserContext`, `IDateTimeProvider`) com NSubstitute.

Cubra: cada caminho de falha do handler (não encontrado, permissão, a falha devolvida pelo método da entidade) e o caminho feliz, com o estado gravado e os eventos de domínio disparados. As regras da própria entidade já estão cobertas pelos testes dela.

```csharp
using Application.Abstractions.Authentication;
using Application.Todos.Archive;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class ArchiveTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime UtcNow = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var command = new ArchiveTodoCommand(Guid.NewGuid());
        var handler = new ArchiveTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(command.TodoItemId));
    }

    [Fact]
    public async Task Handle_Should_ArchiveTodoAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        TodoItem todoItem = TodoItem.Create(UserId, "Archive me", Priority.Low, null, [], UtcNow).Value;
        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(UtcNow.AddHours(1));

        var command = new ArchiveTodoCommand(todoItem.Id);
        var handler = new ArchiveTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem archived = await context.TodoItems.SingleAsync(t => t.Id == todoItem.Id);
        archived.IsArchived.ShouldBeTrue();
        archived.DomainEvents.ShouldContain(e => e is TodoItemArchivedDomainEvent);
    }
}
```

Convenções:
- Nomes dos testes: `Handle_Should_{Outcome}_When{Condition}`.
- Comentários `// Arrange` / `// Act` / `// Assert` em todo teste.
- Monte as entidades pelo `Create` e pelos métodos delas, nunca atribuindo propriedades.
- Confira as falhas comparando o erro exato: `result.Error.ShouldBe(TodoItemErrors.NotFound(id))`.
- O `GlobalUsings.cs` já importa `Xunit`, `NSubstitute`, `Shouldly` e `SharedKernel` — não repita esses `using`.
- Se a entidade ganhar propriedades novas, o `TestDbContext` as reconhece sozinho; só mexa nele ao acrescentar um `DbSet` inteiro.

## Testes de validator

`tests/Application.UnitTests/{Feature}/{Feature}ValidatorsTests.cs` (estenda o arquivo existente, se houver). Uma classe cobre todos os validators de uma feature.

```csharp
[Fact]
public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty()
{
    var command = new CreateTodoCommand
    {
        UserId = Guid.NewGuid(),
        Description = string.Empty,
        Priority = Priority.Low
    };

    TestValidationResult<CreateTodoCommand> result = _createValidator.TestValidate(command);

    result.ShouldHaveValidationErrorFor(c => c.Description);
}
```

Cubra a falha de cada regra mais um command totalmente válido (`ShouldNotHaveAnyValidationErrors`).

## Testes de integração

`tests/IntegrationTests/{Feature}/{Feature}Tests.cs` (estenda o arquivo existente, se houver). Herde `BaseIntegrationTest(factory)` — ele roda a API real contra um Postgres do Testcontainers e fornece `HttpClient`, `RegisterAndLoginAsync()` e `Authenticate(token)`. Os testes passam pelo HTTP real e nunca chamam handlers direto.

**Adaptação do Tickest:** o cadastro de usuários exige `users:create`, então os testes cadastram pelo administrador (`BaseIntegrationTest.RegisterUserAsync` / `PostRegisterAsAdminAsync`) no lugar de `RegisterAndLoginAsync()`.

Persistência — a mudança sobrevive à ida e volta ao banco:

```csharp
[Fact]
public async Task ArchiveTodo_Should_MarkTodoAsArchived()
{
    // Arrange
    (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
    Authenticate(tokens.AccessToken);

    var createRequest = new { userId, description = "Todo to archive", labels = Array.Empty<string>(), priority = 1 };
    HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);
    Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    // Act
    HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    TodoDto? todo = await HttpClient.GetFromJsonAsync<TodoDto>($"todos/{todoId}");
    todo!.IsArchived.ShouldBeTrue();
}
```

Tradução da consulta — o predicado roda no banco e filtra o que deve:

```csharp
[Fact]
public async Task GetOverdue_Should_ReturnOnlyOverdueTodos()
{
    // Arrange
    (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
    Authenticate(tokens.AccessToken);
    DateTime createdAt = DateTime.UtcNow.AddDays(-2);
    await InsertAsync(
        TodoItem.Create(userId, "Late", Priority.Low, createdAt.AddDays(1), [], createdAt).Value,
        TodoItem.Create(userId, "On time", Priority.Low, createdAt.AddDays(9), [], createdAt).Value);

    // Act
    List<TodoDto>? todos = await HttpClient.GetFromJsonAsync<List<TodoDto>>("todos/overdue");

    // Assert
    todos!.Select(t => t.Description).ShouldBe(["Late"]);
}
```

- `InsertAsync` adiciona entidades por um `ApplicationDbContext` com escopo da factory, para estados que a API não cria na hora (um prazo já no passado). Acrescente-o ao `BaseIntegrationTest` uma vez, se não existir.
- Este é o teste que prova que o `Expression<Func<T, bool>>` é traduzido para SQL; um teste unitário em memória não prova isso.

Cobertura mínima por endpoint: um teste sem autorização (sem token → 401) se for uma família de rotas nova, um teste do caminho feliz conferindo o estado observável por um GET seguinte e um teste de tradução de falha (ex.: id desconhecido → 404) quando o handler tiver caminhos de falha. Toda query com predicado tem pelo menos um teste de tradução.

## Rodar

```
dotnet test
```

Os testes de integração precisam do Docker rodando (Testcontainers). Os testes de arquitetura reprovam o build se uma regra de dependência entre camadas for violada — corrija a dependência, nunca o teste.
