---
name: add-feature
description: Cria um slice vertical completo no TickestPristine — command ou query, handler próprio, validator do FluentValidation, endpoint de minimal API e testes (unitário, de validator e de integração). Use quando o usuário pedir para adicionar uma feature, caso de uso, command, query ou endpoint ao TickestPristine.
argument-hint: <descrição da feature, ex.: "archive a todo item" ou "get todos due this week">
---

# Adicionar uma feature (slice vertical)

Crie um caso de uso completo seguindo as convenções do TickestPristine: um caso de uso na camada Application com um handler próprio de command/query, um endpoint de minimal API na Web.Api e testes. Sem MediatR — este código usa as suas próprias abstrações `ICommand`/`IQuery`, com handlers e decorators registrados pelo Scrutor.

## Fluxo de trabalho

1. **Classifique o caso de uso.** Uma mudança de estado é um **command**; uma leitura é uma **query**. Derive os nomes do padrão existente: verbo do caso de uso + entidade, ex.: `ArchiveTodoCommand`, `GetOverdueTodosQuery`.
2. **Confira a camada Domain.** Se a entidade, a sua classe `{Entity}Errors` ou um evento de domínio necessário não existir, crie-o primeiro (veja a skill `add-entity`). Commands que mudam estado devem disparar um evento de domínio via `entity.Raise(...)`.
3. **Crie o slice da Application** em `src/Application/{Feature}/{UseCase}/` — command/query, handler, validator (só commands), DTO de resposta (só queries). Modelos: [references/command-slice.md](references/command-slice.md) e [references/query-slice.md](references/query-slice.md).
4. **Crie o endpoint** em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Modelo: [references/endpoint.md](references/endpoint.md).
5. **Escreva os testes** — testes unitários do handler, testes de validator e um teste de integração. Modelos: [references/tests.md](references/tests.md).
6. **Verifique:** `dotnet build` e depois `dotnet test`. Os três projetos de teste precisam passar, inclusive os `ArchitectureTests` (regras de dependência entre camadas).

## Convenções inegociáveis

- **Pasta = caso de uso.** Uma pasta por caso de uso em `src/Application/{Feature}/` (ex.: `Todos/Archive/`), com todos os arquivos daquele slice.
- **Handlers são `internal sealed`**, com construtor primário, implementando `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>` ou `IQueryHandler<TQuery, TResponse>`.
- **Nada de registro manual na DI.** Handlers, validators e endpoints são descobertos por varredura de assembly (`Scrutor`, `AddValidatorsFromAssembly`, `AddEndpoints`). Nunca mexa em `DependencyInjection.cs` por causa de um slice novo.
- **Retorne `Result` / `Result<T>`, nunca lance exceção** para falhas esperadas. Os erros vêm dos métodos de fábrica estáticos de `{Entity}Errors` na camada Domain, com códigos como `"Todos.NotFound"`.
- **A validação fica num `{Command}Validator`** (FluentValidation). Ela roda automaticamente pelo `ValidationDecorator` — o handler nunca valida o formato da entrada. Queries não têm validator (o decorator só envolve commands).
- **Acesso a dados via `IApplicationDbContext`** (de `Application.Abstractions.Data`) — nunca referencie a Infrastructure a partir da Application.
- **Conferência de autorização** nos handlers que agem sobre dados de um usuário: compare `IUserContext.UserId` e retorne `UserErrors.Unauthorized()` se não bater, ou filtre as queries por `userContext.UserId`.
- **Queries projetam direto num DTO `{X}Response`** com `.Select(...)` — nunca retorne entidades de domínio. Coloque em cache as leituras frequentes com `HybridCache`, usando uma classe estática `{Feature}CacheKeys`; invalide nos commands que alteram os dados em cache.
- **Pagine as coleções sem limite** com `PageRequest` (offset) ou `CursorPageRequest` (cursor) e os helpers de `PaginationExtensions`. Veja [references/pagination.md](references/pagination.md).
- **Endpoints** implementam `IEndpoint`, resolvem a interface do handler direto da DI, usam `result.Match(Results.Ok, CustomResults.Problem)` (ou `Results.NoContent` para commands sem retorno), recebem a tag da classe `Tags` e chamam `.RequireAuthorization()`.

## Referência de nomes

| Artefato | Padrão | Exemplo |
|---|---|---|
| Command | `{Verb}{Entity}Command` | `ArchiveTodoCommand` |
| Query | `Get{X}Query` | `GetOverdueTodosQuery` |
| Handler | `{Command/Query}Handler` | `ArchiveTodoCommandHandler` |
| Validator | `{Command}Validator` | `ArchiveTodoCommandValidator` |
| Response | `{X}Response` | `TodoResponse` |
| Endpoint | `{UseCase}.cs` em `Endpoints/{Feature}/` | `Endpoints/Todos/Archive.cs` |
| Teste unitário | `{Handler}Tests` | `ArchiveTodoCommandHandlerTests` |
| Método de teste | `Handle_Should_{Outcome}_When{Condition}` | `Handle_Should_ReturnNotFound_WhenTodoDoesNotExist` |
