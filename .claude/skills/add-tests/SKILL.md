---
name: add-tests
description: Completa os testes que faltam em casos de uso existentes do projeto — testes unitários de handler, testes de validator do FluentValidation e testes de integração por HTTP. Use quando o usuário pedir para adicionar, melhorar ou completar a cobertura de testes.
argument-hint: '<caso de uso ou feature a cobrir, ex.: "CopyTodoCommand" ou "a feature Users">'
---

# Adicionar testes a um caso de uso existente

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Completa os três tipos de teste que este projeto espera em todo slice. Leia primeiro o handler/validator/endpoint alvo e depois siga a estrutura da classe de teste existente mais parecida.

## Fluxo

1. **Localize o slice.** Encontre o command/query, o handler, o validator e o endpoint do caso de uso alvo. Liste cada resultado possível: cada cláusula de guarda (`return Result.Failure(...)`) e o caminho feliz.
2. **Confira o que já existe** em `tests/Application.UnitTests/{Feature}/` e `tests/IntegrationTests/{Feature}/` — amplie as classes existentes, não duplique.
3. **Escreva os testes unitários do handler** — um teste por caminho de falha, mais um caminho feliz que confere o estado salvo e os eventos de domínio disparados.
4. **Escreva os testes do validator** (só commands) — um teste com falha por regra, mais um command totalmente válido.
5. **Escreva os testes de integração** — caminho feliz por HTTP de verdade, com o estado conferido por um GET em seguida; conversão de erros (404/409/400) quando o handler tem caminhos de falha; 401 sem token se a família de rotas for nova.
6. **Rode** `dotnet test` (o Docker precisa estar rodando para os testes de integração) e corrija as falhas antes de terminar.

## Convenções

- **Frameworks:** xUnit + Shouldly + NSubstitute; `FluentValidation.TestHelper` para os validators. Os global usings já cobrem `Xunit`, `NSubstitute`, `Shouldly`, `SharedKernel`.
- **Base dos testes unitários:** herde `BaseHandlerTest`; use `CreateDbContext()` para um `TestDbContext` novo em memória e `CreateCache()` para um `HybridCache` de verdade. Substitua só interfaces (`IUserContext`, `IDateTimeProvider`, `IPasswordHasher`, `ITokenProvider`) — nunca faça mock do DbContext.
- **Base dos testes de integração:** herde `BaseIntegrationTest(factory)` com a collection fixture `IntegrationTestWebAppFactory`; use `RegisterAndLoginAsync()` + `Authenticate(token)` nas chamadas autenticadas. Defina records privados de DTO de resposta dentro da classe de teste (veja `TodosTests.TodoDto`).
- **Nomes:** classes `{Handler}Tests` / `{Feature}ValidatorsTests` / `{Feature}Tests`; métodos `Handle_Should_{Outcome}_When{Condition}` (unitários) ou `{Action}_Should_{Outcome}[_When{Condition}]` (integração).
- **Estrutura:** comentários `// Arrange` / `// Act` / `// Assert` em todo teste.
- **Asserções:** compare os erros de domínio exatos (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`); confira o estado salvo relendo do contexto (unitário) ou por uma requisição GET (integração); confira os eventos de domínio com `entity.DomainEvents.ShouldContain(e => e is XDomainEvent)`.

Modelos completos e comentados: [../add-feature/references/tests.md](../add-feature/references/tests.md) (se a skill `add-feature` estiver instalada) ou siga `CreateTodoCommandHandlerTests`, `TodoValidatorsTests` e `TodosTests` neste repositório.
