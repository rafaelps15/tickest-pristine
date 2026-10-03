# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar com o código deste repositório.

## O que é

Um template pragmático de Clean Architecture para .NET 10 (domínio de exemplo com Todos + Users), usando PostgreSQL, com autenticação JWT + refresh tokens, permissões por função, cache, logs estruturados e OpenTelemetry já configurados. É um *template* — o código de Todos/Users é conteúdo de exemplo, feito para ser ampliado ou substituído.

Os nomes usados neste arquivo e em `.claude/skills/` são os nomes fictícios do template (`CleanArchitecture.slnx`, `src/Application`, `TodoItem`). Uma solução real costuma ter prefixo nos projetos (ex.: `src/MyApp.Application`, `tests/MyApp.IntegrationTests`); troque os nomes fictícios pelos nomes reais do repositório.

## Comandos

```bash
# Sobe só a infraestrutura (PostgreSQL + Seq; portas no docker-compose.yml).
# Um `docker compose up -d` simples também sobe o container da API.
docker compose up -d postgres seq

# Roda a API (em Development, aplica as migrations e roda os seeders ao iniciar)
dotnet run --project src/Web.Api

# Build / restore
dotnet restore CleanArchitecture.slnx
dotnet build CleanArchitecture.slnx

# Roda todos os testes (os de integração sobem um PostgreSQL descartável via
# Testcontainers, então o Docker precisa estar rodando)
dotnet test CleanArchitecture.slnx

# Roda um projeto de teste
dotnet test tests/Application.UnitTests
dotnet test tests/IntegrationTests
dotnet test tests/ArchitectureTests

# Roda um teste pelo nome (qualquer projeto)
dotnet test --filter "FullyQualifiedName~CreateTodoCommandHandlerTests"

# Cria uma migration (nomes no formato PascalCase_With_Underscores)
dotnet ef migrations add Add_Todos --project src/Infrastructure --startup-project src/Web.Api --output-dir Database/Migrations
```

O Seq (visualizador de logs estruturados) sobe junto com a infraestrutura; a porta está no `docker-compose.yml`.

Para usar .NET 8 ou .NET 9 em vez de .NET 10, veja as notas no `Directory.Build.props` (também é preciso atualizar o Dockerfile em `src/Web.Api`).

Avisos são tratados como erros (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer), então o `dotnet build` é uma verificação de qualidade de verdade, e não só uma compilação.

## Arquitetura

Cinco projetos, com dependências sempre apontando para dentro. Isso é garantido por `tests/ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain e Application nunca podem referenciar Infrastructure ou Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

- **SharedKernel** — peças básicas de DDD, sem dependência de nenhum outro projeto da solução: `Entity` (classe base que guarda os eventos disparados), `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider`.
- **Domain** — entidades, eventos de domínio e uma classe estática `*Errors` por agregado (ex.: `TodoItemErrors`, `UserErrors`), organizados por pasta de agregado (`Todos/`, `Users/`), e não por tipo técnico. As entidades são classes simples, com propriedades com setter e ids das chaves estrangeiras (sem propriedades de navegação); as regras de negócio ficam nos handlers da Application.
- **Application** — casos de uso, um por pasta de slice vertical (ex.: `Todos/Create/`, `Todos/Complete/`), mais `Abstractions/` com as interfaces transversais (`Messaging`, `Behaviors`, `Data`, `Authentication`, `Authorization`, `Pagination`) e `Authorization/` com o catálogo de permissões e os guards. Sem MediatR — commands e queries são tratados por interfaces próprias `ICommandHandler`/`IQueryHandler`, resolvidas pela DI com varredura de assembly do `Scrutor`.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, nomes em snake_case, migrations em `Database/Migrations`, seeders em `Database/Seeding`), JWT + refresh tokens, provedor de permissões e policies de autorização, `HybridCache` e despacho dos eventos de domínio.
- **Web.Api** — endpoints de minimal API (uma classe por endpoint implementando `IEndpoint`, registrada automaticamente pela varredura em `EndpointExtensions`), rate limiting, CORS, OpenTelemetry, log de requisições com Serilog, tratamento global de exceções → `ProblemDetails` e Swagger com JWT.

### Organização dos slices verticais

Cada caso de uso fica em `Application/{Aggregate}/{UseCase}/` como um slice independente, ex.: `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — o DTO `ICommand<TResponse>` (ou `IQuery<TResponse>`).
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependências pelo construtor primário (`IApplicationDbContext`, `IUserContext`, `IPermissionProvider`, `IDateTimeProvider` etc.). Retorna `Result<T>` e nunca lança exceção para falhas esperadas.
- `CreateTodoCommandValidator.cs` — `AbstractValidator<TCommand>` do FluentValidation, `internal sealed`, carregado automaticamente pelo `AddValidatorsFromAssembly`. Todo command tem um; queries não têm.
- Queries projetam direto num DTO `{X}Response` do próprio slice, com `.Select(...)`, e nunca retornam entidades. Listas paginadas usam `ToPagedResponseAsync` (`Abstractions/Pagination`).

Limites usados por mais de um validator do mesmo agregado (tamanhos máximos, política de senha) ficam numa classe estática `{Feature}ValidationRules` ao lado dos slices (ex.: `Application/Todos/TodoValidationRules.cs`), em vez de repetidos como números soltos.

O endpoint correspondente fica em `Web.Api/Endpoints/{Aggregate}/{UseCase}.cs`, como uma `internal sealed class : IEndpoint` com um DTO `Request` aninhado. Ele mapeia a requisição para o command/query, chama o handler e converte o `Result` em HTTP com `result.Match(Results.Ok | Results.NoContent, CustomResults.Problem)`.

Os testes seguem a mesma estrutura: `tests/Application.UnitTests/{Aggregate}/{UseCase}{Command|Query}HandlerTests.cs` para os handlers, `{Aggregate}ValidatorsTests.cs` para os validators, e `tests/IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` para os testes de ponta a ponta por HTTP contra um PostgreSQL do Testcontainers.

### Comportamentos transversais com decorators

O `Application/DependencyInjection.cs` envolve os handlers com decorators usando o `.Decorate` do `Scrutor`. Em execução, a requisição passa por **Logging → Validation → handler** (o logging é o decorator mais externo, então falhas de validação também são registradas). A validação só envolve commands; ela interrompe com um `ValidationError` antes de o handler rodar, então os handlers não reconferem o formato da entrada — só aplicam as regras de negócio.

### Padrão Result (sem exceções para falhas esperadas)

`Result` / `Result<T>` do `SharedKernel` é a convenção de tratamento de erro em Domain, Application e Web.Api. Os handlers retornam `Result.Failure<T>(SomeErrors.Reason(...))` em vez de lançar exceção. Cada agregado tem a sua classe estática de erros (`TodoItemErrors`, `UserErrors`) com códigos como `"TodoItems.NotFound"`. O `CustomResults.Problem` converte `ErrorType` em status HTTP: `Validation`/`Problem` → 400, `NotFound` → 404, `Conflict` → 409, `Forbidden` → 403, `Failure` → 500. O `GlobalExceptionHandler` trata as falhas realmente inesperadas, além de conflitos de concorrência e violações de índice único (409) e requisições ilegíveis (400).

### Eventos de domínio

Os handlers chamam `entity.Raise(new SomeDomainEvent(...))` antes do `SaveChangesAsync`. O `ApplicationDbContext.SaveChangesAsync` salva primeiro e depois o `DomainEventsDispatcher` publica os eventos, cada um no seu próprio escopo de DI e **fora da transação original**. Os handlers de evento implementam `IDomainEventHandler<T>` e são registrados automaticamente pela varredura de assembly.

## Autenticação e autorização

- **Modelo:** Usuários → Funções → Permissões. Os códigos de permissão (ex.: `"todos:manage"`) ficam catalogados em `Application/Authorization/PermissionCodes.cs`, com nome de exibição, descrição, grupo e a marcação `IsAdministrative`. As funções padrão e suas permissões são criadas via `HasData` nas configurações da Infrastructure; usuários novos recebem a função padrão no cadastro.
- **No endpoint:** use `.HasPermission(PermissionCodes.X.Y)`, que impede a API de iniciar se o código não estiver no catálogo. Use `.RequireAuthorization()` simples quando a regra depende do dado (dono do item).
- **No handler:** regras que dependem de uma permissão *e* do dado são conferidas no handler via `IPermissionProvider` — ex.: "o próprio item exige `todos:update-own`, o de qualquer pessoa exige `todos:manage`", ou `IsSelfOrHasPermissionAsync`. Falhas retornam `UserErrors.Unauthorized()` (403).
- **Fonte da verdade:** as permissões são sempre resolvidas no servidor pelo `IPermissionProvider` (banco + `HybridCache` por usuário). Usuários desativados ficam sem nenhuma permissão. Todo command que muda as permissões efetivas de um usuário (funções, permissões de uma função, ativação) precisa chamar `permissionProvider.InvalidateAsync(userId)` depois de salvar. Os clientes leem a lista atualizada em `GET /users/me`; a API nunca autoriza com base nas claims do JWT.
- **Guards:** o `PrivilegeEscalationGuard` impede conceder ou remover permissões administrativas que quem chama não tem; o `AdministratorGuard` garante que sempre reste pelo menos um administrador ativo.
- **Tokens:** o access token expira após `Jwt:ExpirationInMinutes`; o refresh token, após `Jwt:RefreshTokenExpirationInDays`. O refresh token é guardado só como hash SHA-256 (que também é token de concorrência), trocado a cada renovação e revogado no logout ou na troca de senha.

## Convenções a manter ao ampliar

- Todo texto mostrado ao usuário (descrições de erro, mensagens de validação) fica em português do Brasil; código, identificadores, códigos de erro e templates de log ficam em inglês.
- Casos de uso novos vão em `Application/{Aggregate}/{UseCase}/`, seguindo o trio Command/Handler/Validator acima — não introduza MediatR.
- Entidades novas ganham a sua própria classe estática `{Entity}Errors` no Domain, em vez de lançar exceções ou reaproveitar os erros de outro agregado.
- As configurações do EF Core (`IEntityTypeConfiguration<T>`) ficam em `Infrastructure/{Aggregate}/{Entity}Configuration.cs` e são carregadas pelo `ApplyConfigurationsFromAssembly`; `DbSet`s novos vão em `IApplicationDbContext`, `ApplicationDbContext` e no `TestDbContext` dos testes unitários.
- Endpoints são uma classe por rota em `Web.Api/Endpoints/{Aggregate}/`, com tag via `Web.Api/Endpoints/Tags.cs`, e registrados automaticamente — não há tabela de rotas para atualizar.
- Este repositório tem `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) com essas convenções em forma de roteiro executável — prefira usá-las a escrever à mão, para que o código novo fique igual aos slices existentes.
