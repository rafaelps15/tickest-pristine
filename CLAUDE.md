# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar com o código deste repositório.

## O que é

O TickestPristine é o backend de um sistema de chamados internos (helpdesk) em .NET 10 e PostgreSQL, construído sobre uma base pragmática de Clean Architecture: chamados (abertura, atendimento, atribuição, fechamento, reabertura e SLA de resolução), mensagens, anexos, histórico, usuários com roles e permissões, departamentos, setores e auditoria. Autenticação JWT, autorização por permissões, logs estruturados e OpenTelemetry já vêm configurados. Não há front-end neste repositório.

Os exemplos de Todos que aparecem neste arquivo e nas skills vêm do modelo de referência e são ilustrativos; o contexto real deste repositório está na seção [Contexto do projeto](#contexto-do-projeto).

## Comandos

```bash
# Sobe a infraestrutura (PostgreSQL + Seq) antes de rodar a API ou os testes de integração
docker compose up -d

# Roda a API
dotnet run --project src/TickestPristine.Web.Api

# Build / restore
dotnet restore TickestPristine.slnx
dotnet build TickestPristine.slnx

# Roda todos os testes (os de integração sobem um container descartável do PostgreSQL via
# Testcontainers, então o Docker precisa estar rodando)
dotnet test TickestPristine.slnx

# Roda um projeto de teste
dotnet test tests/TickestPristine.Application.UnitTests
dotnet test tests/TickestPristine.IntegrationTests
dotnet test tests/TickestPristine.ArchitectureTests

# Roda um teste pelo nome (qualquer projeto)
dotnet test --filter "FullyQualifiedName~CreateTodoCommandHandlerTests"
```

O Seq (visualizador de logs estruturados) fica disponível em http://localhost:8090 depois do `docker compose up -d`.

Para usar .NET 8 ou .NET 9 em vez de .NET 10, veja as notas no `Directory.Build.props` (também é preciso atualizar o Dockerfile em `src/TickestPristine.Web.Api`).

Avisos são tratados como erros (`TreatWarningsAsErrors`, `AnalysisMode=All`), então o `dotnet build` é uma verificação de qualidade de verdade, e não só uma compilação.

## Arquitetura

Cinco projetos, com dependências sempre apontando para dentro. Isso é garantido por `tests/TickestPristine.ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain e Application nunca podem referenciar Infrastructure ou Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

A Web.Api referencia diretamente só a Infrastructure e chega à Application de forma transitiva.

- **SharedKernel** — peças básicas de DDD, sem dependência de nenhum outro projeto da solução: `Entity` (classe base que guarda os eventos de domínio disparados), `Result`/`Result<T>` (a convenção de tratamento de erro — veja abaixo), `Error`/`ErrorType`, `IDomainEvent`, `IDateTimeProvider`.
- **Domain** — entidades, eventos de domínio e uma classe estática `*Errors` por agregado (ex.: `TodoItemErrors`, `UserErrors`). Sem dependência de frameworks. Organizado por pasta de agregado (`Todos/`, `Users/`), e não por tipo técnico.
- **Application** — casos de uso, um por pasta de slice vertical (ex.: `Todos/Create/`, `Todos/Complete/`), mais `Abstractions/` com as interfaces transversais (`Messaging`, `Behaviors`, `Data`, `Authentication`). Sem MediatR — commands e queries são tratados por interfaces próprias `ICommandHandler`/`IQueryHandler`, resolvidas pela DI com varredura de assembly do `Scrutor`. Os commands acessam os dados pelo `IApplicationDbContext`; as queries leem com Dapper pela `IDbConnectionFactory`, com o SQL no handler.
- **Infrastructure** — EF Core nos commands (`ApplicationDbContext`, PostgreSQL, nomes em snake_case, migrations), a `IDbConnectionFactory` das consultas em Dapper, com os leitores de tipo registrados uma vez, autenticação JWT com rotação de refresh tokens, autorização por permissões, `HybridCache`, Serilog e despacho dos eventos de domínio.
- **Web.Api** — endpoints de minimal API (uma classe por endpoint implementando `IEndpoint`, registrada automaticamente pela varredura de assembly em `EndpointExtensions`), rate limiting, OpenTelemetry, tratamento global de exceções → `ProblemDetails` e Swagger/OpenAPI com JWT.

### Organização dos slices verticais

Cada caso de uso fica em `Application/{Aggregate}/{UseCase}/` como um slice independente, ex.: `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — o DTO `ICommand<TResponse>` (ou `IQuery<TResponse>`).
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependências injetadas pelo construtor primário (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider` etc.). Retorna `Result<T>` e nunca lança exceção para falhas esperadas.
- `CreateTodoCommandValidator.cs` — `AbstractValidator<TCommand>` do FluentValidation, carregado automaticamente pelo `AddValidatorsFromAssembly`.

Um slice de query tem a query, o handler e o DTO de resposta, sem validator; o handler lê com Dapper pela `IDbConnectionFactory`, nos modelos da skill `add-feature` (`references/query-slice.md` e `references/pagination.md`).

O endpoint correspondente fica em `Web.Api/Endpoints/{Aggregate}/{Verb}.cs`, como uma `internal sealed class : IEndpoint` com um DTO `Request` aninhado. Ele mapeia a requisição para o command/query, chama o handler e converte o `Result` em HTTP com `result.Match(Results.Ok, CustomResults.Problem)`.

Os testes seguem a mesma estrutura: `tests/TickestPristine.Application.UnitTests/{Aggregate}/{UseCase}HandlerTests.cs` para os testes unitários dos command handlers, mais os testes de validator; `tests/TickestPristine.IntegrationTests/{Aggregate}/{UseCase}QueryHandlerTests.cs` para os de query handler, contra o banco; e `tests/TickestPristine.IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` para os testes de ponta a ponta por HTTP contra uma instância de PostgreSQL do Testcontainers.

### Comportamentos transversais com decorators

O `Application/DependencyInjection.cs` envolve todos os handlers de command/query com decorators (pelo `.Decorate` do `Scrutor`), aplicados nesta ordem: **Validation → Logging → handler** (a Validation envolve só os commands; as queries passam só pelo Logging). A validação interrompe em caso de falha e retorna `Result.Failure` com um `ValidationError` antes de o handler rodar — os próprios handlers não precisam validar a entrada.

### Padrão Result (sem exceções para falhas esperadas)

`Result` / `Result<T>` do `SharedKernel` é a convenção de tratamento de erro em Domain, Application e Web.Api. Os handlers retornam `Result.Failure<T>(SomeErrors.Reason(...))` em vez de lançar exceção. Cada agregado define a sua própria classe estática de erros (`TodoItemErrors`, `UserErrors`) com métodos de fábrica nomeados. Os endpoints convertem o `Result` em resposta HTTP pelo `CustomResults.Problem` (que converte o `ErrorType` no status adequado do `ProblemDetails`). Exceções ficam reservadas para falhas realmente inesperadas e são capturadas pelo `GlobalExceptionHandler`.

### Eventos de domínio

As entidades herdam de `Entity` e guardam as próprias regras (domínio rico): os métodos da entidade mudam o estado e chamam `Raise(new SomeDomainEvent(...))`; o handler carrega a entidade, chama o método e salva. Os eventos são `sealed record` com ids (mais o valor anterior numa alteração), sem `OccurredOnUtc`. O código listado em [Em migração](#em-migração) ainda dispara os eventos pelos handlers. Depois de um `SaveChangesAsync` bem-sucedido, o `PublishDomainEventsInterceptor` (Infrastructure) recolhe os eventos das entidades e os entrega ao `IDomainEventsDispatcher`, fora da transação; se o save falha, os eventos continuam nas entidades. Os handlers de evento implementam `IDomainEventHandler<T>` e são registrados automaticamente pela varredura de assembly.

## Convenções a manter ao ampliar

- Casos de uso novos vão em `Application/{Aggregate}/{UseCase}/`, seguindo o trio Command/Handler/Validator acima — não introduza MediatR.
- Entidades novas ganham a sua própria classe estática `{Entity}Errors` no Domain, em vez de lançar exceções cruas ou reaproveitar os erros de outro agregado.
- As configurações do EF Core (`IEntityTypeConfiguration<T>`) ficam em `Infrastructure/{Aggregate}/{Entity}Configuration.cs` e precisam ser adicionadas ao `ApplicationDbContext`.
- Endpoints são uma classe por rota em `Web.Api/Endpoints/{Aggregate}/`, com tag via `Web.Api/Endpoints/Tags.cs`, e registrados automaticamente — não há tabela de rotas para atualizar.
- Este repositório tem `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`, `domain-modeling`) com essas convenções em forma de roteiro executável — prefira usá-las a escrever à mão, para que o código novo fique exatamente igual aos slices existentes. Ao usá-las, os caminhos, nomes e desvios da seção [Contexto do projeto](#contexto-do-projeto) prevalecem sobre os exemplos delas. As skills `spec-new`, `spec-implement` e `spec-verify` conduzem o fluxo SDD; `add-entity`, `add-feature`, `add-tests` e `domain-modeling` só escrevem código dentro dele. As skills de código são tradução fiel e genérica da biblioteca canônica `C:\dev\dotnet-universal-skills`, conferida por `scripts/test_instructions.py`: elas não citam este projeto nem outro, e os desvios do Tickest ficam só na seção [Desvios em relação aos exemplos das skills](#desvios-em-relação-aos-exemplos-das-skills).

## Contexto do projeto

### Stack

.NET 10 (minimal APIs), EF Core 10 com Npgsql e PostgreSQL 17, FluentValidation, Scrutor, Dapper nas consultas (o EF Core fica nos commands e nas migrations), JWT, `HybridCache` (Redis opcional), Serilog + Seq, OpenTelemetry. Testes: xUnit, NSubstitute, Shouldly, Testcontainers e NetArchTest. Versões dos pacotes em `Directory.Packages.props`.

### Mapa do repositório

| O quê | Onde (namespace = caminho, com prefixo `TickestPristine.`) |
|---|---|
| Slices de caso de uso | `src/TickestPristine.Application/{Aggregate}/{UseCase}/` |
| Abstrações transversais | `src/TickestPristine.Application/Abstractions/` (`Authentication`, `Authorization`, `Behaviors`, `Data`, `Messaging`, `Pagination`, `Storage`) |
| Conferências de autorização compartilhadas | `src/TickestPristine.Application/Roles/` (`PrivilegeEscalationCheck`, `LastAdministratorCheck`: peças injetadas que respondem e deixam o erro com o handler) e `Tickets/TicketViewAccess.cs`; código novo usa a forma híbrida da skill `add-feature` (`references/authorization.md`) |
| Catálogo de permissões | `src/TickestPristine.Application/Permissions/` (`PermissionCodes`, `PermissionCatalog`) |
| Trechos de SQL com nome | `src/TickestPristine.Application/{Aggregate}/{Aggregate}Sql.cs` (`TicketSql`, `UserSql`), só constantes, com o teste de concordância em `tests/TickestPristine.IntegrationTests/{Aggregate}/` |
| Entidades, erros e eventos | `src/TickestPristine.Domain/{Aggregate}/` |
| Configurações EF | `src/TickestPristine.Infrastructure/{Aggregate}/{Entity}Configuration.cs` |
| Migrations e seed | `src/TickestPristine.Infrastructure/Database/Migrations/` e `Database/Seeding/` |
| Endpoints | `src/TickestPristine.Web.Api/Endpoints/{Aggregate}/{UseCase}.cs`, com as constantes de permissão em `Endpoints/{Aggregate}/Permissions.cs` |
| Testes | `tests/TickestPristine.Application.UnitTests/{Aggregate}/`, `tests/TickestPristine.IntegrationTests/{Aggregate}/`, `tests/TickestPristine.ArchitectureTests/` |

Validação de uma mudança: por impacto, conforme o item "Testes por impacto" da seção [Desvios em relação aos exemplos das skills](#desvios-em-relação-aos-exemplos-das-skills): o build (`dotnet build TickestPristine.slnx`, 0 avisos) e só os testes afetados, com os de integração (Docker ligado) só na verificação final; a suíte completa (`dotnet test TickestPristine.slnx`) roda no CI. Migration nova:

```bash
dotnet ef migrations add Nome_Da_Migration --project src/TickestPristine.Infrastructure --startup-project src/TickestPristine.Web.Api --output-dir Database/Migrations
```

### Referências vivas

Antes de criar algo, siga o slice real mais parecido (caminhos a partir de `src/TickestPristine.Application/` e `tests/`) na estrutura — pastas, nomes, endpoint, validator, paginação, testes. Os slices de `Departments/` e `Sectors/` e os commands de `Tickets/` que alteram o próprio chamado (criar, atualizar, atribuir, remover responsável, fechar, reabrir e excluir) já estão no domínio rico; os demais ainda são do modelo anêmico (ver [Em migração](#em-migração)), e neles o modelo da entidade, da mudança de estado e da autorização por recurso vem das skills (`add-entity/references/rich-entity.md`, `domain-modeling`, `add-feature/references/authorization.md`), e não do slice.

- Command com retorno: `Tickets/Create/` · sem retorno, com permissão na rota: `Tickets/Close/` · com conferência por recurso: `Tickets/Reopen/`
- Query com conferência por recurso (peça injetada `TicketViewAccess`): `Tickets/GetById/` · dados do próprio usuário, paginada por offset: `Tickets/GetOwn/` · paginada por cursor: `TicketMessages/GetAll/` · com coleção filha (multi-mapping): `Departments/GetAll/` · relatório: `Tickets/GetVolume/`
- Entidade e configuração EF: `Domain/Departments/Department.cs` e `Infrastructure/Departments/DepartmentConfiguration.cs`
- Testes: `Tickets/CloseTicketCommandHandlerTests.cs`, `Tickets/TicketValidatorsTests.cs` (unitários), `Tickets/GetTicketByIdQueryHandlerTests.cs` (query handler, na integração) e `Tickets/TicketsTests.cs` (integração por HTTP)

### Desvios em relação aos exemplos das skills

- **Caminhos e namespaces** têm o prefixo `TickestPristine.` (ex.: `src/TickestPristine.Application`, não `src/Application`).
- **Autorização** tem três formas, conforme o caso:
  1. permissão na rota com `.HasPermission(Permissions.X)`, que recebe uma única permissão;
  2. `.RequireAuthorization()` na rota e, para regras por recurso (dono, participante, privilégio), a forma híbrida: a regra sobre o próprio estado no método da entidade, as permissões efetivas pelo `IPermissionProvider` e a conferência que precisa de consulta num método privado do handler (as conferências compartilhadas de funções ficam em `Application/Roles/`, como peças injetadas);
  3. filtro por `IUserContext.UserId` só nas consultas de dados do próprio usuário (`Tickets/GetOwn`, `Tickets/GetOwnSummary`).
  Permissão nova entra em `PermissionCodes` e em `PermissionCatalog`. Exceção à permissão fixa na rota: `Users/GetAttendants` confere `users:read` no handler, pelo `IPermissionProvider`, para o 403 levar a mensagem de negócio (`UserErrors.ReadForbidden`); a rota exige só usuário ativo.
- **Cache:** nenhuma query da Application usa cache; o único uso de `HybridCache` é o `PermissionProvider` (Infrastructure), invalidado com `IPermissionProvider.InvalidateAsync` depois do commit. O padrão de cache dos exemplos de Todo não se aplica aqui sem uma decisão explícita.
- **Histórico de chamados:** todo command que altera um chamado, uma mensagem ou um anexo grava um `TicketHistoryEntry` no mesmo `SaveChangesAsync`; no modelo rico, o método do agregado cria a entrada e o handler a adiciona (o `Ticket` cria as do chamado, inclusive a da mensagem publicada pelo `PostMessage` e a do anexo enviado pelo `AddAttachment`; o `TicketMessage`, as da edição e da exclusão; o `TicketAttachment`, a da exclusão).
- **Exclusão lógica** (`DeletedAtUtc` + filtro global) em `Ticket`, `TicketMessage` e `TicketAttachment`; nas consultas em SQL, o filtro é `deleted_at_utc IS NULL`, escrito em cada SQL que lê `tickets`, `ticket_messages` ou `ticket_attachments`.
- **Consultas em Dapper:**
  - os modelos das skills abrem a conexão e chamam o Dapper sem o token; aqui o handler repassa o `CancellationToken` ao `OpenConnectionAsync` (a `IDbConnectionFactory` do projeto o recebe) e ao Dapper pelo `CommandDefinition`;
  - as tabelas levam o esquema (`public.tickets`);
  - igualdade com coluna `citext` leva o cast no parâmetro (`email = @Email::citext`), e a busca usa `LIKE` com o padrão de `Abstractions/Data/SqlLikePattern` (o termo escapado, como o EF montava);
  - o plano de SLA (jsonb) é lido pelo `TicketSlaPlanTypeHandler`, registrado com a `IDbConnectionFactory`, no mesmo formato da configuração do EF;
  - as regras que se repetem entre consultas (chamado ativo, situação do SLA, cancelamento e prazo do dono, conta pendente, conta utilizável, quem atende) e o recorte comum à listagem e às estatísticas (`TicketSql.MatchesScope`, com os parâmetros no objeto anônimo de cada handler) ficam em `TicketSql` e `UserSql`; os trechos do chamado espelham as regras do `Ticket` (`GetSlaStatus`, `CanBeCanceled`, `OwnerCancelDeadline`), com teste de concordância;
  - o SQL preenche o próprio DTO de resposta, sem classe intermediária; o que não sai do SQL entra no DTO depois da leitura, como no `Departments/GetAll`: o bloco `sla` do detalhe (`TicketSlaReader`, que segue o calendário útil), as roles da página de usuários, as permissões do usuário atual, a descrição do histórico (montada no multi-mapping da página, `QueryCursorPagedAsync<TFirst, TSecond, TResult>`, com os nomes lidos na própria consulta da página) e o conteúdo do anexo baixado;
  - a query de resumo (uma linha de contagens) lê com `QuerySingleAsync` direto no DTO, porque a contagem sempre devolve uma linha; a resposta aninhada do painel (`Tickets/GetOverview`) sai por multi-mapping;
  - a ordenação escolhida pelo usuário (`Tickets/GetAll`, `Users/GetAll`) é uma constante de SQL completa por ordenação, escolhida pela ordem pedida, cada uma acompanhada do seu índice;
  - as conferências que não usam a conexão (a validação da entrada, porque query não tem validator, e as permissões pelo `IPermissionProvider`) vêm antes de abrir a conexão;
  - as consultas que usam regras do cronômetro de SLA leem os `TicketSlaTimer` pelo Dapper, com as colunas de `TicketSql.SlaTimerColumns`.
- **Testes de query handler:** ficam na integração, sobre o `BaseQueryHandlerTest`: cada teste ganha um banco próprio, copiado de um modelo migrado e vazio, e grava o cenário sem as chaves estrangeiras (`session_replication_role = replica` na sessão do arranjo), como no banco em memória dos testes unitários de antes. Os testes de handler sem banco (`Permissions/GetAll`, `TicketAttachments/GetPolicy` e os caminhos de validação de `Tickets/GetOverview` e `Tickets/GetVolume`) continuam unitários.
- **Textos em pt-BR:** mensagens de `{Entity}Errors`, `.WithMessage(...)` dos validators e textos escritos à mão nos `ProblemDetails` (`GlobalExceptionHandler`, `CustomResults`). Os nomes exibidos das propriedades validadas ficam em `Abstractions/Behaviors/ValidationDisplayNames.cs`.
- **Testes de integração:** o cadastro de usuários exige `users:create`, então os testes cadastram pelo administrador (`BaseIntegrationTest.RegisterUserAsync` / `PostRegisterAsAdminAsync`).
- **Colunas `citext` com limite:** o nome de departamento e de setor, o título e a descrição do chamado e o nome, o sobrenome e o e-mail do usuário são `citext` (busca e unicidade sem diferenciar maiúsculas), e o `citext` não guarda tamanho; o limite vale no banco pela check constraint da configuração (`ck_departments_name_length`, `ck_sectors_name_length`, `ck_tickets_title_length`, `ck_tickets_description_length`, que também exige o mínimo de 10 caracteres, e `ck_users_*_length`). A recusa do banco responde 400 "Texto fora do tamanho permitido" pelo `GlobalExceptionHandler`.
- **Persistência:** sem conversor UTC nem auditoria no `SaveChangesAsync`: das seções do modelo `add-entity/references/persistence.md` das skills, só a "Coleção num campo privado" vale aqui, e a "auditoria" delas (`IAuditableEntity`, com datas preenchidas pelo contexto) não é o `IAuditable` nem o `AuditLoggingInterceptor` (log de auditoria) do projeto; as datas usam os nomes `CreatedAtUtc` e `DeletedAtUtc`, chegam pelos métodos da entidade com a hora do `IDateTimeProvider`, e a exclusão lógica é a da linha acima.
- **Permissões na Application:** o código da permissão vem de `PermissionCodes` (`Application/Permissions/`), em classes aninhadas por área (`PermissionCodes.Tickets.Manage`, `PermissionCodes.Users.Read`); a classe `Permissions` fica na Web.Api e só é usada no `.HasPermission(...)` das rotas. Onde o exemplo das skills usa `Permissions.X` dentro de um handler, aqui é `PermissionCodes.{Area}.{Acao}`.
- **Peças do domínio rico no `SharedKernel`:** `ValueObject`, `ResultExtensions`, `Result.Create` e `Result.FirstFailureOrSuccess` ainda não existem; entram, como na skill `domain-modeling`, na primeira tarefa de spec que os usar.
- **Registro criado pelo agregado:** o exemplo real do padrão 7 da `domain-modeling` é o `TicketHistoryEntry` (linha "Histórico de chamados" acima).
- **Tipos de erro:** além dos tipos listados nas skills, o `ErrorType.Forbidden` vira 403 no `CustomResults.Problem`; `UserErrors.Unauthorized()` e as falhas de autorização por recurso usam esse tipo.
- **`Update` de departamento e setor:** `Department.Update` e `Sector.Update` disparam o evento de alteração sempre, mesmo quando nada mudou (comportamento mantido da versão anterior); o padrão das skills para "pode não mudar nada" (`bool`, evento só quando muda) vale para código novo. O `Ticket` não tem `Update` genérico: o `ChangeDescription` só dispara `TicketDescriptionChangedDomainEvent`, com a descrição anterior, quando a descrição muda, e devolve `null`, sem histórico nem evento, quando não muda; as mudanças de status são métodos por intenção, com o `TicketStatusChangedDomainEvent` (ou o `TicketReopenedDomainEvent`, na reabertura).
- **Revisão do legado:** o código listado em [Em migração](#em-migração) não é achado da `ca-review` até a spec que o migrar; código novo, fora da lista, segue as regras da skill.
- **Testes por impacto:** a verificação local roda só o que a mudança afeta, e a suíte completa (`dotnet test TickestPristine.slnx`) fica para o CI. Esta regra prevalece sobre o `dotnet test` integral das skills de código (`add-feature`, `add-tests`, `add-entity`) e do agente `dotnet-architect`; dentro de uma spec, o passo de verificação dessas skills é o da tarefa. Testes de integração não rodam durante as tarefas nem durante as correções.
  - **Critério de seleção:** a entrada é a lista de arquivos alterados (na tarefa, os dela; na verificação, os da spec). A verificação final junta as linhas que se aplicam e roda só o que não tiver resultado válido. Os caminhos omitem o prefixo `src/TickestPristine.` ou `tests/TickestPristine.`, e `{A}` é a pasta do agregado.

    | Alteração | Na tarefa | Na verificação final |
    |---|---|---|
    | Teste unitário criado ou alterado | a classe dele | — |
    | Teste de integração criado ou alterado | não roda | a classe dele |
    | Infraestrutura de teste (classes base, fixtures, helpers) | o projeto de unitários inteiro, se for dele | o projeto de integração inteiro, se for dele |
    | `Domain/{A}/` ou `Application/{A}/` | `Application.UnitTests/{A}/` e as pastas dependentes | `IntegrationTests/{A}/` e as pastas dependentes |
    | `Infrastructure/{A}/` ou `Web.Api/Endpoints/{A}/` | os unitários das pastas dependentes | `IntegrationTests/{A}/` e as pastas dependentes |
    | `SharedKernel`, `Application/Abstractions` (inclusive `Behaviors`) ou qualquer `DependencyInjection.cs` | o projeto de unitários inteiro | o projeto de integração inteiro |
    | Infraestrutura compartilhada e borda HTTP: `ApplicationDbContext`, migrations, seeding, interceptors, autenticação e autorização, `Program.cs`, middleware, `GlobalExceptionHandler`, `CustomResults` | os unitários das pastas dependentes | o projeto de integração inteiro |
    | Build e pacotes: `*.csproj`, `*.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig` | o projeto de unitários inteiro | o projeto de integração inteiro |
    | Qualquer arquivo em `src/` | `ArchitectureTests` | — |
    | Sempre | — | os testes da rastreabilidade |
    | Só normas: `CLAUDE.md`, `.claude/` ou `scripts/` | os testes de `scripts` | os testes de `scripts` e de `.claude/hooks` e a auditoria, sem build nem testes .NET |

  - **Pastas dependentes:** para cada `.cs` de produção alterado, o nome do tipo (o nome do arquivo, e o nome antigo, se foi renomeado) é buscado como palavra inteira em `src/` e `tests/`; toda pasta de agregado que o cita entra como afetada. O filtro usa o namespace, que segue a pasta (ex.: `FullyQualifiedName~TickestPristine.Application.UnitTests.{A}`).
  - **Na dúvida, amplia:** se um arquivo não se encaixa em nenhuma linha, se uma ocorrência fica fora das pastas de agregado ou se o impacto é incerto, roda o projeto de teste inteiro correspondente e registra o motivo nas evidências. Nunca omite testes.
  - **Validade:**
    - afetam o build: `src/`, `tests/`, `*.csproj`, `*.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` e `.editorconfig`;
    - o último build vale até um desses arquivos mudar; a verificação não builda de novo se nenhum deles mudou depois dele;
    - um resultado de teste vale até mudar algum arquivo que, pelo critério de seleção, o afeta;
    - a auditoria (`scripts/auditoria_sdd.py`) cobre tudo o que ela lê: todo arquivo de texto fora de `tests/` (a varredura de segredos), o que inclui `src/`, as specs, as instruções, os settings e o CI; a lista de arquivos rastreados e de projetos de teste; e os últimos 30 commits. Ela vale enquanto nada disso mudar: num ciclo de correção, é reaproveitada se a correção só alterou arquivos `.cs` dentro de `tests/`; se alterou arquivo auditado, ou se entrou commit novo, roda de novo, uma vez, na nova verificação. Fora desses casos, não se repete;
    - as anotações da própria verificação na spec (rastreabilidade, evidências e histórico) e a atualização do documento de estado não invalidam build, testes nem auditoria;
    - só contam as execuções desta sessão.

### Regras permanentes

- Sem MediatR (ver [Convenções a manter ao ampliar](#convenções-a-manter-ao-ampliar)); domínio rico: a entidade guarda as invariantes e as transições (um único construtor privado vazio, `private set`, `Create` com inicializador de objeto e sem guardas, métodos que devolvem `Result`, `bool` ou `void` e disparam o próprio evento); objeto de valor só para valor com regra própria e vários usos; serviço de domínio para regra entre agregados, sem infraestrutura; o handler carrega, autoriza, coordena e grava; a consulta usa predicado traduzível. Padrão completo na skill `domain-modeling`; o legado está em [Em migração](#em-migração).
- **Comentários:** um `<summary>` curto em pt-BR só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio; comentários internos objetivos; nenhum comentário cita SPEC, REQ ou DEC; nenhum `<summary>` existente é removido.
- Datas e horas pelo `IDateTimeProvider`, nunca `DateTime.UtcNow` na Application.
- Identificadores de código em inglês; comentários e textos ao usuário em pt-BR.
- Mensagens de commit em pt-BR, no formato `tipo(escopo): descrição [SPEC-NNN T00n]` (`tipo` em `feat`, `fix`, `refactor`, `perf`, `test`, `docs` ou `chore`; `T00n` é a tarefa da spec, omitido em commits que não são de uma tarefa), com o trailer de coautoria do agente.
- Toda spec tem uma branch própria e atômica, criada a partir da `main` atualizada (`origin/main`) e entregue por pull request para a `main`, onde o CI e a proteção da branch valem, no padrão `<tipo>/SPEC-NNN-<slug>`: `tipo` é `feat`, `fix`, `bugfix`, `chore`, `docs`, `test` ou `refactor`; `NNN` é o número da spec; o slug fica em inglês, em kebab-case (ex.: `feat/SPEC-009-attendant-lookup-assignment`). A branch só contém a spec dela. O título do PR cita `SPEC-NNN`, e o corpo traz o link para o `spec.md` e a lista de REQs atendidos.

### Em migração

Código que ainda segue o modelo anêmico. Ele continua valendo e funcionando até a spec que o migrar (a de `Department` e `Sector` foi na spec 038; a do agregado de chamados, `Ticket`, `TicketSlaTimer`, `TicketMessage`, `TicketAttachment` e `TicketHistoryEntry`, nas specs 039, 041 e 045). Regra para cada mudança: o código que altera ou estende uma entidade, um handler ou uma classe desta lista segue o padrão dela até a migração; entidades e agregados novos, fora desta lista, já nascem no domínio rico.

- **Entidades com `{ get; set; }`:** todas as do Domain, exceto as do agregado de chamados, `Department` e `Sector` (`User`, `UserCredential`, `UserInvitation`, `UserRole`, `RefreshToken`, `Role`, `Permission`, `RolePermission`, `SlaCalendar`, `SlaCalendarHoliday`, `AuditLogEntry`).
- **Regras em classes estáticas da Application:** `Users/UserCodes.cs`.
- **Eventos disparados pelos handlers** (`entity.Raise(...)` na Application) e propriedades atribuídas pelos handlers.
- **Seeders** em `src/TickestPristine.Infrastructure/Database/Seeding/` (`DatabaseSeeder`, `AdminUserSeeder`, `DefaultOrganizationSeeder`, `SampleDataSeeder`), que montam as entidades pelos setters, exceto `Department` e `Sector`, já criados pelo `Create`, e o `Ticket` do `SampleDataSeeder`, com o histórico e os cronômetros, montados pelos métodos do `Ticket` e pelo `TicketSlaCycleService`; a estratégia de seed para o modelo rico é de uma spec própria.

### Documentos de estado

`SYSTEM_SPECIFICATION.md` descreve o que o sistema faz hoje: capacidades (2.7), regras de negócio (2.6), rotas (4.3.1), lacunas (7) e decisões em aberto (9.5). É extenso: leia só as seções de que a tarefa precisa e atualize as que a mudança afetar.

A spec é a unidade de trabalho: não há trabalho sem spec, e o estado de cada demanda fica só no frontmatter da spec dela, sem arquivo central. As ideias e as prioridades ficam com o responsável, fora do repositório. O comando `/spec-status` (somente leitura) mostra a visão geral de todas as specs.

## Política de autonomia

| Pode fazer sem perguntar | Precisa perguntar antes | Precisa de plano aprovado | Precisa de autorização explícita no chat |
|---|---|---|---|
| Ler código e documentos, buscar, rodar build e testes, verificações não destrutivas | Regra de negócio sem fonte no código ou nos documentos; mais de uma opção viável; conflito entre o pedido e este arquivo; teste que falha por defeito fora do escopo | Mudança funcional não trivial em `src/` ou `tests/` | Stage, commit, push e PR; excluir ou mover arquivos; aplicar migration em banco; alterar este arquivo, as skills ou configurações; qualquer ação fora do repositório |

A autorização vale só para a ação pedida, no escopo pedido. Todo commit, dentro ou fora das skills, só acontece depois de o agente mostrar o diff e receber o ok do responsável. Correção realmente trivial (texto, typo) dispensa o plano somente quando autorizada explicitamente no chat.

**Mudança trivial:** corrige texto, typo, comentário ou formatação sem mudar comportamento observável, contrato, banco ou regra; qualquer outra é não trivial. Escrever a spec em `specs/` (pela `spec-new`) e atualizar o `SYSTEM_SPECIFICATION.md` (pela `spec-verify`) fazem parte do fluxo SDD e não exigem autorização à parte.

## Fluxo de desenvolvimento (SDD)

Mudança funcional não trivial segue Spec-Driven Development, com uma spec por mudança em `specs/NNN-slug/spec.md`:

1. **`spec-new`** faz a descoberta (no máximo 5 perguntas por rodada, de negócio e técnicas, e para à espera das respostas) e só então escreve a spec (requisitos, critérios de aceitação, decisões, plano e tarefas); o que não foi confirmado fica marcado como `[SUPOSIÇÃO]`.
2. **Aprovação explícita** do responsável pelo produto no chat, sem `[SUPOSIÇÃO]` pendente; sem ela, nenhum código de `src/` ou `tests/` é escrito.
3. **`/spec-implement NNN`** (só por comando, na branch da spec) executa as tarefas em sequência contínua, ou `tarefa-a-tarefa` a pedido, com o diff e a aprovação do responsável a cada tarefa, usando as skills de implementação e os testes por impacto, e segue direto para a verificação; numa reprovação, corrige e verifica de novo, em até 2 ciclos. Só para diante de uma decisão que a spec não cobre, de um impedimento ou dos 2 ciclos esgotados e, no fim, uma única vez, com o diff, as evidências e a mensagem de commit proposta; stage, commit e push são do responsável. Mudança de escopo ou DEC nova faz a skill parar e perguntar.
4. **`spec-verify`** comprova cada critério com testes aprovados numa execução ainda válida, pelos testes por impacto, roda a auditoria (`python scripts/auditoria_sdd.py .`) uma vez, reaproveitada nos ciclos de correção conforme a validade, e a revisão, e atualiza as seções afetadas do `SYSTEM_SPECIFICATION.md`.
5. **Entrega:** o PR leva a spec `Verificada`, com o número do PR no campo `pr:`; o push e o merge são do responsável. Com o PR mergeado na `main`, a spec está `Entregue`, e o `/spec-status` mostra isso.

Estados e transições da spec: `.claude/skills/spec-new/references/spec-status.md`.

### Portão determinístico

**Estado:** instalado em 23/10/2026 (até essa data só existia na worktree de teste). O `.claude/settings.json` também nega, em `permissions.deny`, a leitura de `.env` e `.env.*` e o `git push` pelo agente: o push é feito pelo responsável (`! git push`).

O hook `.claude/hooks/sdd_gate.py` (registrado em `.claude/settings.json`) bloqueia escrita em `src/` e `tests/`, por ferramenta de edição ou comando de shell, fora de um `/spec-implement NNN` cuja spec esteja `Em implementação` e com `aprovada-em` preenchido; um bloqueio com a spec ainda `Aprovada` não encerra o `/spec-implement` da sessão. Para uma mudança trivial, inclua `[sdd:autorizo]` na sua mensagem: a liberação vale só para ela e termina na sua próxima mensagem, mesmo que ela chegue no meio de uma resposta. O próprio hook e os settings também exigem o marcador. O hook evita erros do agente; não resiste a quem queira burlá-lo de propósito. Os caminhos bloqueados e os limites conhecidos estão no `SYSTEM_SPECIFICATION.md` (6.9). Testes: `python -m unittest discover -s .claude/hooks -p "test_*.py"` (também configurados como passo do CI em `.github/workflows/build.yml`).
