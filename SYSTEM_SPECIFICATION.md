# SYSTEM_SPECIFICATION — TickestPristine

> **Estado documentado:** branch `feat/spec-driven-development`, commit `d0e18f5`. O código de `src/` e `tests/` é o desse commit (inclui a spec 001, remover o responsável, entregue no commit `b0e78b0`), mais as mudanças ainda não commitadas da spec 002 (histórico da alteração da descrição), `Verificada` em 23/10/2026 (seções 5.4 e 6.1). Produzido por leitura do código, da configuração, dos testes e da documentação, mais a execução de `dotnet build` e `dotnet test` (seção 6.1). O histórico das mudanças está no Git.
>
> **Convenções de leitura:**
> - **[Fato]** está no código ou na configuração e tem referência verificável.
> - **[Inferência]** é uma conclusão tirada de fatos, mas não declarada em nenhuma fonte.
> - **[Recomendação]** é uma sugestão do analista e não um requisito.
> - **[Decisão humana]** é um ponto que depende de definição do produto ou da arquitetura.
> - **[Resolvido]** marca um problema da análise original que já foi corrigido, com a data ou o commit da correção.
>
> As referências usam `caminho:linha` relativas à raiz do repositório. Os números de linha valem para o estado documentado acima.

---

## Sumário

1. [Visão executiva](#1-visão-executiva)
2. [Escopo do produto e domínio](#2-escopo-do-produto-e-domínio)
3. [Arquitetura e organização técnica](#3-arquitetura-e-organização-técnica)
4. [Especificação técnica](#4-especificação-técnica)
5. [Fluxos funcionais de ponta a ponta](#5-fluxos-funcionais-de-ponta-a-ponta)
6. [Qualidade, segurança e operação](#6-qualidade-segurança-e-operação)
7. [GAP analysis e evolução recomendada](#7-gap-analysis-e-evolução-recomendada)
8. [Catálogo de evidências e rastreabilidade](#8-catálogo-de-evidências-e-rastreabilidade)
9. [Limitações, pendências e perguntas em aberto](#9-limitações-pendências-e-perguntas-em-aberto)

---

## 1. Visão executiva

### 1.1 Propósito

- **[Fato]** O código implementa o **backend HTTP de um sistema de chamados internos (helpdesk)**. Ele cobre a abertura e o acompanhamento de chamados por setor, com mensagens, anexos, histórico, prazo de resolução (SLA) e painéis de contagem, além de usuários, funções (roles) e permissões, departamentos e setores e trilha de auditoria. Evidências: `src/TickestPristine.Domain/Tickets/`, `src/TickestPristine.Web.Api/Endpoints/` (49 classes de endpoint).
- **[Fato]** O `README.md:5` descreve o projeto como um "backend de sistema de chamados (helpdesk/ticketing)" que "nasceu a partir de um template genérico de Clean Architecture".
- **[Inferência]** O público é **interno a uma organização**: os papéis "Colaborador" (abre chamados) e "Atendente" (atende a fila), mais o seed com departamentos como TI, RH, Financeiro e Facilities (`src/TickestPristine.Infrastructure/Database/Seeding/DefaultOrganizationSeeder.cs:15-59`), apontam para atendimento de demandas internas, e não de clientes externos. Nenhum documento declara isso explicitamente.
- **[Fato]** Não há front-end neste repositório. Os comentários do código citam um "front" (ex.: `src/TickestPristine.Application/Users/GetCurrent/GetCurrentUserQueryHandler.cs:52`), e a configuração de CORS prevê origens de navegador (`src/TickestPristine.Web.Api/Extensions/CorsExtensions.cs`).

### 1.2 Principais capacidades identificadas

| Área | Situação resumida |
|---|---|
| Autenticação (login, refresh com rotação, logout, troca de senha, senha provisória, bloqueio por tentativas) | Implementada e evidenciada |
| Autorização por permissões vindas de roles (RBAC), com proteção contra escalada de privilégio e contra a remoção do último administrador | Implementada e evidenciada |
| Chamados: abrir, editar descrição/status, atribuir responsável, fechar, reabrir, excluir (lógico), listar, detalhar | Implementada e evidenciada (fechamento e atribuição incluídos na revisão de 22/10) |
| SLA de resolução em horas corridas por prioridade, com situação (no prazo, em risco, violado, cumprido, perdido) e painel | Implementada e evidenciada (modelo simples) |
| Mensagens, anexos (disco local) e histórico do chamado | Implementada e evidenciada |
| Departamentos e setores (CRUD sem reativação) | Implementada e evidenciada |
| Auditoria automática de alterações (interceptor EF) com consulta | Implementada e evidenciada |
| Dashboards (contagem por status, volume por período, resumo dos próprios chamados) | Implementada e evidenciada |
| Eventos de domínio | Mecanismo implementado; **quase nenhum consumidor** (1 handler, vazio) |
| SLA completo (calendário útil, pausas, escalonamento, matriz impacto × urgência, relatórios) | Planejada, não implementada (só existe como prompt de trabalho não versionado) |

### 1.3 Maturidade e arquitetura predominante

- **[Fato]** A arquitetura é uma **Clean Architecture em 5 projetos** (SharedKernel, Domain, Application, Infrastructure, Web.Api), com a direção das dependências garantida por testes de arquitetura (`tests/TickestPristine.ArchitectureTests/Layers/LayerTests.cs`). A Application usa **CQRS leve sem MediatR**, com handlers próprios decorados (Validation → Logging → handler), além de **Result pattern**, **minimal APIs** e **EF Core + PostgreSQL**.
- **[Fato]** O **domínio é anêmico**: as entidades só têm propriedades (ex.: `src/TickestPristine.Domain/Tickets/Ticket.cs:5-20`), e as regras ficam nos handlers e em classes estáticas da Application (`TicketStatusTransitions`, `TicketSla`, `TicketAccessGuard`).
- **[Fato]** O build compila sem avisos com `TreatWarningsAsErrors` e `AnalysisMode=All` (`Directory.Build.props:9-13`). A suíte completa passou: 674 testes na análise inicial e 699 depois da revisão de 22/10, sempre com 0 falhas (seção 6.1).
- **[Inferência]** A maturidade é a de um **MVP funcional em evolução ativa**: 53 commits entre 01/10/2026 e 21/10/2026, com migrations e refatorações em curso no working tree. Não há evidência de implantação em produção, de pipeline de deploy nem de configuração para ambiente produtivo.

### 1.4 Principais riscos e limitações conhecidos

1. **[Fato] Os eventos de domínio são publicados em memória depois do commit, sem outbox.** Hoje isso tem baixo impacto porque o único handler é vazio, mas o plano de SLA depende de notificações confiáveis. Ver GAP-05.
2. **[Fato] Os anexos ficam em disco local**, e o arquivo de um anexo excluído não é removido. Ver GAP-08 e GAP-09.
3. **[Fato] Não há procedimento automatizado de implantação nem de migração fora de Development.** Ver GAP-10.
4. **[Fato] O `CLAUDE.md` continua descrevendo o domínio de exemplo Todos**, de forma intencional (os caminhos e a porta foram corrigidos). Ver 9.3.

**[Resolvido] em 22/10/2026:** o status "Fechado" ficou alcançável (GAP-01), há atribuição de responsável depois da abertura (GAP-02), o cadastro de usuários exige `users:create` (GAP-03), e o `README.md` foi alinhado ao modelo de roles (GAP-07).

### 1.5 Limitações desta análise

- A API não foi executada contra um banco real fora dos testes. Nenhum fluxo foi exercitado manualmente.
- O conteúdo completo das 18 migrations não foi revisado linha a linha. Foram lidas a mais recente, o snapshot do modelo (via configurações EF) e os nomes de todas.
- O arquivo de user-secrets local não foi lido (segredos). Por isso, valores efetivos de `Jwt:Secret`, `Admin:Password` e `Seeding:SampleUserPassword` em desenvolvimento não são conhecidos.
- Os arquivos `.claude/skills/*` foram identificados, mas lidos apenas parcialmente (são instruções para agentes, não parte do produto).
- Não houve análise de desempenho nem de carga.

---

## 2. Escopo do produto e domínio

### 2.1 Contexto de negócio conhecido

- **[Fato]** O idioma do produto é o português do Brasil. Mensagens de erro, nomes de roles e permissões estão em pt-BR (`src/TickestPristine.Domain/*/*Errors.cs`, `src/TickestPristine.Application/Permissions/PermissionCatalog.cs`), e o FluentValidation é configurado para `pt-BR` (`src/TickestPristine.Application/DependencyInjection.cs:14-15`).
- **[Fato]** Há fuso horário de negócio fixo: o gráfico de volume agrupa os chamados pelo horário de `America/Sao_Paulo` (`src/TickestPristine.Application/Tickets/GetVolume/GetTicketVolumeQueryHandler.cs:26,36`).
- **[Fato]** Não há multi-tenancy. Nenhuma entidade tem identificador de organização ou tenant.

### 2.2 Atores e perfis identificados

Os perfis são **roles fixas criadas por migration** (`src/TickestPristine.Infrastructure/Roles/RoleConfiguration.cs:23-26`, nomes em `src/TickestPristine.Application/Roles/RoleNames.cs:8-10`). A API **não cria nem exclui roles**: só edita as permissões das que existem, menos a de administrador.

| Ator / role | Permissões padrão (`RolePermissionConfiguration.cs:33-51`) | Observações |
|---|---|---|
| **Visitante anônimo** | — | Pode fazer login, renovar token e trocar a senha provisória. Desde a revisão de 22/10, **não** pode mais se cadastrar. |
| **Colaborador** | `tickets:create`, `tickets:view-own`, `tickets:update-own`, `tickets:delete-own`, `tickets:reopen-own` | É a role atribuída automaticamente no cadastro (`RegisterUserCommandHandler.cs:39`) e marcada como `IsDefault` na listagem de roles (`GetRolesQueryHandler.cs:19`). |
| **Atendente** | As do Colaborador mais `tickets:manage` | Vê e trata os chamados de todos. **Não** tem permissões de usuários, roles ou estrutura. |
| **Administrador** | Todas as permissões do catálogo (16) | As permissões da role são gerenciadas pelo sistema e não podem ser editadas (`AssignRolePermissionsCommandHandler.cs:29-32`). O sistema impede ficar sem nenhum administrador ativo (`AdministratorGuard.cs:24-58`). |
| **Participantes do chamado** (papel contextual) | — | O solicitante (`CreatedByUserId`) e o responsável atual (`AssignedToUserId`) sempre podem **ver** o chamado e os itens dele (`TicketAccessGuard.cs:35-38`). Quem deixa de ser o responsável (remoção, spec 001) perde esse acesso, salvo se for o solicitante ou tiver `tickets:manage` (RN-23). |

**Catálogo de permissões** (`src/TickestPristine.Application/Permissions/PermissionCatalog.cs:16-44`, códigos em `PermissionCodes.cs`):

| Grupo | Código | Administrativa? | Exigida por algum endpoint/handler? |
|---|---|---|---|
| Chamados | `tickets:create`, `tickets:view-own`, `tickets:update-own`, `tickets:delete-own`, `tickets:reopen-own`, `tickets:manage` | Não | Sim |
| Usuários | `users:read`, `users:update`, `users:deactivate`, `users:assign-roles` | Sim | Sim |
| Usuários | `users:create` | Sim | Sim: `POST users/register` (`Endpoints/Users/Register.cs`). **[Resolvido]** GAP-03. |
| Funções | `roles:read`, `roles:manage` | Sim | Sim |
| Estrutura | `departments:manage`, `sectors:manage` | Sim | Sim |
| Auditoria | `audit:read` | Sim | Sim |

"Administrativa" significa que a permissão só pode ser concedida ou removida por quem já a possui (`PrivilegeEscalationGuard.cs:17-32`).

### 2.3 Módulos e áreas funcionais

| Módulo (pasta da Application) | Casos de uso (slices) |
|---|---|
| `Users` | Register, Login, Refresh, Logout, ChangePassword, ChangeRequiredPassword, UpdateProfile, Activate, Deactivate, AssignRoles, GetAll, GetById, GetByEmail, GetCurrent |
| `Roles` / `Permissions` | Roles: GetAll, AssignPermissions · Permissions: GetAll |
| `Departments` / `Sectors` | Create, Update, Deactivate, GetAll, GetById (em cada um) |
| `Tickets` | Create, Update, Assign, Unassign, Close, Reopen, Delete, GetAll, GetById, GetOwn, GetOwnSummary, GetSlaSummary, GetStatusCounts, GetVolume |
| `TicketMessages` | Post, Edit, Delete, GetAll |
| `TicketAttachments` | Upload, Download, Delete, GetAll |
| `TicketHistories` | GetAll (o histórico é gravado pelos handlers dos outros módulos) |
| `AuditLogs` | GetHistory |

### 2.4 Entidades e relacionamentos

```mermaid
erDiagram
    USER ||--|| USER_CREDENTIAL : "possui (1:1, cascade)"
    USER ||--o{ REFRESH_TOKEN : "sessões"
    USER ||--o{ USER_ROLE : ""
    ROLE ||--o{ USER_ROLE : "FK por role.name"
    ROLE ||--o{ ROLE_PERMISSION : "FK por role.name"
    PERMISSION ||--o{ ROLE_PERMISSION : "FK por permission.code"
    DEPARTMENT ||--o{ SECTOR : ""
    USER |o--o{ DEPARTMENT : "responsável (SetNull)"
    SECTOR ||--o{ TICKET : "Restrict"
    USER ||--o{ TICKET : "solicitante (Restrict)"
    USER |o--o{ TICKET : "responsável (SetNull)"
    TICKET ||--o{ TICKET_MESSAGE : "Cascade"
    TICKET ||--o{ TICKET_ATTACHMENT : "Cascade"
    TICKET ||--o{ TICKET_HISTORY : "Cascade"
    AUDIT_LOG_ENTRY }o..o| USER : "sem FK (sobrevive ao usuário)"
```

Fonte: `src/TickestPristine.Infrastructure/*/*Configuration.cs`. Pontos relevantes:

- **[Fato]** `Ticket`, `TicketMessage` e `TicketAttachment` usam **exclusão lógica** (`DeletedAtUtc`) com filtro global do EF (`TicketConfiguration.cs:25`, `TicketMessageConfiguration.cs:16`, `TicketAttachmentConfiguration.cs:20`). `TicketHistory` é imutável e não tem filtro (`TicketHistoryConfiguration.cs:21`).
- **[Fato]** Roles e permissões têm `Id` (Guid determinístico), mas os vínculos usam **chaves alternativas por nome/código** (`RoleConfiguration.cs:16`, `PermissionConfiguration.cs:16`, `RolePermissionConfiguration.cs:20-28`, `UserRoleConfiguration.cs:20-23`).
- **[Fato]** `User` tem `Code` público único no formato `usr_<ULID>` (`UserCodes.cs:9-11`, `UserConfiguration.cs:20-22`).
- **[Fato]** A role Administrador tem o token de concorrência sombra `Version` (`RoleConfiguration.cs:21`). `Ticket` tem token sombra mapeado em `xmin` (`TicketConfiguration.cs:29`). `RefreshToken.TokenHash` é token de concorrência (`RefreshTokenConfiguration.cs:19`).
- **[Fato]** Os enums `TicketStatus`, `TicketPriority` e `TicketHistoryAction` são gravados **pelo nome** (texto), conforme alteração registrada no commit `b2028ae` (`TicketConfiguration.cs:21-23`, `TicketHistoryConfiguration.cs:15`, migration `20261022023422_Store_Ticket_Enums_As_Names.cs`). A API continua expondo os valores numéricos.

### 2.5 Estados e transições do chamado

Regras em `src/TickestPristine.Application/Tickets/TicketStatusTransitions.cs:7-25`:

```mermaid
stateDiagram-v2
    [*] --> Aberto : POST /tickets
    Aberto --> EmAndamento : PUT /tickets/{id}
    Aberto --> Cancelado : PUT /tickets/{id}
    EmAndamento --> Resolvido : PUT /tickets/{id} (grava ResolvedAtUtc na 1ª vez)
    EmAndamento --> Cancelado : PUT /tickets/{id}
    Resolvido --> Aberto : POST /tickets/{id}/reopen
    Cancelado --> Aberto : POST /tickets/{id}/reopen
    Resolvido --> Fechado : POST /tickets/{id}/close (tickets:manage)
    Fechado --> Aberto : POST /tickets/{id}/reopen
```

- **[Fato]** "Ativo" significa Aberto ou Em andamento (`TicketStatusTransitions.cs:7`). `Update` e `Delete` recusam chamados inativos (`UpdateTicketCommandHandler.cs:42-45`, `DeleteTicketCommandHandler.cs:42-45`), e `Reopen` recusa chamados ativos (`ReopenTicketCommandHandler.cs:42-45`).
- **[Fato]** **[Resolvido]** O fechamento é feito por `POST tickets/{id}/close`, que exige `tickets:manage` e aceita só chamados Resolvidos (`Tickets/Close/CloseTicketCommandHandler.cs`). Antes da revisão de 22/10, nenhuma requisição levava a "Fechado".
- **[Fato]** A reabertura volta para Aberto e **não** limpa `ResolvedAtUtc`. O SLA fica congelado na primeira resolução (`UpdateTicketCommandHandler.cs:58-62`, `TicketSlaStatus.cs:3-6`).

### 2.6 Regras de negócio comprovadas

| # | Regra | Evidência (código) | Teste relacionado |
|---|---|---|---|
| RN-01 | O SLA de resolução é contado em **horas corridas** desde a abertura: Urgente 4h, Alta 8h, Média 24h, Baixa 72h. O alerta "em risco" dispara aos 75% da janela. | `Application/Tickets/TicketSla.cs:13-28` | `tests/.../Tickets/TicketSlaTests.cs` |
| RN-02 | Os prazos (`DueAtUtc` e `SlaWarningAtUtc`) são gravados na abertura e não mudam depois (a prioridade não é editável). | `CreateTicketCommandHandler.cs:82-83`, `TicketSla.cs:5-10` | `CreateTicketCommandHandlerTests.cs` |
| RN-03 | Situação do SLA: com resolução, `Met` se resolvido até o prazo e `Missed` se depois. Sem resolução: cancelado é `NotApplicable`; após o prazo, `Breached`; após o alerta, `AtRisk`; senão `OnTime`. | `TicketSla.cs:34-57`; mesma regra em SQL em `TicketSlaFilters.cs:20-36` | `TicketSlaTests.cs`, `GetTicketsQueryHandlerTests.cs` |
| RN-04 | Abrir um chamado em nome de outra pessoa exige `tickets:manage`. | `CreateTicketCommandHandler.cs:24-37` | `TicketsTests.cs:835` |
| RN-05 | Solicitante, responsável e setor informados precisam existir e estar ativos. | `CreateTicketCommandHandler.cs:41-67` | `TicketsTests.cs:88,860` |
| RN-06 | Editar, excluir ou reabrir exige ser o dono com a permissão `*-own` correspondente, ou ter `tickets:manage`. | `TicketAccessGuard.cs:53-72` e os três handlers | `TicketAccessGuardTests.cs`, `TicketsTests.cs:905,979,553` |
| RN-07 | Ver o chamado, as mensagens, os anexos e o histórico é permitido ao solicitante, ao responsável ou a quem tem `tickets:manage`. | `TicketAccessGuard.cs:18-46` | `TicketsTests.cs:350,371` |
| RN-08 | Só o autor edita a própria mensagem. O autor ou quem tem `tickets:manage` a exclui. | `EditTicketMessageCommandHandler.cs:31-34`, `DeleteTicketMessageCommandHandler.cs:33-43` | `EditTicketMessageCommandHandlerTests.cs`, `DeleteTicketMessageCommandHandlerTests.cs` |
| RN-09 | Anexo: até 10 MB, com tipos permitidos (imagens, PDF, Office, txt, zip) e extensão coerente com o tipo declarado. | `TicketAttachmentLimits.cs:8-58`, `UploadTicketAttachmentCommandValidator.cs:9-31` | `TicketAttachmentValidatorsTests.cs` |
| RN-10 | Um departamento ou setor não pode ser desativado com chamados ativos. Desativar o departamento desativa os setores dele. | `DeactivateDepartmentCommandHandler.cs:29-49`, `DeactivateSectorCommandHandler.cs:27-34` | `DeactivateDepartmentCommandHandlerTests.cs`, `DeactivateSectorCommandHandlerTests.cs` |
| RN-11 | O nome do departamento é único entre os ativos, sem diferenciar maiúsculas. O nome do setor é único dentro do departamento, entre os ativos. | Handlers mais índice filtrado `citext` (`DepartmentConfiguration.cs:15-18`, `SectorConfiguration.cs:15-18`) | `DepartmentsTests.cs`, `SectorsTests.cs` |
| RN-12 | O e-mail do usuário é único, sem diferenciar maiúsculas (`citext`). | `RegisterUserCommandHandler.cs:19-22`, `UserConfiguration.cs:14-18` | `RegisterUserCommandHandlerTests.cs` |
| RN-13 | Senha forte: pelo menos 6 caracteres, uma maiúscula e um caractere especial. Na troca, a nova senha precisa ser diferente da atual. | `UserValidationRules.cs:18-40` | `UserValidatorsTests.cs` |
| RN-14 | Cinco senhas erradas seguidas bloqueiam a conta por 15 minutos. Durante o bloqueio, a senha nem é conferida. | `UserCredentialsVerifier.cs:15-17,52-71` | `LoginUserCommandHandlerTests.cs` |
| RN-15 | A sessão dura no máximo 30 dias a partir do login. O refresh token roda a cada renovação. Reusar um token já trocado depois de 30 s revoga todas as sessões. Trocar a senha revoga todas as sessões. | `UserSessions.cs:16`, `RefreshTokenCommandHandler.cs:15,72-104`, `UserPasswords.cs:24-30` | `RefreshTokenCommandHandlerTests.cs`, `ChangeUserPasswordCommandHandlerTests.cs` |
| RN-16 | O administrador inicial é criado com senha provisória. O login devolve 403 até a troca em `users/login/new-password`. | `AdminUserSeeder.cs:21-60`, `LoginUserCommandHandler.cs:38-42` | `IntegrationTestWebAppFactory.cs:76-84`, `DatabaseSeedingTests.cs` |
| RN-17 | Ninguém concede nem remove uma permissão administrativa que não possui, e ninguém desativa ou reativa quem tem uma permissão administrativa que ele não tem. | `PrivilegeEscalationGuard.cs:17-62` | `AssignRolePermissionsCommandHandlerTests.cs`, `AssignUserRolesCommandHandlerTests.cs`, `DeactivateUserCommandHandlerTests.cs` |
| RN-18 | Deve haver sempre ao menos um administrador ativo, com proteção contra duas remoções concorrentes. | `AdministratorGuard.cs:24-58` | `AdministratorGuardTests.cs`, `RoleConcurrencyTests.cs` |
| RN-19 | Ninguém desativa a própria conta. Um usuário desativado perde o acesso na requisição seguinte; só `users/me` e o logout continuam aceitos. | `DeactivateUserCommandHandler.cs:21-24,60-73`, `ActiveUserRequirement.cs`, `EndpointExtensions.cs:45-52` | `DeactivateUserCommandHandlerTests.cs`, `UsersTests.cs` |
| RN-20 | O perfil (nome e sobrenome) pode ser editado pelo próprio usuário ou por quem tem `users:update`. | `UpdateUserProfileCommandHandler.cs:20-29` | `UpdateUserProfileCommandHandlerTests.cs` |
| RN-21 | Só quem tem `tickets:manage` fecha um chamado, e só se ele estiver Resolvido. O fechamento grava o histórico `StatusChanged` e não altera `ResolvedAtUtc`. | `Tickets/Close/CloseTicketCommandHandler.cs`, `Endpoints/Tickets/Close.cs` | `CloseTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Close_*) |
| RN-22 | Só quem tem `tickets:manage` atribui o responsável, e só em chamado ativo. O responsável precisa estar ativo e ter `tickets:manage`. Atribuir a quem já é o responsável não muda nada. | `Tickets/Assign/AssignTicketCommandHandler.cs`, `Endpoints/Tickets/Assign.cs` | `AssignTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Assign_*) |
| RN-23 | Só quem tem `tickets:manage` remove o responsável (deixa o chamado sem ninguém), e só em chamado ativo; a inatividade é conferida antes do responsável, então chamado inativo sem responsável também recebe `400 Tickets.NotActive`. Remover de chamado já sem responsável responde `204` sem gravar nada. O status não muda (um chamado Em andamento pode ficar sem responsável). A remoção grava o histórico `Unassigned` (responsável anterior em `oldValue`) e dispara `TicketUnassignedDomainEvent`; quem foi removido perde o acesso de participante. (Spec 001, 23/10/2026) | `Tickets/Unassign/UnassignTicketCommandHandler.cs`, `Endpoints/Tickets/Unassign.cs` | `UnassignTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Unassign_*) |

### 2.7 Matriz de capacidades

Legenda de estado: **IE** = Implementada e evidenciada · **PI** = Parcialmente implementada · **PNI** = Planejada, não implementada · **IND** = Indeterminada · **INV** = Implementação identificada, comportamento não validado.

| ID | Capacidade | Estado | Evidências | Dependências | Lacunas / questões |
|---|---|---|---|---|---|
| C-01 | Cadastro de usuário | IE | `Users/Register/*`, `Endpoints/Users/Register.cs`, `UsersTests.cs` (inclui 401 sem token e 403 sem `users:create`) | `users:create` | **[Resolvido]** Exige `users:create` (GAP-03). Não há auto-cadastro nem convite por e-mail. |
| C-02 | Login, refresh com rotação, logout | IE | `Users/Login`, `Users/Refresh`, `Users/Logout`, unitários e integração | JWT, PostgreSQL | O access token continua válido após logout ou troca de senha até expirar (GAP-14). |
| C-03 | Senha provisória e troca obrigatória | IE | `ChangeRequiredPassword/*`, `AdminUserSeeder.cs` | Seed | Só o seed do administrador marca `MustChangePassword`. Não há fluxo de "resetar a senha de alguém". |
| C-04 | Recuperação de senha esquecida | IND | Nenhum código | — | Não há fluxo nem documento que o planeje. |
| C-05 | Gestão de usuários (listar, ver, editar perfil, ativar/desativar, atribuir roles) | IE | `Users/*`, `UsersTests.cs` (74 métodos de teste) | Permissões | Não existe exclusão de usuário (o README cita "exclusão"). |
| C-06 | Gestão de permissões das roles | IE | `Roles/AssignPermissions`, `RolesTests.cs` | Cache de permissões | Não é possível criar ou excluir roles pela API. |
| C-07 | Departamentos e setores | IE | `Departments/*`, `Sectors/*`, testes | — | Não há reativação. O responsável pelo departamento pode ser um usuário inativo (`DepartmentResponsibleUser.cs:26`). |
| C-08 | Abertura de chamado | IE | `Tickets/Create/*`, `TicketsTests.cs` | Setor ativo | Na abertura, qualquer pessoa com `tickets:create` ainda pode indicar qualquer usuário ativo como responsável, sem a exigência de `tickets:manage` aplicada na atribuição posterior (Q-05). |
| C-09 | Edição de chamado (descrição e status) | IE | `Tickets/Update/*` | — | Título, prioridade e setor não são editáveis (Q-06). O responsável muda por C-10, e o fechamento por C-25. |
| C-10 | Atribuição ou reatribuição de responsável | IE | `Tickets/Assign/*`, `Endpoints/Tickets/Assign.cs`, `AssignTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Assign_*) | `tickets:manage` do autor e do responsável | **[Resolvido]** (GAP-02). **[Resolvido em 23/10, spec 001]** O responsável pode ser removido por `DELETE tickets/{id}/assignee` (`Tickets/Unassign/*`, RN-23). |
| C-11 | Reabertura | IE | `Tickets/Reopen/*`, testes | — | O SLA não é reiniciado (comportamento declarado). |
| C-12 | Exclusão lógica de chamado | IE | `Tickets/Delete/*`, testes | — | Só funciona para chamados ativos. |
| C-13 | Listagens e painéis (todos, meus, resumo, SLA, status, volume) | IE | `Tickets/Get*/*`, `TicketsTests.cs` | Dapper (volume) | O fuso fixo é `America/Sao_Paulo`. |
| C-14 | SLA de resolução (modelo simples) | IE | `TicketSla.cs`, `TicketSlaFilters.cs`, testes | — | Não há horário útil, pausa nem notificação. |
| C-15 | SLA completo (calendário, pausas, 1ª resposta, escalonamento, matriz, relatórios) | PNI | Plano em `specs/_fontes/prompt-sla.md` (cópia, em 23/10, do `prompt-sla-tickest.md`, não versionado); no backlog, BL-004 e BL-012 a BL-018 | Outbox, job agendado | A stack descrita no prompt diverge da real (ver 9.3). |
| C-16 | Mensagens do chamado | IE | `TicketMessages/*`, testes | — | Não há distinção entre mensagem pública e nota interna. Aceita mensagens em chamados encerrados (Q-07). |
| C-17 | Anexos | IE | `TicketAttachments/*`, `LocalFileStorage.cs`, testes | Disco local | O arquivo de um anexo excluído continua no disco. O armazenamento local não serve para várias instâncias (GAP-09). |
| C-18 | Histórico do chamado | IE | `TicketHistories/*`, testes | — | `Reassigned` passou a ser gravado com os ids do responsável anterior e do novo; a descrição exibida não traz os nomes. A remoção do responsável grava `Unassigned` ("Responsável removido", spec 001). **[Resolvido em 23/10, spec 002]** A alteração da descrição grava `DescriptionChanged` ("Descrição alterada"), com o texto anterior e o novo. |
| C-19 | Auditoria de entidades | IE | `AuditLoggingInterceptor.cs`, `AuditLogsTests.cs` | — | `TicketHistory`, `RefreshToken`, `Role` e `Permission` não implementam `IAuditable` (escolha não documentada). |
| C-20 | Eventos de domínio | INV | 28 tipos de evento declarados, 27 disparados em 31 pontos de `Raise(...)`; 1 handler vazio (`UserRegisteredDomainEventHandler.cs:8-12`) | — | Os eventos não têm efeito observável. `TicketHistoryRecordedDomainEvent` nunca é disparado (GAP-04). |
| C-21 | Notificações (e-mail, push) | IND | Só um comentário "ponto para enviar e-mail" (`UserRegisteredDomainEventHandler.cs:10`) | — | Não há mecanismo de envio. |
| C-22 | Cache de permissões (HybridCache + Redis opcional) | INV para Redis, IE para memória | `Infrastructure/DependencyInjection.cs:43-63`, `PermissionProvider.cs` | Redis (opcional) | Nenhum teste usa Redis. A invalidação entre instâncias não foi validada. |
| C-23 | Observabilidade (Serilog, Seq, OpenTelemetry, Correlation-Id, health check) | IE para Correlation-Id e health, INV para OTLP e Seq | `Program.cs`, `OpenTelemetryExtensions.cs`, `RequestContextLoggingTests.cs` | Seq, coletor OTLP | O OTLP só é ativado com `OTEL_EXPORTER_OTLP_ENDPOINT`, e não há teste. |
| C-24 | Rate limiting e CORS | IE | `RateLimitingExtensions.cs`, `CorsExtensions.cs`, `CorsTests.cs` | — | Não há `ForwardedHeaders`. Atrás de um proxy, a partição por IP pode juntar todos os clientes (GAP-11). |
| C-25 | Fechamento de chamado | IE | `Tickets/Close/*`, `Endpoints/Tickets/Close.cs`, testes unitários e de integração | `tickets:manage` | **[Resolvido]** (GAP-01). Não há fechamento automático por prazo. |

---

## 3. Arquitetura e organização técnica

### 3.1 Estilo arquitetural predominante

- **[Fato]** O estilo é **Clean Architecture em camadas concêntricas**, com slices verticais por caso de uso dentro da Application. O `TickestPristine.slnx` declara 5 projetos de produção e 3 de teste.
- **[Fato]** As referências de projeto são `Domain → SharedKernel`, `Application → Domain, SharedKernel`, `Infrastructure → Application` e `Web.Api → Infrastructure` (arquivos `.csproj`). Os testes NetArchTest garantem 10 regras de não dependência (`LayerTests.cs`).
- **[Fato]** O CQRS é **lógico, não físico**: commands e queries usam o mesmo `ApplicationDbContext` e o mesmo banco. Uma única consulta usa SQL cru via Dapper (`GetTicketVolumeQueryHandler.cs`).
- **[Fato]** O domínio é **anêmico** (propriedades públicas com `set`). Os eventos de domínio são disparados pelos handlers da Application (`Raise(...)`), e não pelas entidades.

### 3.2 Projetos e responsabilidades

| Projeto | Responsabilidade observada | Dependências externas relevantes |
|---|---|---|
| `TickestPristine.SharedKernel` | `Entity` (lista de eventos), `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider`, `IAuditable` | Nenhuma |
| `TickestPristine.Domain` | Entidades, enums, eventos e catálogos de erros por agregado (`Audit`, `Departments`, `Roles`, `Sectors`, `Tickets`, `Users`) | Nenhuma |
| `TickestPristine.Application` | Slices de commands e queries, validators, decorators, guards de autorização, regras de chamados e SLA, catálogo de permissões, paginação, abstrações (`IApplicationDbContext`, `IDbConnectionFactory`, `IFileStorage`, `ITokenProvider`, `IPasswordHasher`, `IUserContext`, `IPermissionProvider`) | EF Core (abstração `DbSet`), Dapper, FluentValidation, Scrutor, Ulid |
| `TickestPristine.Infrastructure` | `ApplicationDbContext`, configurações EF, migrations, seed, JWT, hash de senha, autorização por permissão, HybridCache, interceptor de auditoria, despacho de eventos, armazenamento de arquivos em disco, health check | Npgsql/EF, JwtBearer, HybridCache, StackExchangeRedis, Bogus (seed) |
| `TickestPristine.Web.Api` | Host, 49 endpoints de minimal API, tratamento global de exceções, `CustomResults`, CORS, rate limiting, OpenTelemetry, Serilog, Swagger, middleware de Correlation-Id | Serilog, OpenTelemetry, Swashbuckle |

### 3.3 Diagrama de contexto do sistema

```mermaid
flowchart LR
    U["Usuários (navegador)<br/>Colaborador · Atendente · Administrador"]
    FE["Front-end<br/>(fora deste repositório)"]
    API["TickestPristine.Web.Api<br/>(.NET 10, minimal API)"]
    PG[("PostgreSQL 17")]
    FS[("Disco local<br/>FileStorage:RootPath")]
    SEQ["Seq<br/>(logs, opcional)"]
    OTLP["Coletor OTLP<br/>(opcional, por variável de ambiente)"]
    REDIS[("Redis<br/>(opcional, ConnectionStrings:Cache)")]

    U --> FE -->|"HTTPS/JSON + JWT Bearer"| API
    API --> PG
    API --> FS
    API -.-> SEQ
    API -.-> OTLP
    API -.-> REDIS
```

As linhas tracejadas são dependências **opcionais**, ativadas só por configuração (`Infrastructure/DependencyInjection.cs:47-52`, `OpenTelemetryExtensions.cs:28-31`, `appsettings.Development.json:16-22`). Não foram encontradas integrações com sistemas externos de negócio (e-mail, ERP, SSO, filas).

### 3.4 Componentes e dependências

```mermaid
flowchart TB
    subgraph WebApi["Web.Api"]
        EP["Endpoints (IEndpoint)<br/>49 classes"]
        MW["Middleware<br/>Correlation-Id · Serilog request log · ExceptionHandler · CORS · AuthN/AuthZ · RateLimiter"]
        CR["CustomResults.Problem<br/>Result → ProblemDetails"]
        GEH["GlobalExceptionHandler"]
    end
    subgraph App["Application"]
        DEC["Decorators<br/>Validation → Logging"]
        H["Handlers de command/query"]
        G["Guards<br/>TicketAccessGuard · PrivilegeEscalationGuard · AdministratorGuard"]
        R["Regras estáticas<br/>TicketStatusTransitions · TicketSla · TicketSlaFilters"]
        ABS["Abstrações<br/>IApplicationDbContext · IDbConnectionFactory · IFileStorage · ITokenProvider · IPermissionProvider · IUserContext"]
    end
    subgraph Infra["Infrastructure"]
        DB["ApplicationDbContext (EF Core)"]
        AUD["AuditLoggingInterceptor"]
        DISP["DomainEventsDispatcher"]
        PP["PermissionProvider + HybridCache"]
        AUTH["TokenProvider · PasswordHasher · UserContext · JWT"]
        AZ["Policy provider + handlers<br/>(Permission, ActiveUser)"]
        ST["LocalFileStorage"]
        SEED["DatabaseSeeder<br/>(Admin · Organização · Dados de exemplo)"]
    end
    Dom["Domain (entidades, eventos, erros)"]
    SK["SharedKernel"]

    EP --> DEC --> H
    H --> G & R & ABS
    MW --> AZ --> PP
    ABS -.implementado por.-> DB & ST & AUTH & PP
    DB --> AUD
    DB --> DISP
    DB --> SEED
    H --> Dom --> SK
    EP --> CR
    MW --> GEH
```

### 3.5 Camadas

```mermaid
flowchart LR
    SK["SharedKernel"] 
    D["Domain"] --> SK
    A["Application"] --> D
    A --> SK
    I["Infrastructure"] --> A
    W["Web.Api"] --> I
    W -.usa tipos de.-> A
```

- **[Fato]** A Web.Api depende transitivamente da Application. Os endpoints usam `ICommandHandler<,>`/`IQueryHandler<,>` e os commands diretamente.
- **[Fato]** A Infrastructure usa tipos da Application que não são interfaces, como `PermissionCatalog`, `RoleNames`, `UserCodes`, `TicketSla`, `TicketHistoryEntries` e `TicketStatusTransitions` (ex.: `RolePermissionConfiguration.cs:1-2`, `SampleDataSeeder.cs:174-200`). A direção da dependência é permitida.

### 3.6 Fluxo de execução de um caso de uso representativo (abrir chamado)

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente
    participant MW as Pipeline ASP.NET
    participant AZ as PermissionAuthorizationHandler
    participant PP as PermissionProvider (HybridCache)
    participant EP as Endpoints/Tickets/Create
    participant V as ValidationDecorator
    participant L as LoggingDecorator
    participant H as CreateTicketCommandHandler
    participant G as TicketAccessGuard
    participant DB as ApplicationDbContext
    participant AI as AuditLoggingInterceptor
    participant DD as DomainEventsDispatcher

    C->>MW: POST /tickets (Bearer JWT)
    MW->>MW: Correlation-Id, autenticação JWT
    MW->>AZ: policy "tickets:create"
    AZ->>PP: GetForUserIdAsync(userId)
    PP-->>AZ: permissões (cache ou banco, só se o usuário estiver ativo)
    MW->>MW: RateLimiter (janela fixa global)
    MW->>EP: Request → CreateTicketCommand
    EP->>V: Handle
    V->>V: CreateTicketCommandValidator
    alt inválido
        V-->>EP: Result.Failure(ValidationError) → 400
    end
    V->>L: Handle
    L->>H: Handle
    H->>G: EnsureOwnerOrManager (abertura em nome de outra pessoa)
    H->>DB: confere usuários e setor ativos
    H->>H: monta Ticket (prazos do SLA) + Raise(TicketCreatedDomainEvent)
    H->>DB: Add Ticket + TicketHistory(Created)
    H->>DB: SaveChangesAsync
    DB->>AI: SavingChanges → AuditLogEntry para Ticket (IAuditable)
    DB->>DB: COMMIT
    DB->>DD: publica eventos (fora da transação; nenhum handler para TicketCreated)
    H-->>EP: Result<Guid>
    EP-->>C: 200 { id } ou ProblemDetails
```

Evidência: `Endpoints/Tickets/Create.cs:68-90`, `Application/DependencyInjection.cs:28-34`, `CreateTicketCommandHandler.cs:22-94`, `ApplicationDbContext.cs:59-72`, `AuditLoggingInterceptor.cs:25-58`.

### 3.7 Fluxos assíncronos

- **[Fato]** **Não há fila, broker, job agendado nem worker** no repositório. Não foram encontrados `IHostedService`/`BackgroundService`, Quartz, Hangfire nem MassTransit.
- **[Fato]** O único mecanismo "assíncrono" é o **despacho de eventos de domínio em memória, depois do commit, na mesma requisição**:

```mermaid
sequenceDiagram
    participant H as Handler
    participant DB as ApplicationDbContext.SaveChangesAsync
    participant PG as PostgreSQL
    participant D as DomainEventsDispatcher
    participant EH as Handlers do evento (escopo DI novo)

    H->>DB: SaveChangesAsync
    DB->>DB: coleta as entidades com eventos (antes do save)
    DB->>PG: INSERT/UPDATE + auditoria (uma transação)
    PG-->>DB: OK
    DB->>DB: lê e limpa os eventos
    DB->>D: DispatchAsync(eventos, CancellationToken.None)
    loop cada evento, cada handler
        D->>EH: Handle (escopo DI novo)
        alt exceção
            D->>D: LogError e segue (a requisição não falha)
        end
    end
```

Fonte: `ApplicationDbContext.cs:57-108`, `DomainEventsDispatcher.cs:19-59`. **[Fato]** Se o processo cair entre o commit e o despacho, os eventos se perdem. Não há persistência de eventos.

### 3.8 Persistência e estado

- **[Fato]** O banco é um **PostgreSQL** único, schema `public`, com nomes em snake_case (`Infrastructure/DependencyInjection.cs:101-110`) e a extensão `citext` (`ApplicationDbContext.cs:50`). Um único `NpgsqlDataSource` é compartilhado pelo EF e pelo Dapper (`DependencyInjection.cs:96-98`).
- **[Fato]** Há **18 migrations** (de `20261002014106_Initial` a `20261022023422_Store_Ticket_Enums_As_Names`). As 2 últimas estavam fora do Git no momento da análise e foram registradas no commit `b2028ae`.
- **[Fato]** As migrations são aplicadas **automaticamente só em Development** (`Program.cs:34-39`). O seed roda acoplado ao `Migrate` (`UseSeeding`/`UseAsyncSeeding`, `DependencyInjection.cs:107-110`).
- **[Fato]** O estado em memória se limita ao cache de permissões e de "usuário ativo" (HybridCache: expira em 5 min, cópia local por 1 min), com invalidação explícita (`DependencyInjection.cs:56-60`, `PermissionProvider.cs:44-48`).
- **[Fato]** Os arquivos de anexo ficam em disco local, com chave `Guid + extensão` (`LocalFileStorage.cs:16-45`).

### 3.9 Decisões arquiteturais identificáveis (com evidência)

| Decisão | Evidência | Observação |
|---|---|---|
| Sem MediatR; handlers próprios com decorators via Scrutor | `Application/DependencyInjection.cs:17-34`, `CLAUDE.md` | Decisão explícita no `CLAUDE.md` e confirmada pelo responsável em 22/10/2026: por enquanto o MediatR não será adotado. |
| Erros esperados como `Result`, não exceções | `SharedKernel/Result.cs`, `CustomResults.cs` | Aplicada de forma consistente. As exceções ficam para concorrência, unicidade e requisição malformada (`GlobalExceptionHandler.cs:19-81`). |
| Permissões resolvidas no servidor a cada requisição, não carregadas no JWT | `TokenProvider.cs:22-29` | Permite revogação imediata, ao custo de consulta ou cache por requisição. |
| Concorrência otimista (`xmin` em Ticket, `Version` na role Administrador, `TokenHash` em RefreshToken) | Configurações EF citadas em 2.4; `TicketConcurrencyTests.cs` | Os conflitos viram 409 no `GlobalExceptionHandler`. |
| Prazos de SLA materializados na abertura | `TicketSla.cs:5-10` | Mudar a política não afeta chamados existentes. |
| Texto do histórico derivado na leitura (não gravado) | `TicketHistoryDescriptions.cs:5-8`, migration `20261022022728` | Alteração registrada no commit `b2028ae`. |
| Paginação por offset (listagens) e por cursor composto (data + id) para os subrecursos do chamado e a auditoria | `PaginationExtensions.cs` | Tamanho máximo de página: 100. |

### 3.10 Inconsistências e desvios observados

1. **[Fato]** **SQL específico do PostgreSQL dentro da Application.** `GetTicketVolumeQueryHandler.cs:19-49` referencia `public.tickets`, colunas físicas e funções do PostgreSQL. O `IDbConnectionFactory.cs:5-8` declara essa possibilidade como intencional ("quando o SQL expressa a consulta melhor que o LINQ"), mas ela acopla a Application ao esquema físico.
2. **[Fato]** **`IApplicationDbContext` expõe `DbSet<T>` do EF Core** (`Abstractions/Data/IApplicationDbContext.cs`). A Application depende do EF Core como abstração, o que é coerente com o template pragmático, mas significa que ela não é agnóstica de ORM.
3. **[Fato]** **Eventos de domínio sem consumidores.** São 31 pontos de `Raise(...)` (27 tipos de evento) e 1 handler, que é vazio. `TicketHistoryRecordedDomainEvent` é declarado e nunca disparado.
4. **[Fato]** **Comentário desatualizado no seed.** `SampleDataSeeder.cs:206` diz que o histórico é "igual ao que os handlers de evento de domínio gravam", mas o histórico é gravado pelos handlers de command (ex.: `CreateTicketCommandHandler.cs:89`).
5. **[Fato]** **Inconsistência de leitura entre departamento e setor.** `GetDepartmentById` devolve departamento inativo (`GetDepartmentByIdQueryHandler.cs:14-24`), e `GetSectorById` devolve 404 para setor inativo (`GetSectorByIdQueryHandler.cs:15`).
6. **[Fato]** **Uso de cache restrito.** As skills do repositório orientam usar `HybridCache` nas leituras frequentes (`.claude/skills/add-feature/SKILL.md:29`), mas a Application não usa cache em nenhuma consulta. O único uso está nas permissões, na Infrastructure.

---

## 4. Especificação técnica

### 4.1 Linguagens, frameworks e versões (identificadas nos arquivos)

| Item | Versão | Fonte |
|---|---|---|
| .NET / C# | `net10.0`, `ImplicitUsings`, `Nullable` | `Directory.Build.props:12-14` |
| SDK usado nesta análise | 10.0.401 | `dotnet --version` |
| EF Core / Npgsql EF | 10.0.9 / 10.0.2 | `Directory.Packages.props` |
| PostgreSQL (compose e Testcontainers) | imagem `postgres:17` | `docker-compose.yml`, `IntegrationTestWebAppFactory.cs:22` |
| FluentValidation | 12.1.1 | `Directory.Packages.props` |
| Scrutor | 7.0.0 | idem |
| Dapper | 2.1.89 | idem |
| HybridCache / StackExchangeRedis | 10.7.0 / 10.0.11 | idem |
| JwtBearer | 10.0.9 | idem |
| Serilog.AspNetCore / Sinks.Seq | 10.0.0 / 9.1.0 | idem |
| OpenTelemetry | 1.16.0 (Runtime 1.15.1) | idem |
| Swashbuckle | 10.2.3 | idem |
| Testes | xUnit 2.9.3, NSubstitute 5.3.0, Shouldly 4.3.0, Testcontainers.PostgreSql 4.15.0, NetArchTest 1.3.2, EF InMemory 10.0.9 | idem |
| Análise estática | SonarAnalyzer.CSharp 10.27; `AnalysisMode=All`; avisos tratados como erros | `Directory.Build.props:9-25` |
| Seq (compose) | `datalust/seq:2024.3` | `docker-compose.yml` |

### 4.2 Organização do código e convenções observadas

- **[Fato]** Cada slice segue o trio `{UseCase}Command|Query.cs` + `...Handler.cs` (`internal sealed`, construtor primário) + `...Validator.cs`. Há 26 validators, todos para commands. **Queries não passam por validação**: o `ValidationDecorator` só decora commands (`Application/DependencyInjection.cs:33-34`).
- **[Fato]** Cada endpoint é uma classe `internal sealed : IEndpoint`, registrada por varredura do assembly (`EndpointExtensions.cs:10-22`), com tag de `Tags.cs` e uma classe `Permissions.cs` por pasta, que aponta para `PermissionCodes`.
- **[Fato]** Cada agregado tem a sua classe estática `{Entity}Errors`, com códigos `Agregado.Motivo` e mensagens em pt-BR.
- **[Fato]** Os nomes de propriedades aparecem em pt-BR nas mensagens de validação por meio de `ValidationDisplayNames.cs`.

### 4.3 Contratos e mecanismos de comunicação

- **[Fato]** A comunicação é **HTTP/JSON síncrona** (minimal APIs). O upload usa `multipart/form-data`, com limite de 11 MB por requisição (`Endpoints/TicketAttachments/Upload.cs:15,43-44`). O download devolve o arquivo em stream (`Download.cs:27`).
- **[Fato]** Os erros saem como **ProblemDetails** (RFC 7807). `ValidationError` inclui `errors[]` (`CustomResults.cs:45-56`), e erros do tipo `Failure` têm título e detalhe mascarados (`CustomResults.cs:14-19`).
- **Mapeamento `ErrorType` → HTTP** (`CustomResults.cs:35-43`): Validation/Problem → 400, NotFound → 404, Conflict → 409, Forbidden → 403, Failure → 500.
- **Exceções → HTTP** (`GlobalExceptionHandler.cs`): `DbUpdateConcurrencyException` → 409, violação de unicidade (`23505`) → 409, `BadHttpRequestException` → status dela (400/413), `UserContextUnavailableException` → 401, demais → 500.
- **[Fato]** O OpenAPI é gerado pelo Swashbuckle, exposto **só em Development** (`Program.cs:34-37`), com esquema Bearer aplicado apenas às rotas protegidas (`AuthorizeOperationFilter.cs`). Não há versionamento de API.

#### 4.3.1 Inventário de rotas

| Método e rota | Autorização | Rate limit dedicado |
|---|---|---|
| `POST users/register` | `users:create` (desde 22/10; antes era anônima) | — |
| `POST users/login` | Anônima | `authentication` |
| `POST users/login/new-password` | Anônima | `authentication` |
| `POST users/refresh-token` | Anônima | `authentication` |
| `POST users/logout` | Token válido (aceita usuário desativado) | — |
| `GET users/me` | Token válido (aceita usuário desativado) | — |
| `PUT users/me/password` | Usuário ativo | `authentication` |
| `PUT users/{id}/profile` | Usuário ativo, e no handler: o próprio usuário ou `users:update` | — |
| `GET users/{id}` | Usuário ativo, e no handler: o próprio usuário ou `users:read` | — |
| `GET users`, `GET users/by-email` | `users:read` | — |
| `PUT users/{id}/activate`, `PUT users/{id}/deactivate` | `users:deactivate` | — |
| `PUT users/{id}/roles` | `users:assign-roles` | — |
| `GET roles` | `roles:read` | — |
| `PUT roles/{roleName}/permissions` | `roles:manage` | — |
| `GET permissions` | Usuário ativo | — |
| `GET departments`, `GET departments/{id}` | Usuário ativo | — |
| `POST/PUT/DELETE departments...` | `departments:manage` | — |
| `GET sectors`, `GET sectors/{id}` | Usuário ativo | — |
| `POST/PUT/DELETE sectors...` | `sectors:manage` | — |
| `POST tickets` | `tickets:create` | — |
| `GET tickets` | `tickets:manage` | — |
| `GET tickets/mine`, `GET tickets/mine/summary` | `tickets:view-own` | — |
| `GET tickets/sla-summary`, `GET tickets/stats/status`, `GET tickets/stats/volume` | `tickets:manage` | — |
| `GET tickets/{id}` | Usuário ativo, e no handler: participante ou `tickets:manage` | — |
| `PUT tickets/{id}`, `DELETE tickets/{id}`, `POST tickets/{id}/reopen` | Usuário ativo, e no handler: dono com `*-own` ou `tickets:manage` | — |
| `POST tickets/{id}/close` | `tickets:manage` | — |
| `PUT tickets/{id}/assignee`, `DELETE tickets/{id}/assignee` | `tickets:manage` | — |
| `GET/POST tickets/{id}/messages` | Usuário ativo, e no handler: participante ou `tickets:manage` | — |
| `PUT/DELETE ticket-messages/{id}` | Usuário ativo, e no handler: autor (editar) ou autor/`tickets:manage` (excluir) | — |
| `GET/POST tickets/{id}/attachments` | Usuário ativo, e no handler: participante ou `tickets:manage` | — |
| `GET ticket-attachments/{id}/download` | idem | — |
| `DELETE ticket-attachments/{id}` | Usuário ativo, e no handler: quem enviou ou `tickets:manage` | — |
| `GET tickets/{id}/history` | Usuário ativo, e no handler: participante ou `tickets:manage` | — |
| `GET audit-logs/{entityName}/{entityId}` | `audit:read` | — |
| `GET health` | Anônima | Global |

Fonte: `src/TickestPristine.Web.Api/Endpoints/**`. O limite global (100 req/60 s por usuário ou IP) vale para todas as rotas (`RateLimitingExtensions.cs:29-32`, `appsettings.json:36-45`).

### 4.4 Entidades e DTOs relevantes

- **Entidades**: `User`, `UserCredential`, `RefreshToken`, `UserRole`, `Role`, `Permission`, `RolePermission`, `Department`, `Sector`, `Ticket`, `TicketMessage`, `TicketAttachment`, `TicketHistory`, `AuditLogEntry` (`src/TickestPristine.Domain/**`).
- **Objetos de valor**: **[Fato]** nenhum foi identificado. Valores como e-mail, prazos e nomes são tipos primitivos.
- **Enums de negócio**: `TicketStatus` (1–5), `TicketPriority` (1–4), `TicketSlaStatus` (1–6), `TicketHistoryAction` (1–12; `Unassigned = 11` desde a spec 001 e `DescriptionChanged = 12` desde a spec 002).
- **DTOs**: um `*Response` por slice (ex.: `TicketSummaryResponse`, `TicketResponse`, `CurrentUserResponse`), mais `PagedResponse<T>` e `CursorPagedResponse<T>`.

### 4.5 Validação, erros e resultados

- **[Fato]** A validação de entrada fica no FluentValidation dos commands. O decorator executa todos os validators e devolve um `ValidationError` agregado (`ValidationDecorator.cs:48-71`).
- **[Fato]** As regras que dependem do banco (existência, unicidade, estado) ficam nos handlers e retornam `Result.Failure`.
- **[Fato]** O `LoggingDecorator` registra o início, o fim e as falhas de cada command ou query, com o `Error` no escopo (`LoggingDecorator.cs`). Ele registra **erros esperados (404/409) em nível `Error`**, o que pode poluir os alertas. **[Inferência]**

### 4.6 Autenticação, autorização e fronteiras de segurança

- **JWT** HMAC-SHA256, com segredo de pelo menos 32 caracteres validado na inicialização (`JwtOptions.cs:17-19`, `DependencyInjection.cs:130-133`). As claims são só `sub`, `email` e `name` (`TokenProvider.cs:24-29`). `ClockSkew = 0` (`ConfigureJwtBearerOptions.cs:32`). Expiração configurável de 1 a 1440 minutos (60 em Development).
- **Senhas**: PBKDF2-SHA512 com 500.000 iterações, salt de 16 bytes e comparação em tempo constante (`PasswordHasher.cs:8-31`). O login com e-mail inexistente gasta tempo equivalente ao de um hash (`UserCredentialsVerifier.cs:36-42`).
- **Refresh token**: 32 bytes aleatórios. Só o hash SHA-256 é gravado (`RefreshTokenHasher.cs`, `RefreshTokenStorageTests.cs`). Ele também é ligado ao `SecurityStamp` da credencial (`RefreshTokenCommandHandler.cs:51-61`).
- **Autorização**:
  - A policy padrão exige usuário autenticado e ativo (`DependencyInjection.cs:150-153`, `ActiveUserAuthorizationHandler.cs`).
  - `HasPermission(code)` cria dinamicamente uma policy com `PermissionRequirement` (`PermissionAuthorizationPolicyProvider.cs:14-18`). Essa policy **não** inclui `ActiveUserRequirement`, mas a consulta de permissões já filtra usuários inativos (`PermissionProvider.cs:18`).
  - As regras por recurso (participante, dono) ficam nos guards da Application.
- **Auditoria**: grava toda inclusão, alteração ou exclusão de entidades `IAuditable` na mesma transação, excluindo `PasswordHash` e `SecurityStamp` dos valores (`AuditLoggingInterceptor.cs:19-23`, `AuditLogsTests.cs:128`).
- **Arquivos**: a chave de armazenamento é gerada pelo servidor, a extensão é sanitizada e há proteção contra path traversal (`LocalFileStorage.cs:20,71-91`). **Não há** verificação do conteúdo real do arquivo (assinatura ou "magic bytes") nem antivírus. O tipo e o nome vêm do cliente.

### 4.7 Transações, concorrência e consistência

- **[Fato]** Cada caso de uso faz **um `SaveChangesAsync`**, ou seja, uma transação implícita. A exceção é a contagem de senhas erradas, gravada antes (`UserCredentialsVerifier.cs:67,80`).
- **[Fato]** O upload grava o arquivo **antes** da transação e o apaga se o `SaveChanges` falhar (`UploadTicketAttachmentCommandHandler.cs:35,66-75`). Se o processo cair entre os dois passos, o arquivo fica órfão. **[Inferência]**
- **[Fato]** A invalidação de cache de permissões acontece **depois** do commit e fora do `CancellationToken` da requisição (ex.: `AssignUserRolesCommandHandler.cs:91-94`).
- **[Fato]** A unicidade é garantida por índices únicos (e-mail, código, nome de departamento/setor entre ativos, `TokenHash`, pares role/permissão e usuário/role). Uma colisão concorrente vira 409 (`GlobalExceptionHandler.cs:32-44`).

### 4.8 Configuração necessária para execução (sem valores sensíveis)

| Chave | Obrigatória | Onde é validada | Observação |
|---|---|---|---|
| `ConnectionStrings:Database` | Sim | `Infrastructure/DependencyInjection.cs:31-32` (exceção na inicialização) | Em Development aponta para `localhost:5440`. |
| `ConnectionStrings:Cache` | Não | — | Ativa o Redis como segundo nível do HybridCache. |
| `Jwt:Secret` (≥ 32 caracteres), `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationInMinutes` (1–1440), `Jwt:RefreshTokenExpirationInDays` (1–365) | Sim | `ValidateOnStart` | O segredo vem de user-secrets (`UserSecretsId` em `TickestPristine.Web.Api.csproj:4`). |
| `Admin:Email`, `Admin:Password` (+ `FirstName`, `LastName`) | Só quando não há administrador ativo | `ValidateDataAnnotations` (sem `ValidateOnStart`) | O seed falha se o e-mail já existir sem ser administrador (`AdminUserSeeder.cs:31-36`). |
| `Seeding:DefaultOrganization` (padrão `true`), `Seeding:SampleData`, `Seeding:SampleUserPassword` | Condicional | `SampleDataSeeder.cs:102-106` | A senha de exemplo é obrigatória com `SampleData=true`. |
| `FileStorage:RootPath` | Não (padrão `App_Data/attachments`) | `ValidateOnStart` | — |
| `Cors:AllowedOrigins[]`, `Cors:AllowLoopbackOrigins` | Não | — | Sem origens configuradas, nenhuma origem externa é aceita. |
| `RateLimiting:Global|Authentication:{PermitLimit,WindowInSeconds}` | Sim (têm padrão no appsettings) | `RateLimitingOptionsValidator` + `ValidateOnStart` | — |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Não | — | Ativa a exportação OTLP. |
| `Serilog:*` | Não | — | Em Development, envia para o Seq em `http://seq:5341`. |

---

## 5. Fluxos funcionais de ponta a ponta

### 5.1 Primeiro acesso do administrador

1. **Objetivo**: criar o primeiro administrador e obrigar a troca da senha provisória.
2. **Gatilho**: `Migrate()` (automático em Development, `Program.cs:38`), seguido de `POST users/login/new-password`.
3. **Componentes**: `DatabaseSeeder` → `AdminUserSeeder` → (cliente) `ChangeRequiredPassword` endpoint → `ChangeRequiredUserPasswordCommandHandler` → `UserCredentialsVerifier` → `UserPasswords.ReplaceAsync` → `UserSessions.StartAsync`.
4. **Regras**: o seed só roda sem administrador ativo, recusa e-mail já existente e marca `MustChangePassword=true` (`AdminUserSeeder.cs:23-56`). A troca exige credenciais válidas, conta ativa e pendência de troca (`ChangeRequiredUserPasswordCommandHandler.cs:21-44`).
5. **Domínio**: `UserPasswordChangedDomainEvent` e `RefreshTokenCreatedDomainEvent` (sem handlers).
6. **Persistência**: `users`, `user_credentials`, `user_roles`, `refresh_tokens`, `audit_log_entries`.
7. **Resposta**: `200 { accessToken, refreshToken }`.
8. **Erros**: 400 (credenciais inválidas), 403 (bloqueado ou desativado), 409 (sem troca pendente).
9. **Evidências**: `IntegrationTestWebAppFactory.cs:61-84` exercita o fluxo completo em toda execução da suíte de integração. Também `DatabaseSeedingTests.cs`.
10. **Lacunas**: fora de Development, as migrations e o seed precisam ser disparados por fora (ex.: `dotnet ef database update`). O procedimento não está documentado.

### 5.2 Login e renovação de sessão

1. **Objetivo**: obter tokens e renová-los com segurança.
2. **Gatilho**: `POST users/login`, depois `POST users/refresh-token`.
3. **Componentes**: endpoint → Validation → Logging → `LoginUserCommandHandler` (`UserCredentialsVerifier`, `UserSessions`), e depois `RefreshTokenCommandHandler`.
4. **Regras**: RN-14 (bloqueio) e RN-15 (rotação, limite de 30 dias, reuso, `SecurityStamp`). A situação da conta é checada só depois da senha (`LoginUserCommandHandler.cs:32-36`).
5. **Domínio**: eventos `RefreshToken*` (sem handlers).
6. **Persistência**: `refresh_tokens` (só o hash). Os tokens expirados do usuário são removidos a cada login (`UserSessions.cs:31-35`).
7. **Resposta**: `200 { accessToken, refreshToken }`.
8. **Erros**: 400 (`InvalidCredentials`, `InvalidRefreshToken`), 403 (`LockedOut`, `Deactivated`, `PasswordChangeRequired`), 409 (renovação concorrente, via `TokenHash` como token de concorrência), 429 (rate limit `authentication`, 10 req/60 s).
9. **Evidências**: `LoginUserCommandHandlerTests.cs` (10), `RefreshTokenCommandHandlerTests.cs` (8), `UsersTests.cs`, `RefreshTokenStorageTests.cs`.
10. **Lacunas**: o access token emitido não é revogável antes de expirar. O bloqueio por usuário desativado depende do cache (invalidado na desativação).

### 5.3 Abrir chamado

Descrito no diagrama 3.6.

- **Regras**: RN-01, RN-02, RN-04, RN-05.
- **Persistência**: `tickets`, `ticket_histories` (`Created`), `audit_log_entries`.
- **Resposta**: `200` com o `Guid`.
- **Erros**: 400 (validação), 403 (abertura em nome de outra pessoa sem `tickets:manage`), 404 (usuário ou setor inexistente ou inativo).
- **Evidências**: `CreateTicketCommandHandlerTests.cs` (10), `TicketsTests.cs:68-135,835-900`.
- **Lacunas**: nenhuma regra limita quem pode ser indicado como responsável (Q-05). Não há notificação ao responsável.

### 5.4 Tratar chamado (mudança de status e descrição)

1. **Objetivo**: avançar o atendimento.
2. **Gatilho**: `PUT tickets/{id}` com `{ description, status }`.
3. **Componentes**: endpoint → Validation → Logging → `UpdateTicketCommandHandler` → `TicketAccessGuard.EnsureOwnerOrManagerAsync` → `TicketStatusTransitions`.
4. **Regras**: o chamado precisa estar ativo e a transição precisa ser válida. Na primeira ida a `Resolved`, grava `ResolvedAtUtc` (`UpdateTicketCommandHandler.cs:42-62`). **[Fato]** O dono com `tickets:update-own` passa pelo guard e pode mudar o status do próprio chamado, inclusive para Em andamento, Resolvido ou Cancelado, já que o handler não diferencia quem muda o status.
5. **Domínio**: `TicketUpdatedDomainEvent` e, se o status mudar, `TicketStatusChangedDomainEvent`.
6. **Persistência**: `tickets` (com `xmin`), `ticket_histories` (`StatusChanged` com nomes dos enums, quando o status muda; `DescriptionChanged` com o texto anterior e o novo, quando a descrição muda pela comparação exata, ordinal; os dois quando ambos mudam; nenhum quando nada muda; spec 002), auditoria.
7. **Resposta**: `204`.
8. **Erros**: 400 (inativo, transição inválida), 403, 404, 409 (concorrência, `TicketConcurrencyTests.cs`).
9. **Evidências**: `UpdateTicketCommandHandlerTests.cs` (15), `TicketsTests.cs` (Update_*).
10. **Lacunas**: o fechamento e a troca de responsável ficam em rotas próprias (5.10). A descrição é obrigatória mesmo quando só o status muda (`UpdateTicketCommandValidator.cs:10-12`).

### 5.5 Reabrir chamado

- **Gatilho**: `POST tickets/{id}/reopen`. **Regras**: o chamado precisa estar inativo e a transição para Aberto precisa ser válida. Exige o dono com `tickets:reopen-own` ou `tickets:manage` (`ReopenTicketCommandHandler.cs:30-50`).
- **Efeitos**: status Aberto, histórico `Reopened` com os status antigo e novo, evento `TicketReopenedDomainEvent`. O SLA não é recalculado.
- **Erros**: 400, 403, 404, 409 (`AlreadyActive`).
- **Evidências**: `ReopenTicketCommandHandlerTests.cs` (8), `TicketsTests.cs:213,525,553,1023`.

### 5.6 Conversa e anexos do chamado

- **Mensagens**: `POST tickets/{id}/messages` (participante ou `tickets:manage`), com limite de 4000 caracteres. Editar só o autor. Excluir: autor ou `tickets:manage` (exclusão lógica). Cada ação grava histórico. Evidências: `TicketMessages/*`, `TicketMessagesTests.cs` (15).
- **Anexos**: `POST tickets/{id}/attachments` (`multipart`) segue a sequência validação → guard de visualização → grava o arquivo no disco → grava a linha e o histórico → em caso de falha, apaga o arquivo. O download repete o guard e abre o stream do disco. A exclusão é lógica: **o arquivo físico não é removido** (`DeleteTicketAttachmentCommandHandler.cs:45-57`). Evidências: `TicketAttachmentsTests.cs` (15), `UploadTicketAttachmentCommandHandlerTests.cs`.
- **Lacunas**: não há restrição pelo status do chamado (é possível conversar e anexar em chamado cancelado ou fechado). Não há limite de quantidade ou de volume total por chamado. Não há tratamento para arquivo ausente no disco no download: `OpenReadAsync` lança `FileNotFoundException`, que vira 500. **[Inferência, não testado]**

### 5.7 Painel de SLA e dashboards

- **Gatilhos**: `GET tickets?slaStatus=...&sort=DueDate`, `GET tickets/sla-summary?sectorId=`, `GET tickets/stats/status`, `GET tickets/stats/volume?groupBy=`, `GET tickets/mine/summary`.
- **Regras**: RN-03, com a mesma lógica em memória (`TicketSla.GetStatus`) e em SQL (`TicketSlaFilters`). O resumo conta só chamados ativos com o relógio correndo (`TicketSlaSummaryResponse.cs:3-7`). O volume usa o horário de Brasília e preenche os períodos sem chamados com zero.
- **Evidências**: `GetTicketsQueryHandlerTests.cs` (11), `GetTicketSlaSummaryQueryHandlerTests.cs`, `TicketsTests.cs:283-350,469-505,633-833` (inclui casos de fronteira de fuso).
- **Lacunas**: o SLA é só de resolução, em horas corridas. Não há alerta ativo; a situação só é calculada na leitura.

### 5.8 Gestão de acesso (roles e usuários)

- **Gatilhos**: `PUT roles/{roleName}/permissions`, `PUT users/{id}/roles`, `PUT users/{id}/deactivate|activate`.
- **Regras**: RN-17, RN-18 e RN-19. A role Administrador não é editável. Depois do commit, o cache de permissões de todos os usuários afetados é invalidado (`AssignRolePermissionsCommandHandler.cs:71-82`).
- **Evidências**: `RolesTests.cs` (10), `UsersTests.cs` (74), testes unitários correspondentes, `RoleConcurrencyTests.cs`.
- **Lacunas**: não há criação de roles. Nos testes de integração, roles extras são inseridas direto no banco (`BaseIntegrationTest.cs:182-196`).

### 5.9 Auditoria

- **Gatilho**: qualquer `SaveChanges` com entidades `IAuditable`. A consulta é feita em `GET audit-logs/{entityName}/{entityId}` (`audit:read`).
- **Evidências**: `AuditLogsTests.cs` (5), `GetAuditHistoryQueryHandlerTests.cs`.
- **Lacunas**: não há consulta por usuário ou período nem política de retenção. Os valores ficam em `jsonb`, e o crescimento da tabela não é tratado. **[Inferência]**


### 5.10 Fechar chamado, atribuir e remover responsável (adicionados em 22 e 23/10/2026)

**Fechar**
1. **Objetivo**: encerrar um chamado resolvido.
2. **Gatilho**: `POST tickets/{id}/close`.
3. **Componentes**: endpoint (`HasPermission(tickets:manage)`) → Validation → Logging → `CloseTicketCommandHandler`.
4. **Regras**: RN-21. Usa `TicketStatusTransitions.CanTransition(status, Closed)`, que só aceita `Resolved`.
5. **Domínio**: `TicketStatusChangedDomainEvent` (sem handlers).
6. **Persistência**: `tickets` (com `xmin`), `ticket_histories` (`StatusChanged`, `Resolved` → `Closed`), auditoria.
7. **Resposta**: `204`.
8. **Erros**: 400 (`InvalidStatusTransition`), 403 (sem `tickets:manage`), 404 (inexistente ou excluído), 409 (concorrência).
9. **Evidências**: `CloseTicketCommandHandlerTests.cs` (casos de não encontrado, status inválido e sucesso), `TicketsTests.cs` (Close_*).
10. **Lacunas**: não há fechamento automático. O solicitante não fecha o próprio chamado (decisão de 22/10).

**Atribuir**
1. **Objetivo**: definir ou trocar o responsável por um chamado ativo.
2. **Gatilho**: `PUT tickets/{id}/assignee` com `{ assignedToUserId }`.
3. **Componentes**: endpoint (`HasPermission(tickets:manage)`) → Validation → Logging → `AssignTicketCommandHandler` → `IPermissionProvider` (confere o responsável).
4. **Regras**: RN-22.
5. **Domínio**: `TicketReassignedDomainEvent(TicketId, PreviousAssignedToUserId, AssignedToUserId, ChangedByUserId)` (sem handlers).
6. **Persistência**: `tickets`, `ticket_histories` (`Reassigned`, com os ids do anterior e do novo), auditoria.
7. **Resposta**: `204`, inclusive quando o responsável já era o informado (nada é gravado).
8. **Erros**: 400 (validação, chamado inativo, responsável sem `tickets:manage`), 403, 404 (chamado ou usuário inexistente/inativo), 409.
9. **Evidências**: `AssignTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Assign_*).
10. **Lacunas**: **[Resolvido em 23/10]** o chamado pode ficar sem responsável (ver "Remover responsável"). A abertura (`POST tickets`) ainda aceita como responsável qualquer usuário ativo (Q-05). A descrição do histórico ("Responsável alterado") não mostra nomes.

**Remover responsável** (spec 001, 23/10/2026)
1. **Objetivo**: deixar um chamado ativo sem responsável.
2. **Gatilho**: `DELETE tickets/{id}/assignee`, sem corpo.
3. **Componentes**: endpoint (`HasPermission(tickets:manage)`) → Validation → Logging → `UnassignTicketCommandHandler`.
4. **Regras**: RN-23.
5. **Domínio**: `TicketUnassignedDomainEvent(TicketId, PreviousAssignedToUserId, ChangedByUserId)` (sem handlers).
6. **Persistência**: `tickets` (com `xmin`), `ticket_histories` (`Unassigned`, com o id do anterior em `oldValue` e `newValue` nulo), auditoria. Sem migration: a ação é gravada como texto.
7. **Resposta**: `204`, inclusive quando o chamado já estava sem responsável (nada é gravado).
8. **Erros**: 400 (id vazio, `Validation.General`; chamado inativo, `Tickets.NotActive`), 401, 403 (sem `tickets:manage`), 404 (inexistente ou excluído), 409 (concorrência).
9. **Evidências**: `UnassignTicketCommandHandlerTests.cs`, `TicketValidatorsTests.cs` (UnassignValidator_*), `TicketHistoryEntriesTests.cs`, `TicketHistoryDescriptionsTests.cs`, `TicketsTests.cs` (Unassign_*); rastreabilidade completa na spec 001.
10. **Lacunas**: o responsável removido não é notificado (C-21). Um chamado Em andamento pode ficar sem responsável (GAP-18). A reversão da migration `20261022023422_Store_Ticket_Enums_As_Names` falha se houver registros `Unassigned` (R-1 da spec 001).

---

## 6. Qualidade, segurança e operação

### 6.1 Testes e builds executados nesta análise

| Comando | Resultado real |
|---|---|
| `dotnet --version` | `10.0.401` |
| `docker info` | Servidor Docker 29.6.1 disponível |
| `dotnet build TickestPristine.slnx` | **Sucesso, 0 avisos, 0 erros** (com `TreatWarningsAsErrors`) |
| `dotnet test TickestPristine.slnx --no-build` (análise inicial) | **Todos aprovados, 0 falhas, 0 ignorados**: ArchitectureTests 10/10 (0,8 s); Application.UnitTests 424/424 (1 s); IntegrationTests 240/240 (1 min 52 s, PostgreSQL 17 via Testcontainers) |
| `dotnet build` + `dotnet test` (depois da revisão de 22/10) | Build com 0 avisos e 0 erros. **Todos aprovados, 0 falhas**: ArchitectureTests 10/10; Application.UnitTests 441/441; IntegrationTests 248/248 (2 min 44 s) |
| Verificação da spec 002 (23/10/2026 17:53, `spec-verify`): `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx --no-build --logger trx` | Build com **0 avisos e 0 erros**. **742 aprovados, 0 falhas, 0 ignorados**: ArchitectureTests 10/10; Application.UnitTests 462/462; IntegrationTests 270/270 (3 min 3 s) |
| Verificação da spec 001 (23/10/2026 16:55, `spec-verify`): `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx --no-build --logger trx` | Build com **0 avisos e 0 erros**. **726 aprovados, 0 falhas, 0 ignorados**: ArchitectureTests 10/10; Application.UnitTests 455/455; IntegrationTests 261/261 (3 min 5 s) |
| Linha de base antes da spec 001 (22/10/2026, commit `8660490`): `docker version`, `docker compose ps`, `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx --no-build` | Docker 29.6.1 disponível, com os três serviços do `docker compose` em execução (`tickestpristine-postgres`, `tickestpristine-seq`, `tickestpristine-web-api`); os testes de integração usam um PostgreSQL próprio via Testcontainers. Build com **0 avisos e 0 erros**. **699 aprovados, 0 falhas, 0 ignorados**: ArchitectureTests 10/10; Application.UnitTests 441/441; IntegrationTests 248/248 (2 min 49 s) |

- **[Fato]** O `WebApplicationFactory` sobe a API no ambiente padrão dele (Development), então a configuração de `appsettings.Development.json`, inclusive os enrichers sem pacote (6.3), **não impediu a inicialização**. Isso não prova que os enrichers funcionem.
- **[Fato]** Testes aprovados mostram que o comportamento coberto funciona nesse ambiente controlado. **Não** são evidência de funcionamento em produção.

### 6.2 Estratégia e cobertura identificáveis

| Projeto | Casos executados | O que cobre | Limitações |
|---|---|---|---|
| `TickestPristine.Application.UnitTests` | 441, em 66 arquivos `*Tests.cs` (424 antes da revisão) | Handlers (EF InMemory via `TestDbContext` + NSubstitute) e validators de todos os módulos; regras de SLA, paginação e guards | O EF InMemory não reproduz `citext`, filtros parciais, `xmin` nem SQL do Dapper. |
| `TickestPristine.IntegrationTests` | 248, em 20 arquivos `*Tests.cs` (240 antes da revisão) | HTTP de ponta a ponta contra PostgreSQL real (Testcontainers), autorização por rota, seed, migrations sem pendência (`MigrationsTests.cs`), concorrência, CORS, Correlation-Id, Swagger, auditoria | Uma única instância de banco compartilhada pela coleção. Não cobre Redis, OTLP nem Seq. |
| `TickestPristine.ArchitectureTests` | 10 | Direção das dependências entre camadas | Não verifica convenções (nomes, `sealed`, `internal`) nem a ausência de EF no Domain além das referências de projeto. |

- **[Fato]** Não há medição de cobertura configurada no CI (há `coverlet.collector` nos projetos, mas nenhum relatório é gerado em `build.yml`).
- **[Fato]** Não há testes de carga, de contrato de API nem de front-end.

### 6.3 Execução e reprodução do ambiente

- **[Fato]** `docker compose up -d` sobe o PostgreSQL (host `5440`), o Seq (UI `8090`) e a própria API (`5000/5001`) (`docker-compose.yml`). O `README.md:44-58` está coerente com o compose. O `CLAUDE.md` cita o Seq em `8081`, o que diverge.
- **[Fato]** Em Development, os logs vão para o Seq em `http://seq:5341` (`appsettings.Development.json:20`). Esse nome só resolve dentro da rede do compose. **[Inferência]** Rodando a API no host com `dotnet run`, o envio ao Seq falha sem interromper a aplicação.
- **[Fato]** `appsettings.Development.json:23` declara os enrichers `WithMachineName` e `WithThreadId`, mas os pacotes `Serilog.Enrichers.Environment`/`Thread` não estão entre as dependências restauradas (`obj/project.assets.json`). O efeito em execução não foi verificado. **[Inferência]** Esses enrichers provavelmente são ignorados.
- **[Fato]** O volume do PostgreSQL é mapeado para `./.containers/db` na raiz do repositório (`docker-compose.yml`). O diretório existe com dados locais e está no `.gitignore` (linha 400).

### 6.4 Logs e observabilidade

- **[Fato]** Serilog configurado por `appsettings`. Uma propriedade `CorrelationId` é aceita do cliente se corresponder a `^[A-Za-z0-9._-]{1,64}$`; senão, usa o TraceId do OpenTelemetry. Ela é devolvida no header `Correlation-Id`, inclusive em respostas de erro (`RequestContextLoggingMiddleware.cs`, `RequestContextLoggingTests.cs`).
- **[Fato]** O OpenTelemetry tem tracing (ASP.NET Core, HttpClient, Npgsql) e métricas (ASP.NET Core, HttpClient, Runtime). Não há métricas de negócio.
- **[Fato]** O health check `GET health` verifica o PostgreSQL. O relatório detalhado só aparece em Development (`Program.cs:41-50`).

### 6.5 Segurança

Pontos positivos comprovados: hash de senha forte, refresh token com hash e rotação e detecção de reuso, bloqueio por tentativas, rate limit nas rotas de autenticação, autorização no servidor, proteção contra escalada de privilégio, mascaramento de erro 500, validação do Correlation-Id, proteção contra path traversal, exclusão de campos sensíveis da auditoria e segredos fora do `appsettings.json` (valores vazios nos arquivos versionados).

Pontos de atenção (detalhados na seção 7):

- O auto-cadastro anônimo (GAP-03) foi **resolvido** em 22/10: o cadastro exige `users:create`.
- Access token não revogável antes de expirar, até 1440 min por configuração (GAP-14).
- Não há verificação de conteúdo dos anexos (GAP-09).
- Não há `UseHttpsRedirection`, HSTS nem `ForwardedHeaders` no pipeline (`Program.cs`). **[Inferência]** Pode ser intencional se TLS e proxy forem tratados fora da aplicação (GAP-11).
- `docker-compose.override.yml` e `appsettings.Development.json` contêm credenciais **padrão de desenvolvimento** do PostgreSQL em texto plano. Os valores não foram reproduzidos aqui.

### 6.6 Tratamento de falhas e resiliência

- **[Fato]** Não há retry ou circuit breaker explícito (ex.: `EnableRetryOnFailure` do Npgsql não está configurado em `DependencyInjection.cs:103-104`).
- **[Fato]** A falha de um handler de evento é registrada em log e engolida (`DomainEventsDispatcher.cs:43-56`).
- **[Fato]** A invalidação de cache depois do commit não tem compensação: se falhar, o cache expira em até 5 min.

### 6.7 Desempenho e escalabilidade

- **[Fato]** Existem índices dedicados às listagens e paginações (`TicketConfiguration.cs:32-40`, configurações de mensagens, anexos, histórico e auditoria).
- **[Inferência]** Escalar a API para várias instâncias exige Redis (cache) e **armazenamento de arquivos compartilhado**. `LocalFileStorage` grava em disco local, e o rate limiting também é por instância. Não há evidência de que isso tenha sido testado.
- Não há evidência suficiente para avaliar o desempenho sob carga.

### 6.8 CI/CD e deploy

- **[Fato]** `.github/workflows/build.yml` roda em três eventos: push na `main`, pull request para a `main` e disparo manual (`workflow_dispatch`) (`build.yml:3-10`). Os passos são os testes do portão SDD (`Test SDD gate hook`, `python3 -m unittest` sobre `.claude/hooks`, `build.yml:22-23`; acrescentado em 22/10/2026 e ainda não executado no GitHub), restore, build (Release) e test (Release). O passo `Publish` tem a condição `if: github.event_name != 'pull_request'` (`build.yml:39-41`): roda em push e no disparo manual, e não roda em pull request. O workflow não publica artefatos nem imagens, e não há etapa de deploy.
- **[Fato]** O modelo de PR (`.github/pull_request_template.md`) pede o caminho da spec, o status `Verificada`, os critérios de aceitação cobertos, as evidências de verificação (build, testes, revisão de convenções), a informação de migration e um checklist; uma mudança trivial sem spec só vale com autorização explícita do responsável no chat e precisa ser descrita no PR. O modelo só aparece no GitHub depois de chegar à branch padrão.
- **Validação:**
  - **[Fato]** Validado localmente em 22/10/2026 (Windows): o YAML do workflow foi lido sem erro, e os quatro comandos do workflow em Release terminaram com 0 avisos, 0 erros, 699 de 699 testes aprovados e publish concluído.
  - **[Fato]** No GitHub, as 5 últimas execuções registradas do workflow foram em push na `main` e tiveram sucesso, a última em 04/10/2026 (`gh run list`). O gatilho de pull request **ainda não rodou no GitHub**: a branch `feat/spec-driven-development` não foi enviada e nenhum PR foi aberto.
  - **[Inferência]** O CI em pull request só bloqueia o merge se a branch `main` exigir o check "Build" nas regras de proteção do GitHub; essa configuração fica fora do repositório e não foi verificada.
- **[Fato]** O Dependabot está configurado para NuGet, com frequência diária (`.github/dependabot.yml`).
- **[Fato]** O Dockerfile tem 4 estágios e roda com usuário não root (`USER $APP_UID`) (`src/TickestPristine.Web.Api/Dockerfile`).

### 6.9 Processo de desenvolvimento (Spec-Driven Development)

- **[Fato]** O `CLAUDE.md` define o fluxo nas seções "Política de autonomia" e "Fluxo de desenvolvimento (SDD)": mudança funcional não trivial passa por spec, aprovação explícita do responsável no chat, implementação, verificação e entrega (commit e PR) só com autorização explícita. `Verificada` não significa `Entregue`.
- **[Fato]** Três skills de projeto implementam as etapas:
  - `spec-new` (`.claude/skills/spec-new/`): escreve só em `specs/`, a partir do modelo `references/spec-template.md`, e para à espera da aprovação;
  - `spec-implement` (`.claude/skills/spec-implement/`): declara `disable-model-invocation: true`, para só rodar por `/spec-implement` (efeito observado em 22/10/2026: a skill não aparece entre as que o modelo pode acionar sozinho; ver "Frontmatter das skills" abaixo), começa só de uma spec `Aprovada` (e retoma `Em implementação`, `Verificada` com código a alterar e `Bloqueada`, recusando os demais estados), define a retomada de uma spec `Bloqueada` e usa as skills `add-entity`, `add-feature` e `add-tests`;
  - `spec-verify` (`.claude/skills/spec-verify/`): roda build e testes, exige que cada critério seja coberto por um teste que exercita o comportamento e passou na execução feita por ela, chama a `ca-review` e atualiza as seções afetadas deste documento.
- **[Fato]** Os estados (`Rascunho`, `Bloqueada`, `Aprovada`, `Em implementação`, `Verificada`, `Entregue`, `Cancelada`) e as transições permitidas ficam numa tabela única, `.claude/skills/spec-new/references/spec-status.md`; toda transição acrescenta uma linha no histórico da spec.
- **[Fato]** Cada spec é um arquivo único em `specs/NNN-slug/spec.md`, com requisitos (REQ), critérios de aceitação (AC, no formato dado/quando/então), decisões (DEC, com quem decidiu e quando), fora do escopo, plano técnico, tarefas (T, ligadas a ACs e à skill que as executa), rastreabilidade (AC → teste → como o teste cobre o critério → resultado), evidências de verificação e histórico.
- **[Fato]** Specs existentes: `specs/001-remover-responsavel/spec.md`, aprovada em 2026-10-22 (commit `8660490`), revisada no mesmo dia a pedido do responsável e aprovada de novo na versão revisada, implementada e `Verificada` em 2026-10-23 e entregue no commit `b0e78b0`; `specs/002-historico-descricao-chamado/spec.md`, aprovada em 2026-10-22, implementada e `Verificada` em 2026-10-23 (ainda não entregue); `specs/003-testes-arquitetura-regras-permanentes/spec.md`, `Rascunho`, com DEC-1 a DEC-8 decididas pelo responsável e à espera da revisão e da aprovação formal do documento completo. Das quatro specs, a 001 e a 002 foram implementadas.
- **[Fato]** Backlog (desde 23/10/2026): `specs/BACKLOG.md` lista as demandas futuras (`BL-n`) em três faixas (`Agora`, `Próxima` e `Depois`), com origem, dependências e os estados `Proposta`, `Pronta`, `Especificada` e `Descartada`, definidos no próprio arquivo. O item aponta para as specs e, depois de `Especificada`, não acompanha o andamento, que fica só no status da spec; a única mudança permitida nele é a `spec-new` acrescentar à coluna Specs uma spec posterior da mesma demanda, sem remover nem trocar números já registrados e sem alterar as demais colunas. A coluna Specs só traz specs existentes; as planejadas ficam numa nota. Mudanças nas regras, e de faixa, ordem ou estado fora da `spec-new`, são registradas na seção "Histórico" do próprio arquivo, sem editar linhas anteriores. A `spec-new` preenche o campo `backlog:` do frontmatter (novo no modelo) e marca o item como `Especificada`; as specs 001 a 004 estão nos itens BL-001 a BL-004; as 001 a 003, sem o campo, porque são anteriores a ele, e a 004 o recebeu na revisão de 23/10/2026. As fontes externas usadas no backlog ficam em `specs/_fontes/` (hoje, `prompt-sla.md`).
- **[Fato]** Portão determinístico: o hook de projeto `.claude/hooks/sdd_gate.py`, registrado em `.claude/settings.json` (`UserPromptSubmit` e `PreToolUse` com matcher `Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell`), bloqueia escrita em `src/` e `tests/`, por ferramenta de edição ou comando de shell, fora de um `/spec-implement NNN` cuja spec esteja `Em implementação`, e também a alteração do próprio hook e dos settings. A mensagem do usuário com `[sdd:autorizo]` libera a escrita só naquela mensagem: a próxima mensagem do usuário, inclusive uma enviada no meio de uma resposta, encerra a liberação (observado em 22/10/2026). O hook também vale para subagentes (observado: bloqueou um subagente de revisão). O estado de cada sessão fica fora do repositório (pasta temporária do sistema).
  - **Estado da instalação (22/10/2026):** v2 completa no repositório: `sdd_gate.py`, `test_sdd_gate.py` e `.claude/settings.json` (com `|| exit 2` e timeout de 30 s), idênticos aos arquivos validados de ponta a ponta na worktree de teste; 33 testes aprovados no lugar.
  - **O que a v2 bloqueia, com teste executado** (33 testes automatizados; "e2e" = cenário de ponta a ponta com `claude -p`, E-41): Edit, Write, MultiEdit e NotebookEdit cujo alvo esteja em `src/` ou `tests/`, inclusive com maiúsculas e caminhos `/c/...` (Write: e2e); em comandos de shell, cada trecho (`;`, `&&`, `||`, `|`) é avaliado: redirecionamento `>` e `>>`, com ou sem espaço, alvo entre aspas, absoluto, com `./` ou `../` e em variável (`f=src/…; echo x > $f`: e2e), heredoc com redirecionamento; `cd src && sed -i`; `sed -Ei` e `sed --in-place`; `cp` (inclusive com destino em variável); `git checkout -- src/…`; código embutido em `python` (heredoc e `-c`) e em `pwsh -Command`; no PowerShell, `Set-Content` (e2e), `Out-File` em pipeline, `'x' > $p`, `ni`, `copy`, `[IO.File]::WriteAllText`, `-EncodedCommand` e `mklink /J`; comandos que mudam a árvore sem citar caminho (`git stash pop`, `git checkout main`, `git switch`, `git merge`, `git pull`, `git restore .`, `git clean -fd`, `git reset --hard`, `dotnet format`); escrita com o diretório atual dentro de `src/`. Protege `.claude/hooks`, `.claude/settings.json`, `.claude/settings.local.json` (Edit, Write e `Remove-Item`) e o estado da sessão. No modo `/spec-implement`, exige `status: Em implementação` e `aprovada-em` preenchido, e termina quando a spec sai de `Em implementação`; o `/spec-implement` no meio da mensagem não conta e não libera o próprio portão. A liberação por `[sdd:autorizo]` vale só na mensagem, expira em 60 minutos e é isolada por sessão. JSON inválido bloqueia; `python` ausente bloqueia com o `|| exit 2` (e2e).
  - **Regra presente no código da v2, sem teste executado:** `Add-Content`, `New-Item` e `Copy-Item` (nome completo), `Move-Item`, `Rename-Item`, `Clear-Content`, `Tee-Object`, `Expand-Archive`, `Invoke-WebRequest`, `[IO.File]::WriteAllBytes/WriteAllLines/AppendAllText`, `robocopy`, `xcopy`, `rm`, `mv`, `mkdir`, `rmdir`, `touch`, `tee` como escrita, `ln`, `truncate`, `dd`, `install`, `del`, `erase`, `move`, `ren` e os demais aliases do PowerShell (`sc`, `ac`, `md`, `rd`, `cpi`, `mi`, `ri`, `rni`, `clc`), `patch`, `tar`, `unzip`, `rsync`, `perl -i`, `dotnet ef/new/add/remove/sln`, `curl -o` e `wget`, `git apply/am/rebase/cherry-pick/revert/mv/rm`, código embutido em `node`, `ruby`, `perl` e `powershell -Command`, e a falha interna (exceção) numa ferramenta que escreve. A existência do padrão no código não prova o bloqueio.
  - **O que a v2 libera:** leitura (`grep`, `cat`, `Get-Content`, `git diff`), `dotnet build` e `dotnet test` (inclusive com log redirecionado para fora de `src/` e `tests/`), `git commit` isolado (mesmo com mensagem em heredoc que cite `src/`), escrita em `specs/`, no documento de estado e fora do repositório.
  - **Limites que continuam:** caminho montado por concatenação ou vindo de outra variável sem citar `src/`/`tests/` no comando; script em arquivo (`python script.py`, `./build.sh`) que escreva em `src/`; ferramentas MCP que escrevam em disco (fora do matcher); hook que estoure o timeout de 30 s (o comportamento do Claude Code nesse caso não foi verificado e deve ser tratado como possível liberação; uma chamada típica leva cerca de 84 ms); o `status` e o `aprovada-em` da spec podem ser editados pelo modelo. Falso positivo conhecido (observado quatro vezes em 22/10/2026): um comando que tenha um trecho de escrita com variável (ex.: `Copy-Item … "$destino\x.md"`, `'x' | Out-File "$destino\y.py"`, `New-Item … "$destino\pasta"` ou `… 2>$null | Out-File -Encoding utf8 "$destino\z.diff"`) e cite `src`, `tests` ou `.claude/hooks` em outra instrução, mesmo só de leitura, é bloqueado, porque a regra de alvo em variável considera o comando inteiro; separar a escrita das leituras, ou usar caminhos literais, resolve. Também continuam fora, na v2 (por leitura do código, sem teste executado): escrita com destino vindo da entrada do pipeline sem variável nem caminho citado (ex.: `Get-ChildItem … | Set-Content -Value x`); `cd`/`Set-Location` com caminho em variável; `git ls-files src | xargs sed -i …` (xargs sem `-I{}`); `find src -delete`; `bash -c` e `cmd /c` com o caminho só dentro da string; `read -r f < lista.txt`. O portão evita erros do agente; não resiste a quem queira burlá-lo.
  - **Proposta v3 (descartada, não instalada):** restringia o contexto da escrita com variável à própria instrução e às instruções de atribuição ou laço. Passava nos seus 35 testes (Windows e Linux), mas a revisão independente de 22/10/2026 mostrou, por leitura do código, que ela libera escritas que a v2 bloqueia, em formas comuns fora desses testes: `ls src | while read f; do … > $f; done`, `-OutVariable`/`-ov`/`Tee-Object -Variable`, pipelines e blocos em várias linhas, atribuições que não estão no início da instrução (`if (…) { $p = … }`, `{ f=…; }`, funções, scriptblocks), `+=`, `[string]$p =`, `readonly`/`declare`/`printf -v`, `set --`, `Set-Item Env:`, `mapfile`/`readarray`/`read`, e até a escrita no próprio portão (`Get-ChildItem .claude\hooks -ov h; $h | % { Set-Content $_ x }`). Descartada como candidata.
  - **Proposta v4 (candidata, não instalada, no rascunho de trabalho):** mantém a regra da v2 como padrão e só abre uma exceção quando todas as variáveis da instrução que escreve têm valor literal (atribuição simples `d=…` ou `$d = '…'`, feita uma única vez, antes da escrita, sem religação por `+=`, `-OutVariable`, `Set-Variable`, `read`, laço, escopo `$script:` ou afins, e sem `eval`/`iex`/`source` no comando) e o caminho final, com os valores no lugar, fica fora de `src/`, `tests/` e do portão; na dúvida, vale a v2. As constantes `$null`, `$true` e `$false` (ex.: `2>$null`) contam como valor conhecido, que não é caminho, só se o comando não as atribuir em lugar nenhum (no Bash são variáveis comuns). Testes executados em 22/10/2026: 41 (os 33 da v2 mais 8 novos, com 72 comandos), aprovados em Windows e em Linux (`python:3.12-slim`); a mesma suíte contra a v2 falha só nos 11 casos de exceção, e os 28 casos de regressão da v3, as 14 tentativas de burlar a exceção, os 9 destinos literais dentro de `src/`, `tests/` ou do portão e os 9 casos negativos das constantes são bloqueados pelas duas versões. Dos quatro falsos positivos reais, três passam com a v4; o segundo continua bloqueado, porque o comando enviava ao `Out-File` um here-string e carregava, dentro do texto, escritas com variáveis sem valor literal (caso ambíguo). Os testes cobrem casos específicos: não provam ausência de outras brechas. A instalação depende de decisão e autorização explícitas do responsável, com `[sdd:autorizo]`.
  - **CI:** o passo "Test SDD gate hook" do `.github/workflows/build.yml` está configurado para rodar os testes do hook em cada push e pull request para a `main`; ainda não foi executado no GitHub (a branch não foi enviada).
- **[Resolvido em 22/10/2026, commit `6dbe6a5`] Frontmatter das skills:** até 22/10, o YAML do frontmatter de 6 skills (`add-entity`, `add-feature`, `add-tests`, `spec-implement`, `spec-new` e `spec-verify`) não era lido, porque o valor sem aspas do `argument-hint` continha `ex.: ` (dois-pontos seguido de espaço). O `claude plugin validate .claude/skills` (Claude Code 2.1.309) acusava "YAML frontmatter failed to parse … all frontmatter fields silently dropped" nas seis. Com isso, a `description` era substituída pela primeira linha do conteúdo (o título) e o `disable-model-invocation: true` da `spec-implement` não tinha efeito: a skill aparecia entre as que o modelo podia acionar sozinho. Na `ca-review`, o mesmo campo começava com `[` e era lido como lista, não como texto. Correção: o valor do `argument-hint` das sete skills ficou entre aspas simples, sem mudar o texto, e o validador passou a aprovar todas.
- **Validação:** a `spec-new` gerou a spec 001 e, em 22/10/2026, revisou a 001 e gerou a 002 e a 003. A primeira execução real da `spec-implement` e da `spec-verify` foi a da spec 001, em 23/10/2026: a `spec-implement` rodou por `/spec-implement 001` digitado pelo responsável, com o portão liberando a escrita em `src/` e `tests/` enquanto a spec estava `Em implementação`; a `spec-verify` levou a spec a `Verificada`. Depois da correção do frontmatter, uma sessão nova mostrou as descrições completas e a `spec-implement` fora das skills que o modelo pode acionar sozinho. O comportamento diante de pedidos diretos foi testado (E-42) e o portão por hook, também (E-41).

---

## 7. GAP analysis e evolução recomendada

### 7.1 Problemas confirmados

| ID | Categoria | Evidência | Impacto potencial | Prioridade e justificativa | Dependências | Próxima ação recomendada |
|---|---|---|---|---|---|---|
| GAP-01 | Funcional / fluxo | **[Resolvido em 22/10]** Antes: `UpdateTicketCommandHandler.cs:42-45` recusava inativos e nenhum handler levava a `Closed`. Agora: `Tickets/Close/CloseTicketCommandHandler.cs` + `POST tickets/{id}/close` (`tickets:manage`) | — | Era **P1** | Q-01 (respondida: o atendente fecha) | Nenhuma. Fechamento automático por prazo continua fora do escopo. |
| GAP-02 | Funcional | **[Resolvido em 22/10]** `Tickets/Assign/AssignTicketCommandHandler.cs` + `PUT tickets/{id}/assignee` (`tickets:manage`); o responsável precisa estar ativo e ter `tickets:manage`; grava o histórico `Reassigned` e o evento `TicketReassignedDomainEvent` | Resta a divergência com a abertura, que aceita qualquer usuário ativo como responsável (Q-05) | Era **P1** | Q-05 | Decidir se a regra do responsável também vale na abertura. |
| GAP-03 | Segurança / produto | **[Resolvido em 22/10]** `Endpoints/Users/Register.cs` passou de `AllowAnonymous` para `.HasPermission(users:create)`; o rate limit `authentication` saiu da rota, que agora é autenticada | O cadastro passou a ser feito só por quem tem `users:create` (por padrão, o Administrador) | Era **P1** | Q-02 (respondida) | Nenhuma. Convite ou primeiro acesso por e-mail continua inexistente. |
| GAP-04 | Manutenção | 31 `Raise(...)`, 1 handler vazio; `TicketHistoryRecordedDomainEvent.cs` sem uso | Custo de manutenção e falsa sensação de reatividade | **P3**: não afeta o comportamento atual. | — | Manter enquanto não há consumidores, se for padrão desejado; remover `TicketHistoryRecordedDomainEvent` ou passar a dispará-lo. |
| GAP-05 | Arquitetura / confiabilidade | `ApplicationDbContext.cs:63-69`, `DomainEventsDispatcher.cs:9-11`: eventos em memória depois do commit | Efeitos colaterais futuros (notificações de SLA, e-mails) podem se perder em falhas ou quedas. | **P2**: sem impacto hoje (não há handlers relevantes), mas é pré-requisito declarado do plano de SLA (`prompt-sla-tickest.md`, Fase 0). | Decisão sobre o plano de SLA | Introduzir o outbox só quando o primeiro efeito colateral relevante for implementado. |
| GAP-06 | Documentação | **[Parcialmente resolvido em 22/10]** Os caminhos `src/TickestPristine.*` e `tests/TickestPristine.*` e a porta do Seq (`8090`) foram corrigidos no `CLAUDE.md`. Os exemplos de Todos foram mantidos de propósito | Os exemplos de Todos não correspondem ao domínio real | **P3** (era P2) | Q-09 | Nenhuma, salvo nova decisão sobre os exemplos. |
| GAP-07 | Documentação | **[Resolvido em 22/10]** O `README.md` descreve roles fixas, desativação em vez de exclusão, o administrador vindo de `Admin:*`, o fechamento, a atribuição e o cadastro com `users:create` | — | Era **P2** | — | Nenhuma. |
| GAP-08 | Funcional | `DeleteTicketAttachmentCommandHandler.cs:47` só marca `DeletedAtUtc` | Os arquivos continuam ocupando disco indefinidamente, e um "anexo removido" continua existindo fisicamente (relevante para a LGPD). | **P2** | Q-08 (política de retenção) | Definir a retenção e implementar a remoção física (imediata ou por rotina). |

### 7.2 Riscos potenciais

| ID | Categoria | Evidência | Impacto potencial | Prioridade e justificativa | Próxima ação |
|---|---|---|---|---|---|
| GAP-09 | Segurança / escalabilidade | `LocalFileStorage.cs`; `TicketAttachmentLimits.cs` confia em `ContentType` e extensão | Não escala para várias instâncias. Um arquivo malicioso com extensão permitida é aceito. | **P2**: depende do modelo de implantação, que é desconhecido. | Definir o armazenamento alvo (ex.: blob) e a política de inspeção de arquivos. |
| GAP-10 | Operação | Migrations e seed automáticos só em Development (`Program.cs:34-39`); não há pipeline de deploy | Implantação manual e propensa a erro; o primeiro administrador depende de rodar a ferramenta EF com a seção `Admin`. | **P2** | Documentar ou automatizar o procedimento de migração em produção. |
| GAP-11 | Segurança / operação | Não há `ForwardedHeaders`, HTTPS redirection nem HSTS; rate limit por `RemoteIpAddress` (`RateLimitingExtensions.cs:50-56`) | Atrás de um proxy reverso, todos os anônimos podem cair na mesma partição (bloqueio coletivo do login). | **P2**: condicionado à topologia de rede, que é desconhecida. | Confirmar a topologia e configurar os forwarded headers, se aplicável. |
| GAP-12 | CI | **[Resolvido no repositório em 22/10, commit `d9d1f50`; validação no GitHub pendente]** Antes: `build.yml` sem gatilho `pull_request`, com testes de integração só depois do merge. Agora: gatilho `pull_request` para a `main` e `Publish` só fora de PR (seção 6.8) | Até rodar num PR real, não há prova de que os 699 testes passam no runner Linux do GitHub | **P2** | Abrir um PR para a `main` e conferir a execução do workflow; avaliar exigir o check "Build" na proteção da `main`. |
| GAP-13 | Observabilidade | Seq em `http://seq:5341` (só na rede do compose); enrichers sem pacote | Logs estruturados ausentes ao rodar a API no host | **P3** | Ajustar a URL por ambiente e adicionar ou remover os enrichers. |
| GAP-14 | Segurança | Access token sem revogação (`TokenProvider.cs`); logout e troca de senha só revogam o refresh | Um token vazado vale até expirar (60 min em Development, até 1440 permitido) | **P3**: compensado pela checagem de usuário ativo a cada requisição. | Manter o prazo curto em produção. Avaliar uma denylist só se houver requisito. |
| GAP-15 | Arquitetura | SQL cru do PostgreSQL na Application (`GetTicketVolumeQueryHandler.cs`) | Acoplamento ao esquema físico; renomear uma coluna quebra só em execução (mitigado pelos testes de integração) | **P3** | Aceitar como decisão consciente ou mover para a Infrastructure. |
| GAP-16 | Dados | Fuso `America/Sao_Paulo` fixo no SQL | Organizações em outros fusos teriam gráficos deslocados | **P3** | Só tratar se houver requisito multifuso. |
| GAP-17 | Observabilidade | `LoggingDecorator` registra falhas esperadas (404, 409, validação) como `LogError` | Ruído em alertas baseados em nível de log | **P3** | Avaliar `Warning` para erros de negócio. |
| GAP-18 | Funcional / fluxo | Spec 001, DEC-4: remover o responsável não muda o status (`Tickets/Unassign/UnassignTicketCommandHandler.cs`; `TicketsTests.Unassign_Should_KeepStatus_WhenTicketIsInProgress`) | Um chamado pode ficar Em andamento sem ninguém atendendo, sem alerta | **P3**: decisão consciente da spec 001; nenhuma regra atual depende do responsável em Em andamento | Decidir se a fila deve destacar esses chamados ou se o status deve voltar a Aberto. |

### 7.3 Melhorias recomendadas (sem urgência)

- **[Recomendação]** Medir cobertura no CI (o `coverlet` já está referenciado).
- **[Recomendação]** Uniformizar a leitura de entidades inativas (departamento × setor, item 3.10.5).
- **[Recomendação]** Tratar `FileNotFoundException` no download como 404 ou erro controlado, depois de confirmar o comportamento atual com um teste.

### 7.4 Decisões que dependem de requisitos ou aprovação humana

As perguntas Q-01 a Q-12 estão na seção 9.5 (Q-01 e Q-02 foram respondidas e Q-05 em parte). Nenhuma das ações dos GAP-05, 06, 08, 09 e 10 deve ser executada sem as respostas correspondentes.

### 7.5 Etapas evolutivas sugeridas (não obrigatórias)

**[Fato]** Desde 23/10/2026, a ordem de execução fica em `specs/BACKLOG.md`, que cita os GAPs e as perguntas desta seção e da 9.5 pelo ID. As etapas que estavam aqui viraram itens do backlog: o que restava da Etapa 1 (Q-05 e a validação do GAP-12) está em BL-005 e BL-006; a Etapa 2 (GAP-09, GAP-10, GAP-11 e GAP-13), em BL-008 a BL-011; a Etapa 3 (SLA), em BL-004 (Fase 1, specs 004 a 006) e BL-012 a BL-018 (Fases 0 e 2 a 7). A divergência sobre construtores primários foi decidida na DEC-2 da spec 004, a favor do `CLAUDE.md`, e a ordem do outbox, na DEC-4 da mesma spec (pré-requisito da Fase 5).

---

## 8. Catálogo de evidências e rastreabilidade

| # | Afirmação | Arquivo : linhas | Símbolo | Teste relacionado |
|---|---|---|---|---|
| E-01 | Pipeline HTTP e ordem dos middlewares | `src/TickestPristine.Web.Api/Program.cs:13-69` | top-level | `RequestContextLoggingTests.cs`, `CorsTests.cs` |
| E-02 | Migrations automáticas só em Development | `src/TickestPristine.Web.Api/Program.cs:34-39` | `ApplyMigrations` | — |
| E-03 | Decorators Validation → Logging → handler | `src/TickestPristine.Application/DependencyInjection.cs:28-34` | `AddApplication` | — |
| E-04 | Commands validados, queries não | `src/TickestPristine.Application/DependencyInjection.cs:33-34` | — | — |
| E-05 | Eventos publicados depois do commit, falhas engolidas | `src/TickestPristine.Infrastructure/Database/ApplicationDbContext.cs:59-88`; `DomainEvents/DomainEventsDispatcher.cs:43-56` | `SaveChangesAsync`, `DispatchAsync` | `DomainEventsPublishingTests.cs` |
| E-06 | Único handler de evento, sem efeito | `src/TickestPristine.Application/Users/Register/UserRegisteredDomainEventHandler.cs:6-12` | `UserRegisteredDomainEventHandler` | — |
| E-07 | Auditoria na mesma transação, sem campos sensíveis | `src/TickestPristine.Infrastructure/Audit/AuditLoggingInterceptor.cs:19-58` | `AuditLoggingInterceptor` | `AuditLogsTests.cs:128` |
| E-08 | Permissões por roles, com cache | `src/TickestPristine.Infrastructure/Authorization/PermissionProvider.cs:10-48` | `PermissionProvider` | `PermissionsTests.cs`, `RolesTests.cs` |
| E-09 | Policy dinâmica por permissão | `src/TickestPristine.Infrastructure/Authorization/PermissionAuthorizationPolicyProvider.cs:14-18` | — | `SwaggerTests.cs` (indireto) |
| E-10 | Usuário ativo exigido na policy padrão | `src/TickestPristine.Infrastructure/DependencyInjection.cs:150-156`; `ActiveUserAuthorizationHandler.cs` | — | `UserIsActiveTests.cs`, `UsersTests.cs` |
| E-11 | JWT só com identidade | `src/TickestPristine.Infrastructure/Authentication/TokenProvider.cs:22-29` | `CreateAsync` | — |
| E-12 | PBKDF2-SHA512 com 500k iterações | `src/TickestPristine.Infrastructure/Authentication/PasswordHasher.cs:8-31` | `PasswordHasher` | — |
| E-13 | Bloqueio por tentativas | `src/TickestPristine.Application/Users/UserCredentialsVerifier.cs:15-71` | `VerifyAsync` | `LoginUserCommandHandlerTests.cs` |
| E-14 | Rotação e reuso do refresh token | `src/TickestPristine.Application/Users/Refresh/RefreshTokenCommandHandler.cs:15-104` | — | `RefreshTokenCommandHandlerTests.cs` |
| E-15 | Cadastro exige `users:create` e atribui a role Colaborador | `src/TickestPristine.Web.Api/Endpoints/Users/Register.cs`; `RegisterUserCommandHandler.cs:39` | — | `UsersTests.cs` (Register_Should_ReturnUnauthorized_WhenNoTokenProvided, Register_Should_ReturnForbidden_WhenCallerLacksUsersCreatePermission) |
| E-16 | `users:create` usada pelo cadastro | `src/TickestPristine.Web.Api/Endpoints/Users/Permissions.cs` (`UsersCreate`); `PermissionCatalog.cs:24` | `PermissionCatalog.UsersCreate` | `PermissionCatalogTests.cs`, `UsersTests.cs` |
| E-17 | Transições de status | `src/TickestPristine.Application/Tickets/TicketStatusTransitions.cs:7-25` | `CanTransition` | `UpdateTicketCommandHandlerTests.cs`, `ReopenTicketCommandHandlerTests.cs` |
| E-18 | Update só em chamados ativos | `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs:42-45` | — | `TicketsTests.cs:928` |
| E-19 | SLA em horas corridas | `src/TickestPristine.Application/Tickets/TicketSla.cs:13-57` | `TicketSla` | `TicketSlaTests.cs` |
| E-20 | SLA em SQL equivalente | `src/TickestPristine.Application/Tickets/TicketSlaFilters.cs:20-36` | `WhereSlaStatus` | `GetTicketsQueryHandlerTests.cs`, `TicketsTests.cs:308` |
| E-21 | Acesso ao chamado por participante ou `tickets:manage` | `src/TickestPristine.Application/Authorization/TicketAccessGuard.cs:18-72` | `EnsureCanViewAsync`, `EnsureOwnerOrManagerAsync` | `TicketAccessGuardTests.cs` |
| E-22 | Escalada de privilégio e último administrador | `src/TickestPristine.Application/Authorization/PrivilegeEscalationGuard.cs`; `AdministratorGuard.cs:24-58` | — | `AdministratorGuardTests.cs`, `RoleConcurrencyTests.cs` |
| E-23 | Concorrência `xmin` em Ticket | `src/TickestPristine.Infrastructure/Tickets/TicketConfiguration.cs:27-29` | — | `TicketConcurrencyTests.cs` |
| E-24 | Enums gravados como texto (commit `b2028ae`) | `src/TickestPristine.Infrastructure/Tickets/TicketConfiguration.cs:20-23`; migration `20261022023422_Store_Ticket_Enums_As_Names.cs` | — | `MigrationsTests.cs` |
| E-25 | Volume com SQL e fuso fixo | `src/TickestPristine.Application/Tickets/GetVolume/GetTicketVolumeQueryHandler.cs:19-49` | — | `TicketsTests.cs:709-833` |
| E-26 | Upload com compensação e arquivo antes da transação | `src/TickestPristine.Application/TicketAttachments/Upload/UploadTicketAttachmentCommandHandler.cs:35-75` | — | `UploadTicketAttachmentCommandHandlerTests.cs` |
| E-27 | Exclusão de anexo só lógica | `src/TickestPristine.Application/TicketAttachments/Delete/DeleteTicketAttachmentCommandHandler.cs:45-57` | — | `DeleteTicketAttachmentCommandHandlerTests.cs` |
| E-28 | Proteção contra path traversal | `src/TickestPristine.Infrastructure/Storage/LocalFileStorage.cs:71-81` | `ResolveContainedPath` | — (não identificado) |
| E-29 | Erros → ProblemDetails | `src/TickestPristine.Web.Api/Infrastructure/CustomResults.cs:7-57`; `GlobalExceptionHandler.cs:12-88` | — | vários testes de integração (status 400/403/404/409) |
| E-30 | Rate limiting | `src/TickestPristine.Web.Api/Extensions/RateLimitingExtensions.cs:11-56`; `appsettings.json:36-45` | — | — (os testes elevam o limite: `IntegrationTestWebAppFactory.cs:44-45`) |
| E-31 | Seed de administrador, organização e exemplos | `src/TickestPristine.Infrastructure/Database/Seeding/*.cs` | — | `DatabaseSeedingTests.cs` |
| E-32 | Roles fixas e permissões padrão | `src/TickestPristine.Infrastructure/Roles/RoleConfiguration.cs:23-26`; `RolePermissionConfiguration.cs:33-51` | — | `DatabaseSeedingTests.cs:20,44` |
| E-33 | Regras de camadas | `tests/TickestPristine.ArchitectureTests/Layers/LayerTests.cs` | `LayerTests` | (o próprio) |
| E-34 | Gatilhos do CI (push e pull request para a `main`, disparo manual) e `Publish` só fora de PR | `.github/workflows/build.yml:3-10`, `:39-41` | — | — (validado localmente; no GitHub, pendente) |
| E-35 | Seq e enrichers em Development | `src/TickestPristine.Web.Api/appsettings.Development.json:16-23` | — | — |
| E-36 | Fechamento de chamado | `src/TickestPristine.Application/Tickets/Close/CloseTicketCommandHandler.cs`; `src/TickestPristine.Web.Api/Endpoints/Tickets/Close.cs` | `CloseTicketCommandHandler` | `CloseTicketCommandHandlerTests.cs`, `TicketsTests.cs` |
| E-37 | Atribuição de responsável | `src/TickestPristine.Application/Tickets/Assign/AssignTicketCommandHandler.cs`; `src/TickestPristine.Web.Api/Endpoints/Tickets/Assign.cs`; `TicketErrors.AssigneeCannotManageTickets`; `TicketReassignedDomainEvent`; `TicketHistoryEntries.Reassigned` | `AssignTicketCommandHandler` | `AssignTicketCommandHandlerTests.cs`, `TicketsTests.cs` |
| E-38 | Modelo de PR alinhado ao fluxo SDD | `.github/pull_request_template.md` | — | — |
| E-39 | Fluxo SDD, estados e skills | `CLAUDE.md` ("Política de autonomia", "Fluxo de desenvolvimento (SDD)"); `.claude/skills/spec-new/SKILL.md`, `references/spec-template.md`, `references/spec-status.md`; `.claude/skills/spec-implement/SKILL.md` (`disable-model-invocation: true`); `.claude/skills/spec-verify/SKILL.md` | — | — (só a `spec-new` foi executada) |
| E-40 | Primeira spec, implementada, verificada e entregue | `specs/001-remover-responsavel/spec.md` (aprovada no commit `8660490`; `Verificada` em 23/10/2026; entregue no commit `b0e78b0`) | — | Rastreabilidade AC → teste na seção 8 da spec |
| E-43 | Remoção do responsável | `src/TickestPristine.Application/Tickets/Unassign/UnassignTicketCommandHandler.cs`; `src/TickestPristine.Web.Api/Endpoints/Tickets/Unassign.cs`; `src/TickestPristine.Domain/Tickets/TicketUnassignedDomainEvent.cs`; `TicketHistoryAction.cs` (`Unassigned = 11`) | `UnassignTicketCommandHandler` | `UnassignTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Unassign_*) |
| E-44 | Histórico da alteração da descrição (spec 002) | `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs` (comparação ordinal e registro `DescriptionChanged`); `TicketHistoryEntries.cs` (`DescriptionChanged`); `TicketHistoryAction.cs` (`DescriptionChanged = 12`) | `UpdateTicketCommandHandler` | `UpdateTicketCommandHandlerTests.cs`, `TicketsTests.cs` (Update_*) |
| E-41 | Portão SDD por hook de projeto | `.claude/hooks/sdd_gate.py`; `.claude/settings.json` | — | v1: 27 testes e 4 cenários de ponta a ponta (Write em `src/` e `echo >` em `tests/` bloqueados com `acceptEdits`; `/spec-implement` registrado pelo `UserPromptSubmit`; escrita liberada com `[sdd:autorizo]`). v2: `.claude/hooks/test_sdd_gate.py` com 33 testes aprovados em Windows e em Linux (`python:3.12-slim`); 6 cenários de ponta a ponta numa worktree descartável, todos em 22/10/2026: Write em `src/`, `f=src/…; echo x > $f` no Bash e `Set-Content tests\…` no PowerShell bloqueados pelo hook; `git diff -- src tests > log` liberado; Write com `[sdd:autorizo]` liberado; com o interpretador inexistente, Write com `[sdd:autorizo]` bloqueado pelo `|| exit 2`. Também observado: o hook bloqueou um subagente e esta sessão; o build com 0 avisos e os 699 testes da solução aprovados na mesma data |
| E-42 | Teste de contorno do portão (comportamento do modelo) | `claude -p` numa worktree descartável, antes do hook, em 22/10/2026 | — | Pedido com spec aprovada (1 execução): nenhuma escrita, encaminhado para `/spec-implement 001`. Pedido sem spec ("arquivar um chamado", 3 execuções): a `spec-new` acionada nas três, só escrita tentada em `specs/`, nenhuma skill `add-*`, nenhuma tentativa em `src/` ou `tests/` |

---

## 9. Limitações, pendências e perguntas em aberto

### 9.1 Estado do working tree analisado

**[Fato]** No momento da análise, as mudanças abaixo ainda não estavam commitadas. Todas foram registradas depois no commit `b2028ae` (22/10/2026), exceto o `prompt-sla-tickest.md`, que continua fora do Git.

Arquivos modificados (19): handlers de `TicketAttachments`, `TicketHistories`, `TicketMessages` e `Tickets` (Delete, GetOwnSummary, Reopen), `Domain/Tickets/TicketHistory.cs`, `TicketConfiguration.cs`, `TicketHistoryConfiguration.cs`, o snapshot do modelo e 4 arquivos de teste. Arquivos novos: as migrations `20261022022728_Derive_Ticket_History_Description` e `20261022023422_Store_Ticket_Enums_As_Names`, `TicketHistoryDescriptionsTests.cs` e `prompt-sla-tickest.md` (este continua fora do Git). **[Fato]** Essas mudanças retiram a coluna `description` do histórico e passam a gravar os enums como texto. Este documento descreve o estado **com** essas mudanças aplicadas.

**Revisão de 22/10 (registrada no mesmo commit `b2028ae`):** arquivos novos `Application/Tickets/Close/*`, `Application/Tickets/Assign/*`, `Domain/Tickets/TicketReassignedDomainEvent.cs`, `Web.Api/Endpoints/Tickets/Close.cs` e `Assign.cs`, `CloseTicketCommandHandlerTests.cs` e `AssignTicketCommandHandlerTests.cs`. Arquivos alterados: `TicketErrors.cs`, `TicketHistoryEntries.cs`, `Endpoints/Users/Register.cs` e `Permissions.cs`, `TicketValidatorsTests.cs`, `BaseIntegrationTest.cs` (o cadastro passa pelo administrador), `UsersTests.cs`, `AuditLogsTests.cs`, `TicketsTests.cs`, `README.md`, `CLAUDE.md` e `prompt-sla-tickest.md` (este continua fora do Git). Nenhuma migration nova: as mudanças não alteram o modelo do banco.

### 9.2 Áreas não inspecionadas ou inspecionadas parcialmente

- O conteúdo SQL das migrations antigas (só nomes e o efeito final pelas configurações EF).
- `.claude/skills/**` (lidas só parcialmente) e os diretórios `.vs/`, `bin/`, `obj/` e `.containers/` (ignorados).
- Os testes foram contados e amostrados pelos nomes. As asserções de cada teste não foram revisadas uma a uma.
- `appsettings` de outros ambientes: **não existem** além de `appsettings.json` e `appsettings.Development.json`.

### 9.3 Contradições entre código e documentação

| Fonte | O que afirma | O que o código mostra |
|---|---|---|
| `CLAUDE.md` | Antes de 22/10: domínio de exemplo "Todos + Users" | **Resolvido em 22/10:** os caminhos e a porta do Seq foram corrigidos no commit `b2028ae`, e a seção "O que é" passou a descrever o domínio de chamados no commit `13de660`; os exemplos de Todos continuam no arquivo e nas skills, identificados como ilustrativos |
| `README.md` | Antes de 22/10: permissões por usuário "sem papéis fixos", `AdminMaster`, "exclusão" de usuários, ciclo até `Closed` e atribuição | **Resolvido em 22/10**: o README foi alinhado ao código |
| `prompt-sla-tickest.md` (não versionado; cópia em `specs/_fontes/prompt-sla.md` desde 23/10) | Antes de 22/10: SQL Server, MediatR, Quartz, `PublishDomainEventsInterceptor`, `AuditableEntity` e `RowVersion` | **Corrigido em 22/10**: cita PostgreSQL, handlers próprios, `BackgroundService` e advisory lock, `IAuditable` e `Version`/`xmin`. **Divergências que continuam:** a regra 4 proíbe construtores primários, mas o código usa construtores primários em todos os handlers; o prompt também cita entidades que não existem (categorias, multi-tenancy) e já instrui o agente a avisar se faltarem |
| `SampleDataSeeder.cs:206` (comentário) | Histórico gravado por "handlers de evento de domínio" | Histórico gravado pelos handlers de command |
| `GetTicketsQueryHandler.cs:66` (comentário) | Chama o cálculo de situação do SLA de "Regra do Domain" | A regra fica na Application (`TicketSla` está em `src/TickestPristine.Application/Tickets/TicketSla.cs`) |

### 9.4 Comportamentos não validados

- Funcionamento com Redis (`ConnectionStrings:Cache`) e invalidação entre instâncias.
- Exportação OTLP e envio de logs ao Seq.
- Download de anexo cujo arquivo não existe mais no disco.
- Comportamento do Serilog com enrichers sem pacote.
- Comportamento atrás de proxy reverso (IP do rate limit, esquema HTTPS).
- Execução da imagem Docker (o Dockerfile não foi construído nesta análise).
- Execução do workflow de CI disparado por pull request no GitHub (seção 6.8).
- **[Validado em 23/10/2026]** Execução da `spec-implement` e da `spec-verify`, com a spec 001 (seção 6.9). O efeito de `disable-model-invocation` na `spec-implement` já tinha sido observado (seção 6.9).

### 9.5 Perguntas em aberto (decisão humana)

| # | Pergunta | Relacionada a |
|---|---|---|
| Q-01 | **Respondida em 22/10:** o atendente (`tickets:manage`) fecha os chamados resolvidos manualmente. Implementado. | GAP-01 |
| Q-02 | **Respondida em 22/10:** o cadastro exige `users:create`. Implementado. | GAP-03 |
| Q-03 | O solicitante (Colaborador com `tickets:update-own`) deve poder mover o próprio chamado para Em andamento, Resolvido ou Cancelado? Hoje pode. | 5.4 |
| Q-04 | O responsável (`AssignedToUserId`) que não tem `tickets:manage` deve poder tratar o chamado (mudar status, reabrir)? Hoje só pode ver e conversar. | GAP-02 |
| Q-05 | **Respondida em parte (22/10):** depois da abertura, só quem tem `tickets:manage` atribui, e só a um usuário ativo com `tickets:manage`. Falta decidir se a mesma regra deve valer para o responsável indicado **na abertura**, que hoje aceita qualquer usuário ativo indicado por quem tem `tickets:create`. | C-08 |
| Q-06 | A prioridade, o título e o setor devem ser editáveis? Se a prioridade for, o SLA deve ser recalculado (`TicketSla.cs:8-9` já prevê isso)? | C-09 |
| Q-07 | Mensagens e anexos devem ser bloqueados em chamados Resolvidos, Fechados ou Cancelados? | 5.6 |
| Q-08 | Qual é a política de retenção para anexos excluídos, logs de auditoria e chamados excluídos logicamente (inclusive por exigência da LGPD)? | GAP-08 |
| Q-09 | O `CLAUDE.md` deve descrever o domínio real ou permanecer como guia genérico do template com exemplos de Todo? | GAP-06 |
| Q-10 | **Respondida em parte (23/10, Rafael Santos, no chat):** as fases do `prompt-sla-tickest.md` entraram no backlog (`specs/BACKLOG.md`, BL-004 e BL-012 a BL-018), e o prompt foi copiado como fonte para `specs/_fontes/prompt-sla.md`; a prioridade de cada fase fica no backlog e o escopo, em cada spec. Antes, já tinham sido resolvidas a stack (seção 9.3) e, na spec 004, os construtores primários (DEC-2) e a ordem do outbox (DEC-4). Faltam as decisões sobre multi-instância (Q-11) e notificações. | C-15 |
| Q-11 | Qual é o modelo de implantação alvo: uma ou várias instâncias, proxy reverso, armazenamento de arquivos? | GAP-09, GAP-10, GAP-11 |
| Q-12 | Departamentos e setores desativados devem poder ser reativados? O responsável pelo departamento deve ser um usuário ativo? | C-07 |

---

_Documento gerado por análise estática e execução local de build e testes. Para atualizá-lo incrementalmente, revise primeiro a matriz 2.7, a seção 7 e o catálogo 8, que concentram as afirmações sujeitas a mudança._
