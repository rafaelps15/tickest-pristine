# Modelos de teste

> Exemplos ilustrativos do modelo de referência (Todos). Use os caminhos, namespaces e referências vivas do `CLAUDE.md` do projeto.

Stack: xUnit + Shouldly + NSubstitute (unitários), FluentValidation.TestHelper (validators), WebApplicationFactory + Testcontainers (integração). Todo caso de uso novo ganha os três.

## Testes unitários de handler

`tests/Application.UnitTests/{Feature}/{UseCase}{Command|Query}HandlerTests.cs`. Herde `BaseHandlerTest` — ele fornece `CreateDbContext()` (um `TestDbContext` em memória que implementa `IApplicationDbContext`) e `CreateCache()` (um `HybridCache` de verdade). Use mocks do NSubstitute só para interfaces (`IUserContext`, `IDateTimeProvider`).

Cubra: cada caminho de falha (um teste por guard clause) e o caminho feliz, incluindo o estado salvo e os eventos de domínio disparados.

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
- Nomes dos testes: `Handle_Should_{Outcome}_When{Condition}`.
- Comentários `// Arrange` / `// Act` / `// Assert` em todo teste.
- Confira as falhas comparando o erro exato: `result.Error.ShouldBe(TodoItemErrors.NotFound(id))`.
- O `GlobalUsings.cs` já importa `Xunit`, `NSubstitute`, `Shouldly` e `SharedKernel` — não repita esses usings.
- Se a entidade ganhar propriedades novas, o `TestDbContext` as reconhece sozinho; só mexa nele ao acrescentar um `DbSet` inteiro novo.

## Testes de validator

`tests/Application.UnitTests/{Feature}/{Feature}ValidatorsTests.cs` (amplie o arquivo existente, se houver). Uma classe cobre todos os validators de uma feature.

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

Cubra a falha de cada regra, mais um command totalmente válido (`ShouldNotHaveAnyValidationErrors`).

## Testes de integração

`tests/IntegrationTests/{Feature}/{Feature}Tests.cs` (amplie o arquivo existente, se houver). Herde `BaseIntegrationTest(factory)` — ele roda a API de verdade contra um Postgres do Testcontainers e fornece `HttpClient`, `RegisterAndLoginAsync()` e `Authenticate(token)`. Os testes passam por HTTP de verdade e nunca chamam os handlers diretamente.

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

Cobertura mínima por endpoint: um teste sem autorização (sem token → 401) se for uma família de rotas nova, um teste do caminho feliz conferindo o estado observável por um GET em seguida, e um teste da conversão de falha (ex.: id desconhecido → 404) quando o handler tiver caminhos de falha.

## Execução

```
dotnet test
```

Os testes de integração precisam do Docker rodando (Testcontainers). Os testes de arquitetura quebram o build se uma regra de dependência entre camadas for violada — corrija a dependência, nunca o teste.
