---
name: add-tests
description: Completa os testes que faltam em casos de uso existentes do template de Clean Architecture — testes unitários de handler, testes de validator do FluentValidation e testes de integração por HTTP. Use quando o usuário pedir para adicionar, melhorar ou completar a cobertura de testes.
argument-hint: <caso de uso ou feature a cobrir, por exemplo "CopyTodoCommand" ou "a feature de Users">
---

# Adicionar testes a um caso de uso existente

Complete os três tipos de teste que este template espera para cada slice. Leia primeiro o handler, o validator e o endpoint alvo, e depois siga a estrutura da classe de teste existente mais parecida.

## Fluxo de trabalho

1. **Localize o slice.** Encontre o command/query, o handler, o validator e o endpoint do caso de uso. Liste cada resultado possível: cada cláusula de guarda (`return Result.Failure(...)`) e o caminho feliz.
2. **Veja o que já existe** em `tests/Application.UnitTests/{Feature}/` e `tests/IntegrationTests/{Feature}/` — amplie as classes existentes, não duplique.
3. **Escreva os testes unitários do handler** — um teste por caminho de falha e um do caminho feliz conferindo o estado gravado e os eventos de domínio disparados.
4. **Escreva os testes do validator** (só commands) — um teste de falha por regra e um command totalmente válido.
5. **Escreva os testes de integração** — caminho feliz por HTTP real, com o estado conferido por um GET em seguida; tradução de erro (404/409/400) quando o handler tem caminhos de falha; 401 sem token se a família de rotas for nova.
6. **Rode** `dotnet test` (o Docker precisa estar rodando para os testes de integração) e corrija as falhas antes de terminar.

## Convenções

- **Ferramentas:** xUnit + Shouldly + NSubstitute; `FluentValidation.TestHelper` para validators. Os global usings já cobrem `Xunit`, `NSubstitute`, `Shouldly` e `SharedKernel`.
- **Base dos testes unitários:** herde de `BaseHandlerTest`; use `CreateDbContext()` para um `TestDbContext` novo em memória e `CreateCache()` para um `HybridCache` real. Substitua só interfaces (`IUserContext`, `IDateTimeProvider`, `IPasswordHasher`, `ITokenProvider`, `IPermissionProvider`) — nunca faça mock do DbContext.
- **Base dos testes de integração:** herde de `BaseIntegrationTest(factory)` com a collection fixture `IntegrationTestWebAppFactory`; use `RegisterAndLoginAsync()` + `Authenticate(token)` para chamadas autenticadas. Declare os records de DTO de resposta como privados dentro da classe de teste (ex.: `TodosTests.TodoDto`).
- **Nomes:** classes `{Handler}Tests` / `{Feature}ValidatorsTests` / `{Feature}Tests`; métodos `Handle_Should_{Outcome}_When{Condition}` (unitários) ou `{Action}_Should_{Outcome}[_When{Condition}]` (integração).
- **Estrutura:** comentários `// Arrange` / `// Act` / `// Assert` em todos os testes.
- **Verificações:** compare o erro de domínio exato (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`); confira o estado gravado relendo do contexto (unitários) ou por uma requisição GET (integração); confira eventos de domínio com `entity.DomainEvents.ShouldContain(e => e is XDomainEvent)`.

Templates completos e comentados: [../add-feature/references/tests.md](../add-feature/references/tests.md) (se a skill `add-feature` estiver instalada), ou siga `CreateTodoCommandHandlerTests`, `TodoValidatorsTests` e `TodosTests` do repositório.
