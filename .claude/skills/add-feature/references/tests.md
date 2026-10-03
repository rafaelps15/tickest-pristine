# Templates de teste

Ferramentas: xUnit + Shouldly + NSubstitute (unitários), FluentValidation.TestHelper (validators), WebApplicationFactory + Testcontainers (integração). Todo caso de uso novo ganha os três tipos.

## Testes unitários de handler

`tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Herde de `BaseHandlerTest` — ele fornece `CreateDbContext()` (um `TestDbContext` em memória que implementa `IApplicationDbContext`) e `CreateCache()` (um `HybridCache` real). Use NSubstitute só para interfaces (`IUserContext`, `IDateTimeProvider`).

Cubra: cada caminho de falha (um teste por cláusula de guarda) e o caminho feliz, incluindo o estado gravado e os eventos de domínio disparados.

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
        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Archive me",
            CreatedAt = DateTime.UtcNow
        };
        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

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
- Nome dos testes: `Handle_Should_{Outcome}_When{Condition}`.
- Comentários `// Arrange` / `// Act` / `// Assert` em todos os testes.
- Confira falhas comparando o erro exato: `result.Error.ShouldBe(TodoItemErrors.NotFound(id))`.
- O `GlobalUsings.cs` já importa `Xunit`, `NSubstitute`, `Shouldly` e `SharedKernel` — não repita esses usings.
- Se a entidade ganhar propriedades novas, o `TestDbContext` as reconhece sozinho; só mexa nele ao adicionar um `DbSet` novo.

## Testes de validator

`tests/Application.UnitTests/{Feature}/{Feature}ValidatorsTests.cs` (se o arquivo já existir, amplie-o). Uma classe cobre todos os validators de uma feature.

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

Cubra a falha de cada regra e um command totalmente válido (`ShouldNotHaveAnyValidationErrors`).

## Testes de integração

`tests/IntegrationTests/{Feature}/{Feature}Tests.cs` (se o arquivo já existir, amplie-o). Herde de `BaseIntegrationTest(factory)` — ele sobe a API real contra um Postgres do Testcontainers e fornece `HttpClient`, `RegisterAndLoginAsync()` e `Authenticate(token)`. Os testes passam por HTTP de verdade e nunca chamam handlers direto.

```csharp
[Fact]
public async Task ArchiveTodo_Should_MarkTodoAsArchived()
{
    // Arrange
    (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
    Authenticate(tokens.AccessToken);

    var createRequest = new
    {
        userId,
        description = "Todo to archive",
        labels = Array.Empty<string>(),
        priority = 1
    };
    HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest);
    createResponse.EnsureSuccessStatusCode();
    Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    // Act
    HttpResponseMessage response = await HttpClient.PutAsync($"todos/{todoId}/archive", null);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
}
```

Cobertura mínima por endpoint: um teste sem autenticação (sem token → 401) se for uma família de rotas nova, um teste do caminho feliz conferindo o estado por um GET em seguida, e um teste de tradução de falha (ex.: id inexistente → 404) quando o handler tem caminhos de falha.

## Rodar

```
dotnet test
```

Os testes de integração precisam do Docker rodando (Testcontainers). Os testes de arquitetura falham se alguma regra de dependência entre camadas for violada — corrija a dependência, nunca o teste.
