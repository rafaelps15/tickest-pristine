---
name: add-feature
description: Cria um slice vertical completo num projeto .NET de Clean Architecture — command ou query, handler próprio que delega as regras à entidade, validator do FluentValidation, endpoint de minimal API e testes (unitário, de validator e de integração). Use quando o usuário pedir para adicionar uma feature, caso de uso, command, query ou endpoint ao projeto.
argument-hint: '<descrição da feature, ex.: "archive a todo item" ou "get todos due this week">'
---

# Adicionar uma feature (slice vertical)

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

Crie um caso de uso completo: um caso de uso na Application com handler próprio de command/query, um endpoint de minimal API na Web.Api e os testes. Sem MediatR — o código usa as próprias abstrações `ICommand`/`IQuery`, com handlers e decorators registrados pelo Scrutor. A entidade guarda as próprias regras (veja a `domain-modeling`); o handler carrega, autoriza, chama a entidade e salva.

## Workflow

1. **Classifique o caso de uso.** Mudança de estado é **command**; leitura é **query**. Derive os nomes do padrão existente: verbo do caso de uso + entidade, ex.: `ArchiveTodoCommand`, `GetOverdueTodosQuery`.
2. **Confira o Domain.** Se a entidade, a classe `{Entity}Errors`, o método da entidade para a mudança ou o evento de domínio não existirem, crie-os antes (`add-entity`, `domain-modeling`). O método da entidade dispara o evento; o handler nunca chama `Raise`.
3. **Crie o slice na Application** em `src/Application/{Feature}/{UseCase}/` — command/query, handler, validator (só commands), DTO de resposta (só queries). Modelos: [references/command-slice.md](references/command-slice.md), [references/query-slice.md](references/query-slice.md), [references/rich-domain-handler.md](references/rich-domain-handler.md).
4. **Crie o endpoint** em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`. Modelo: [references/endpoint.md](references/endpoint.md).
5. **Escreva os testes** — da entidade, unitários do handler, do validator e de integração. Modelos: [references/tests.md](references/tests.md).
6. **Verifique:** `dotnet build` e depois `dotnet test`. Todos os projetos de teste precisam passar, inclusive os `ArchitectureTests` (regras de dependência entre camadas).

## Convenções inegociáveis

- **Pasta = caso de uso.** Uma pasta por caso de uso em `src/Application/{Feature}/` (ex.: `Todos/Archive/`), com todos os arquivos do slice.
- **Handlers são `internal sealed`** com construtor primário, implementando `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>` ou `IQueryHandler<TQuery, TResponse>`.
- **Fluxo do handler:** carrega → não encontrado → conferência de permissão → chama o método da entidade → devolve a falha dele → `SaveChangesAsync`. Uma entidade que o `Create` referencia é carregada e passada a ele (`Order.Create(customer)`); uma pergunta sim ou não é um `AnyAsync` direto no `if`. O handler nunca atribui as propriedades da entidade e nunca chama `Raise`.
- **Objetos de valor são criados no handler**, e a primeira falha é devolvida antes de montar a entidade (`Result.FirstFailureOrSuccess(...)`).
- **Um registro que precisa existir junto com a mudança** (filho, histórico, auditoria) é criado pelo método da entidade e adicionado pelo handler antes do único `SaveChangesAsync` (padrão 7 da `domain-modeling`).
- **Outras reações a uma mudança de estado são handlers de evento de domínio**: um por reação, `{Action}On{Event}DomainEventHandler`, em `src/Application/{Feature}/{Event}/` ([references/domain-event-handler.md](references/domain-event-handler.md)).
- **Sem registro manual na DI.** Handlers, validators e endpoints são descobertos por varredura de assembly (`Scrutor`, `AddValidatorsFromAssembly`, `AddEndpoints`). Nunca mexa no `DependencyInjection.cs` para um slice novo.
- **Devolva `Result` / `Result<T>`, nunca lance exceção** para falhas esperadas. Os erros vêm de métodos de fábrica estáticos em `{Entity}Errors` no Domain, com códigos como `"Todos.NotFound"`.
- **A validação fica num `{Command}Validator`** (FluentValidation): o formato da entrada, nunca o tamanho da coluna, que fica só na configuração do EF (`HasMaxLength`). Ela roda sozinha pelo `ValidationDecorator`. Regras sobre o estado da entidade são da entidade. Queries não têm validator.
- **Acesso a dados pelo `IApplicationDbContext`** (de `Application.Abstractions.Data`) — nunca referencie a Infrastructure a partir da Application.
- **Autorização** ([references/authorization.md](references/authorization.md)): permissão fixa no endpoint (`.HasPermission(...)`); regra sobre o próprio estado da entidade no método dela; permissões efetivas pelo `IPermissionProvider`; conferência que precisa de consulta num método privado `HasPermissionTo{Action}` do handler. Peça compartilhada só depois de a repetição aparecer, nunca classe estática.
- **Queries projetam direto num DTO `{X}Response`** com `.Select(...)` — nunca devolva entidades de domínio, e nunca chame um método da entidade dentro de `Where` ou `Select`: use um predicado traduzível. Coloque em cache as leituras frequentes com `HybridCache`, usando uma classe estática `{Feature}CacheKeys`; invalide nos commands que alteram os dados em cache.
- **Pagine as coleções sem limite** com `PageRequest` (offset) ou `CursorPageRequest` (cursor) e os helpers de `PaginationExtensions`. Veja [references/pagination.md](references/pagination.md).
- **Trabalho técnico fica atrás de uma interface.** Hash, tokens, arquivos, bytes ou ZIP: uma interface em `Application/Abstractions/{Area}/`, implementada `internal sealed` em `Infrastructure/{Area}/` ([references/abstraction.md](references/abstraction.md)).
- **Endpoints** implementam `IEndpoint`, resolvem a interface do handler direto da DI, usam `result.Match(Results.Ok, CustomResults.Problem)` (ou `Results.NoContent` para commands sem retorno), recebem a tag da classe `Tags` e chamam `.RequireAuthorization()`.
- **Comentários:** um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio.
- **Não usado:** repositórios, `IUnitOfWork`, MediatR, controllers, `Maybe<T>`, uma classe central `DomainErrors`.

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
