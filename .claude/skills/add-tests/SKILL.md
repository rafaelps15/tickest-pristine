---
name: add-tests
description: Completa os testes que faltam em entidades e casos de uso existentes — testes das regras da entidade, testes unitários de handler, testes de validator do FluentValidation e testes de integração por HTTP que cobrem a persistência e a tradução das consultas. Use quando o usuário pedir para adicionar, melhorar ou completar a cobertura de testes.
argument-hint: '<caso de uso ou feature a cobrir, ex.: "CopyTodoCommand" ou "a feature Users">'
---

# Adicionar testes a um caso de uso existente

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

> **Portão de especificação.** Se o `CLAUDE.md` do repositório adotar um fluxo de especificação antes do
> código (por exemplo, Spec-Driven Development), esta skill só escreve código de produção ou testes dentro
> desse fluxo: chamada pela skill de implementação para uma tarefa de spec aprovada, ou numa mudança
> trivial autorizada explicitamente pelo responsável no chat (quando o `CLAUDE.md` declarar um marcador de
> autorização, como `[sdd:autorizo]`, ele precisa estar na mensagem do responsável). Num pedido direto fora desse fluxo, não
> escreva código: procure em `specs/` uma spec que cubra o pedido e indique o comando de implementação
> dela; se não houver, use a skill de especificação.

Complete os tipos de teste de que toda entidade e todo slice precisam. Leia primeiro a entidade/handler/validator/endpoint alvo e depois espelhe a estrutura da classe de teste existente mais parecida.

## Workflow

1. **Ache o slice.** Encontre os métodos da entidade que ele chama, e o command/query, o handler, o validator e o endpoint do caso de uso. Liste cada resultado distinto: cada `return Result.Failure(...)` na entidade e no handler, e cada caminho feliz.
2. **Confira o que já existe** em `tests/Application.UnitTests/{Feature}/` e `tests/IntegrationTests/{Feature}/` — estenda as classes existentes, não duplique.
3. **Escreva os testes da entidade** — as invariantes conferidas na construção (`Create` devolvendo o erro); cada transição válida (estado novo e evento disparado); cada transição inválida (o erro exato, com o estado e os eventos inalterados).
4. **Escreva os testes unitários do handler** — um teste por caminho de falha do handler (não encontrado, permissão, a falha devolvida pela entidade) mais um caminho feliz conferindo o estado gravado e os eventos de domínio disparados.
5. **Escreva os testes de validator** (só commands) — um teste de falha por regra mais um command totalmente válido.
6. **Escreva os testes de integração** — caminho feliz por HTTP real, com o estado conferido por um GET seguinte (persistência); para toda query com predicado, um teste que prove que ela filtra no banco (tradução); conversão de erro (404/409/400) onde o handler tiver caminhos de falha; 401 sem token se a família de rotas for nova.
7. **Rode** `dotnet test` (o Docker precisa estar rodando para os testes de integração) e corrija as falhas antes de terminar.

## Convenções

- **Frameworks:** xUnit + Shouldly + NSubstitute; `FluentValidation.TestHelper` para validators. Os global usings já cobrem `Xunit`, `NSubstitute`, `Shouldly`, `SharedKernel`.
- **Testes da entidade:** `{Entity}Tests`, sem banco e sem substitutos; a entidade é criada pelo próprio `Create`.
- **Base dos testes unitários:** herde `BaseHandlerTest`; use `CreateDbContext()` para um `TestDbContext` novo em memória e `CreateCache()` para um `HybridCache` real. Substitua só interfaces (`IUserContext`, `IDateTimeProvider`, `IPasswordHasher`, `ITokenProvider`) — nunca faça mock do DbContext.
- **Monte pelo domínio:** crie as entidades com `Create` e os métodos delas, nunca atribuindo propriedades.
- **Base dos testes de integração:** herde `BaseIntegrationTest(factory)` com a collection fixture `IntegrationTestWebAppFactory`; use `RegisterAndLoginAsync()` + `Authenticate(token)` nas chamadas autenticadas. Defina records privados de resposta dentro da classe de teste (veja `TodosTests.TodoDto`).
- **Nomes:** classes `{Entity}Tests` / `{Handler}Tests` / `{Feature}ValidatorsTests` / `{Feature}Tests`; métodos `{Method}_Should_{Outcome}_When{Condition}` (entidade), `Handle_Should_{Outcome}_When{Condition}` (handler) ou `{Action}_Should_{Outcome}[_When{Condition}]` (integração).
- **Estrutura:** comentários `// Arrange` / `// Act` / `// Assert` em todo teste; sem `<summary>` nos testes.
- **Conferências:** compare os erros de domínio exatos (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`); confira o estado gravado relendo do contexto (unitário) ou por uma requisição GET (integração); confira os eventos com `entity.DomainEvents.ShouldContain(e => e is XDomainEvent)`, e a ausência deles depois de uma falha.
- **Handlers de evento de domínio:** uma classe de teste unitário por handler ([../add-feature/references/domain-event-handler.md](../add-feature/references/domain-event-handler.md)), com as mesmas bases e nomes.

Modelos completos e comentados: [../add-feature/references/tests.md](../add-feature/references/tests.md) (se a skill `add-feature` estiver instalada), ou espelhe `CreateTodoCommandHandlerTests`, `TodoValidatorsTests` e `TodosTests` do repositório.
