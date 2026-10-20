# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar com o código deste repositório.

## O que é

O TickestPristine é uma base pragmática de Clean Architecture para .NET 10 (domínio de exemplo com Todos + Users), usando PostgreSQL, com autenticação JWT, autorização por permissões, cache, logs estruturados e OpenTelemetry já configurados. É um *template* — o código de Todos/Users é conteúdo de exemplo, feito para ser ampliado ou substituído.

## Comandos

```bash
# Sobe a infraestrutura (PostgreSQL + Seq) antes de rodar a API ou os testes de integração
docker compose up -d

# Roda a API
dotnet run --project src/Web.Api

# Build / restore
dotnet restore TickestPristine.slnx
dotnet build TickestPristine.slnx

# Roda todos os testes (os de integração sobem um container descartável do PostgreSQL via
# Testcontainers, então o Docker precisa estar rodando)
dotnet test TickestPristine.slnx

# Roda um projeto de teste
dotnet test tests/Application.UnitTests
dotnet test tests/IntegrationTests
dotnet test tests/ArchitectureTests

# Roda um teste pelo nome (qualquer projeto)
dotnet test --filter "FullyQualifiedName~CreateTodoCommandHandlerTests"
```

O Seq (visualizador de logs estruturados) fica disponível em http://localhost:8081 depois do `docker compose up -d`.

Para usar .NET 8 ou .NET 9 em vez de .NET 10, veja as notas no `Directory.Build.props` (também é preciso atualizar o Dockerfile em `src/Web.Api`).

Avisos são tratados como erros (`TreatWarningsAsErrors`, `AnalysisMode=All`), então o `dotnet build` é uma verificação de qualidade de verdade, e não só uma compilação.

## Arquitetura

Cinco projetos, com dependências sempre apontando para dentro. Isso é garantido por `tests/ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain e Application nunca podem referenciar Infrastructure ou Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

- **SharedKernel** — peças básicas de DDD, sem dependência de nenhum outro projeto da solução: `Entity` (classe base que guarda os eventos de domínio disparados), `Result`/`Result<T>` (a convenção de tratamento de erro — veja abaixo), `Error`/`ErrorType`, `IDomainEvent`, `IDateTimeProvider`.
- **Domain** — entidades, eventos de domínio e uma classe estática `*Errors` por agregado (ex.: `TodoItemErrors`, `UserErrors`). Sem dependência de frameworks. Organizado por pasta de agregado (`Todos/`, `Users/`), e não por tipo técnico.
- **Application** — casos de uso, um por pasta de slice vertical (ex.: `Todos/Create/`, `Todos/Complete/`), mais `Abstractions/` com as interfaces transversais (`Messaging`, `Behaviors`, `Data`, `Authentication`). Sem MediatR — commands e queries são tratados por interfaces próprias `ICommandHandler`/`IQueryHandler`, resolvidas pela DI com varredura de assembly do `Scrutor`.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, nomes em snake_case, migrations), autenticação JWT com rotação de refresh tokens, autorização por permissões, `HybridCache`, Serilog e despacho dos eventos de domínio.
- **Web.Api** — endpoints de minimal API (uma classe por endpoint implementando `IEndpoint`, registrada automaticamente pela varredura de assembly em `EndpointExtensions`), rate limiting, OpenTelemetry, tratamento global de exceções → `ProblemDetails` e Swagger/OpenAPI com JWT.

### Organização dos slices verticais

Cada caso de uso fica em `Application/{Aggregate}/{UseCase}/` como um slice independente, ex.: `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — o DTO `ICommand<TResponse>` (ou `IQuery<TResponse>`).
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependências injetadas pelo construtor primário (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider` etc.). Retorna `Result<T>` e nunca lança exceção para falhas esperadas.
- `CreateTodoCommandValidator.cs` — `AbstractValidator<TCommand>` do FluentValidation, carregado automaticamente pelo `AddValidatorsFromAssembly`.

O endpoint correspondente fica em `Web.Api/Endpoints/{Aggregate}/{Verb}.cs`, como uma `internal sealed class : IEndpoint` com um DTO `Request` aninhado. Ele mapeia a requisição para o command/query, chama o handler e converte o `Result` em HTTP com `result.Match(Results.Ok, CustomResults.Problem)`.

Os testes seguem a mesma estrutura: `tests/Application.UnitTests/{Aggregate}/{UseCase}HandlerTests.cs` para os testes unitários dos handlers, mais os testes de validator, e `tests/IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` para os testes de ponta a ponta por HTTP contra uma instância de PostgreSQL do Testcontainers.

### Comportamentos transversais com decorators

O `Application/DependencyInjection.cs` envolve todos os handlers de command/query com decorators (pelo `.Decorate` do `Scrutor`), aplicados nesta ordem: **Validation → Logging → handler**. A validação interrompe em caso de falha e retorna `Result.Failure` com um `ValidationError` antes de o handler rodar — os próprios handlers não precisam validar a entrada.

### Padrão Result (sem exceções para falhas esperadas)

`Result` / `Result<T>` do `SharedKernel` é a convenção de tratamento de erro em Domain, Application e Web.Api. Os handlers retornam `Result.Failure<T>(SomeErrors.Reason(...))` em vez de lançar exceção. Cada agregado define a sua própria classe estática de erros (`TodoItemErrors`, `UserErrors`) com métodos de fábrica nomeados. Os endpoints convertem o `Result` em resposta HTTP pelo `CustomResults.Problem` (que converte o `ErrorType` no status adequado do `ProblemDetails`). Exceções ficam reservadas para falhas realmente inesperadas e são capturadas pelo `GlobalExceptionHandler`.

### Eventos de domínio

As entidades herdam de `Entity` e chamam `Raise(new SomeDomainEvent(...))` nos seus próprios métodos que mudam estado, ou nos handlers no momento da criação. O `DomainEventsDispatcher` (Infrastructure) despacha os eventos disparados depois do `SaveChangesAsync`; os handlers de evento implementam `IDomainEventHandler<T>` e são registrados automaticamente pela varredura de assembly.

## Convenções a manter ao ampliar

- Casos de uso novos vão em `Application/{Aggregate}/{UseCase}/`, seguindo o trio Command/Handler/Validator acima — não introduza MediatR.
- Entidades novas ganham a sua própria classe estática `{Entity}Errors` no Domain, em vez de lançar exceções cruas ou reaproveitar os erros de outro agregado.
- As configurações do EF Core (`IEntityTypeConfiguration<T>`) ficam em `Infrastructure/{Aggregate}/{Entity}Configuration.cs` e precisam ser adicionadas ao `ApplicationDbContext`.
- Endpoints são uma classe por rota em `Web.Api/Endpoints/{Aggregate}/`, com tag via `Web.Api/Endpoints/Tags.cs`, e registrados automaticamente — não há tabela de rotas para atualizar.
- Este repositório tem `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) com essas convenções em forma de roteiro executável — prefira usá-las a escrever à mão, para que o código novo fique exatamente igual aos slices existentes.
