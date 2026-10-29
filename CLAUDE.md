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
- **Application** — casos de uso, um por pasta de slice vertical (ex.: `Todos/Create/`, `Todos/Complete/`), mais `Abstractions/` com as interfaces transversais (`Messaging`, `Behaviors`, `Data`, `Authentication`). Sem MediatR — commands e queries são tratados por interfaces próprias `ICommandHandler`/`IQueryHandler`, resolvidas pela DI com varredura de assembly do `Scrutor`.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, nomes em snake_case, migrations), autenticação JWT com rotação de refresh tokens, autorização por permissões, `HybridCache`, Serilog e despacho dos eventos de domínio.
- **Web.Api** — endpoints de minimal API (uma classe por endpoint implementando `IEndpoint`, registrada automaticamente pela varredura de assembly em `EndpointExtensions`), rate limiting, OpenTelemetry, tratamento global de exceções → `ProblemDetails` e Swagger/OpenAPI com JWT.

### Organização dos slices verticais

Cada caso de uso fica em `Application/{Aggregate}/{UseCase}/` como um slice independente, ex.: `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — o DTO `ICommand<TResponse>` (ou `IQuery<TResponse>`).
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependências injetadas pelo construtor primário (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider` etc.). Retorna `Result<T>` e nunca lança exceção para falhas esperadas.
- `CreateTodoCommandValidator.cs` — `AbstractValidator<TCommand>` do FluentValidation, carregado automaticamente pelo `AddValidatorsFromAssembly`.

O endpoint correspondente fica em `Web.Api/Endpoints/{Aggregate}/{Verb}.cs`, como uma `internal sealed class : IEndpoint` com um DTO `Request` aninhado. Ele mapeia a requisição para o command/query, chama o handler e converte o `Result` em HTTP com `result.Match(Results.Ok, CustomResults.Problem)`.

Os testes seguem a mesma estrutura: `tests/TickestPristine.Application.UnitTests/{Aggregate}/{UseCase}HandlerTests.cs` para os testes unitários dos handlers, mais os testes de validator, e `tests/TickestPristine.IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` para os testes de ponta a ponta por HTTP contra uma instância de PostgreSQL do Testcontainers.

### Comportamentos transversais com decorators

O `Application/DependencyInjection.cs` envolve todos os handlers de command/query com decorators (pelo `.Decorate` do `Scrutor`), aplicados nesta ordem: **Validation → Logging → handler** (a Validation envolve só os commands; as queries passam só pelo Logging). A validação interrompe em caso de falha e retorna `Result.Failure` com um `ValidationError` antes de o handler rodar — os próprios handlers não precisam validar a entrada.

### Padrão Result (sem exceções para falhas esperadas)

`Result` / `Result<T>` do `SharedKernel` é a convenção de tratamento de erro em Domain, Application e Web.Api. Os handlers retornam `Result.Failure<T>(SomeErrors.Reason(...))` em vez de lançar exceção. Cada agregado define a sua própria classe estática de erros (`TodoItemErrors`, `UserErrors`) com métodos de fábrica nomeados. Os endpoints convertem o `Result` em resposta HTTP pelo `CustomResults.Problem` (que converte o `ErrorType` no status adequado do `ProblemDetails`). Exceções ficam reservadas para falhas realmente inesperadas e são capturadas pelo `GlobalExceptionHandler`.

### Eventos de domínio

As entidades herdam de `Entity` e guardam as próprias regras (domínio rico): os métodos da entidade mudam o estado e chamam `Raise(new SomeDomainEvent(...))`; o handler carrega a entidade, chama o método e salva. Os eventos são `sealed record` com ids (mais o valor anterior numa alteração), sem `OccurredOnUtc`. O código listado em [Em migração](#em-migração) ainda dispara os eventos pelos handlers. O `DomainEventsDispatcher` (Infrastructure) despacha os eventos disparados depois do `SaveChangesAsync`; os handlers de evento implementam `IDomainEventHandler<T>` e são registrados automaticamente pela varredura de assembly.

## Convenções a manter ao ampliar

- Casos de uso novos vão em `Application/{Aggregate}/{UseCase}/`, seguindo o trio Command/Handler/Validator acima — não introduza MediatR.
- Entidades novas ganham a sua própria classe estática `{Entity}Errors` no Domain, em vez de lançar exceções cruas ou reaproveitar os erros de outro agregado.
- As configurações do EF Core (`IEntityTypeConfiguration<T>`) ficam em `Infrastructure/{Aggregate}/{Entity}Configuration.cs` e precisam ser adicionadas ao `ApplicationDbContext`.
- Endpoints são uma classe por rota em `Web.Api/Endpoints/{Aggregate}/`, com tag via `Web.Api/Endpoints/Tags.cs`, e registrados automaticamente — não há tabela de rotas para atualizar.
- Este repositório tem `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`, `domain-modeling`) com essas convenções em forma de roteiro executável — prefira usá-las a escrever à mão, para que o código novo fique exatamente igual aos slices existentes. Ao usá-las, os caminhos, nomes e desvios da seção [Contexto do projeto](#contexto-do-projeto) prevalecem sobre os exemplos delas. As skills `spec-new`, `spec-implement` e `spec-verify` conduzem o fluxo SDD; `add-entity`, `add-feature`, `add-tests` e `domain-modeling` só escrevem código dentro dele. As skills de código são adaptação da biblioteca canônica `C:\dev\dotnet-universal-skills`: cada adaptação fica marcada no texto delas como "Adaptação do Tickest" e é conferida por `scripts/test_instructions.py`, com os diffs esperados em `scripts/instruction_parity/`.

## Contexto do projeto

### Stack

.NET 10 (minimal APIs), EF Core 10 com Npgsql e PostgreSQL 17, FluentValidation, Scrutor, Dapper (só em consultas de relatório), JWT, `HybridCache` (Redis opcional), Serilog + Seq, OpenTelemetry. Testes: xUnit, NSubstitute, Shouldly, Testcontainers e NetArchTest. Versões dos pacotes em `Directory.Packages.props`.

### Mapa do repositório

| O quê | Onde (namespace = caminho, com prefixo `TickestPristine.`) |
|---|---|
| Slices de caso de uso | `src/TickestPristine.Application/{Aggregate}/{UseCase}/` |
| Abstrações transversais | `src/TickestPristine.Application/Abstractions/` (`Authentication`, `Authorization`, `Behaviors`, `Data`, `Messaging`, `Pagination`, `Storage`) |
| Guards de autorização | `src/TickestPristine.Application/Authorization/` (`TicketAccessGuard`, `PrivilegeEscalationGuard`, `AdministratorGuard`) |
| Catálogo de permissões | `src/TickestPristine.Application/Permissions/` (`PermissionCodes`, `PermissionCatalog`) |
| Entidades, erros e eventos | `src/TickestPristine.Domain/{Aggregate}/` |
| Configurações EF | `src/TickestPristine.Infrastructure/{Aggregate}/{Entity}Configuration.cs` |
| Migrations e seed | `src/TickestPristine.Infrastructure/Database/Migrations/` e `Database/Seeding/` |
| Endpoints | `src/TickestPristine.Web.Api/Endpoints/{Aggregate}/{UseCase}.cs`, com as constantes de permissão em `Endpoints/{Aggregate}/Permissions.cs` |
| Testes | `tests/TickestPristine.Application.UnitTests/{Aggregate}/`, `tests/TickestPristine.IntegrationTests/{Aggregate}/`, `tests/TickestPristine.ArchitectureTests/` |

Validação de uma mudança: `dotnet build TickestPristine.slnx` (0 avisos) e `dotnet test TickestPristine.slnx` (Docker ligado). Migration nova:

```bash
dotnet ef migrations add Nome_Da_Migration --project src/TickestPristine.Infrastructure --startup-project src/TickestPristine.Web.Api --output-dir Database/Migrations
```

### Referências vivas

Antes de criar algo, siga o slice real mais parecido (caminhos a partir de `src/TickestPristine.Application/` e `tests/`):

- Command com retorno: `Tickets/Create/` · sem retorno, com permissão na rota: `Tickets/Close/` · com guard por recurso: `Tickets/Reopen/`
- Query com guard: `Tickets/GetById/` · dados do próprio usuário, paginada por offset: `Tickets/GetOwn/` · paginada por cursor: `TicketMessages/GetAll/` · relatório em SQL (Dapper): `Tickets/GetVolume/`
- Entidade e configuração EF: `Domain/Departments/Department.cs` e `Infrastructure/Departments/DepartmentConfiguration.cs`
- Testes: `Tickets/CloseTicketCommandHandlerTests.cs`, `Tickets/TicketValidatorsTests.cs` (unitários) e `Tickets/TicketsTests.cs` (integração)

### Desvios em relação aos exemplos das skills

- **Caminhos e namespaces** têm o prefixo `TickestPristine.` (ex.: `src/TickestPristine.Application`, não `src/Application`).
- **Autorização** tem três formas, conforme o caso:
  1. permissão na rota com `.HasPermission(Permissions.X)`, que recebe uma única permissão;
  2. `.RequireAuthorization()` na rota e, para regras por recurso (dono, participante, privilégio), a forma híbrida: a regra sobre o próprio estado no método da entidade, as permissões efetivas pelo `IPermissionProvider` e a conferência que precisa de consulta num método privado do handler (os guards estáticos atuais estão em [Em migração](#em-migração));
  3. filtro por `IUserContext.UserId` só nas consultas de dados do próprio usuário (`Tickets/GetOwn`, `Tickets/GetOwnSummary`).
  Permissão nova entra em `PermissionCodes` e em `PermissionCatalog`.
- **Cache:** nenhuma query da Application usa cache; o único uso de `HybridCache` é o `PermissionProvider` (Infrastructure), invalidado com `IPermissionProvider.InvalidateAsync` depois do commit. O padrão de cache dos exemplos de Todo não se aplica aqui sem uma decisão explícita.
- **Histórico de chamados:** todo command que altera um chamado, uma mensagem ou um anexo grava um `TicketHistory` no mesmo `SaveChangesAsync`; no modelo rico, o método do agregado cria a entrada e o handler a adiciona (hoje ela é montada por `TicketHistoryEntries`, em migração).
- **Exclusão lógica** (`DeletedAtUtc` + filtro global) em `Ticket`, `TicketMessage` e `TicketAttachment`.
- **Textos em pt-BR:** mensagens de `{Entity}Errors`, `.WithMessage(...)` dos validators e textos escritos à mão nos `ProblemDetails` (`GlobalExceptionHandler`, `CustomResults`). Os nomes exibidos das propriedades validadas ficam em `Abstractions/Behaviors/ValidationDisplayNames.cs`.
- **Testes de integração:** o cadastro de usuários exige `users:create`, então os testes cadastram pelo administrador (`BaseIntegrationTest.RegisterUserAsync` / `PostRegisterAsAdminAsync`).
- **Adaptações em relação à biblioteca canônica** (marcadas no texto das skills como "Adaptação do Tickest"): prefixo `TickestPristine.` nos caminhos e namespaces; prosa, comentários e mensagens em pt-BR; avisos "Contexto do projeto primeiro" e "Portão de especificação"; sem `HybridCache` nos handlers; sem conversor UTC nem auditoria no `SaveChangesAsync` (nomes `CreatedAtUtc` e `DeletedAtUtc`); `TicketHistory` como exemplo do registro criado pelo agregado; autorização com `.HasPermission`, conferência no handler e `IPermissionProvider`.

### Regras permanentes

- Sem MediatR (ver [Convenções a manter ao ampliar](#convenções-a-manter-ao-ampliar)); domínio rico: a entidade guarda as invariantes e as transições (construtor privado com guardas, `private set`, `Create`, métodos que devolvem `Result` ou `bool` e disparam o próprio evento); objeto de valor só para valor com regra própria e vários usos; serviço de domínio para regra entre agregados, sem infraestrutura; o handler carrega, autoriza, coordena e grava; a consulta usa predicado traduzível. Padrão completo na skill `domain-modeling`; o legado está em [Em migração](#em-migração).
- **Comentários:** um `<summary>` curto em pt-BR só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio; comentários internos objetivos; nenhum comentário cita SPEC, REQ ou DEC; nenhum `<summary>` existente é removido.
- Datas e horas pelo `IDateTimeProvider`, nunca `DateTime.UtcNow` na Application.
- Identificadores de código em inglês; comentários e textos ao usuário em pt-BR.
- Mensagens de commit em pt-BR, no formato `tipo(escopo): descrição [SPEC-NNN T00n]` (`tipo` em `feat`, `fix`, `refactor`, `perf`, `test`, `docs` ou `chore`; `T00n` é a tarefa da spec, omitido em commits que não são de uma tarefa), com o trailer de coautoria do agente.
- Toda spec tem uma branch própria e atômica, criada a partir da `main` atualizada (`origin/main`) e entregue por pull request para a `main`, onde o CI e a proteção da branch valem, no padrão `<tipo>/SPEC-NNN-<slug>`: `tipo` é `feat`, `fix`, `bugfix`, `chore`, `docs`, `test` ou `refactor`; `NNN` é o número da spec; o slug fica em inglês, em kebab-case (ex.: `feat/SPEC-009-attendant-lookup-assignment`). A branch só contém a spec dela. O título do PR cita `SPEC-NNN`, e o corpo traz o link para o `spec.md` e a lista de REQs atendidos.

### Em migração

Código que ainda segue o modelo anêmico. Ele continua valendo e funcionando até a spec que o migrar (a migração do `Ticket` é a primeira); código novo e código alterado fora desta lista seguem o domínio rico. As specs de chamados que seguem antes dessa migração (como a 029, a 031 e as seguintes) mantêm o padrão do código que alteram e entram nesta lista.

- **Entidades com `{ get; set; }`:** todas as do Domain (`Ticket`, `TicketMessage`, `TicketAttachment`, `TicketHistory`, `TicketSlaTimer`, `User`, `UserCredential`, `UserInvitation`, `UserRole`, `RefreshToken`, `Role`, `Permission`, `RolePermission`, `Department`, `Sector`, `SlaCalendar`, `SlaCalendarHoliday`, `AuditLogEntry`).
- **Regras em classes estáticas da Application:** `Tickets/TicketStatusTransitions.cs`, `Tickets/TicketSla.cs`, `TicketHistories/TicketHistoryEntries.cs`, `Users/UserCodes.cs` e os calendários de SLA em `SlaCalendars/`.
- **Guards estáticos de autorização:** `Authorization/TicketAccessGuard.cs`, `Authorization/PrivilegeEscalationGuard.cs` e `Authorization/AdministratorGuard.cs`.
- **Eventos disparados pelos handlers** (`entity.Raise(...)` na Application) e propriedades atribuídas pelos handlers.
- **Seeders** em `src/TickestPristine.Infrastructure/Database/Seeding/` (`DatabaseSeeder`, `AdminUserSeeder`, `DefaultOrganizationSeeder`, `SampleDataSeeder`), que montam as entidades pelos setters; a estratégia de seed para o modelo rico é de uma spec própria.

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
3. **`/spec-implement NNN`** (só por comando, na branch da spec) executa uma tarefa por vez com as skills de implementação: build e testes, diff, ok do responsável e commit. Mudança de escopo ou DEC nova faz a skill parar e perguntar.
4. **`spec-verify`** comprova cada critério com testes aprovados na execução atual, roda a auditoria (`python scripts/auditoria_sdd.py .`) e a revisão e atualiza as seções afetadas do `SYSTEM_SPECIFICATION.md`.
5. **Entrega:** o PR leva a spec `Verificada`, com o número do PR no campo `pr:`; o push e o merge são do responsável. Com o PR mergeado na `main`, a spec está `Entregue`, e o `/spec-status` mostra isso.

Estados e transições da spec: `.claude/skills/spec-new/references/spec-status.md`.

### Portão determinístico

**Estado:** instalado em 23/10/2026 (até essa data só existia na worktree de teste). O `.claude/settings.json` também nega, em `permissions.deny`, a leitura de `.env` e `.env.*` e o `git push` pelo agente: o push é feito pelo responsável (`! git push`).

O hook `.claude/hooks/sdd_gate.py` (registrado em `.claude/settings.json`) bloqueia escrita em `src/` e `tests/`, por ferramenta de edição ou comando de shell, fora de um `/spec-implement NNN` cuja spec esteja `Em implementação` e com `aprovada-em` preenchido; um bloqueio com a spec ainda `Aprovada` não encerra o `/spec-implement` da sessão. Para uma mudança trivial, inclua `[sdd:autorizo]` na sua mensagem: a liberação vale só para ela e termina na sua próxima mensagem, mesmo que ela chegue no meio de uma resposta. O próprio hook e os settings também exigem o marcador. O hook evita erros do agente; não resiste a quem queira burlá-lo de propósito. Os caminhos bloqueados e os limites conhecidos estão no `SYSTEM_SPECIFICATION.md` (6.9). Testes: `python -m unittest discover -s .claude/hooks -p "test_*.py"` (também configurados como passo do CI em `.github/workflows/build.yml`).
