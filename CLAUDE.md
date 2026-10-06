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

# Segredos locais (uma vez por máquina): ficam fora do repositório, nos user-secrets.
# A API não sobe sem o Jwt:Secret (mínimo de 32 caracteres); o Admin:Password só é lido
# quando o seed cria o primeiro administrador.
dotnet user-secrets set "Jwt:Secret" "<string aleatória longa>" --project src/Web.Api
dotnet user-secrets set "Admin:Password" "<senha provisória>" --project src/Web.Api

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

### Seeds e deploy

O seed (`Infrastructure/Database/Seeding`) roda dentro de `Migrate()` pelo `UseSeeding`/`UseAsyncSeeding` do EF Core. Isso vale para a API em Development, para o `dotnet ef database update` e para os migration bundles (`dotnet ef migrations bundle`), que são o caminho recomendado para produção. O script SQL (`dotnet ef migrations script --idempotent`) **não** roda o seed: quem usar esse caminho precisa chamar `Migrate()` depois, ou o banco fica sem o primeiro administrador.

Cada dado fica no mecanismo de quem é dono dele:
- **Dados de que o código depende** (as roles, o catálogo de permissões e o vínculo padrão entre eles): `HasData` na configuração do EF, ou seja, migration. O vínculo role → permissão é o ponto de partida e o administrador pode alterá-lo pela API; por isso, quando uma migration acrescentar um vínculo padrão, troque o `InsertData` gerado em `role_permissions` por `migrationBuilder.Sql("INSERT ... ON CONFLICT DO NOTHING")` (o vínculo pode já ter sido concedido à mão), e nunca reinsira vínculos que já existiam.
- **Primeiro administrador**: seeder, porque usa segredo e configuração do ambiente (`Admin:*`). Só roda quando não há nenhum administrador ativo e cria a senha como provisória; depois disso os administradores são geridos pela API.
- **Conteúdo inicial que o usuário edita** (ex.: cadastros de partida): seeder que só roda com a tabela vazia, ligado por uma flag em `Seeding:*`.
- **Dados fictícios de desenvolvimento**: seeder com Bogus e semente fixa, desligado por padrão (`Seeding:SampleData`). Nunca em migration.

O teste `MigrationsTests` falha quando o modelo (inclusive o `HasData`) muda sem uma migration nova.

## Arquitetura

Cinco projetos, com dependências sempre apontando para dentro. Isso é garantido por `tests/ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain e Application nunca podem referenciar Infrastructure ou Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

- **SharedKernel** — peças básicas de DDD, sem dependência de nenhum outro projeto da solução: `Entity` (classe base que guarda os eventos disparados), `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider`, `IAuditable`.
- **Domain** — entidades, eventos de domínio e uma classe estática `*Errors` por agregado (ex.: `TodoItemErrors`, `UserErrors`), organizados por pasta de agregado (`Todos/`, `Users/`), e não por tipo técnico. As entidades são classes simples, com propriedades com setter e ids das chaves estrangeiras (sem propriedades de navegação); as regras de negócio ficam nos handlers da Application. **Exceção: `Role` e `Permission`** (`Domain/Roles`) são catálogos com instâncias `static readonly` (ex.: `Role.Administrator`, `Permission.TicketsManage`), compartilhadas pela aplicação inteira e levadas ao banco pelo `HasData`. `Role` tem construtor privado e `private set`, e `Permission` tem construtor público e propriedades só de leitura (`{ get; }`, mapeadas explicitamente na configuração do EF). Não herdam `Entity` e não têm id: a chave é o próprio nome (`Role`, até 50 caracteres) ou código (`Permission`). Nenhuma instância é alterada em execução.
- **Application** — casos de uso, um por pasta de slice vertical (ex.: `Todos/Create/`, `Todos/Complete/`), mais `Abstractions/` com as interfaces transversais (`Messaging`, `Behaviors`, `Data`, `Authentication`, `Authorization`, `Pagination`) e `Authorization/` com o catálogo de permissões e os guards. Sem MediatR — commands e queries são tratados por interfaces próprias `ICommandHandler`/`IQueryHandler`, resolvidas pela DI com varredura de assembly do `Scrutor`.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, nomes em snake_case, migrations em `Database/Migrations`, seeders em `Database/Seeding`), JWT + refresh tokens, provedor de permissões e policies de autorização, `HybridCache` e despacho dos eventos de domínio.
- **Web.Api** — endpoints de minimal API (uma classe por endpoint implementando `IEndpoint`, registrada automaticamente pela varredura em `EndpointExtensions`), rate limiting, CORS, OpenTelemetry, log de requisições com Serilog, tratamento global de exceções → `ProblemDetails` e Swagger com JWT.

### Organização dos slices verticais

Cada caso de uso fica em `Application/{Aggregate}/{UseCase}/` como um slice independente, ex.: `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — o DTO `ICommand<TResponse>` (ou `IQuery<TResponse>`).
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependências pelo construtor primário (`IApplicationDbContext`, `IUserContext`, `IPermissionProvider`, `IDateTimeProvider` etc.). Retorna `Result<T>` e nunca lança exceção para falhas esperadas.
- `CreateTodoCommandValidator.cs` — `AbstractValidator<TCommand>` do FluentValidation, `internal sealed`, carregado automaticamente pelo `AddValidatorsFromAssembly`. Todo command tem um; queries não têm.
- Queries projetam direto num DTO `{X}Response` do próprio slice, com `.Select(...)`, e nunca retornam entidades.
- **Paginação** (`Abstractions/Pagination`): toda coleção que cresce sem limite é paginada. Só os catálogos de tamanho fixo ou administrado (roles, permissões, departamentos, setores) devolvem lista.
  - **Os dados da página nunca são declarados um a um.** O endpoint recebe um único parâmetro — `[AsParameters] PageRequest paging` (offset: `page`, `pageSize`) ou `[AsParameters] CursorPageRequest paging` (cursor: `cursorDate`, `cursorId`, `pageSize`) — e o repassa; a query carrega o mesmo objeto como último parâmetro, `PageRequest Paging` ou `CursorPageRequest Paging`. Valores ausentes ou inválidos são ajustados dentro da `PaginationExtensions` (página mínima 1, tamanho padrão 20, máximo 100), nunca no endpoint ou no handler.
  - **Offset** (telas administrativas que pulam para qualquer página): devolve `PagedResponse<T>`, e o handler termina com `.ToPagedListAsync(query.Paging, cancellationToken)`, que faz a contagem e a página.
  - **Cursor/keyset** (linhas do tempo, feeds, rolagem infinita e tabelas grandes, em que o cliente só avança): o cursor é composto (data + chave, porque a data pode repetir). O handler aplica os filtros próprios, chama `.AfterCursor(query.Paging, x => x.Data, x => x.Id)` — que filtra depois do cursor e ordena do mais novo para o mais antigo, com os mesmos seletores —, projeta no DTO e termina com `.ToCursorPagedListAsync(query.Paging, x => x.Data, x => x.Id, cancellationToken)`, que busca um item a mais para saber se há próxima página e devolve `CursorPagedResponse<T>(Items, NextCursorDate, NextCursorId, HasNextPage)`, sem total. Nunca escreva à mão o filtro `data < cursor || ...` nem o `DateTime.SpecifyKind` do cursor.
  - Regras das duas: os filtros vêm antes da paginação; a ordenação é estável, com a chave por último; toda ordenação paginada tem um índice que a acompanhe na configuração do EF (filtro, data descendente e id); e o total (`COUNT`) só é calculado quando a tela mostra o número de páginas. Contagens para gráficos e painéis vêm de uma query de resumo própria, nunca da listagem paginada.

Limites usados por mais de um validator do mesmo agregado (tamanhos máximos, política de senha) ficam numa classe estática `{Feature}ValidationRules` ao lado dos slices (ex.: `Application/Todos/TodoValidationRules.cs`), em vez de repetidos como números soltos.

O endpoint correspondente fica em `Web.Api/Endpoints/{Aggregate}/{UseCase}.cs`, como uma `internal sealed class : IEndpoint` com um DTO `Request` aninhado. Ele mapeia a requisição para o command/query, chama o handler e converte o `Result` em HTTP com `result.Match(Results.Ok | Results.NoContent, CustomResults.Problem)`.

Os testes seguem a mesma estrutura: `tests/Application.UnitTests/{Aggregate}/{UseCase}{Command|Query}HandlerTests.cs` para os handlers, `{Aggregate}ValidatorsTests.cs` para os validators, e `tests/IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` para os testes de ponta a ponta por HTTP contra um PostgreSQL do Testcontainers.

### Comportamentos transversais com decorators

O `Application/DependencyInjection.cs` envolve os handlers com decorators usando o `.Decorate` do `Scrutor`. Em execução, a requisição passa por **Logging → Validation → handler** (o logging é o decorator mais externo, então falhas de validação também são registradas). A validação só envolve commands; ela interrompe com um `ValidationError` antes de o handler rodar, então os handlers não reconferem o formato da entrada — só aplicam as regras de negócio.

### Padrão Result (sem exceções para falhas esperadas)

`Result` / `Result<T>` do `SharedKernel` é a convenção de tratamento de erro em Domain, Application e Web.Api. Os handlers retornam `Result.Failure<T>(SomeErrors.Reason(...))` em vez de lançar exceção. Cada agregado tem a sua classe estática de erros (`TodoItemErrors`, `UserErrors`) com códigos como `"TodoItems.NotFound"`. O `CustomResults.Problem` converte `ErrorType` em status HTTP: `Validation`/`Problem` → 400, `NotFound` → 404, `Conflict` → 409, `Forbidden` → 403, `Failure` → 500. O `GlobalExceptionHandler` trata as falhas realmente inesperadas, além de conflitos de concorrência e violações de índice único (409) e requisições ilegíveis (400).

### Eventos de domínio

Os handlers chamam `entity.Raise(new SomeDomainEvent(...))` antes do `SaveChangesAsync`. O `ApplicationDbContext.SaveChangesAsync` salva primeiro e depois o `DomainEventsDispatcher` publica os eventos, cada um no seu próprio escopo de DI e **fora da transação original**. Os handlers de evento implementam `IDomainEventHandler<T>` e são registrados automaticamente pela varredura de assembly.

### Log de auditoria

O `AuditLoggingInterceptor` (`Infrastructure/Audit`, um `SaveChangesInterceptor`) grava um `AuditLogEntry` para cada entidade marcada com `IAuditable` que foi incluída, alterada ou excluída, no mesmo `SaveChanges` (portanto na mesma transação) da mudança: nome e id da entidade, ação (`Added`/`Modified`/`Deleted`), usuário (nulo fora de requisição autenticada, como cadastro, login e seed), horário, valores antigos e novos em `jsonb` (na alteração, só as colunas modificadas) e a lista de colunas alteradas. Chaves compostas viram as partes separadas por vírgula. A consulta fica em `GET audit-logs/{entityName}/{entityId}`, com a permissão de leitura de auditoria.

- Entidade nova que precisa de trilha de quem mudou o quê implementa `IAuditable`. Dados técnicos de escrita frequente (ex.: refresh tokens) e históricos de negócio (ex.: o histórico do chamado) ficam de fora.
- Propriedades sensíveis (hashes de senha e de token, segredos) entram na lista `ExcludedProperties` do interceptor quando a entidade é criada: o valor nunca vai para o log, só o nome da coluna em `AffectedColumns`.
- `ExecuteUpdate`/`ExecuteDelete` e SQL puro não passam pelo `SaveChanges` e não são auditados; quem usar precisa gravar o `AuditLogEntry` explicitamente.

## Autenticação e autorização

- **Modelo:** Usuários → Funções → Permissões, definidos no Domain (`Domain/Roles`) e levados ao banco pelas migrations:
  - `Role` — as roles como instâncias `static readonly`, identificadas pelo nome (ex.: `Role.Administrator`). Só existem as roles do código: a API não cria roles. O cadastro atribui a role padrão (`Role.Collaborator`) direto no handler.
  - `Permission` — uma instância `static readonly` por permissão, com nome de exibição, descrição, grupo e `IsAdministrative`, mais `All`, `Find` e `GetDisplayName`; vai para a tabela `permissions`, referenciada por `role_permissions`.
  - `PermissionCodes` — as constantes dos códigos (ex.: `"todos:manage"`), para endpoints, handlers e testes. Toda constante precisa da sua `Permission` (um teste confere), e permissão nova exige migration.
  - `RolePermissionConfiguration` — as permissões padrão de cada role (`HasData`). O administrador pode incluir ou remover permissões de qualquer role pela API, menos do Administrador, que recebe sempre o catálogo inteiro.

  Em `Application/Authorization` ficam os guards (`PrivilegeEscalationGuard` e `AdministratorGuard`), que devolvem `Result` prontos para o handler.

  Decisões de modelagem (não trocar sem rever estes motivos):
  - **`Role` como catálogo fixo**: construtor privado, `Name` como chave e nenhum outro campo. `UserRole` e `RolePermission` apontam para a role pelo nome (`RoleName`).
  - **Vínculos como entidades (`UserRole`, `RolePermission`)**, sem navegações: segue a regra de não ter navegações, permite que o administrador edite as permissões das roles e mantém atribuições e permissões no log de auditoria (`IAuditable`).
  - **Concorrência do último administrador** num token sombra `Version` da tabela `roles` (configurado no `RoleConfiguration`, fora da entidade): o `AdministratorGuard` o incrementa por `context.Entry(role)`, e de duas remoções simultâneas dos últimos administradores só a primeira salva.
  - **Role padrão atribuída no handler de cadastro** (`Role.Collaborator`), como o resto das regras: as entidades não têm métodos de fábrica.

- **No endpoint:** cada pasta de endpoints tem um `Permissions.cs` (`internal static class Permissions`) com as constantes que os endpoints dela usam, apontando para `PermissionCodes` (ex.: `internal const string TodosManage = PermissionCodes.Todos.Manage;`), e o endpoint chama `.HasPermission(Permissions.TodosManage)` antes do `.WithTags(...)`. O `HasPermission` só registra a policy; quem garante que o código existe é o teste de `Permission`. Use `.RequireAuthorization()` simples quando a regra depende do dado (dono do item).
- **No handler:** regras que dependem de uma permissão *e* do dado são conferidas no handler via `IPermissionProvider` — ex.: "o próprio item exige `todos:update-own`, o de qualquer pessoa exige `todos:manage`", ou `IsSelfOrHasPermissionAsync`. Falhas retornam `UserErrors.Unauthorized()` (403).
- **Fonte da verdade:** as permissões são sempre resolvidas no servidor pelo `IPermissionProvider` (banco + `HybridCache` por usuário). Usuários desativados ficam sem nenhuma permissão. Todo command que muda as permissões efetivas de um usuário (funções, permissões de uma função, ativação) precisa chamar `permissionProvider.InvalidateAsync(userId)` depois de salvar. Os clientes leem a lista atualizada em `GET /users/me`; o JWT só carrega a identidade (id, e-mail e nome), nunca permissões.
- **Guards:** o `PrivilegeEscalationGuard.EnsureCallerCanChangeAsync` impede conceder ou remover permissões administrativas que quem chama não tem (recebe as permissões que entram e as que saem); o `AdministratorGuard.EnsureNotLastAdministratorAsync` é chamado antes de um usuário perder o acesso de administrador e garante que sempre reste pelo menos um administrador ativo. Quando várias queries de um agregado repetem a mesma regra de acesso (participante do item ou permissão de gerenciar), ela vira um guard próprio em `Application/Authorization` (ex.: `{Aggregate}AccessGuard.EnsureCanViewAsync`), que lê só os campos necessários e devolve `NotFound`/`Unauthorized` prontos — nunca a mesma checagem copiada em cada handler.
- **Senha provisória:** credenciais com `MustChangePassword` (como a do primeiro administrador) não recebem token no login, que responde 403 `Users.PasswordChangeRequired`. O cliente troca a senha em `POST users/login/new-password` (anônimo, confere as credenciais de novo), que já devolve os tokens.
- **Tokens:** o access token expira após `Jwt:ExpirationInMinutes`; o refresh token, após `Jwt:RefreshTokenExpirationInDays`. O refresh token é guardado só como hash SHA-256 (que também é token de concorrência), trocado a cada renovação e revogado no logout ou na troca de senha.

## Convenções a manter ao ampliar

- Todo texto mostrado ao usuário (descrições de erro, mensagens de validação) fica em português do Brasil; código, identificadores, códigos de erro e templates de log ficam em inglês.
- Casos de uso novos vão em `Application/{Aggregate}/{UseCase}/`, seguindo o trio Command/Handler/Validator acima — não introduza MediatR.
- Entidades novas ganham a sua própria classe estática `{Entity}Errors` no Domain, em vez de lançar exceções ou reaproveitar os erros de outro agregado.
- As configurações do EF Core (`IEntityTypeConfiguration<T>`) ficam em `Infrastructure/{Aggregate}/{Entity}Configuration.cs` e são carregadas pelo `ApplyConfigurationsFromAssembly`; `DbSet`s novos vão em `IApplicationDbContext`, `ApplicationDbContext` e no `TestDbContext` dos testes unitários.
- Endpoints são uma classe por rota em `Web.Api/Endpoints/{Aggregate}/`, com tag via `Web.Api/Endpoints/Tags.cs`, e registrados automaticamente — não há tabela de rotas para atualizar.
- Este repositório tem `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) com essas convenções em forma de roteiro executável — prefira usá-las a escrever à mão, para que o código novo fique igual aos slices existentes.
