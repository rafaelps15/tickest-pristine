# SPEC 049 — Anexo B: inventário e destinação

> Parte da [SPEC 049](spec.md). O estado, as decisões e a aprovação ficam no `spec.md`; este anexo não tem estado próprio. Os inventários das áreas, como foram escritos na investigação, e a cobertura de cada componente pelas etapas do plano (B.7). O mapeamento dos componentes de chamados está no `spec.md` (7.6). No fim, as evidências das rodadas de revisão (B.9). A B.10 traz a relação integral das decisões por situação, gerada da seção 5 do `spec.md` e do anexo E.

## 2.3 Inventário do estado atual (chamados)

| Camada | Componentes | Qtd. |
|---|---|---|
| Domain — chamado | `Ticket` (518 linhas, raiz); `TicketErrors`; enums `TicketStatus`, `TicketPriority`, `TicketWaitingOn`, `TicketSlaStatus`; auxiliar `TicketStatusNames`; eventos `TicketCreated`, `TicketDescriptionChanged`, `TicketStatusChanged`, `TicketReopened`, `TicketReassigned`, `TicketUnassigned`, `TicketDeleted` | 14 arquivos |
| Domain — SLA | `TicketSlaTimer` (198, raiz própria); `TicketSlaCycleService` (200, serviço de domínio); records `TicketSlaPlan` (103), `SlaDeadlines`, `TicketReopening`; enums `SlaMetric`, `SlaCompletionCause`, `SlaObjectiveState`; evento `TicketSlaBreached` | 9 |
| Domain — mensagem | `TicketMessage` (85, raiz própria); `TicketMessageErrors`; 3 eventos | 5 |
| Domain — anexo | `TicketAttachment` (162, raiz própria); `TicketAttachmentErrors`; 2 eventos | 4 |
| Domain — histórico | `TicketHistory` (48, registro só de inclusão); `TicketHistoryAction` (13 ações); `TicketHistoryRecordedDomainEvent` (nunca disparado) | 3 |
| Domain — calendários | `BusinessCalendar` (170); `DefaultSlaCalendar`, `ContinuousSlaCalendar`, `SlaCalendarIds`, `BrazilianNationalHolidays`, `BrazilianHoliday`, `HolidayKind`, `SlaWorkPeriod`, `SlaCalendarErrors`; `SlaCalendar` e `SlaCalendarHoliday` (anêmicas; tabelas lidas, nunca escritas) | 11 |
| Application — commands | Chamado: `Create`, `Update`, `Assign`, `Unassign`, `Close`, `Reopen`, `Delete`, `ProcessSlaBreaches` (sem rota). Mensagem: `Post`, `Edit`, `Delete`. Anexo: `Upload`, `Delete`. Todos com validator | 13 |
| Application — queries | Chamado: `GetAll`, `GetById`, `GetOwn`, `GetOwnSummary`, `GetAssignees`, `GetOverview`, `GetSla`, `GetSlaSummary`, `GetStatusCounts`, `GetTimes`, `GetVolume`. Mensagem: `GetAll`. Anexo: `GetAll`, `Download`, `GetPolicy`. Histórico: `GetAll` | 16 |
| Application — evento | `TicketAttachmentDeletedDomainEventHandler` (purga o arquivo); é o único handler de evento da área | 1 |
| Application — peças compartilhadas | `TicketAgentLookup` (instância); `TicketAssigneeEligibility`, `TicketSlaFilters`, `TicketHistoryDescriptions`, `TicketVolumePeriods`, `TicketTimeStatistics` (estáticas); `TicketScopeFilter`, `LocalDatePeriod` (records); `TicketAccessGuard` (estático, legado) | 9 |
| Application — abstrações usadas | `ITicketActivityWriter`, `IFileStorage`, `IAttachmentContentInspector`, `ISlaCalendarProvider`, `IPermissionProvider`, `IDbConnectionFactory`, paginação | 7 |
| Web.Api | 18 rotas de chamado, 4 de mensagem, 5 de anexo e 1 de histórico, mais `Endpoints/Tickets/Permissions.cs` | 28 rotas |
| Infrastructure | 5 configurações do chamado e 2 de calendário; `TicketActivityWriter`; `SlaMonitorBackgroundService` e `SlaMonitorSettings` (o único serviço hospedado); `SlaCalendarProvider` e auxiliares; `LocalFileStorage`, `AttachmentContentInspector`, `FileStorageOptions`; `PublishDomainEventsInterceptor`, `DomainEventsDispatcher`; `AuditLoggingInterceptor`; `SampleDataSeeder` e `SampleTicketCatalog`; 16 migrations com tabelas ou dados do chamado, mais 2 de permissões de chamado | ≈ 25 |
| Testes (045) | Unitários: 422 métodos `[Fact]`/`[Theory]` nas pastas do chamado, incluídos `TicketTests` (43), `TicketSlaTimerTests` (29) e `TicketSlaCycleServiceTests` (22). Integração: 243 métodos, incluídos 179 em `TicketsTests.cs`. Arquitetura: 34 métodos no projeto, incluídos `TicketSlaAggregateTests` (3) e `RichEntityTests` | — |

O esquema atual das tabelas `tickets`, `ticket_sla_timers`, `ticket_messages`, `ticket_attachments`, `ticket_histories`, `sla_calendars` e `sla_calendar_holidays` está em `ApplicationDbContextModelSnapshot.cs:615-1022` (chaves estrangeiras em 1312-1403). Pontos que pesam nesta spec:

- `tickets` tem `xmin` como token de linha (`TicketConfiguration.cs:53`), filtro de exclusão lógica (:49) e índices de prazo, criação, "meus chamados" e um índice parcial de espera (:56-68).
- `ticket_sla_timers` tem `CompletedOnUtc` e `StoppedOnUtc` como tokens (`TicketSlaTimerConfiguration.cs:21-25`), o índice único `(ticket_id, metric, cycle)` (:28), o índice parcial de fila `WHERE next_action_on_utc IS NOT NULL` (:31) e chave estrangeira em cascata para o chamado (:33-36).
- `ticket_messages` e `ticket_attachments` não têm token de concorrência.
- `ticket_histories` não tem filtro, e o autor fica nulo se o usuário for apagado (`SET NULL`).

## 2.9.1 Inventário e destinação (usuários e identidade)

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `User` | entidade anêmica (raiz de fato) | identidade, perfil, situação (`IsActive`, `DeactivatedAtUtc`), setor e código, tudo com `{ get; set; }` (`User.cs:7-15`) | refatorar | raiz rica do agregado (DEC-20): fábricas, métodos e filhos internos |
| `UserCredential` | entidade anêmica (1:0..1) | hash, senha provisória, carimbo, tentativas e bloqueio (`UserCredential.cs:7-32`) | refatorar | objeto de valor do `User`, com as regras de bloqueio e troca (REQ-103); a entidade sai na U2 (DEC-20 revista em 2026-11-04; o `Password` entra junto, decisão de 2026-11-05) |
| `UserInvitation` | entidade anêmica (1:N) | hash do token, validade, uso e revogação (`UserInvitation.cs:12-32`) | refatorar | entidade interna, criada e revogada só pelo `User` (REQ-102) |
| `UserRole` | entidade anêmica (1:N) | vínculo usuário × função pelo nome (`UserRole.cs:7-9`) | refatorar | entidade interna, mudada só por `User.AssignRoles` (REQ-104); o vínculo passa a ser pelo id da função com a spec 036 |
| `RefreshToken` | entidade anêmica | sessão: hash atual e anterior, validade, limite e carimbo (`RefreshToken.cs:7-30`) | refatorar | raiz própria e rica da sessão, em `Domain/RefreshTokens/` (REQ-101, DEC-27) |
| `UserErrors` | catálogo de erros | 21 erros de usuário, convite, credencial e sessão (corrigido na reconferência: eram 20) (`UserErrors.cs:7-89`) | manter | os códigos `Users.*` são contrato; pontuação e tipos vão para a Q-29 |
| `UserRegisteredDomainEvent` | evento | cadastro; disparado pelo handler (`RegisterUserCommandHandler.cs:44`) | manter | passa a ser disparado por `User.Register` |
| `UserActivatedDomainEvent` | evento | reativação de usuário desativado (`ActivateUserCommandHandler.cs:47`) | renomear | `UserReactivatedDomainEvent`: "ativar" tem dois sentidos (US-15) |
| `UserDeactivatedDomainEvent` | evento | desativação (`DeactivateUserCommandHandler.cs:66`) | manter | disparado por `User.Deactivate` |
| `UserProfileUpdatedDomainEvent` | evento | perfil; leva os nomes novos (`UserProfileUpdatedDomainEvent.cs:5`) | refatorar | levar os nomes anteriores (US-14) |
| `UserSectorChangedDomainEvent` | evento | setor anterior e novo (`UserSectorChangedDomainEvent.cs:5`) | manter | disparado por `User.ChangeSector` |
| `UserRolesAssignedDomainEvent` | evento | funções; disparado mesmo sem mudança (`AssignUserRolesCommandHandler.cs:89`) | refatorar | só quando muda, com as funções anteriores (US-14) |
| `UserPasswordChangedDomainEvent` | evento | troca de senha (`UserPasswords.cs:28`) | manter | disparado pelos métodos de troca do `User` |
| `UserInvitationIssuedDomainEvent` | evento | convite emitido; só o `UserId` (`UserInvitations.cs:57`) | refatorar | levar o id do convite (US-14) |
| `UserInvitationAcceptedDomainEvent` | evento | convite aceito; só o `UserId` (`ActivateUserAccountCommandHandler.cs:48`) | refatorar | levar o id do convite (US-14) |
| `RefreshTokenCreatedDomainEvent` | evento | sessão aberta (`UserSessions.cs:51`) | mover | com a sessão, disparado por `RefreshToken.Create` |
| `RefreshTokenRotatedDomainEvent` | evento | renovação (`RefreshTokenCommandHandler.cs:79`) | mover | com a sessão, disparado por `RefreshToken.Rotate` |
| `RefreshTokenRevokedDomainEvent` | evento | revogação; disparado em entidades que o handler remove e nunca despachado (US-13) | remover | DEC-26 |
| `AccessTokensResponse` | DTO de resposta | par de tokens do login, da renovação e da troca obrigatória | manter | contrato igual nas três rotas |
| `UserCodes` | classe estática | gera `usr_<ULID>` (`UserCodes.cs:9-11`), usada também pelos seeders | substituir | `IUserCodeGenerator` com implementação na Infrastructure (DEC-25) |
| `UserCredentialsVerifier` e `VerifiedCredentials` | classe estática e record | busca por e-mail, tempo equivalente, bloqueio, contagem e gravação imediata (`UserCredentialsVerifier.cs:24-88`) | dividir | regras no `User`; busca e tempo numa peça injetada (REQ-106) |
| `UserPasswords` | classe estática | grava o hash, tira a pendência, troca o carimbo, dispara o evento e revoga as sessões (`UserPasswords.cs:17-31`) | remover | métodos do `User` e remoção das sessões pelo handler |
| `UserSessions` | classe estática | abre a sessão, apaga as vencidas, revoga todas, `Earliest` (`UserSessions.cs:16-78`) | remover | `RefreshToken.Create` e handlers |
| `UserInvitations` e `IssuedInvitation` | classe estática e record | token, hash, revogação, emissão, limite e envio do e-mail (`UserInvitations.cs:20-134`) | dividir | regras no `User`; token e hash na DEC-25; envio na DEC-24 |
| `UserInvitationEmail` | classe estática | monta o e-mail do convite a partir do `User` (`UserInvitationEmail.cs:20-49`) | refatorar | recebe valores (nome, código, e-mail), não a entidade |
| `RefreshTokenHasher` | classe estática | SHA-256 dos tokens (`RefreshTokenHasher.cs:12-13`), também usado pelo convite | substituir | `ITokenHasher` (DEC-25) |
| `Register/*` (command, handler, validator) | slice de command | cadastro sem senha, função padrão, convite e e-mail depois do commit | refatorar | `User.Register` e `IssueInvitation`; envio pela peça injetada |
| `Register/UserRegisteredDomainEventHandler` | handler de evento | vazio (`UserRegisteredDomainEventHandler.cs:8-12`) | remover | código morto (US-11) |
| `ResendInvitation/*` | slice de command | reenvio com RN-17 e limite | refatorar | `EnsureCanReceiveInvitation` e `IssueInvitation` |
| `ActivateAccount/*` | slice de command | ativação anônima pelo token | refatorar | `User.ActivateAccount` |
| `Login/*` | slice de command | login com bloqueio e sessão | refatorar | verificador injetado e `RefreshToken.Create` |
| `ChangeRequiredPassword/*` | slice de command | troca da senha provisória, com sessão | refatorar | verificador, `User.ChangeProvisionalPassword` e `RefreshToken.Create` |
| `Refresh/*` | slice de command | rotação, reuso e carimbo | refatorar | `RefreshToken.Rotate` e perguntas da sessão |
| `Logout/*` | slice de command | remove a sessão informada | refatorar | sem `Raise` no handler (DEC-26) |
| `ChangePassword/*` | slice de command | troca autenticada | refatorar | `User.ChangePassword`; bloqueio na DEC-23 |
| `UpdateProfile/*` | slice de command | perfil | refatorar | `User.UpdateProfile` devolvendo `bool` |
| `Activate/*` | slice de command | reativação de usuário desativado | renomear | `Reactivate/*` com `User.Reactivate`; rota igual (DEC-27) |
| `Deactivate/*` | slice de command | desativação, convites, sessões, RN-17 e RN-18 | refatorar | `User.Deactivate` e remoção das sessões no mesmo save |
| `AssignRoles/*` | slice de command | troca das funções com RN-17 e RN-18 | refatorar | `User.AssignRoles`; os guards ficam com a 2.10; ids das funções com a spec 036 |
| `ChangeSector/*` | slice de command | setor com RN-17 | refatorar | `User.ChangeSector` devolvendo `bool` |
| `GetAll/*` (`GetUsersQuery`, handler, `UserSortField`, `UserStatusFilter`, `UserSummaryResponse`) | slice de query | listagem paginada com filtros, setor e pendência | converter | Dapper na 043p1, com as situações da spec 037 e a regra com nome (US-05) |
| `GetAttendants/*` | slice de query | atendentes elegíveis, com `users:read` no handler | converter | Dapper na 043p1 (DEC-11 da 043); a query vira `GetAttendantsQuery` (DEC-27) |
| `GetByEmail/*` | slice de query | usuário pelo e-mail | converter | 043p1; o uso da rota fica para a Q-24 |
| `GetById/*` | slice de query | detalhe com setor e pendência | converter | 043p1 |
| `GetCurrent/*` | slice de query | usuário logado com funções e permissões | converter | 043p1 |
| `GetStatusCounts/*` | slice de query | totais por situação num comando SQL | converter | 043p1, com a contagem da spec 037; resposta como `record`, igual às outras (US-15) |
| `IPasswordHasher` | interface | hash e conferência de senha | manter | ganha `<summary>`; a entidade confere pelo `IPasswordHashChecker` do Domain (padrão 5) |
| `ITokenProvider` e `GeneratedRefreshToken` | interface e record | JWT e refresh token | refatorar | gera também o token do convite (DEC-25); ganha `<summary>` |
| `IUserContext` | interface | id do usuário da requisição | manter | ganha `<summary>` na interface |
| `IEmailSender` e `EmailMessage` | interface e record | envio de e-mail | manter | — |
| `IActivationLinkBuilder` | interface | link com o token no fragmento | manter | — |
| Endpoints `Login`, `Refresh`, `ChangeRequiredPassword`, `ActivateAccount` | endpoints anônimos com rate limit `authentication` | autenticação e ativação | manter | contrato (o transporte do refresh token é da spec 046); sai o `<summary>` do DTO de `ActivateAccount.cs:11-13` |
| Endpoints `Logout`, `GetCurrent` | endpoints com token válido (aceitam desativado) | sair; dados do logado | manter | — |
| Endpoints `Register`, `ResendInvitation`, `Activate`, `Deactivate`, `AssignRoles`, `ChangeSector` | endpoints com `.HasPermission` | gestão de usuários | manter | contrato; `Activate.cs` vira `Reactivate.cs` com a mesma rota (DEC-27); sai o `<summary>` do DTO de `Register.cs:11-13` |
| Endpoints `ChangePassword`, `UpdateProfile`, `GetById`, `GetAttendants` | endpoints com `.RequireAuthorization()` e conferência no handler | troca de senha, perfil e leituras | manter | — |
| Endpoints `GetAll`, `GetByEmail`, `GetStatusCounts` | endpoints com `users:read` | leituras administrativas | manter | `GetByEmail` sem consumidor no front (Q-24) |
| `Endpoints/Users/Permissions.cs` | constantes | permissões das rotas | manter | — |
| `PasswordHasher` | implementação | PBKDF2-SHA512, 500 mil iterações, comparação em tempo constante (`PasswordHasher.cs:8-31`) | refatorar | implementa também o `IPasswordHashChecker` |
| `TokenProvider` | implementação | JWT (`sub`, `email`, `name`) e refresh token em Base64 (`TokenProvider.cs:16-52`) | refatorar | gera também o token do convite, com a mesma codificação (US-10) |
| `JwtOptions` e `ConfigureJwtBearerOptions` | configuração | segredo, emissor, audiência, validades, `ClockSkew` zero | manter | `RefreshTokenExpirationInDays` acima de 30 é cortado em silêncio (US-10) |
| `TokenClaimTypes` e `ClaimsPrincipalExtensions` | auxiliares | claims gravadas e leitura do id | manter | ver 2.9.6 (mapeamento do `sub`) |
| `UserContext` e `UserContextUnavailableException` | implementação | id do usuário; 401 sem contexto | manter | — |
| `UserConfiguration` | configuração EF | `citext`, checks, índices, FK do setor (`UserConfiguration.cs:10-48`) | refatorar | navegações dos filhos (DEC-20) e `ValueGeneratedNever()` (US-20) |
| `UserCredentialConfiguration` | configuração EF | índice único por usuário, carimbo padrão | refatorar | mapeada como filho |
| `UserInvitationConfiguration` | configuração EF | hash único, índice `(user_id, created_at_utc)`, tokens de uso e revogação | refatorar | mapeada como filho; tokens iguais |
| `UserRoleConfiguration` | configuração EF | único por usuário × função, FK pelo nome | refatorar | mapeada como filho |
| `RefreshTokenConfiguration` | configuração EF | hash único e token de concorrência, índice do hash anterior | mover | `Infrastructure/RefreshTokens/` (DEC-27) |
| `SmtpEmailSender` | implementação | SMTP com MailKit e TLS oportunista (`SmtpEmailSender.cs:26-30`) | refatorar | TLS obrigatório com `UseTls` (DEC-28) |
| `EmailOptions` | configuração | servidor e remetente | manter | o comentário de `UseTls` passa a valer (US-07) |
| `ActivationLinkBuilder`, `InvitationOptions` e `InvitationOptionsValidator` | implementação e configuração | link com fragmento; HTTPS fora de Development | manter | — |
| `DependencyInjection` (autenticação e e-mail) | registro | serviços de identidade e e-mail | refatorar | registra as abstrações da DEC-25; comentário sem citação de spec |
| `AdminUserSeeder` e `AdminUserOptions` | seed | primeiro administrador com senha provisória | refatorar | fábrica do `User`; "conta utilizável" da DEC-21 |
| `SampleDataSeeder` (parte de usuários) | seed | atendentes e colaboradores de exemplo com credencial (`SampleDataSeeder.cs:131-150`) | refatorar | fábrica do `User` |
| Migrations `Initial`, `Add_User_Dates`, `Add_User_Citext`, `Hash_Refresh_Tokens`, `Add_Credential_Must_Change_Password`, `Role_Name_As_Key`, `Add_Ticket_Sla_And_Pagination_Indexes`, `Add_Session_Limits_And_Login_Lockout`, `Add_Ids_To_Roles_And_Permissions`, `Add_User_Sector_And_Tickets_Assign_Permission`, `Add_User_Invitations`, `Add_Ticket_User_Text_Length_Constraints` | migrations | esquema das cinco tabelas | manter | histórico; nenhuma migration nova na fase EF (REQ-114) |
| Testes unitários de handlers e consultas de `Users/` (19 classes, 131 métodos) | testes | handlers e queries | refatorar | montar pelo domínio; mesmas asserções |
| `UserValidatorsTests` (52 métodos) | testes | validators | manter | — |
| `RefreshTokenHasherTests` e `UserCodesTests` | testes | auxiliares estáticos | substituir | testes das implementações da DEC-25 |
| `UsersTests` (111), `UserInvitationsTests` (17), `RefreshTokenStorageTests` (1), `UserIsActiveTests` (1), `DatabaseSeedingTests` (5) e os auxiliares do `BaseIntegrationTest` | testes de integração | ponta a ponta e banco | manter | asserções iguais; arranjos podem mudar; entram os testes de concorrência e da DEC-21 |
| `RichEntityTests` | teste de arquitetura | lista das entidades ricas (`RichEntityTests.cs:12-21`) | refatorar | incluir as cinco entidades |
| `PrivilegeEscalationGuard` e `AdministratorGuard` (uso nos slices de usuário) | guards estáticos (legado) | RN-17 e RN-18 | manter | destino na 2.10; a DEC-21 muda o "ativo" do último administrador |
| `TicketAssigneeEligibility` e `TicketAgentLookup` (uso em `GetAttendants`) | filtro estático e peça de chamados | atendente elegível | manter | destino na 043p1 (DEC-10 e DEC-11 dela) e na área de chamados; a DEC-21 muda o "ativo" |

## 2.10.1 Inventário e destinação (funções, permissões, autorização e segurança)

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `Domain/Roles/Role.cs` | entidade anêmica (`{ get; set; }`) | nome da função; o token `Version` existe só na configuração (`RoleConfiguration.cs:21`) | refatorar | raiz rica com a coleção de `RolePermission` (DEC-31); id e CRUD na 036 |
| `Domain/Roles/RolePermission.cs` | entidade anêmica, `IAuditable` | vínculo função × permissão pelo nome e pelo código (`:8-9`) | converter | entidade interna da `Role`, com `internal static Create` (DEC-31) |
| `Domain/Roles/Permission.cs` | entidade anêmica | espelho do catálogo na tabela `permissions`; nenhum código da Application a lê | converter | dado de referência imutável, só para a chave estrangeira (AZ-13) |
| `Domain/Roles/RoleErrors.cs` | catálogo de erros | `NotFound(nome)`, `PrivilegeEscalation`, `AdministratorRoleIsManaged`, `LastAdministrator` | manter | a 036 acrescenta os erros de criar, renomear e excluir |
| `Domain/Roles/RolePermissionsAssignedDomainEvent.cs` | evento | disparado pelo handler (`AssignRolePermissionsCommandHandler.cs:69`); sem consumidor | manter | passa a ser disparado pela `Role` (REQ-204) |
| `Domain/Users/UserRole.cs` | entidade anêmica, `IAuditable` | vínculo usuário × função pelo nome | converter | entidade interna do `User` (DEC-31; desenho na 2.9) |
| `Application/Roles/AssignPermissions/AssignRolePermissionsCommand.cs` | command | nome da função e códigos; `using` sem uso (`:3`) | manter | rota por id na 036 |
| `Application/Roles/AssignPermissions/AssignRolePermissionsCommandHandler.cs` | command handler (legado) | carrega pelo nome, recusa o Administrador, aplica a RN-17, monta os vínculos, dispara o evento e invalida o cache | refatorar | `Role.ReplacePermissions`; escalada injetada; membros lidos depois do commit (AZ-10, AZ-11) |
| `Application/Roles/AssignPermissions/AssignRolePermissionsCommandValidator.cs` | validator | nome obrigatório; todo código no catálogo | manter | — |
| `Application/Roles/GetAll/{GetRolesQuery,GetRolesQueryHandler,RoleResponse}.cs` | query slice | lista as funções; "padrão" e "administrador" pelo nome (`GetRolesQueryHandler.cs:19-20`) | manter | SQL na 043p1; ids e contagens na 036 |
| `Application/Roles/RoleNames.cs` | constantes | nomes das três funções fixas usados em regras e seeds | substituir | `RoleIds` da 036 (DEC-9 dela) |
| `Application/Permissions/PermissionCodes.cs` | constantes | códigos por área | manter | fonte dos códigos (desvio "Permissões na Application") |
| `Application/Permissions/PermissionCatalog.cs` (+ `PermissionCatalogEntry`) | catálogo estático | nome, descrição, grupo e marca administrativa (padrão `true`, `:59-64`); fonte da tabela pelas migrations | manter | fonte única; teste catálogo × tabela (REQ-206) |
| `Application/Permissions/GetAll/*` (3 arquivos) | query slice | devolve o catálogo sem ler o banco | manter | — |
| `Application/Authorization/TicketAccessGuard.cs` | classe estática com consulta (legado) | RN-07 em seis consultas, com 404 antes de 403 | converter | peça injetada da DEC-9 da 043 nas consultas; `Ticket.CanBeViewedBy` nos commands (DEC-30, DEC-39) |
| `Application/Authorization/PrivilegeEscalationGuard.cs` | classe estática com consulta (legado) | RN-17 em seis handlers; permissões do alvo lidas das tabelas | converter | instância injetada `PrivilegeEscalationCheck` (DEC-30, DEC-39); alvo pela leitura sem cache (DEC-32) |
| `Application/Authorization/AdministratorGuard.cs` | classe estática com consulta e efeito (legado) | RN-18; incrementa o token `Version` da função Administrador | converter | instância injetada `LastAdministratorCheck`, com o efeito explícito; trava de linha na 043p2 (DEC-33) |
| `Application/Abstractions/Authorization/IPermissionProvider.cs` | interface sem `<summary>` | permissões efetivas (em cache), "usuário ativo" e invalidação | refatorar | `<summary>`; leitura sem cache (DEC-32) |
| `Application/Abstractions/Authorization/PermissionProviderExtensions.cs` | extensão estática | "o próprio usuário ou a permissão", sem consulta própria (2 usos) | manter | função pura sobre a abstração |
| `Application/Tickets/TicketAgentLookup.cs` | instância injetada | "quem atende" lido das tabelas, sem cache | refatorar | usa a leitura sem cache do provedor (DEC-32); o nome é da F-28 |
| `Application/Tickets/TicketAssigneeEligibility.cs` | extensão estática de `IQueryable` | filtro dos atendentes elegíveis | substituir | SQL na 043p1 (DEC-10 e DEC-11 da 043), com teste de concordância |
| `Application/Users/{AssignRoles,Deactivate,Activate,ChangeSector,ResendInvitation}/*CommandHandler.cs` | command handlers (legado; inventário na 2.9) | aplicam a RN-17 e, dois deles, a RN-18 | refatorar | usam as peças injetadas |
| `Application/Users/UpdateProfile/UpdateUserProfileCommandHandler.cs` | command handler (2.9) | o próprio usuário ou `users:update`; sem RN-17 | refatorar | DEC-35 (AZ-05) |
| `Application/Users/Register/RegisterUserCommandHandler.cs` | command handler (2.9) | concede a função padrão sem RN-17 | refatorar | DEC-35 (AZ-05) |
| `Application/Users/GetAttendants/GetBacklogAttendantsQueryHandler.cs` | query handler (2.9) | `users:read` conferida no handler | manter | Q-35 |
| `Application/Tickets/Create/CreateTicketCommandHandler.cs` | command handler | abrir em nome de outra pessoa exige `tickets:manage`; o responsável não é conferido | refatorar | DEC-35 (AZ-06) |
| `Application/Tickets/Delete/DeleteTicketCommandHandler.cs` | command handler | `tickets:delete` conferida no handler | manter | F-29, Q-01 |
| `Application/Users/UserCredentialsVerifier.cs` | classe estática (legado; 2.9) | bloqueio por tentativas | refatorar | contador atômico; resposta da DEC-38 |
| `Infrastructure/Authorization/PermissionProvider.cs` | implementação | permissões efetivas e "ativo" com `HybridCache`; invalidação | refatorar | consulta única e leitura sem cache (DEC-32); SQL na 043p2 ou p3 (seção 5 da 043) |
| `Infrastructure/Authorization/PermissionCacheKeys.cs` | constantes | chaves do cache | manter | — |
| `Infrastructure/Authorization/PermissionAuthorizationHandler.cs` | handler de autorização | confere a permissão; cria um escopo; não repassa cancelamento | refatorar | AZ-18 |
| `Infrastructure/Authorization/ActiveUserAuthorizationHandler.cs` | handler de autorização | confere "ativo" (em cache); cria um escopo | refatorar | AZ-18 |
| `Infrastructure/Authorization/{ActiveUserRequirement,PermissionRequirement}.cs` | requisitos | marcadores das policies | manter | — |
| `Infrastructure/Authorization/PermissionAuthorizationPolicyProvider.cs` | provedor de policy | nome não registrado vira policy de permissão (falha fechada) | manter | ganha teste unitário (AZ-17) |
| `Infrastructure/Authorization/HasPermissionAttribute.cs` | atributo | `AuthorizeAttribute` com o código como policy | manter | — |
| `Infrastructure/Authorization/AuthorizationPolicies.cs` | constantes | `AuthenticatedUser` | manter | — |
| `Infrastructure/DependencyInjection.cs` (`AddCaching`, `AddAuthenticationInternal`, `AddAuthorizationInternal`) | composição | cache de 5 min com cópia local de 1 min; policy padrão; sem `FallbackPolicy` | refatorar | DEC-34 |
| `Infrastructure/Authentication/ConfigureJwtBearerOptions.cs` | configuração | emissor, audiência, chave e `ClockSkew = 0`; `RequireHttpsMetadata = false`; sem `ValidAlgorithms` | refatorar | DEC-37 |
| `Infrastructure/Authentication/JwtOptions.cs` | opções | segredo de pelo menos 32 caracteres; access token de 1 a 1.440 min; refresh de 1 a 365 dias | refatorar | teto da DEC-36 |
| `Infrastructure/Authentication/{TokenProvider,PasswordHasher,UserContext,ClaimsPrincipalExtensions,TokenClaimTypes,UserContextUnavailableException}.cs` | autenticação (inventário principal na 2.9) | JWT só com identidade; PBKDF2; id do usuário pelo `sub` | manter | SE-05 (formato do hash) fica registrado |
| `Infrastructure/Roles/RoleConfiguration.cs` | configuração EF | chave alternativa no nome; token `Version`; seed das três funções | refatorar | 036 (id, `citext`); nome do token num lugar só (REQ-207) |
| `Infrastructure/Roles/PermissionConfiguration.cs` | configuração EF | seed pelo catálogo; o comentário diz que o código "vai nas claims" (`:15`), o que é falso | refatorar | REQ-206, REQ-219 |
| `Infrastructure/Roles/RolePermissionConfiguration.cs` | configuração EF | chave estrangeira pelo nome; seed das permissões padrão | refatorar | mapeamento pela coleção da `Role` (DEC-31); id na 036 |
| `Infrastructure/Users/UserRoleConfiguration.cs` | configuração EF | chave estrangeira pelo nome | refatorar | coleção do `User` (DEC-31); id na 036 |
| `Infrastructure/Database/Seeding/AdminUserSeeder.cs` | seed (legado) | cria o primeiro administrador só sem administrador ativo | refatorar | senha pela política (REQ-216); o seed rico é de spec própria ("Em migração") |
| `Infrastructure/Database/Seeding/AdminUserOptions.cs` | opções | `Password` só com `[Required]` | refatorar | REQ-216 |
| 7 migrations: `Initial`, `Role_Seed_Only_Administrator`, `Add_Permissions_Table`, `Role_Name_As_Key`, `Add_Ids_To_Roles_And_Permissions`, `Add_User_Sector_And_Tickets_Assign_Permission`, `Replace_Ticket_Delete_Own_With_Cancel_And_Delete_Permissions` | migrations | histórico das tabelas e dos vínculos; inclusões com `ON CONFLICT DO NOTHING` | manter | migration aplicada não se edita |
| `Web.Api/Endpoints/Roles/{GetAll,AssignPermissions}.cs` | endpoints | `roles:read`; `roles:manage`, com a rota pelo nome | manter | rota por id na 036 |
| `Web.Api/Endpoints/Permissions/GetAll.cs` | endpoint | usuário ativo | manter | — |
| `Web.Api/Endpoints/{Roles,Tickets,Users,Departments,Sectors,AuditLogs}/Permissions.cs` | constantes (6) | apelidos de `PermissionCodes` para as rotas | manter | desvio declarado |
| `Web.Api/Extensions/EndpointExtensions.cs` | extensões | `HasPermission`, `RequireAuthenticatedUser`, registro dos endpoints | manter | teste da matriz (REQ-200) |
| `Web.Api/Extensions/RateLimitingExtensions.cs` | configuração | limites global, de autenticação e de anexos; partição por usuário ou IP | refatorar | 429 com corpo (REQ-214); partição atrás de proxy (Q-33) |
| `Web.Api/Infrastructure/{RateLimitingPolicies,RateLimitingOptions,FixedWindowLimitOptions,RateLimitingOptionsValidator}.cs` | opções | limites validados na subida | manter | — |
| `Web.Api/Extensions/CorsExtensions.cs` + `Infrastructure/CorsPolicyOptions.cs` | CORS | origens explícitas, loopback só pela chave, sem credenciais | manter | muda com a 046 |
| `Web.Api/Infrastructure/AuthorizeOperationFilter.cs` + `Extensions/{ServiceCollectionExtensions,ApplicationBuilderExtensions}.cs` | OpenAPI (só Development) | marca as rotas protegidas; persiste o token na interface | manter | — |
| `Web.Api/Infrastructure/GlobalExceptionHandler.cs` | erros | 500 sem detalhe; 401 sem id do usuário; 409, 400 e 413 | manter | contrato na Q-05 |
| `Web.Api/Infrastructure/CustomResults.cs` | erros | `ErrorType` → status; `Failure` mascarado | manter | contrato na Q-05 |
| `Web.Api/Middleware/RequestContextLoggingMiddleware.cs` + `Extensions/MiddlewareExtensions.cs` | log | `Correlation-Id` validado por expressão regular | manter | — |
| `Web.Api/Program.cs` | pipeline | ordem dos middlewares; sem HTTPS, HSTS nem cabeçalhos; `health` sem metadado de autorização | refatorar | REQ-201, REQ-214; Q-33 |
| `Web.Api/DependencyInjection.cs` | apresentação | Problem Details; `ThrowOnBadRequest` | manter | — |
| `Web.Api/appsettings.json` | configuração | segredos vazios; limites; `AllowedHosts: *` | manter | Q-33 |
| `Web.Api/appsettings.Development.json`, `Properties/launchSettings.json` | configuração de desenvolvimento | credenciais padrão de desenvolvimento versionadas (valores não reproduzidos) | manter | só desenvolvimento (SE-12) |
| `Application.UnitTests/Authorization/AdministratorGuardTests.cs` (1 teste) | teste | corrida pelo token no modelo em memória | refatorar | segue a peça injetada (REQ-207) |
| `Application.UnitTests/Authorization/TicketAccessGuardTests.cs` (4) | teste | RN-07 | refatorar | segue a peça da DEC-9 da 043 |
| `Application.UnitTests/Authorization/PermissionProviderExtensionsTests.cs` (2) | teste | "o próprio ou a permissão" | manter | — |
| `Application.UnitTests/Permissions/{PermissionCatalogTests,GetPermissionsQueryHandlerTests}.cs` (4 e 1) | teste | unicidade e cobertura do catálogo | manter | — |
| `Application.UnitTests/Roles/{AssignRolePermissionsCommandHandlerTests,GetRolesQueryHandlerTests,RoleValidatorsTests}.cs` (5, 2 e 4) | teste | RN-17, Administrador gerenciado, listagem e validator | refatorar | testes da `Role` (REQ-204) |
| `Application.UnitTests/Abstractions/TestDbContext.cs` | apoio de teste | `Role` com chave no nome e `RolePermission` com chave composta (`:59-69`), diferentes do modelo real | refatorar | REQ-218 (F-42) |
| `IntegrationTests/Roles/RolesTests.cs` (10), `Permissions/PermissionsTests.cs` (2) | teste | 401, 403, RN-17 e catálogo | manter | — |
| `IntegrationTests/Database/RoleConcurrencyTests.cs` (1) | teste | só o mecanismo do token no EF | refatorar | corrida pela API (REQ-207) |
| `IntegrationTests/{Cors/CorsTests,Seeding/DatabaseSeedingTests,Swagger/SwaggerTests}.cs` (3, 5 e 1) | teste | CORS, seed do administrador, segurança no OpenAPI | manter | — |
| `IntegrationTests/Users/UsersTests.cs` (casos de autorização, bloqueio, sessão, desativação, último administrador e cache) | teste | proteção das rotas de usuários | manter | ganha os casos dos REQ-210 e REQ-212 a REQ-215 |
| `ArchitectureTests` (`LayerTests` com 11, `RichEntityTests`, `HandlerShapeTests`, `EndpointTagTests`) | teste | camadas, setters, forma dos handlers, tags; nenhuma regra de autorização | refatorar | regra contra classe estática com `IApplicationDbContext` (AC-202); `Role` no `RichEntityTests` (AC-204) |

## 2.11.1 Inventário e destinação (organização e auditoria)

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `Domain/Departments/Department.cs` (54 linhas) | entidade rica, raiz; referência viva de entidade (`CLAUDE.md:123`) | nome, descrição, ativo e responsável; `Create(name, description, User?)`; `Update(...)`, que dispara o evento sempre; `Deactivate()` `void` | refatorar | `Update` passa a `bool`, com evento só quando muda e com os valores anteriores [DEC-43] (OR-04); o resto fica |
| `Domain/Sectors/Sector.cs` (53) | entidade rica, raiz | nome, descrição opcional, ativo e `DepartmentId`; `Create(Department, …)`, `Update`, `Deactivate()` | refatorar | o mesmo do `Department` [DEC-43]; continua raiz própria (DEC-40) |
| `DepartmentErrors.cs`, `SectorErrors.cs` | catálogos de erros | `NotFound` (404); `NameNotUnique`, `HasActiveTickets` e `AlreadyDeactivated` (409) | manter | todos usados; o erro da reabertura recusada (DEC-42) é do chamado |
| `DepartmentCreatedDomainEvent`, `DepartmentDeactivatedDomainEvent`, `SectorCreatedDomainEvent`, `SectorDeactivatedDomainEvent` | eventos | disparados pelas entidades, sem consumidor; o de criação do departamento é o veículo do `DomainEventsPublishingTests` | manter | convenção das skills (OR-10) |
| `DepartmentUpdatedDomainEvent`, `SectorUpdatedDomainEvent` | eventos de alteração | só o id; disparados mesmo sem mudança | refatorar | valores anteriores (padrão 10) [DEC-43] |
| `Domain/Audit/AuditLogEntry.cs` (16) | registro técnico com `{ get; set; }`, herda `Entity` | montado só pelo interceptor; lido pela consulta da trilha e pelo handler de mensagem | mover | para `Infrastructure/Audit/`, sem herdar `Entity` [DEC-47]; não é conceito de domínio (AU-06) |
| `SharedKernel/IAuditable.cs` | marcador | marca as 13 entidades auditadas | manter | vira também a lista conferida pelo teste de cobertura (REQ-308) |
| `Departments/{Create,Update,Deactivate}/*Command.cs`, `Sectors/{Create,Update,Deactivate}/*Command.cs` | commands | DTOs de entrada | manter | — |
| `CreateDepartmentCommandValidator`, `UpdateDepartmentCommandValidator`, `CreateSectorCommandValidator`, `UpdateSectorCommandValidator` | validators (`internal sealed`) | `NotEmpty` no nome, na descrição do departamento e nos ids | refatorar | recusar espaço nas pontas do nome [DEC-44] (OR-06); sem a DEC, manter |
| `DeactivateDepartmentCommandValidator`, `DeactivateSectorCommandValidator` | validators | id não vazio | manter | — |
| `CreateDepartmentCommandHandler.cs` (42) | handler | unicidade por `AnyAsync`; responsável carregado e passado ao `Create`; um save | converter | Dapper na 043p2 (S-OR-1); o fluxo fica |
| `UpdateDepartmentCommandHandler.cs` (50), `UpdateSectorCommandHandler.cs` (34) | handlers | carregam sem filtro de ativo; unicidade; responsável (só no departamento); `Update` | converter | 043p2 (S-OR-2); a edição de desativado é da 033 (Q-40) |
| `DeactivateDepartmentCommandHandler.cs` (54) | handler | já desativado (409); chamados ativos por `TicketSlaFilters.WhereActive`; cascata nos setores; um save | refatorar | "ativo" sem `TicketSlaFilters` (REQ-301); trava antes da conferência na 043p2 (REQ-300, S-OR-3) |
| `CreateSectorCommandHandler.cs` (39) | handler | departamento ativo (404 se não); unicidade no departamento | converter | 043p2 com `FOR SHARE` no departamento (S-OR-4) |
| `DeactivateSectorCommandHandler.cs` (40) | handler | já desativado; chamados ativos; `Deactivate()` | refatorar | REQ-301 e REQ-300 (S-OR-5) |
| `GetDepartmentsQuery`, `GetDepartmentsQueryHandler.cs` (35), `GetAll/DepartmentResponse.cs` | query e DTOs | departamentos ativos com o nome do responsável e os setores ativos, numa projeção | converter | SQL na 043p1; o DTO mantém o contrato, com os campos da 022 e da 023 (2.11.4) |
| `GetDepartmentByIdQuery`, `GetDepartmentByIdQueryHandler.cs`, `GetById/DepartmentResponse.cs` | query e DTO | detalhe, inclusive de desativado | converter | 043p1; Q-40 |
| `GetSectorsQuery`, `GetSectorsQueryHandler.cs`, `GetAll/SectorResponse.cs` | query e DTO | setores ativos com `DepartmentId` | converter | 043p1 |
| `GetSectorByIdQuery`, `GetSectorByIdQueryHandler.cs`, `GetById/SectorResponse.cs` | query e DTO | detalhe; 404 para desativado | converter | 043p1; Q-40 |
| `AuditLogs/GetHistory/GetAuditHistoryQuery.cs`, `AuditLogResponse.cs` | query e DTO | trilha por entidade, com cursor composto; valores JSON como texto | manter | contrato sem consumidor no front (AU-08, Q-43) |
| `GetAuditHistoryQueryHandler.cs` (35) | handler | `context.AuditLogs` por nome e id, com cursor | converter | SQL na 043p1 (S-AU-2) |
| `Endpoints/Departments/{Create,Update,Deactivate,GetAll,GetById}.cs`, `Endpoints/Sectors/{Create,Update,Deactivate,GetAll,GetById}.cs` | endpoints (10 rotas) | `HasPermission` na escrita e `RequireAuthorization` na leitura; `DELETE` desativa | manter | contrato sem mudança |
| `Endpoints/AuditLogs/GetHistory.cs` | endpoint | `GET audit-logs/{entityName}/{entityId}` com `audit:read` | manter | só o comentário de chave composta sai (AU-10) |
| `Endpoints/{Departments,Sectors,AuditLogs}/Permissions.cs` | constantes | `departments:manage`, `sectors:manage`, `audit:read` | manter | — |
| `Infrastructure/Departments/DepartmentConfiguration.cs` (30) | configuração EF | `citext`, check de 100, índice único filtrado, descrição com 500, FK do responsável com `SetNull` | manter | referência viva; a proteção concorrente vem de trava no SQL, não de token (DEC-41) |
| `Infrastructure/Sectors/SectorConfiguration.cs` (29) | configuração EF | o mesmo, com índice `(department_id, name) WHERE is_active`; FK para o departamento em cascata (padrão do EF) | manter | cascata registrada (OR-08, Q-44) |
| `Infrastructure/Audit/AuditLoggingInterceptor.cs` (96) | interceptor EF | um registro por entidade `IAuditable` no `SavingChangesAsync`; exclusão global de 3 nomes | substituir | pelo `IAuditLog` na 043p2 [DEC-45]; até lá, ajustes do REQ-16 da 049, do REQ-309 e do REQ-312 |
| `Infrastructure/Audit/AuditLogEntryConfiguration.cs` (26) | configuração EF | `jsonb`, sem FK para `users`, índices de tempo e de trilha | manter | recebe o tipo movido; continua para as migrations (043p3) |
| `Infrastructure/DependencyInjection.cs:142-152` | registro | interceptors no `ApplicationDbContext` | manter | até a 043p3 |
| `Database/Seeding/DefaultOrganizationSeeder.cs` (90) | seed | 7 departamentos e 21 setores pelo `Create`, só num banco sem departamentos | manter | já no modelo rico |
| Migrations `20261002014106_Initial` (tabelas), `20261003170333_Unique_Department_And_Sector_Names`, `20261004232321_Add_Audit_Logs`, `20261005232851_Add_Ticket_Sla_And_Pagination_Indexes` (índice da trilha), `20261026214827_Add_User_Sector_And_Tickets_Assign_Permission` (`users.sector_id`), `20261030000904_Department_Sector_Name_Length` | migrations | esquema e correções de dados | manter | histórico; nenhuma migration é editada |
| `DepartmentTests` (4), `SectorTests` (4) | testes de entidade | criação, alteração (inclusive sem mudança) e desativação | manter | as asserções do `Update` mudam com a DEC-43 |
| Testes de handler de departamento (`Create` 5, `Update` 5, `Deactivate` 5, `GetAll` 1, `GetById` 2) | unitários | falhas e caminho feliz | manter | estender (OR-12) |
| Testes de handler de setor (`Create` 5, `Update` 3, `Deactivate` 4, `GetAll` 1, `GetById` 3) | unitários | falhas e caminho feliz | manter | estender: Aberto recusa; Resolvido, Fechado e Cancelado não recusam (OR-12) |
| `DepartmentValidatorsTests` (9), `SectorValidatorsTests` (8) | validators | uma falha por regra e um command válido | manter | regra nova da DEC-44 |
| `GetAuditHistoryQueryHandlerTests` (2) | unitário de query | trilha vazia e ordem | mover | para a integração, com a 043p1 (REQ-7 dela) |
| `DepartmentsTests` (17), `SectorsTests` (17) | integração | rotas, persistência, `citext`, limites, cascata e recusas | manter | estender: edição de desativado; corridas na 043p2 |
| `AuditLogsTests` (5) | integração | 401, 403, inclusão, só as colunas alteradas, credencial sem hash | manter | estender: `Deleted`, `SecurityStamp`, mesmo instante e save que falha (AU-12) |
| Asserções de auditoria em `UserInvitationsTests.cs:71,399`, `TicketsTests.cs:4009,4523,4833` e `SlaCalendarsTests.cs:128` | integração | formato da trilha: ações, colunas, autor nulo, sem `TokenHash` | manter | rede de segurança do formato na 043p2 (AC-308) |
| `DatabaseSeedingTests.cs:117`, `DomainEventsPublishingTests.cs:18-75`, `RichEntityTests.cs:12-21` | integração e arquitetura | seed sem duplicar; eventos depois do commit (com `Department`); setters privados de `Department` e `Sector` | manter | — |
| `Application/Tickets/TicketSlaFilters.WhereActive` (dependência externa) | extensão estática de `IQueryable<Ticket>` | "chamado ativo" para os dois handlers de desativação | substituir | pela condição definida na 043 (DEC-10 e DEC-13 dela); REQ-301 |

São 96 componentes em 41 linhas. Os componentes novos do modelo-alvo (`IAuditLog` e a implementação dele) estão em 2.11.5.

## 2.12.1 Inventário e destinação (plataforma)

Destino: **Manter**; **Ajustar** (mudança local, nesta seção); **Refatorar**; **Remover**; **Substituir** (por outra peça, na spec indicada); **Congelar** (sem evolução até sair). A fase vem entre parênteses.

**SharedKernel**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `Entity` | classe base | guarda os eventos; `Raise` e `ClearDomainEvents` públicos; `DomainEvents` devolve uma cópia `List<IDomainEvent>` (`Entity.cs:3-17`) | Manter; `Raise` protegido depois da migração de `Users` (Q-58); `DomainEvents` como `IReadOnlyCollection` | F-40; 15 `Raise` ainda na Application (2.12.6) |
| `Result`, `Result<TValue>` | tipos | sucesso ou falha com `Error`; `Value` lança na falha; nulo convertido vira `Error.NullValue`, tipo `Failure`, 500 (`Result.cs:5-53`) | Manter; `Create` e `FirstFailureOrSuccess` só na primeira spec que usar (`CLAUDE.md:142`) | base da convenção de erro |
| `Error` | record | código, descrição e tipo; fábricas `Failure`, `NotFound`, `Problem`, `Conflict`, `Forbidden` (`Error.cs:3-38`) | Ajustar (P1): erros compartilhados `General.*` (REQ-401) | conflito por "zero linhas" na 043p2 |
| `ErrorType` | enum | 6 tipos, sem 401 (`ErrorType.cs:3-11`) | Manter | `Forbidden` → 403 é desvio registrado (`CLAUDE.md:144`); PL-15 |
| `ValidationError` | record | agrega os erros da validação; `FromResults` sem uso (`ValidationError.cs:3-17`) | Ajustar (P1): sai `FromResults` | PL-14 |
| `IDomainEvent`, `IDomainEventHandler<T>` | interfaces | marcador e contrato do handler de evento (`IDomainEvent.cs:3`; `IDomainEventHandler.cs:3-6`) | Manter; `<summary>` (P1) | PL-13 |
| `IDateTimeProvider` | interface | hora atual (`IDateTimeProvider.cs:3-6`) | Manter; `<summary>` (P1) | regra permanente das datas |
| `IAuditable` | marcador | entidades que o interceptor audita (13) (`IAuditable.cs:3-8`) | Manter até a 043p2 (auditoria em SQL, DEC-3 (a) da 043) | — |
| `TickestPristine.SharedKernel.csproj` | projeto | sem dependências | Manter | `LayerTests` |

**Application transversal**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `DependencyInjection` (Application) | composição | varredura do Scrutor; decorators com a Validation por fora e o Logging por dentro; handlers de evento; validators; `TicketAgentLookup` à mão; cultura e nomes globais do FluentValidation (`DependencyInjection.cs:15-44`) | Manter; ordem fixada em teste (P1, REQ-414) | a ordem é a do `CLAUDE.md:73`; o modelo de referência registra o Logging por fora (`C:\dev\clean-architecture\src\Application\DependencyInjection.cs:24-29`) |
| `ValidationDecorator` | decorator | valida o command (validators em paralelo) e devolve `ValidationError` sem chamar o handler (`ValidationDecorator.cs:10-71`) | Ajustar (P2): `property` em cada item (REQ-400) | PL-04 |
| `LoggingDecorator` | decorator | registra início e fim; toda falha em `Error` (`LoggingDecorator.cs:9-97`) | Ajustar (P1): nível por tipo e tempo (REQ-402); span e métricas (REQ-406) | PL-03, PL-10 |
| `ValidationDisplayNames` | classe estática | 30 nomes pt-BR de propriedades (`ValidationDisplayNames.cs:16-55`) | Manter; teste de cobertura (P1, REQ-416) | PL-05 |
| `ICommand`, `ICommand<T>`, `IQuery<T>`, `ICommandHandler<>`, `ICommandHandler<,>`, `IQueryHandler<,>` | interfaces | CQRS lógico sem MediatR (`Abstractions/Messaging/*.cs`) | Manter; `<summary>` (P1) | PL-13 |
| `IApplicationDbContext` | abstração | 18 `DbSet`, `Entry` e `SaveChangesAsync` (`IApplicationDbContext.cs:13-38`) | Ajustar (P1): uso restrito por teste (REQ-405); Remover (043p3) | PL-08 |
| `IDbConnectionFactory` | abstração | conexão aberta do `NpgsqlDataSource` único (`IDbConnectionFactory.cs:5-12`) | Manter: vira o único acesso a dados (043); o `<summary>` "escritas continuam pelo `IApplicationDbContext`" muda na 043p2 | DEC-2 da 043 |
| `ITicketActivityWriter` | abstração de chamado | `UPDATE` da projeção depois do commit (`ITicketActivityWriter.cs:5-21`) | Refatorar pela 049 (REQ-5 dela); Remover (043p2, S-5 da 7.7.2) | F-21; peça de chamado numa pasta transversal |
| `IUserContext`, `IPasswordHasher`, `ITokenProvider`, `GeneratedRefreshToken` | abstrações | identidade da requisição, hash e tokens | Manter; `<summary>` nos três tipos (P1) | `UserId` lança `UserContextUnavailableException`, que vira 401 |
| `IPermissionProvider`, `PermissionProviderExtensions` | abstração e extensão | permissões efetivas com cache; "o próprio usuário ou a permissão" | Manter; `<summary>` (P1) | forma híbrida do `CLAUDE.md` |
| `IEmailSender`, `IActivationLinkBuilder`, `EmailMessage` | abstrações | e-mail enviado depois do commit; falha não desfaz | Manter | sem nova tentativa (Q-52) |
| `ISlaCalendarProvider` | abstração | calendário de SLA com cache; `InvalidateAsync` sem chamador | Manter | F-39 (fora) |
| `IFileStorage`, `IAttachmentContentInspector` | abstrações | arquivos em disco e assinatura do conteúdo | Manter; `<summary>` no `IFileStorage` (P1) | GAP-09 (fora) |
| `PageRequest`, `PagedResponse<T>`, `CursorPageRequest`, `CursorPagedResponse<T>`, `SortDirection` | contratos | página, tamanho, cursor composto e direção | Manter | contrato preservado pelo REQ-3 da 043 |
| `PaginationExtensions` | classe estática | paginação sobre `IQueryable` do EF, com a normalização (`PaginationExtensions.cs:13-145`) | Substituir (043p1): mesma normalização em SQL | PL-09 |
| `TickestPristine.Application.csproj` | projeto | EF Core, Dapper, FluentValidation, Scrutor, Ulid (`csproj:4-9`) | Ajustar (043p3): sai o EF Core | DEC-4 e DEC-6 da 043 |

**Infrastructure transversal**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `DependencyInjection` (Infrastructure) | composição | cache, banco, saúde, autenticação, autorização, opções e o serviço de SLA (`DependencyInjection.cs:35-207`) | Ajustar (P1): saúde pela fonte registrada (REQ-407) | PL-11, PL-19 |
| `ApplicationDbContext` | EF | 18 `DbSet`, extensão `citext`, configurações por varredura, esquema `public` (`ApplicationDbContext.cs:13-60`) | Manter até a 043p3; depois, `DbContext` só das migrations (DEC-4 da 043) | — |
| `DbConnectionFactory` | conexão | abre do `NpgsqlDataSource` compartilhado com o EF (`DbConnectionFactory.cs:7-11`; `DependencyInjection.cs:133-134`) | Manter | um pool só |
| `DeterministicGuid`, `Schemas` | utilitários | ids fixos do `HasData`; esquema `public` (`DeterministicGuid.cs:12-16`; `Schemas.cs:3-6`) | Manter | `Add_Ids_To_Roles_And_Permissions.cs:42-54` calcula o mesmo id no banco |
| `PublishDomainEventsInterceptor` | interceptor | depois do save, coleta os eventos das entidades rastreadas e despacha (`PublishDomainEventsInterceptor.cs:12-48`) | Ajustar (P1, REQ-403); Substituir pelo despacho explícito (043p2, REQ-404) | PL-06 |
| `DomainEventsDispatcher`, `IDomainEventsDispatcher` | despacho | um escopo por evento; falha só no log (`DomainEventsDispatcher.cs:12-84`) | Manter; contador de falhas (P1, REQ-406); interface vai para a Application (043p2, DEC-52) | PL-07 |
| `DateTimeProvider` | tempo | `DateTime.UtcNow` (`DateTimeProvider.cs:5-8`) | Manter | único uso do relógio do sistema em `src/` |
| `DatabaseSeeder`, `AdminUserSeeder`, `DefaultOrganizationSeeder`, `SampleDataSeeder`, `SampleTicket`, `SampleTicketCatalog`, `AdminUserOptions`, `SeedingOptions` | seed | roda em todo `Migrate` (inclusive na ferramenta), recarrega os tipos do Npgsql, cria administrador, organização e dados de exemplo | Manter (legado em migração, `CLAUDE.md:165`); estratégia nova fora (Q-51) | DB-07 |
| `AuditLoggingInterceptor` (relacionado) | interceptor | auditoria no mesmo save, só no caminho assíncrono (`AuditLoggingInterceptor.cs:15-95`) | Ajustar pela 049 (REQ-16 dela); Substituir por SQL (043p2) | F-24 |
| `TickestPristine.Infrastructure.csproj` | projeto | inclui `Bogus` no assembly de produção (`csproj:5`) | Ajustar com a Q-51 | DB-07 |
| 31 migrations e `ApplicationDbContextModelSnapshot` | esquema | evolução do banco desde `20261002014106_Initial` (anexo abaixo) | Manter; migration nova de integridade (P3, REQ-410) | DB-01, DB-02, DB-05 |

**Web.Api (fora dos endpoints de cada área)**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `Program` | host | ordem: log de contexto → log de requisição → exceções → CORS → autenticação → autorização → rate limiter; Swagger e `Migrate` só em Development; `GET health` (`Program.cs:11-69`) | Ajustar: rate limiter (P1, REQ-408), páginas de status (P2, REQ-400), saúde (P1, REQ-407) | PL-01, PL-11, PL-12 |
| `DependencyInjection` (Web.Api) | composição | `GlobalExceptionHandler`, `AddProblemDetails`, `ThrowOnBadRequest` (`DependencyInjection.cs:7-19`) | Ajustar (P2) | REQ-400 |
| `CustomResults` | erros | `Result` → Problem Details por `ErrorType`; `title` com o código; `Failure` mascarado (`CustomResults.cs:7-57`) | Ajustar (P2): `code` e `property` (REQ-400) | PL-01 |
| `GlobalExceptionHandler` | erros | exceções → Problem Details com texto pt-BR (`GlobalExceptionHandler.cs:10-158`) | Refatorar: banco com e sem embrulho (P1, REQ-401); `IProblemDetailsService` e `code` (P2, REQ-400) | PL-01, PL-02, F-35 |
| `RequestContextLoggingMiddleware`, `MiddlewareExtensions` | correlação | `Correlation-Id` validado ou o trace id, devolvido em toda resposta (`RequestContextLoggingMiddleware.cs:7-46`) | Manter | 5 testes de integração |
| `OpenTelemetryExtensions` | observabilidade | instrumentação automática; OTLP por variável (`OpenTelemetryExtensions.cs:11-34`) | Ajustar (P1, REQ-406) | PL-10 |
| `RateLimitingExtensions`, `RateLimitingOptions`, `FixedWindowLimitOptions`, `RateLimitingOptionsValidator`, `RateLimitingPolicies` | limites | janela fixa global e 3 políticas, por usuário ou IP, em memória (`RateLimitingExtensions.cs:11-67`) | Ajustar (P1, REQ-408) | PL-12 |
| `CorsExtensions`, `CorsPolicyOptions` | CORS | origens configuradas ou qualquer loopback; sem validação das origens (`CorsExtensions.cs:11-50`) | Ajustar (P1, REQ-420); coordenar com a 046 (Q-56) | PL-19 |
| `EndpointExtensions`, `IEndpoint`, `Tags`, `Endpoints/*/Permissions.cs` (6) | endpoints | varredura, `HasPermission`, `RequireAuthenticatedUser` (`EndpointExtensions.cs:8-53`) | Manter; regra de autorização declarada (P1, REQ-416) | TE-04 |
| `ResultExtensions` (Web.Api) | extensão | `Match` (`Extensions/ResultExtensions.cs:5-22`) | Manter; renomear quando o `SharedKernel` ganhar `ResultExtensions` | PL-15 |
| `ServiceCollectionExtensions`, `ApplicationBuilderExtensions`, `AuthorizeOperationFilter` | Swagger | esquema Bearer só nas rotas protegidas; só em Development | Manter | — |
| `MigrationExtensions` | migrations | `Migrate()` síncrono no startup de Development (`MigrationExtensions.cs:8-16`) | Manter em Development; produção pela DEC-53 (P5) | DB-06 |
| `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` | configuração | valores vazios fora de Development; Seq em `http://seq:5341` e enrichers sem pacote em Development (`appsettings.Development.json:20,23`) | Manter (GAP-13 já registrado) | — |
| `Dockerfile`, `TickestPristine.Web.Api.csproj` | imagem e projeto | 4 estágios, usuário não root, tags flutuantes, sem `HEALTHCHECK` | Ajustar (P1, REQ-419) | PL-18 |

**Raiz, build, CI e ferramentas**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `Directory.Build.props` | build | `net10.0`, `AnalysisMode=All`, avisos como erro, SonarAnalyzer, `NoWarn NU1608` (`:11-21`) | Manter; reavaliar o `NU1608` | TE-07 |
| `Directory.Packages.props` | versões centrais | versões; `Serilog` sem projeto que o use (`:22`); HealthChecks 9.0.0 no .NET 10 (`:24,39`) | Ajustar (P1) | PL-14, TE-07 |
| `global.json` | SDK | 10.0.401, `latestFeature` | Manter; o CI passa a lê-lo (REQ-417) | TE-06 |
| `.editorconfig` | estilo e analisadores | 63 diagnósticos desligados (corrigido na reconferência: eram 66), entre eles CS8600, CS8603 e CS8618 (`:343-349`) | Manter nesta spec; reativação fora (Q-54) | TE-07 |
| `docker-compose.yml`, `docker-compose.override.yml`, `docker-compose.dcproj`, `launchSettings.json` (raiz), `.dockerignore` | ambiente local | API, PostgreSQL 17, Seq e Mailpit; API sem `depends_on` nem healthcheck do banco (`docker-compose.yml:4-41`) | Ajustar (P1, REQ-419) | PL-18 |
| `.github/workflows/build.yml` | CI | portão SDD, testes das instruções, auditoria, restore, build, test e publish (`build.yml:1-55`) | Ajustar (P1, REQ-417) | TE-06 |
| `.github/dependabot.yml`, `.github/CODEOWNERS`, `.github/pull_request_template.md` | governança | NuGet diário; revisor; modelo de PR | Ajustar o Dependabot (P1) | TE-06 |
| `scripts/auditoria_sdd.py` | ferramenta (só descrição) | auditoria de conformidade SDD com 32 verificações (GOV, SDD, RAS, QUA, CI, SEG, REV); sai com 1 abaixo do score mínimo; roda no CI com `--min-score 70` | Manter | — |
| `scripts/test_instructions.py` | ferramenta (só descrição) | 14 testes: paridade das skills com a biblioteca canônica (pulada no CI), ausência de menções ao projeto nas skills, regras do `CLAUDE.md` e do agente | Manter | — |
| `.claude/hooks/sdd_gate.py`, `.claude/hooks/test_sdd_gate.py`, `.claude/settings.json` | portão (só descrição) | bloqueia escrita em `src/` e `tests/` fora do `/spec-implement` com a spec `Em implementação`; 39 testes; `deny` de `.env` e `git push` | Manter | 6.9 do estado |

**Testes: infraestrutura**

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `BaseHandlerTest`, `TestDbContext`, `EntityTestState`, `RecordingLogger`, `TestSlaCalendarProvider`, `TicketTestData`, `GlobalUsings` (unitários) | base unitária | EF InMemory com mapeamento à mão; arranjos pelo domínio, com ajustes por reflexão | Congelar (P1, REQ-413); `TestDbContext` sai na 043p3 | TE-01, TE-05 |
| `PaginationExtensionsTests` (14) | teste da plataforma | regras de paginação sobre o EF | Substituir pela versão SQL (043p1) | PL-09 |
| `IntegrationTestWebAppFactory`, `IntegrationTestCollection`, `BaseIntegrationTest`, `EfCommandCounter`, `EntityTestState`, `TicketTestData`, `CapturingEmailSender`, `TestLogger`, `SlaBreachEventRecorder`, `GlobalUsings` (integração) | base de integração | Testcontainers `postgres:17`, uma coleção e um banco para todos, ambiente Development | Ajustar (P1, REQ-415) | TE-03 |
| `Database/` (8 arquivos, 13 métodos), `Middleware/` (5), `Cors/` (3), `Swagger/` (1), `Seeding/` (5), `Storage/` (7) | testes da plataforma | eventos, migrations, concorrência, tamanho de texto, correlação, CORS, Swagger, seed, inspeção de arquivos | Manter e estender (P1, REQ-414) | TE-02 |
| `BaseTest`, `Layers/LayerTests`, `Aggregates/TicketSlaAggregateTests`, `Conventions/*` (verificadores, autotestes e fixtures) | regras de arquitetura | 19 regras de produção e 15 autotestes (2.12.4) | Estender (P1, REQ-416); o `TicketSlaAggregateTests` é trocado pela 049 (F-41) | TE-04 |

**Anexo — as 31 migrations** (todas em `src/TickestPristine.Infrastructure/Database/Migrations/`; as linhas citadas são de cada arquivo)

| Migration | Família | O que faz | SQL à mão e dados | `Down` | Observação |
|---|---|---|---|---|---|
| `20261002014106_Initial` | esquema | cria 12 tabelas (roles, users, role_permissions, departments, refresh_tokens, user_credentials, user_roles, sectors, tickets, ticket_attachments, ticket_histories, ticket_messages) e as chaves estrangeiras | `InsertData` de roles e vínculos | estrutural | `users.is_active` com `DEFAULT true` (`:45`), também no modelo |
| `20261002022114_Add_User_Dates` | usuários | `created_at_utc` e `deactivated_at_utc` | `DROP DEFAULT` depois de preencher (`:23`) | estrutural | o único caso que retira o padrão temporário |
| `20261002042001_Add_User_Citext` | usuários | `citext` em nome, sobrenome e e-mail | — | estrutural | — |
| `20261002215936_Hash_Refresh_Tokens` | sessões | troca `token` por `token_hash` único | apaga os tokens (`:15`) | apaga de novo (`:48`) | `DEFAULT ''` residual (`:34`) |
| `20261003170333_Unique_Department_And_Sector_Names` | organização | `citext` e únicos parciais entre ativos | desativa setores órfãos e numera nomes repetidos (`:41-69`) | não desfaz os dados | mutação irreversível |
| `20261004223222_Add_Credential_Must_Change_Password` | usuários | `must_change_password` | — | estrutural | `DEFAULT false` residual (`:19`) |
| `20261004232321_Add_Audit_Logs` | auditoria | `audit_logs` e índices | `InsertData` de vínculo (`:34`) | `DeleteData` (`:66`) | índices refeitos na `Add_Ticket_Sla_And_Pagination_Indexes` |
| `20261005001323_Role_Seed_Only_Administrator` | acesso | tira `is_administrator` das roles | — | estrutural | o comentário cita o `DefaultRolesSeeder`, que não existe mais |
| `20261005013651_Add_Permissions_Table` | acesso | `permissions` e FK de `role_permissions` pelo código | catálogo por `InsertData`; apaga vínculos órfãos (`:69-70`); o `InsertData` das roles não administradoras foi retirado à mão (`:66-68`) | recria `is_default` | DB-04 |
| `20261005032039_Role_Name_As_Key` | acesso | chave das roles pelo nome; FKs por nome | troca de chaves em SQL (`:17-51`) | SQL inverso (`:74-112`) | revista pela 036 (FK por id) |
| `20261005232851_Add_Ticket_Sla_And_Pagination_Indexes` | chamados | `due_at_utc`, `sla_warning_at_utc`, `resolved_at_utc` e índices de paginação | prazos pela política da época e primeira resolução (`:80-110`) | estrutural | padrões sentinela residuais (`:61,76`) |
| `20261007022455_Add_Ticket_Concurrency_Token` | chamados | `xmin` como token | — | — | coluna de sistema, sem DDL efetivo |
| `20261007024215_Add_Ticket_Created_At_Index` | chamados | índice `(created_at_utc desc, id desc)` | — | estrutural | — |
| `20261008211919_Add_Id_To_Ticket_Requester_Index` | chamados | índice do solicitante com o id | — | estrutural | trocado na `Add_Ticket_Last_Activity` |
| `20261020030856_Add_Session_Limits_And_Login_Lockout` | sessões | contador de falhas, bloqueio, `security_stamp`, `session_expires_on_utc`, rotação | preenche sessão e carimbo (`:69-76`) | estrutural | padrões `Guid.Empty` e sentinela residuais (`:58,66`) |
| `20261022014002_Add_Ids_To_Roles_And_Permissions` | acesso | ids nas roles, permissões e vínculos | ids por SHA-256 em SQL (`:42-54`), iguais ao `DeterministicGuid` | estrutural | — |
| `20261022022728_Derive_Ticket_History_Description` | chamados | tira `description` do histórico | extrai o nome do anexo (`:17-22`) | remonta as frases (`:42-78`) | `Down` aproximado |
| `20261022023422_Store_Ticket_Enums_As_Names` | chamados | status, prioridade e ação como texto | `ALTER ... USING CASE` (`:16-46`) | só ações 1 a 10 (`:69-80`) | F-37 |
| `20261025012628_Add_Sla_Calendars` | SLA | `sla_calendars` e `sla_calendar_holidays` | — | estrutural | tabelas sem escrita (F-39) |
| `20261025201353_Add_Ticket_Sla_Timers` | SLA | `ticket_sla_timers`; plano em `jsonb` obrigatório | backfill idempotente com `ON CONFLICT DO NOTHING` (`:20-71`), reexecutado por teste (`TicketsTests.cs:4483-4587`) | `DropTable` (perde os cronômetros) | — |
| `20261025225544_Add_Sla_Breach_Monitoring` | SLA | `breached_on_utc`, `next_action_on_utc` e índice parcial | backfills idempotentes (`:20-35`), reexecutados por teste (`TicketsTests.cs:5192-5197`) | estrutural | — |
| `20261026214827_Add_User_Sector_And_Tickets_Assign_Permission` | usuários e acesso | `users.sector_id`; permissão `tickets:assign` | `INSERT ... ON CONFLICT DO NOTHING` (`:19-28`), reexecutado por teste (`TicketsTests.cs:5422`) | apaga pelo código (`:73-77`) | — |
| `20261027025440_Add_User_Invitations` | usuários | `user_invitations` | — | estrutural | — |
| `20261027173000_Add_Ticket_Attachment_Purged_At` | anexos | `purged_at_utc` | — | estrutural | sem check com `deleted_at_utc` (F-15) |
| `20261027201258_Add_Ticket_Last_Activity` | chamados | `last_activity_at_utc` e índice de "meus chamados" | preenche pelo histórico (`:29-35`) | estrutural | padrão sentinela residual (`:25`) |
| `20261027225828_Replace_Ticket_Delete_Own_With_Cancel_And_Delete_Permissions` | acesso | troca `tickets:delete-own` por `tickets:cancel` e `tickets:delete` | SQL idempotente (`:18-35`) | recria `delete-own` com os vínculos padrão (`:47-62`) | vínculos concedidos pela API não voltam |
| `20261028152342_Ticket_Description_Citext` | chamados | descrição em `citext` | — | estrutural | — |
| `20261028222209_Add_Ticket_Sla_Timer_Completion_Cause` | SLA | `completion_cause` | infere a causa pela mensagem no mesmo instante (`:25-40`) | estrutural | heurística por igualdade de instante |
| `20261029005235_Add_Ticket_Waiting_On` | chamados | `waiting_on` e índice parcial | calcula a situação com a regra de "quem atende" em SQL (`:24-44`) | estrutural | cópia da regra (X-1 da 041) |
| `20261030000904_Department_Sector_Name_Length` | organização | checks de tamanho do nome (`:13-24`) | — | estrutural | sem conferência prévia dos dados |
| `20261030164131_Add_Ticket_User_Text_Length_Constraints` | chamados e usuários | 5 checks de tamanho (`:13-41`) | — | estrutural | `ck_tickets_description_length` exige o mínimo de 10 nos dados existentes, sem conferência prévia |

## 2.13.1 Inventário e destinação (front-end)

| Componente | Tipo | Responsabilidade atual | Destino | Motivo |
|---|---|---|---|---|
| `core/routes/app.routes.ts` | rotas (shell) | rotas lazy; `authGuard` e `adminMasterGuard` com `data.requiredPermission` | refatorar | `/tickets` e `/tickets/novo` sem a permissão da rota do back (`tickets:view-own`, `tickets:create`) (FE-14) |
| `core/config/app.config.ts`, `core/config/app.routes.server.ts` | configuração (DI, SSR) | `provideHttpClient(withFetch, authInterceptor)`; SSR renderiza no cliente as rotas com sessão | manter | a sessão só existe no navegador; a 046 muda o transporte, não isto |
| `src/environments/environment*.ts` | configuração | `apiUrl` (produção com TODO, A-27) e os valores das flags de rotas do back | refatorar | só a URL e o que não for flag de rota entregue (FE-24) |
| `shared/entities/dashboard-features.ts` (`DASHBOARD_FEATURES`) | configuração | liga e desliga leituras do Início conforme a rota existe no back | remover | as flags descrevem rotas já entregues; os comentários dizem que `stats/status` e `stats/volume` exigem `tickets:manage`, o que a spec 031 tirou (FE-24); `volumeResolved` segue com o responsável (A-28) |
| `shared/infrastructure/auth.data.service.ts` | Data Service | login, troca obrigatória, ativação, cadastro, renovação, `users/me`, logout | dividir | `register` é administração de usuários (só o `UsersStore` usa), não autenticação: vai para `users/infrastructure` |
| `shared/services/session.service.ts` | estado de sessão | tokens no `localStorage`, decodifica `sub` e `email` do JWT, sincroniza abas, guarda permissões | refatorar | spec 046 (Q-60) |
| `shared/services/token-refresh.service.ts` | serviço | uma renovação por aba; recarrega `users/me` depois | refatorar | 046 (cookie); a coordenação continua |
| `shared/services/current-user.service.ts` | serviço | `GET users/me` com novas tentativas de 1, 2, 4 e 8 s e resultado tipado | manter | com `Retry-After` (DEC-62), as esperas passam a seguir o servidor |
| `shared/interceptors/auth.interceptor.ts` | interceptor | Bearer; 401 renova e repete; 403 recarrega `users/me` | refatorar | descarta o `unauthenticated` do `users/me` (FE-08); 046 |
| `shared/guards/auth.guard.ts` | guard | garante `users/me` carregado; falha passageira vai a `/sessao-indisponivel` | manter | — |
| `shared/guards/admin-master.guard.ts` | guard | confere `data.requiredPermission` | renomear | é um guard de permissão genérico (usado também com `tickets:manage`); o nome de papel engana |
| `shared/application/login.store.ts`, `shared/guards/pending-password-change.guard.ts` | Store e guard | login; guarda e-mail e senha provisória em memória até a troca | manter | o contrato de `users/login/new-password` exige a senha (FE-26, Q-66) |
| `shared/application/activate-account.store.ts`, `session-recovery.store.ts` | Stores | ativação por convite; recuperação da sessão | manter | — |
| `shared/utils/http-error.util.ts` | utilitário | mensagem do ProblemDetails (`errors[].description`, senão `detail`, senão `title`); falha passageira | refatorar | erros por campo e códigos (DEC-62) |
| `shared/utils/password.validators.ts`, `iso-date.util.ts` | utilitários | política de senha (RN-13); "hoje" em São Paulo | manter | validação de experiência; fuso fixo do produto |
| `tickets/infrastructure/ticket.data.service.ts` | Data Service | 18 chamadas: chamado, estatísticas, histórico, responsáveis | refatorar | fechar pela rota dedicada; `PUT` só com o campo da intenção; `delete` sem chamador (FE-01, FE-02, FE-23) |
| `tickets/infrastructure/ticket-message.data.service.ts`, `ticket-attachment.data.service.ts` | Data Services | conversa e anexos, paginados por cursor | manter | — |
| `tickets/application/ticket-detail.store.ts` | Store | detalhe, SLA, status, descrição, reabertura, atribuição | refatorar | FE-01, FE-02, FE-04 |
| `tickets/application/tickets.store.ts` | Store | Meus Chamados: listar, criar, editar, cancelar, reabrir | refatorar | editar e cancelar reenviam estado antigo (FE-02) |
| `tickets/application/ticket-summary.store.ts`, `ticket-monitor.store.ts` (`clockOffsetMs`) | Stores | Alertas de SLA, números da empresa, fila; cada um calcula o relógio do servidor de uma resposta diferente | consolidar | um só cálculo de relógio, alimentado pelo `serverNowUtc` da própria listagem (DEC-66); `sla-summary` só para o relógio (FE-10) |
| `tickets/application/ticket-dashboard.store.ts` | Store | agregado do período e do anterior, tempos, volume do recorte | refatorar | regra dos 366 pontos duplicada (FE-25) |
| `tickets/application/{ticket-quick-info,ticket-assignees,my-tickets-summary,ticket-messages,ticket-history,ticket-attachments}.store.ts`, `ticket-assignee-search.ts` | Stores | sub-recursos, resumos e busca de responsáveis | manter | leituras do servidor e somas de apresentação (A-17, A-18) |
| `tickets/entities/ticket-status.ts` (`TICKET_STATUS_TRANSITIONS`, `TICKET_ACTIVE_STATUSES`) | regra espelhada | transições e "ativo" | substituir | próximos status e ações vindos da API (047, DEC-3 dela); a tabela não diz a rota de cada transição (FE-01) |
| `tickets/ui/ticket-action-permissions.ts` | regra espelhada | editar descrição, reabrir, atribuir a outros | substituir | spec 047 (FE-09) |
| `tickets/ui/ticket-cancel-window.ts` | apresentação | esconde o "Cancelar" quando a janela vence com a tela aberta | manter | usa `canCancel` e `ownerCancelDeadlineUtc` do servidor (A-13) |
| `tickets/feature-ticket-list/ticket-list-due.ts` | apresentação | prazo do ciclo vigente, com fallback | refatorar | sem fallback depois da 024 |
| `tickets/entities/*.ts` | contratos | interfaces e enums que espelham os DTOs | refatorar | `TicketHistoryAction` sem 12 e 13; `CreateTicketData.responsibleId` com nome que o back não tem (FE-22) |
| `tickets/feature-ticket-detail/` | tela | detalhe, conversa, anexos, SLA, histórico | refatorar | autor inferido (FE-20); botões por espelho (FE-09) |
| `tickets/feature-ticket-monitor/` | tela | fila com filtros, atribuição na linha, painel | manter | reclassificação "ao vivo" é apresentação (A-14); a 048 muda os campos |
| `dashboard/*` | tela (shell) | Início por permissão; filtros na URL; departamento vira setores | refatorar | departamento no servidor (FE-12); persona inferida das permissões (FE-14) |
| `users/infrastructure/user.data.service.ts` (`countOpenAssignedTickets`) | Data Service | conta chamados do responsável chamando `GET tickets` | substituir | consulta agregada da 034 (`dimension=assignee`) (FE-03) |
| `users/application/users.store.ts` | Store | lista, cadastro mais funções, lote | refatorar | fan-out e composição não atômica (FE-03, FE-13) |
| `users/feature-user-list/` | tela | usuários, funções, lote, convite | refatorar | ações sem a permissão da rota (FE-14); regra do próprio administrador (FE-15) |
| `users/application/profile-edit.store.ts`, `users/feature-profile-edit/` | Store e tela | perfil e troca de senha | refatorar | texto e sessão depois da troca (FE-07) |
| `departments/application/departments.store.ts` | Store | departamentos e setores; busca de responsável em `GET users` | refatorar | detalhe só para o id do responsável (023); busca exige `users:read` (FE-16) |
| `departments/infrastructure/{department,sector}.data.service.ts` (`delete`) | Data Services | `DELETE departments/{id}` e `DELETE sectors/{id}` | renomear | no back é desativação (FE-21) |
| `departments/feature-department-list/` | tela | cadastro de departamentos e setores | refatorar | "Excluir… não pode ser desfeita" × desativação (FE-21) |
| `roles/*` | Store, Data Services, tela | funções por nome; permissões | manter | até a 036 (funções por id, com transição) |
| `docs/audit/*.md` | documentação | catálogo A-01 a A-46 e aceitação | manter | histórico; a A-22 está desatualizada no motivo da reabertura (500) |

## B.7 Cobertura do inventário pelas etapas

Cada revisor conferiu, linha a linha, os inventários da área dele (2.3 e 7.6 para chamados). Para cada área: as contagens conferidas e as linhas sem etapa ou tarefa, com o tratamento na consolidação. A cobertura por domínio está no `spec.md` (7.15.3).

### Chamados (parte 1)

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `Domain/Tickets/TicketMessagePosting.cs` (linha "Introduzir", com `TicketAttachmentUpload.cs`) | Introduzir | `TicketAttachmentUpload` na T-5; `TicketMessagePosting` só pelo AC-5 na T-8 (fase 3; REQ-3 [SUPOSIÇÃO — DEC-1]) | Com a DEC-1 (a), a 7.11 reduz a fase 3 e o record fica sem tarefa, embora a DEC-6 seja aprovada com a spec. Proposta: pôr o `TicketMessagePosting` na T-4 (fase 2), onde o `Post` já usa o `WaitingOn` do resultado (REQ-5) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `Application/Abstractions/Data/IApplicationDbContext.cs` | Manter, restrito | REQ-13 e checklist 1 (AC-19, T-10); AC-23 (T-14) | "Mantido só para consultas até a 043p1 (DEC-4)" supõe a 043p1 depois; pela DEC-4 (a) e pela 7.15, a G2 vem antes da G3-a, e as consultas que usam o `DbSet<TicketSlaTimer>` (`GetById`, `GetSla`, `GetTimes`) já estarão em SQL. Proposta: dizer o estado na fase 3 e a saída na 043p3 (P5) | Registrado: pela ordem da 7.15, a G2 vem antes da G3-a, e as consultas que usam o `DbSet<TicketSlaTimer>` já estarão em SQL na fase 3; a saída do `IApplicationDbContext` é na G5 (T-415). Sem mudança de contrato. |
| Testes da linha "Ajustar" (`TicketTestData`, `ProcessSlaBreachesCommandHandlerTests`, `SlaMonitorBackgroundServiceTests`, `AttachmentContentInspectorTests`, `RichEntityTests`, `TestDbContext`) | Ajustar | genérica: T-2 a T-10 e AC-16 | Lista incompleta: também usam `TicketSlaCycleService`, `TicketSlaTimers` ou constantes da entidade `BaseHandlerTest`, `TicketTests`, `TicketSlaTimerTests`, `UpdateTicketCommandHandlerTests`, `GetTicketSlaQueryHandlerTests`, `GetTicketSlaSummaryQueryHandlerTests`, `GetTicketsQueryHandlerTests`, `GetTicketAttachmentContentQueryHandlerTests`, `UploadTicketAttachmentCommandHandlerTests`, `IntegrationTests/Database/TicketConcurrencyTests` e `TicketsTests` (:4195-4202). A 2.12.1 congela `TestDbContext`, `BaseHandlerTest` e `TicketTestData` (REQ-413). Proposta: completar a lista e conciliar (ver E) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `Endpoints/TicketAttachments/Upload.cs` (dentro de "Os 28 endpoints … Manter") | Manter | nenhuma tarefa | `MaxRequestBodySizeBytes = 11 * 1024 * 1024` (`Upload.cs:12-16`) deriva dos 10 MB da entidade e é outra cópia do limite que o AC-11 quer só na `TicketAttachmentPolicy`. Proposta: derivar da política na T-3 ou registrar a exceção no AC-11 | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `PublishDomainEventsInterceptor`, `DomainEventsDispatcher`; `GlobalExceptionHandler`, `CustomResults` | Manter | ações na plataforma (2.12.1): Ajustar (P1, REQ-403) e Substituir (043p2); Refatorar (P1 e P2, REQ-401 e REQ-400) | Sem lacuna de execução, mas o mesmo componente é "Manter" aqui e "Ajustar"/"Refatorar" na 2.12.1, e a 7.16 conta os dois. Proposta: nota "ação na 2.12.1" na 7.6 | Registrado: a ação desses componentes é a da 2.12.1 (P1, P2 e 043p2); a nota da 7.16 explica a contagem. |
| Itens da 2.3 sem linha na 7.6: `IFileStorage`, `IAttachmentContentInspector`, `ISlaCalendarProvider`, `IPermissionProvider`, `IDbConnectionFactory`, paginação; 16 migrations do chamado e 2 de permissões; `TicketTests`, `TicketSlaTimerTests`, `TicketsTests` | sem destino na 7.6 | indireta: 7.12, AC-14 e AC-15 (migrations); 7.9 (testes que recebem cenários); DEC-32 (`IPermissionProvider`); Q-09 (`InvalidateAsync`) | Proposta: linha "Manter" (ou "Estender", para os testes) em "Componentes que ficam", com essas referências; e incluir na 2.3 o `IApplicationDbContext`, que a 7.6 cita e a 2.3 não lista | Registrado aqui: ficam (manter), com as referências citadas na proposta; o `IApplicationDbContext` segue a 2.12.1. |

Linhas conferidas na 7.6: 51 (37 na tabela principal e 14 em "Componentes que ficam"), por destino: Manter 15, Refatorar 19 (14 refatorar e 5 ajustar), Mover 5, Renomear 4, Remover 3, Substituir 2 (1 substituir e 1 "043p1"), Introduzir 2, Consolidar 1 (incorporar); a contagem bate com a 7.16. Fora das linhas acima, todas as linhas com destino diferente de "manter" têm tarefa (T-2 a T-10) ou etapa (G2, para a linha "043p1"). Os números da 2.3 conferem em `bc2395c` (35 e 11 arquivos no Domain, 69 arquivos e 19 handlers em `Application/Tickets`, 28 rotas, 422, 243 e 34 métodos de teste e as linhas de `Ticket`, `TicketSlaTimer`, `TicketSlaCycleService`, `TicketSlaPlan`, `TicketMessage`, `TicketAttachment`, `TicketHistory` e `BusinessCalendar`); a divisão "16 + 2" das migrations não foi recontada (o total do projeto é 31).

### Chamados (parte 2)

Coberta por outra pessoa.

### Usuários e identidade

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `JwtOptions` e `ConfigureJwtBearerOptions` | manter | Nenhuma tarefa; a linha registra o corte silencioso acima de 30 dias (US-10) | A US-10 recomenda limitar `RefreshTokenExpirationInDays` a 30 na validação das opções ou documentar o corte; não há REQ, AC nem T. Proposta: AC novo na T-103 ou citação na T-113 (a escolha é do responsável) | Registrado: Q-77 (escolha do responsável). |
| `EmailOptions` | manter | T-110 (DEC-28) muda só o `SmtpEmailSender` | O `<summary>` de `UseTls` diz "conforme o servidor" (`EmailOptions.cs:19-22`); com a DEC-28 a escolha passa a ser pela porta. Proposta: a T-110 ajusta o texto, sem remover o `<summary>` (`CLAUDE.md:151`) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| Endpoints `Login`, `Refresh`, `ChangeRequiredPassword`, `ActivateAccount` | manter | T-101 cita comentários e `<summary>` das interfaces | "Sai o `<summary>` do DTO de `ActivateAccount.cs:11-13`" não tem tarefa e colide com o `CLAUDE.md:151` ("nenhum `<summary>` existente é removido"; ver E). Proposta: manter o `<summary>`; nenhuma ação | Aplicado: REQ-117 ajustado (os `<summary>` ficam); nenhuma ação no endpoint. |
| Endpoints `Register`, `ResendInvitation`, `Activate`, `Deactivate`, `AssignRoles`, `ChangeSector` | manter | T-101 (`Reactivate.cs`, AC-116) | "Sai o `<summary>` do DTO de `Register.cs:11-13`": mesma colisão com o `CLAUDE.md:151`. A citação "(spec 008)" sai editando o texto. Proposta: a T-101 só retira a citação | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `TokenClaimTypes` e `ClaimsPrincipalExtensions` | manter | Nenhuma tarefa; a linha remete à 2.9.6 | Conferência do mapeamento `sub` para `NameIdentifier`, que depende do padrão do JwtBearer (`ConfigureJwtBearerOptions.cs:22-33` não fixa `MapInboundClaims`); os testes de integração autenticados já o exercitam. Proposta: citar na T-100 como conferência da linha de base | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `GetAll/*`, `GetById/*`, `GetStatusCounts/*` | converter | U0 pela 043p1 (G2), sem T da 049 | O trecho SQL "pendente" e o teste de concordância (US-05) não têm T nem AC; o `record` de `UserStatusCountsResponse` (US-15) também não. Proposta: T na G2 (ou no plano da 043p1) com o AC de concordância da seção D | Aplicado: AC-121 e T-115 (G2); o `record` da US-15 no AC-116. |
| `RefreshTokenConfiguration` | mover | T-103 ("na pasta própria"); REQ-113 | O AC-116 confere só o namespace do `RefreshToken`; a configuração e os dois eventos da sessão (linhas "mover") ficam sem conferência. Proposta: estender o AC-116 | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `RefreshTokenRevokedDomainEvent` | remover | T-105 (AC-115) | A remoção depende da DEC-26, pendente, e o AC-405 (T-402) usa o evento (ver E). Proposta: AC-115 e a linha condicionados à DEC-26 (b) | Aplicado: AC-115 condicionado à DEC-26 (b); REQ-112 com `[SUPOSIÇÃO — DEC-26]`. |
| Testes de integração de usuários (linha "manter") e arranjos de outras áreas | manter | AC-118 na T-107; testes novos na T-108 e na T-111 | Os arranjos com `new User` estão em 35 arquivos, 12 fora da área (`Tickets/`, `Departments/`, `Roles/`, `TicketHistories/`, `TicketMessages/`, `TicketTestData.cs` unitário e de integração); a T-107 fala só em testes de handler. Proposta: a T-107 cita esses arquivos | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |

Linhas conferidas: 81 (manter 26, refatorar 35, converter 6, substituir 3, mover 3, renomear 2, remover 4, dividir 2), iguais à 7.16. As demais linhas que não são "manter" têm fase e tarefa: entidades e eventos (T-103, T-104), estáticas (T-102, T-104, T-105), slices de command (T-101, T-103, T-105), configurações EF (T-103, T-104), `PasswordHasher` e `TokenProvider` (T-102), `SmtpEmailSender` (T-110), seeders (T-106, T-108), testes (T-102, T-107).

### Funções, permissões, autorização e segurança

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `Application/Roles/RoleNames.cs` | substituir | spec 036 (G3-b, passo 3; DEC-9 dela); nenhuma tarefa da 049 | Sem lacuna na 049; a revisão da 036 (7.17) deve citar também as regras por nome de `GetRolesQueryHandler.cs:19-20` e o seed de `RoleConfiguration.cs:23-26`. | — |
| `Application/Tickets/TicketAssigneeEligibility.cs` | substituir | 043p1 (G2; DEC-10 e DEC-11 da 043); concordância no AC-203 (T-205, depois da G2) | Sem lacuna; destino diferente na 2.9.1 ("manter"), ver seção E. | — |
| `Domain/Roles/RoleErrors.cs` | manter | spec 036 (erros de criar, renomear e excluir) | Sem lacuna na 049. | — |
| `Domain/Roles/RolePermissionsAssignedDomainEvent.cs` | manter | T-208 (AC-204: evento disparado pela `Role`) | Sem lacuna. | — |
| `Application/Roles/AssignPermissions/AssignRolePermissionsCommand.cs` | manter | spec 036 (rota por id) | O `using` sem uso (`:3`, também em `AssignUserRolesCommand.cs:3`) não tem tarefa; incluir na T-208 ou na revisão da T-215. | Aplicado: T-208. |
| `Application/Roles/GetAll/{GetRolesQuery,GetRolesQueryHandler,RoleResponse}.cs` | manter | 043p1 (SQL, G2) e spec 036 (ids e contagens) | Sem lacuna na 049. | — |
| `Application/Permissions/PermissionCatalog.cs` (+ `PermissionCatalogEntry`) | manter | T-210 (AC-206, teste catálogo × tabela) | Sem lacuna; a correção mínima da descrição de `tickets:view-own` (AZ-07) está só no destino da Q-31. | — |
| `Application/Users/GetAttendants/GetBacklogAttendantsQueryHandler.cs` | manter | Q-35 (fora); filtro em SQL na 043p1; concordância no AC-203 | Sem lacuna. | — |
| `Application/Tickets/Delete/DeleteTicketCommandHandler.cs` | manter | Q-01 (F-29), fora do escopo | Sem lacuna. | — |
| `Infrastructure/Authorization/PermissionAuthorizationPolicyProvider.cs` | manter | T-213 (AC-218, "teste unitário") | O teste de classe `internal` da Infrastructure não cabe no `Application.UnitTests` (seção E); proposta: `tests/TickestPristine.IntegrationTests/Authorization/`. | Aplicado: REQ-218 e AC-217 levam os testes para a integração. |
| `Infrastructure/Authentication/{TokenProvider,PasswordHasher,UserContext,ClaimsPrincipalExtensions,TokenClaimTypes,UserContextUnavailableException}.cs` | manter | 2.9: T-102 (DEC-25) refatora `TokenProvider` e `PasswordHasher` | Destino diverge da 2.9.1 (seção E); SE-05 sem vínculo (seção D). | Registrado: vale o destino da 2.9.1; nota na 7.16. |
| `Web.Api/Endpoints/Roles/{GetAll,AssignPermissions}.cs` | manter | spec 036 (rota por id) | Sem lacuna na 049. | — |
| `Web.Api/Extensions/EndpointExtensions.cs` | manter | T-200 (AC-200) | Sem lacuna. | — |
| `Web.Api/Extensions/CorsExtensions.cs` + `Infrastructure/CorsPolicyOptions.cs` | manter | spec 046 (Q-34); validação das origens na T-410 (AC-422) | Sem lacuna. | — |
| `Web.Api/Infrastructure/GlobalExceptionHandler.cs` | manter | contrato na Q-05: T-411 (DEC-51) e T-404 (ramos), na plataforma | Sem lacuna na área. | — |
| `Web.Api/Infrastructure/CustomResults.cs` | manter | contrato na Q-05: T-411 e T-404 | Sem lacuna na área. | — |
| `Web.Api/appsettings.json` | manter | Q-33 (fora; spec de implantação) | Sem lacuna. | — |
| `IntegrationTests/Users/UsersTests.cs` | manter | T-211, T-202, T-203 e T-212 (AC-210, AC-212 a AC-215) | Sem lacuna; o caso de cache na desativação já existe (`:1064`, `:1085`). | — |

Linhas conferidas: 73 (manter 36, refatorar 29, converter 6, substituir 2). Todas as 35 linhas "refatorar" e "converter" têm tarefa da 049 (T-201 a T-214, inclusive as condicionadas a DEC pendente); as 2 "substituir" são executadas por outras specs do plano global; 16 das 36 "manter" pedem alguma ação, listadas acima com as 2 "substituir".

### Organização e auditoria

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `Infrastructure/Audit/AuditLoggingInterceptor.cs` e o registro dele em `Infrastructure/DependencyInjection.cs:142-152` | substituir (interceptor, "na 043p2") / manter (registro, "até a 043p3") | T-304 ajusta o interceptor (REQ-312, AC-313); T-311 cria o `IAuditLog` (REQ-307, AC-308) | Nenhuma tarefa retira o interceptor nem o registro (`DependencyInjection.cs:144,152`), e as linhas dão etapas diferentes: 043p2 na linha do interceptor e na 2.11.5, 043p3 na do DI. Proposta: a T-311 inclui "retirar o `AuditLoggingInterceptor` e o registro quando o último command auditável sair do EF", ou a G5 (043p3) registra a retirada; as duas linhas passam a citar a mesma etapa | Aplicado: T-311 e T-415 (retirada na G5); Q-79. |
| `Database/Seeding/DefaultOrganizationSeeder.cs` | manter | Nenhuma tarefa; a 2.11.7 deixa "seed e auditoria depois da 043p3" como não determinado | O seed grava `Department` e `Sector` pelo EF (`UseAsyncSeeding`, `DependencyInjection.cs:145-148`) e hoje é auditado pelo interceptor e dispara eventos; quando o interceptor sair, nada diz se o seed continua auditado. Proposta: o S-AU-3 e o REQ-313 dizem se o seed é auditado, e a T-311 confere esse comportamento (ou a pergunta fica registrada para a 043p3) | Registrado: Q-79 (auditoria do seed, 043 parte 3). |

Linhas conferidas: 41 (manter 23, refatorar 6, converter 8, substituir 2, mover 2), as mesmas contagens da 7.16. As demais têm fase ou tarefa: refatorar → T-305, T-306, T-308 e T-310; converter → T-308 (043p1, Q-48) e T-310 (043p2, Q-49); mover → T-309 e, para o `GetAuditHistoryQueryHandlerTests`, o REQ-7 da 043 na 043p1; substituir → T-308 (`TicketSlaFilters`) e T-311 (interceptor, com a lacuna acima); linhas "manter" com ação → T-300 (testes de handler e de integração), T-302 (`IAuditable`), T-304 (comentário de `GetHistory.cs:14` e `AuditLogsTests`), T-305 (testes de entidade), T-306 (testes de validator), T-309 (`AuditLogEntryConfiguration`), T-310 e T-311 (asserções de auditoria e corridas).

### Plataforma, banco, testes e CI

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `Error` | Ajustar (P1): erros compartilhados `General.*` (REQ-401) | T-401 (AC-402, AC-403) cria só `General.ConcurrencyConflict`; os demais `General.*` entram na resposta pela T-411 (P2, REQ-400) | Parcial: a 2.12.5 põe as constantes na P1 e a T-409 (P1) usa `General.TooManyRequests`, mas nenhuma tarefa P1 cria as outras constantes. Proposta: o T-401 cria as constantes da 2.12.5 e o AC-403 confere tipo e status de cada uma | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `Entity` | Manter; `Raise` protegido (Q-58); `DomainEvents` como `IReadOnlyCollection` | `DomainEvents`: REQ-409 e T-406; `Raise`: Q-58, U5 (T-112, AC-26) | Parcial: o AC-411 não confere o tipo de `DomainEvents`. Proposta: acrescentar ao AC-411 "`Entity.DomainEvents` é `IReadOnlyCollection<IDomainEvent>`" | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider`, interfaces de `Messaging`, `IUserContext`/`IPasswordHasher`/`ITokenProvider`, `IPermissionProvider`, `IFileStorage` | Manter; `<summary>` (P1) | REQ-409, T-406, AC-411 e a regra (4) do REQ-416 (AC-418) | Sem lacuna. As linhas de `ISlaCalendarProvider`, `IEndpoint` e `IDomainEventsDispatcher` não citam o `<summary>`, embora as três estejam no PL-13 e no T-406: acrescentar a ação nessas linhas | Registrado: a ação já está na T-406 (PL-13); as linhas do inventário ficam como estão. |
| `DependencyInjection` (Application) | Manter; ordem fixada em teste (P1) | REQ-414, T-404, AC-416 | Sem lacuna | — |
| `ValidationDisplayNames` | Manter; teste de cobertura (P1) | REQ-416 (7), T-405, AC-418 | Sem lacuna de vínculo; a regra reprova hoje o `BatchSize` (ver E) | — |
| `IApplicationDbContext` | Ajustar (P1): uso restrito por teste; Remover (043p3) | REQ-405, T-405, AC-407; remoção na 043p3 (Q-59, G5) | Parcial: "quando a lista esvaziar, `Entry` sai" (REQ-405; P4 na 2.12.8) não tem tarefa nem AC. Proposta: o T-417 cita a retirada do `Entry` e um AC confere a interface sem `Entry` com a lista vazia | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `IDbConnectionFactory` | Manter; o `<summary>` muda na 043p2 | 043p2 (Q-59, G4) | Sem lacuna nesta spec | — |
| `ITicketActivityWriter` | Refatorar pela 049 (REQ-5); Remover (043p2, S-5) | REQ-5 na fase 2 de chamados (G3-a); S-5 da 7.7.2 e G4, que atribui o fim à T-417 | Parcial: o texto do T-417 e o AC-406 não citam o fim do gravador. Proposta: o T-417 cita a S-5 e aponta o AC-22 | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `DomainEventsDispatcher`, `IDomainEventsDispatcher` | Manter; contador de falhas (P1); interface na Application (043p2) | REQ-406, T-407, AC-408; REQ-404, T-417, AC-406 | Parcial: o AC-408 não confere o contador de falhas de handler (ver D) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `ApplicationDbContext` | Manter até a 043p3 | 043p3 (DEC-4 da 043, G5) | Sem lacuna nesta spec | — |
| Seeders (`DatabaseSeeder` e demais) | Manter (legado); estratégia nova fora (Q-51) | Q-51 | Sem lacuna (fora) | — |
| `TickestPristine.Infrastructure.csproj` | Ajustar com a Q-51 | Só a Q-51 (fora); nenhuma fase P | Sem tarefa nesta spec, por desenho. Proposta: escrever "Ajustar fora (Q-51)" no destino | Registrado: ajuste fora desta spec (Q-51). |
| `OpenTelemetryExtensions` | Ajustar (P1, REQ-406) | T-407, AC-408 | Parcial: o AC-408 não confere `service.version` nem o registro das fontes (ver D) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `EndpointExtensions`, `IEndpoint`, `Tags`, `Permissions.cs` | Manter; regra de autorização declarada (P1) | REQ-416 (1), T-405, AC-418; `<summary>` do `IEndpoint` no T-406 | Sem lacuna | — |
| `ResultExtensions` (Web.Api) | Manter; renomear quando o `SharedKernel` ganhar `ResultExtensions` | Nenhum REQ, tarefa ou Q | Lacuna: ação condicional sem registro. Proposta: o REQ-418 registra a troca para `ResultMatchExtensions` na linha "Peças do domínio rico no `SharedKernel`" do `CLAUDE.md` (DEC-59), conferida pelo AC-420 | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `MigrationExtensions` | Manter em Development; produção pela DEC-53 (P5) | REQ-412, T-415, AC-414 | Sem lacuna | — |
| `appsettings.json`, `appsettings.Development.json`, `launchSettings.json` | Manter (GAP-13 já registrado) | GAP-13 no `SYSTEM_SPECIFICATION.md`; sem Q nem tarefa | Lacuna leve: os enrichers sem pacote (`appsettings.Development.json:23`) ficam sem destino nesta spec. Proposta: uma Q curta ou registro como aceito | Registrado: Q-80. |
| `Directory.Build.props` | Manter; reavaliar o `NU1608` | Q-54 | Sem lacuna (fora) | — |
| `Directory.Packages.props` | Ajustar (P1) | `Serilog` sai pelo REQ-409 e T-406 (AC-411); HealthChecks 9.0.0 e `NU1608` ficam na Q-54 (TE-07) | Parcial: o "(P1)" vale só para o `Serilog`. Proposta: destino "Ajustar (P1): sai o `Serilog`; versões de saúde com a Q-54" | Registrado: a P1 tira o `Serilog`; as versões de saúde ficam na Q-54. |
| `global.json` | Manter; o CI passa a lê-lo | REQ-417, T-414, AC-419 | Sem lacuna | — |
| `.editorconfig` | Manter nesta spec; reativação fora (Q-54) | Q-54 | Sem lacuna de cobertura; a contagem da linha está errada (ver E) | — |
| Testes da plataforma (`Database/`, `Middleware/`, `Cors/`, `Swagger/`, `Seeding/`, `Storage/`) | Manter e estender (P1) | REQ-414, T-404, AC-416 | Sem lacuna | — |
| 31 migrations e `ApplicationDbContextModelSnapshot` | Manter; migration nova de integridade (P3) | REQ-410, REQ-411, T-413, AC-412, AC-413 | Sem lacuna | — |

Linhas conferidas: 65 (37 Manter, das quais 23 com ação escrita na própria linha; 22 Ajustar; 2 Refatorar; 2 Substituir; 1 Congelar; 1 Estender, termo fora do vocabulário da 2.12.1). As linhas de destino na 043 (`PaginationExtensions`, `PaginationExtensionsTests`, `TickestPristine.Application.csproj`, saída do `TestDbContext`) têm etapa na G2 e na G5 (Q-59) e não entram como lacuna.

Migrations: nenhuma migration existente exige edição. As ações vão numa migration nova (REQ-410, T-413), que retira os `DEFAULT` sentinela deixados por `Hash_Refresh_Tokens`, `Add_Ticket_Sla_And_Pagination_Indexes`, `Add_Session_Limits_And_Login_Lockout` e `Add_Ticket_Last_Activity` e cria as checks dos enums; a política do REQ-411 vale para as próximas. O `Down` de `Store_Ticket_Enums_As_Names` fica no registro (Q-57, Q-08), e o comentário velho de `Role_Seed_Only_Administrator` (`:10`, `DefaultRolesSeeder`) não pede ação.

### Front-end

Conferidas as 39 linhas da 2.13.1: 12 manter, 19 refatorar, 3 substituir, 2 renomear, 1 dividir, 1 remover e 1 consolidar. As linhas delegadas a outras specs (046, 024, 048, 036) contam como cobertas pela fase FE-4.

| Componente | Destino | Cobertura encontrada | Lacuna e proposta | Tratamento na consolidação |
|---|---|---|--- |---|
| `shared/infrastructure/auth.data.service.ts` | dividir | nenhuma tarefa nem fase; a T-507 só junta cadastro e funções | Citar na T-507: mover `register` para `users/infrastructure` (o único chamador é o `UsersStore`, `users.store.ts:195`) | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `shared/guards/admin-master.guard.ts` | renomear | nenhuma tarefa nem fase | Citar na T-512 (visibilidade por permissão): renomear para guard de permissão, com o spec `admin-master.guard.spec.ts` | Aplicado como proposto (tarefa ou critério com a marca de ajuste no `spec.md`). |
| `tickets/application/ticket-dashboard.store.ts` | refatorar | o FE-25 é "registro", sem REQ, AC nem tarefa | Tarefa curta no FE-3 (tratar o 400 `Tickets.VolumeTooManyPoints` e tirar a conta local) ou registrar a linha como "registro"; escolha do responsável | Registrado: Q-81 (escolha do responsável). |
| `tickets/entities/*.ts` | refatorar | a Q-69 cobre o glossário e a unificação no back; nada tira `responsibleId` nem completa `TicketHistoryAction` | Tarefa no FE-0 (ou na T-514): tirar `responsibleId` de `CreateTicketData` (`ticket.ts:75`) e acrescentar 12 e 13 em `ticket-history.ts` | Aplicado: T-518 e AC-521. |
| `tickets/infrastructure/ticket.data.service.ts` | refatorar | T-500 (`close`) e T-502 (`PUT`) cobertas; o `delete` sem chamador (FE-23) não tem destino, e a DEC-69 mantém `DELETE tickets/{id}` como capacidade (Q-65) | Registrar na Q-65 se o método do front fica declarado ou sai | Aplicado: nota na Q-65. |
| `users/application/profile-edit.store.ts`, `users/feature-profile-edit/` | refatorar | a T-505 é só do back; a FE-4 cita a 046 sem dizer quem faz o front da DEC-63 | Tarefa de front no FE-3 (ou na rodada do front da 046): gravar os tokens devolvidos e corrigir o texto "suas demais sessões" (REQ-504) | Aplicado: T-520 e AC-508. |
| `departments/application/departments.store.ts` | refatorar | a busca vai para a Q-65; o uso do `responsibleUserId` da 023 (já na `main`) não tem tarefa | Tarefa no FE-0: editar com o `responsibleUserId` da listagem, sem `GET departments/{id}` | Aplicado: T-519 e AC-522. |
| `departments/infrastructure/{department,sector}.data.service.ts` (`delete`) | renomear | T-512 e AC-515 cobrem só os textos | Citar na T-512 a troca de `delete` por `deactivate` nos dois Data Services (e a T-514 testa o método novo) | Aplicado: T-512 e AC-515. |
| `shared/entities/dashboard-features.ts` | remover | T-517 e AC-520 tiram as flags de rotas entregues, mas o próprio motivo mantém `volumeResolved` com o responsável (A-28) | Remoção condicionada à A-28; a T-517 deve dizer o que fica (só `volumeResolved`) até a decisão | Aplicado: T-517. |
| `src/environments/environment*.ts` | refatorar | T-517 cobre as flags; o `apiUrl` de produção com TODO (A-27) não tem tarefa | Registrar a A-27 como fora do escopo ou citar na T-517 | Registrado: Q-82. |
| `shared/services/current-user.service.ts` | manter (com ação) | as esperas pelo `Retry-After` estão na T-504 | Sem AC de front para a espera (ver D) | Aplicado: AC-523 (T-504). |
| `docs/audit/*.md` | manter (com ação) | a A-22 está desatualizada (motivo 500) e nenhuma tarefa a corrige | A T-512, ao mudar o limite, atualiza a A-22, ou a spec registra que o catálogo é histórico e não muda | Aplicado: T-512. |
| `tickets/feature-ticket-monitor/`, `roles/*`, `shared/application/login.store.ts` | manter (com ação) | a ação vem da 048, da 036 (Q-63) e da Q-66, todas na FE-4 ou fora do escopo | Sem lacuna nesta spec | — |
| Componentes que as tarefas alteram e que não estão no inventário: `tickets/ui/ticket-reopen-dialog/` (T-512), `tickets/feature-ticket-list/ticket-list.component.*` (T-502, T-512), `shared/ui/layouts/main-layout/` (T-512), `shared/feature-login/` (T-504, REQ-514), `tickets/feature-ticket-create/` e `tickets/ui/ticket-edit-dialog/` (erro no campo, T-504) | sem linha | as tarefas citam o comportamento, não o arquivo | Acrescentar as linhas no inventário, com destino "refatorar" e a tarefa | Registrado aqui: os componentes citados ficam cobertos pelas tarefas da proposta (T-502, T-504, T-512 e REQ-514). |

## B.8 Lacunas de vínculo e tratamento

Conferência de vínculos feita por área: requisito sem critério, critério sem tarefa, tarefa sem critério, decisão sem requisito nem tarefa e achado sem vínculo, inclusive lacunas parciais. O resultado consolidado, gerado das tabelas, está no `spec.md` (9.4).

### Chamados (parte 1)

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| F-07 e DEC-6 | O `TicketMessagePosting` (F-07; DEC-6 aprovada com a spec) só tem REQ, AC e tarefa sob a DEC-1: REQ-3 [SUPOSIÇÃO — DEC-1], AC-5 e T-8 (fase 3) | Ligar F-07 a um REQ fora da suposição (REQ-5 ou REQ-6) e ao AC-19 (checklist 3) na T-4 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-09 | Destino "024/048": a seção 6 registra o item numa linha sem ID ("—"); nenhum Q, REQ ou AC | Dar ID à linha da seção 6 ou citar a 7.17 (linhas 024 e 048) como vínculo formal | Aplicado: a linha da seção 6 ganhou ID (Q-73). |
| F-01 (efeito de excluir) | O AC-3 cita só `Resolve`, `Cancel` e `Reopen`; a 7.9 atribui o `Delete` (tirar da fila sem violação, parada nem evento) ao AC-3 e ao AC-4 | Incluir o `Delete` no "Quando" e no "Então" do AC-3 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-15 (parte do banco) | "Nenhum check do banco liga as duas datas" não tem REQ, AC nem Q; a 2.12.2 chama o check de "recomendação do modelo-alvo, sem REQ" | Registrar como Q na seção 6 ou ligar à DEC-54 (migration de integridade, T-413) | Aplicado: Fonte do REQ-410, a avaliar com a DEC-54. |
| F-12 e DEC-3 | Sem REQ, AC ou tarefa na faixa de chamados; o vínculo é indireto (S-6, REQ-20 pela GL-03, AC-22) | Citar F-12 e S-6 na Fonte do REQ-20 e no AC-22 (teste de dois envios simultâneos) | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-19 | Mesmo caso: S-7, REQ-20 e AC-22 só pela GL-03 | Citar F-19 e S-7 na Fonte do REQ-20 e no AC-22 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-20 | Nenhum DEC, REQ, AC ou T (é registro); o destino aponta para a 2.7, que não o registra | Destino "registro (GL-08 na 2.8.2; 2.8.3)"; nenhum REQ necessário | Aplicado: destino corrigido no F-20; vínculo pelo AC-18 e pela T-11. |
| F-06 | O REQ-12 (que renomeia `ProcessDue`) cita F-28, não F-06; o destino de F-06 não cita a DEC-1, de que dependem a saída do `IsQueued` e o `internal` do `SettleDeadline` | Citar a DEC-1 no destino de F-06 e F-06 na Fonte do REQ-12 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-08 | O AC-7 só fixa o resultado da ordem "exclusão antes do lote"; a ordem inversa não tem resultado esperado (ver E) | Acrescentar ao AC-7 o resultado quando o lote grava antes da exclusão | Levado à decisão: a DEC-11 voltou a pendente (CT-13); nota no AC-7. |
| DEC-4 | Nenhum REQ cita a DEC-4; o REQ-19 cita só a DEC-18, que a amplia | Citar a DEC-4 na Fonte do REQ-19 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| DEC-8 | REQ-8, AC-12 e T-6 (fase 2) não registram que o `EnsureCanReopen()` sem parâmetro depende da DEC-1 (b) | Marcar a parte do `Reopen` do REQ-8 como dependente da DEC-1 ou dizer a forma intermediária (ver E) | Conciliado: CT-17; T-6 ajustada. |

### Chamados (parte 2)

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| REQ-1 a REQ-17 | Nenhuma: todo REQ tem AC (o REQ-15 só pelo AC-16 e pelo AC-19) | — | — |
| AC-1 a AC-19 | Nenhuma: todo AC tem tarefa | — | — |
| T-1 a T-12 | Nenhuma tarefa sem AC. Nenhuma cita DEC: o vínculo DEC → tarefa vem só da 7.11 (fase → DEC) | Acrescentar [DEC-x] ao texto das T-2 a T-11, como nas T-303 a T-309 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| DEC-1, DEC-2, DEC-5 a DEC-11, DEC-14 | Nenhuma: REQ pela Fonte (a DEC-11 no cabeçalho do REQ-4 e no REQ-14) e tarefa pela fase da 7.11 | — | — |
| DEC-3 | REQ só na faixa global (REQ-20); sem tarefa: a 7.11 a manda à 043p2 sem T | Tarefa nova, como a T-216: "Entregar à 043 parte 2 o contrato de SQL dos chamados (7.7.2, S-1 a S-10, com a trava do S-6)", com o AC-22 | Aplicado: a T-16 leva o contrato S-* (inclusive o S-6) à 043; vínculo pelo REQ-20 e pelo AC-22. |
| DEC-4 | Nenhum REQ a cita (o REQ-19 cita a DEC-18, que a amplia); tarefa só indireta (7.11: fase 0 → T-1) | O REQ-19 cita a DEC-4 e a DEC-29 na Fonte; a T-1 cita a DEC-4 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| DEC-12 | Sem REQ e sem tarefa (o REQ-5 cita só a 7.7.2; a G4 cita a DEC-12 sem T) | A mesma tarefa nova da DEC-3; o REQ-20 cita a DEC-12 na Fonte | Aplicado: T-16 e Fonte do REQ-20. |
| DEC-13 | Sem REQ e sem tarefa; "documentado" não diz onde | O REQ-17 inclui "convenção `*_on_utc` do cronômetro documentada (DEC-13)"; a T-11 a cita; o AC-18 confere | Aplicado: REQ-17, AC-18 e T-11 citam a DEC-13; Fonte do REQ-12. |
| DEC-15 | Sem REQ e sem tarefa | Item novo no checklist da 7.10 (AC-19): "nenhum objeto de valor novo; `TicketSlaPlan` e `SlaDeadlines` sem mudança"; a T-10 cita a DEC-15 | Aplicado: AC-19 (item do checklist) e T-10. |
| DEC-16 | Sem REQ e sem tarefa | O REQ-11 cita a DEC-16 (`TicketHistoryAction` em `TicketHistories/`, sem sufixo); o AC-14 já confere o namespace; a T-2 a cita | Aplicado: Fonte do REQ-11 e T-2. |
| F-22 a F-42 | Nenhum achado SEM VÍNCULO; as lacunas parciais estão nas linhas seguintes | — | — |
| F-23 (corrida) | Na faixa de chamados, só a contradição do estado tem AC (AC-18); a corrida não tem AC próprio (o AC-22 é genérico) e o S-5 não a fecha (ver E) | O AC-22 cita o F-23 e o S-5 ganha o caso das duas mensagens, com o teste da coluna Regressão | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| F-28 (dois `TicketAttachmentResponse`) | Destino 043p1 sem linha na seção 6 nem REQ | Acrescentar o F-28 (respostas das queries) à linha da 043 parte 1 na seção 6 | Aplicado: a Q-70 (043 parte 1) cita o F-28. |
| F-34 (drenagem e `Result` descartado) | A métrica tem REQ-406 e AC-408 e o conflito por cronômetro tem o S-8; drenagem e uso do `Result` não têm REQ, AC nem Q | Registrar os dois no S-8 (043p2) ou numa Q nova da seção 6 | Aplicado: notas no S-8 e na Q-71. |
| F-42 (`SampleDataSeeder` e `LocalFileStorage`) | Sem REQ, AC, T nem Q; a T-7 muda o seeder sem teste que o exercite | AC novo da T-7: "um teste de integração com `Seeding:SampleData` ligado cria chamados com os cronômetros do ciclo 1"; `LocalFileStorage`: Q nova ou T-404 (P1) | Aplicado: AC-31 (T-7) e teste do `LocalFileStorage` na T-5. |

### Usuários e identidade

REQ-100 a REQ-117 sem AC: nenhum. AC-100 a AC-120 sem tarefa: nenhum. T-100 a T-114 sem AC: nenhuma. Achados SEM VÍNCULO: nenhum; as lacunas abaixo são parciais.

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| DEC-29 | Sem REQ nem T na faixa da área; a ordem só aparece na REQ-19 (global), cuja Fonte cita a DEC-18 e não a DEC-29 | A REQ-19 cita a DEC-29 na Fonte; a T-100 cita a DEC-29 e o AC-21 (dependências da U0) | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-05 (parte) | "Conta pendente" não está entre os conceitos do REQ-22; nenhum AC fixa a concordância `GET users` × `GET users/{id}` × `GET users/stats/status` pedida no achado; o trecho SQL "pendente" não tem T | AC novo: "AC-121 (REQ-102, REQ-22): com ativo com senha, pendente com convite vigente, pendente com convite vencido e desativado, as três rotas e `User.IsPendingActivation` dão a mesma situação"; executado na G2 (043p1) ou na T-104 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-10 (parte) | "Limitar `RefreshTokenExpirationInDays` a 30 ou documentar o corte" sem REQ, AC nem T | AC novo na T-103: "`RefreshTokenExpirationInDays` acima de 30 recusado na inicialização", ou citação na T-113 (RN-15 do estado), conforme o responsável | Registrado: Q-77. |
| US-15 (parte) | `UserStatusCountsResponse` como `record` (2.9.1) sem AC nem T | O AC-116 acrescenta "`UserStatusCountsResponse` é `record`"; a T-101 (ou a conversão da 043p1) executa | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-19 (parte) | A desativação em dobro não está no AC-112 (só senhas e convites) | Acrescentar ao AC-112: "duas desativações simultâneas de usuário com senha, sem sessões, sem convite e não administrador: uma 204 e outra 409, com um evento e um registro de auditoria" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-20 (parte) | `ValueGeneratedNever()` só aparece na Fonte do REQ-114; nenhuma T ou AC cita a troca | A T-104 cita "`users.id` com `ValueGeneratedNever()` se o AC-117 continuar verde" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-23 e AC-118 | O AC-118 alcança 35 arquivos de teste, 12 de outras áreas, mas a T-107 fala só em testes de handler | A T-107 cita os testes de integração e os arquivos de `Tickets/`, `Departments/`, `Roles/`, `TicketHistories/`, `TicketMessages/` e `TicketTestData.cs` | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| US-24 | Nenhum teste da suíte impede comentário com spec ou DEC em `src` e `tests`; o AC-119 é busca pontual e o `test_instructions.py` não lê código | O AC-119 cita uma regra em `tests/TickestPristine.ArchitectureTests/Conventions/` (já há `CSharpSource` e `SourceFile`), ou registra que a prova é só a busca | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-22 (convite utilizável) | O modelo-alvo da 2.9.4 não tem pergunta nomeada de "convite utilizável", exigida pelo REQ-22, e nenhum AC da área a cobre | A T-104 cita o REQ-22 e o AC-24; a 2.9.4 nomeia a pergunta | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| AC-116 (parte) | O REQ-113 move também `RefreshTokenConfiguration` e os dois eventos da sessão; o AC-116 só confere o `RefreshToken` | O AC-116 acrescenta os três tipos nos namespaces `Domain.RefreshTokens` e `Infrastructure.RefreshTokens` | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| T-106 | Cita só o AC-118 (testes); o que prova o seed pelas fábricas é o AC-100 (setters privados) e o `DatabaseSeedingTests` | A T-106 cita o AC-100 | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-117 (parte) e AC-119 | O REQ-117 pede "sem `<summary>` em DTO nem em teste", e o AC-119 não confere isso (e há colisão com o `CLAUDE.md:151`, ver E) | Resolver a colisão antes; se o requisito ficar, o AC-119 passa a conferir | Aplicado: REQ-117 e AC-119 ajustados à regra do `CLAUDE.md`. |

### Funções, permissões, autorização e segurança

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| REQ-200 a REQ-221; AC-200 a AC-221; T-200 a T-216; DEC-30 a DEC-39 | Nenhum REQ sem AC, nenhum AC sem tarefa, nenhuma tarefa sem AC e nenhuma DEC sem REQ ou tarefa. | — | — |
| SE-05 | SEM VÍNCULO: a recomendação "registrar; formato com versão só quando os parâmetros mudarem" não tem REQ, AC, tarefa nem Q. | Nova linha na seção 6: "formato do hash de senha com versão e migração dos parâmetros (SE-05) → spec própria, quando os parâmetros mudarem"; ou citar o SE-05 no REQ-219 (registro no estado). | Registrado: Q-78. |
| AZ-10 (parte da migration) | "registrar o caso da migration na 4.6" não está em REQ nem em AC. | Acrescentar ao REQ-219 e ao AC-219: "a 4.6 registra que migration que muda vínculos de função não invalida o cache (efeito por até 5 min)". | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| AZ-09 (funções do mesmo usuário) | O REQ-109 (DEC-22) cobre pela trava, mas nenhum AC testa duas atribuições simultâneas de funções; o AC-112 só testa login e reenvio. | Acrescentar ao AC-112 (ou ao AC-220) dois `PUT users/{id}/roles` simultâneos com conjuntos diferentes: um vence, o outro espera ou recebe 409. | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| AZ-04 (ponto 3) | O AC-207 cobre só desativação contra desativação; o outro caminho que tira administrador (`PUT users/{id}/roles` sem Administrador) não tem corrida testada. | Acrescentar ao AC-207 uma repetição com `PUT users/{id}/roles` sem Administrador contra `PUT users/{id}/deactivate`. | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-212 (documentação) | "o valor de produção fica documentado na 4.8" não é conferido no AC-212 nem no AC-219. | Acrescentar ao AC-212: "a 4.8 do estado traz o valor de produção de `Jwt:ExpirationInMinutes`". | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-213 (`RequireHttpsMetadata`) | "sem `RequireHttpsMetadata = false`" não é conferido no AC-213. | Acrescentar ao AC-213: "a busca não acha `RequireHttpsMetadata = false` em `src`". | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-215 e AC-215 (DEC-38) | O AC-215 só traz o resultado esperado da DEC-38 (a). | Acrescentar os resultados das opções (b) e (c), ou reescrever o AC-215 depois da decisão. | Aplicado: o AC-215 diz que foi escrito para a opção (a) e que a D-11 o reescreve com (b) ou (c). |
| SE-14 (procedimento) | "procedimento de implantação (GAP-10)" não tem AC na área; o REQ-412 trata só das migrations. | Acrescentar ao AC-219 (ou ao AC-414): "a 4.8 manda trocar a senha provisória do administrador logo depois do seed". | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |

### Organização e auditoria

Conferência: REQ-300 a REQ-313 têm AC (AC-300 a AC-314); AC-300 a AC-314 têm tarefa; T-300 a T-313 têm AC; das DEC-40 a DEC-48, só a DEC-40 não tem REQ nem tarefa; achados sem vínculo: OR-10 e OR-11. As linhas abaixo incluem partes de REQ e de tarefa que ficam sem AC.

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| OR-10 | SEM VÍNCULO: a recomendação ("manter pela convenção") não tem REQ, AC nem tarefa; o canônico F-20 também é só registro | Citar OR-10 (com F-20) na T-313 como registro sem ação, conferido por `DomainEventsPublishingTests` verde e pela busca sem `IDomainEventHandler` de evento da organização | Aplicado: REQ-313 e AC-314 (registro no estado, T-312). |
| OR-11 | SEM VÍNCULO: "aceitar e registrar" sem dizer onde registrar | Acrescentar OR-11 à Fonte do REQ-313 e ao AC-314: "o 4.7 registra que edições simultâneas de departamento ou setor gravam a última por coluna, sem token" (T-312); conferir antes com o REQ-20 (i) (seção E) | Aplicado: REQ-313 e AC-314; CT-12. |
| DEC-40 | Sem REQ nem tarefa (decisão de manter a fronteira) | Citar a DEC-40 na Fonte do REQ-300 e na T-310, porque as travas S-OR-3 a S-OR-5 pressupõem duas raízes; ou critério na T-313: "`Sector` continua raiz, sem coleção de setores no `Department`" | Aplicado: Fonte do REQ-300 e T-310. |
| REQ-303 (parte do `CLAUDE.md`) | "O desvio sai do `CLAUDE.md`" não tem AC nem tarefa explícita: a T-305 e o AC-304 não citam o arquivo, e a OA-4 só lembra a autorização | T-305: "e retirar o desvio do `Update` em `CLAUDE.md:145`, com a DEC-14"; AC-304: "o `CLAUDE.md` não tem mais o desvio; `python -I scripts/test_instructions.py` verde" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| T-304 e AU-12 (testes de `Deleted` e do save que falha) | O AC-313 cobre instante único, save síncrono e chave composta, e o AC-310 cobre `SecurityStamp`; `Deleted` e "save que falha não deixa registro" ficam sem AC | Estender o "Então" do AC-313: "a remoção de um `UserRole` grava um registro `Deleted` com `oldValues`; um save que falha não deixa registro para a tentativa seguinte" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-311 (renome de propriedade) | O AC-312 fixa só os nomes dos 13 tipos; o renome de propriedade auditável (chaves do JSON, AU-07, I-AU-06) não tem AC | Estender o AC-312 e a T-302 ao instantâneo das propriedades auditadas de cada tipo, ou registrar a regra do renome no estado pela T-312 (AC-314) | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |

### Plataforma, banco, testes e CI

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| T-400 | Ligada ao AC-416, que confere os testes criados pela T-404; a linha de base não consegue cumpri-lo | Ligar a T-400 ao AC-16 (suíte inteira, 0 avisos) ou a um AC novo: "na base, build com 0 avisos, suíte aprovada e contagens da 2.12.4 conferidas" | Aplicado: a T-400 passa a apontar o AC-16. |
| REQ-405 (segunda frase) | "Quando a lista esvaziar, `Entry` sai do `IApplicationDbContext`" sem AC nem tarefa; a P4 cita, a T-417 não | O T-417 cita a retirada; AC novo: "com a lista do REQ-405 vazia, o `IApplicationDbContext` não declara `Entry`; build com 0 avisos" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-406 | O AC-408 não confere span de query, contador de falhas de handler de evento, contadores de cronômetros apurados e de lotes desfeitos nem `service.version` | Estender o AC-408: "uma query gera span; um handler de evento que lança soma 1 no contador dele; a volta soma os apurados; o recurso tem `service.version`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-407 | O AC-409 não confere a escrita na pasta de arquivos, o Redis configurado nem o uso do `NpgsqlDataSource` registrado | Estender o AC-409: "pasta de arquivos sem escrita: `ready` 503; `ConnectionStrings:Cache` inalcançável: `ready` 503" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-408 | A partição por usuário autenticado, que o REQ manda manter, não tem conferência (2.12.7) | Acrescentar ao AC-410: "dois usuários autenticados no mesmo IP têm limites separados" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-409 | `Entity.DomainEvents` como `IReadOnlyCollection` fora do AC-411 | Acrescentar ao AC-411: "`Entity.DomainEvents` é `IReadOnlyCollection<IDomainEvent>`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-415 (segunda frase) | A mesma versão fixa do PostgreSQL no compose e no Testcontainers não tem AC | Acrescentar ao AC-417 ou ao AC-421: "a tag do `postgres` no `docker-compose.yml` é igual à do `IntegrationTestWebAppFactory`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-417 | O cache do NuGet não é conferido no AC-419 | Acrescentar ao AC-419: "o restore usa cache do NuGet" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-419 | O `.dockerignore` (`tests/`, `specs/`, `appsettings.Development.json`) fica fora do "Então" do AC-421 | Acrescentar ao AC-421: "o contexto da imagem não contém `tests/`, `specs/` nem `appsettings.Development.json`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-400 | O AC-400 não cobre 405 (`General.MethodNotAllowed`), a falha `Failure` (`code` `General.ServerFailure`, sem `detail`) nem o `code` dos ramos 413, 400 ilegível e 401 sem id; o AC-416 confere só o status | Estender o AC-400 com esses casos | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| AC-400 × DEC-34 | Com o `FallbackPolicy` da DEC-34 (b), a rota inexistente sem token responde 401, e não 404 | O AC-400 chama a rota inexistente com token válido | Aplicado: o AC-400 chama a rota inexistente com token válido. |
| AC-405 × DEC-26 | O arranjo usa o `RefreshTokenRevokedDomainEvent`, que a DEC-26 (b) remove (CT-02) | O AC-405 usa "uma entidade removida com evento" (evento de fixture), ou declara a dependência da DEC-26 | Aplicado: o AC-405 usa entidade e evento de teste próprios. |
| T-405 × T-406 e REQ-405 | As regras (3) e (4) do REQ-416 reprovam o código até o T-406 sair; o REQ-405 lista `TicketAttachments/Delete/TicketAttachmentDeletedDomainEventHandler.cs`, que o T-406 move e renomeia | Ordem T-406 antes do T-405 na P1; o AC-407 cita o handler de purga pelo caminho novo | Aplicado: a T-405 vem depois da T-406; o AC-407 cita o caminho novo. |
| T-401 e `General.*` | A 2.12.5 põe as constantes `General.*` na P1 e a T-409 (P1) usa `General.TooManyRequests`, mas o REQ-401 e a T-401 criam só `General.ConcurrencyConflict` | O T-401 cria as constantes da 2.12.5; o AC-403 confere tipo e status de cada uma | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| TE-05 (loggers) | `RecordingLogger` e `TestLogger` duplicados sem REQ nem AC | Acrescentar ao REQ-413 e ao AC-415 "um logger de teste só", ou registrar como aceito | Aplicado: REQ-413 e AC-415 (um logger de teste só). |
| PL-07 (resolução fora do `try`) | `GetServices` e `HandlerWrapper.Create` fora do `try` (`DomainEventsDispatcher.cs:32,41`): 500 depois do commit, sem REQ | Incluir no T-402 (mesmo fluxo de eventos) com AC "handler que não resolve: falha registrada e resposta 2xx", ou registrar como aceito | Aplicado: T-402. |
| PL-15 (`ResultExtensions` da Web.Api) | Troca de nome condicional sem REQ, tarefa nem Q | Registrar no REQ-418 (linha "Peças do domínio rico" do `CLAUDE.md`, DEC-59), conferida pelo AC-420 | Aplicado: REQ-418 e AC-420. |
| DB-04 | O teste "migration nova num banco com vínculos editados" não tem AC; o AC-413 confere só o registro da política | O REQ-411 inclui esse teste na política, e o AC-413 o cita | Aplicado: REQ-411 e AC-413. |
| REQ-400 a REQ-420 sem AC | Nenhum: todos têm AC (lacunas parciais acima) | — | — |
| AC-400 a AC-422 sem tarefa | Nenhum: todos têm tarefa | — | — |
| T-400 a T-419 sem AC | Nenhuma sem AC; a T-400 tem AC inadequado (primeira linha) | — | Aplicado: a T-400 passa a apontar o AC-16. |
| DEC-50 a DEC-59 sem REQ nem tarefa | Nenhuma: DEC-50 (REQ-402, T-403), 51 (REQ-400, T-411), 52 (REQ-404, T-417), 53 (REQ-412, T-415), 54 (REQ-410, T-413), 55 (REQ-413, T-416), 56 (REQ-416, T-405), 57 (REQ-406, T-407), 58 (REQ-408, T-409), 59 (REQ-411, REQ-418, T-413, T-418) | — | — |
| Achados SEM VÍNCULO | Nenhum; DB-07 e TE-07 têm só Q (fora), e PL-09 só a Q-59 | — | — |

### Front-end

Nenhum REQ-500..515 está sem AC, nenhum AC-500..520 está sem tarefa, nenhuma T-500..517 está sem AC e nenhuma DEC-60..69 está sem REQ e tarefa. As lacunas abaixo são parciais, mais os achados sem vínculo.

| Item | Lacuna | Proposta | Tratamento na consolidação |
|---|---|--- |---|
| REQ-500 (parte) | "Até a 047, a tabela de transições do front diz a rota de cada transição" não tem AC; o AC-500 só cobre o fechamento | AC-500: acrescentar "Iniciar, Resolver e Cancelar seguem pelo `PUT`, Fechar pelo `close` (teste de componente por transição)" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-501 (parte do front) | "O front manda só o campo da intenção" sem AC de front; a T-502 aponta para o AC-501 e o AC-502, que são da API | AC-501 e AC-502: acrescentar "o teste de Data Service confere o corpo do `PUT` com um campo só" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-502 (parte) e FE-06 | "`detail` não traz ids técnicos" sem AC | AC novo, na T-503: "um erro de `TicketErrors` (ex.: `PUT` com status 4 num Resolvido) responde `detail` sem GUID; o teste de catálogo reprova id interpolado" | Aplicado: AC-524 (T-503). |
| DEC-62 (parte) | "Limites de texto publicados no OpenAPI" sem REQ, AC nem tarefa; a linha 15 da 2.13.3 aponta a T-512, que só muda o front | REQ-502 ganha o item; AC novo: "o OpenAPI de `POST tickets` mostra `maxLength` de título e descrição vindo da constante do EF"; tarefa T-503 | Aplicado: REQ-502 e AC-525 (T-503). |
| REQ-504 (parte do front) e FE-07 | "O front grava os tokens novos" sem AC de front e sem tarefa de front (a T-505 é do back) | AC-508 com a parte do front ("o `ProfileEditStore` grava o par novo") e uma tarefa de front no FE-3 ou na rodada do front da 046 | Aplicado: T-520 e AC-508. |
| REQ-505 (partes) | Reativar em lote (o "Desfazer") e RN-17, RN-18 e RN-19 por item sem AC; o AC-509 só desativa | AC-509: "o Desfazer reativa numa requisição; um item recusado por RN-17, RN-18 ou RN-19 volta com o código e não impede os outros" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-506 (parte) | `uploadedByName` sem AC | AC-511: "`GET tickets/{id}/attachments` traz `uploadedByName`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-507 (parte) | `serverNowUtc` em `GET tickets/mine` (com a 024) sem AC | AC-512 ou um AC da 024: "`GET tickets/mine` traz `serverNowUtc`" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-508 (parte do front) | O Início mandar `departmentId` sem AC de front; o AC-513 é da API, e a T-511 aponta para ele | AC-513: "o Início manda `departmentId`, sem expandir em setores (spec de `dashboard-filters`)" | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| REQ-509 (parte) | Usuário sem `tickets:view-own` (Meus Chamados) fora do AC-514 | AC-514: terceiro usuário sem `tickets:view-own`, sem link para Meus Chamados, e `/tickets` redireciona | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| T-504 (partes) | "Erro no campo certo" e "espera pelo `Retry-After`" sem AC de front | AC novo de front: "um 400 com o campo mostra a mensagem no campo" e "um 429 com `Retry-After` faz o `CurrentUserService` esperar o tempo do cabeçalho" | Aplicado: AC-523. |
| T-512 (parte) | Troca de `delete` por `deactivate` nos Data Services de departamento e setor (FE-21) sem AC | AC-515: "os Data Services de departamento e setor chamam `deactivate`" (ou só os textos, se o nome ficar) | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| T-515 e AC-517 | O Monitor também importa o espelho (ver E-09); o AC-517 cita só detalhe e Meus Chamados | AC-517 e T-515 incluem o Monitor, ou registram que ele fica para depois | Aplicado como proposto (vínculo com a marca de ajuste no `spec.md`). |
| AC-518 (parte do front) | "O front não chama nenhuma delas" depende da T-511 (sai o `sla-summary`), que não cita o AC-518 | A T-511 cita o AC-518 | Aplicado: a T-511 cita o AC-518. |
| DEC-61 (opção (d)) | "Versão do chamado numa spec de contrato" sem item na seção 6 | Q nova: "Versão do chamado (RB-09: `ETag` ou `If-Match`, 409 ou 412)", para uma spec de contrato | Registrado: Q-83. |
| FE-25 | SEM VÍNCULO | Q nova (registro) ou tarefa curta no FE-3: tratar o 400 `Tickets.VolumeTooManyPoints` e tirar a conta local; liga a linha do `ticket-dashboard.store.ts` | Registrado: Q-81. |
| FE-28 | SEM VÍNCULO | Q nova: "Leitura do painel rápido do Monitor (F-32)", para a 043p1, ou a 043p1 cita o painel | Registrado: Q-84. |
| FE-22 (parte do front) | Sem REQ, AC e tarefa para tirar `responsibleId` e completar o enum do histórico | Tarefa no FE-0 (ou na T-514) e AC: "o front não tem `responsibleId`; `TicketHistoryAction` tem 12 e 13" | Aplicado: T-518 e AC-521. |
| FE-16 (parte da 023) | O uso do `responsibleUserId` da listagem (023 entregue) sem tarefa | Tarefa no FE-0 (ver C), com AC: "editar departamento não chama `GET departments/{id}`" | Aplicado: T-519 e AC-522. |

## B.9 Evidências das rodadas de revisão

> Movido da seção 10 do `spec.md` em 2026-11-01, por autorização do responsável, sem mudança de conteúdo. No `spec.md` ficou um ponteiro, e a conferência da rodada mais recente continua lá.

- **Terceira rodada: reconferência, reorganização e consolidação.**
  - **Natureza dos achados** (2.15.1): 173 fatos comprovados pela leitura, 14 hipóteses não reproduzidas e 2 pontos que dependem de execução. Cada achado tem estratégia de validação e regressão prevista (anexo A), e o REQ-28 obriga a derivada a validar antes de corrigir.
  - **Divergências encontradas:** 82, todas com tratamento no anexo A:
    - 36 corrigidas no registro;
    - 29 aplicadas como ajuste em requisito, critério, tarefa ou plano;
    - 14 levadas a conciliação ou a decisão (CT-12 a CT-20);
    - 3 registradas.
    As de maior peso:
    - DEC-11, que voltou a pendente (CT-13);
    - a leitura sem cache contra a RN-17 (CT-19);
    - a versão contra as travas (CT-12);
    - o contrato de erro (CT-14);
    - o congelamento dos testes contra a DEC-1 (CT-16);
    - os registros F-37, F-42, OR-05, OR-12, DB-05, FE-08 e AZ-17, corrigidos.
  - **Cobertura do inventário** (7.16.1, B.7): 44 lacunas nas 350 linhas; 31 aplicadas e 13 registradas com destino.
  - **Vínculos** (B.8): 86 lacunas parciais apontadas pelas áreas; 79 aplicadas, 5 registradas e 2 conciliadas. O resultado gerado das tabelas (9.4) não tem nenhum requisito, critério, tarefa, decisão ou achado sem vínculo: 119 requisitos, 139 critérios, 106 tarefas, 70 decisões, 77 itens fora do escopo e 189 achados.
  - **Plano** (7.15): toda tarefa tem etapa, e os critérios de cada etapa são calculados das tarefas dela. A conferência das dependências corrigiu 13 pontos (7.15.2); com os portões da 7.15.1, nenhuma etapa depende de decisão sem aprovação, de spec não concluída ou de etapa posterior.
  - **Preservação** (script `conferir_preservacao.py`, fora do repositório): das 3.293 linhas não vazias da versão anterior:
    - 3.033 estão iguais no conjunto novo;
    - 156 estão preservadas com acréscimo (ajuste ou correção);
    - 104 foram reescritas de propósito: guia de leitura e faixas de IDs, contagens que mudaram com a DEC-11, a 7.15 com os portões, a 7.18.2 substituída pelo inventário, os sete itens fora do escopo que ganharam ID (Q-70 a Q-76), as correções de fato listadas no anexo A e a nota de resolução do item de auditoria da rodada anterior.
    Todas as 741 linhas com ID da versão anterior existem no conjunto novo. O texto integral das decisões, como foram escritas, está no anexo E.
  - **Tamanho:** o `spec.md` tem cerca de 424 KB, abaixo do limite de 500.000 bytes da auditoria; os anexos têm cerca de 294 KB (A), 127 KB (B), 167 KB (C), 82 KB (D) e 40 KB (E).
  - **Auditoria SDD depois da reorganização:** `python -I scripts/auditoria_sdd.py .`, só com saída no console, rodou depois da cópia da spec reorganizada para a pasta: score 100%, CONFORME, sem falha crítica. A SDD-03 passou (46 specs completas, nenhuma incompleta), e a SDD-04 encontrou 315 IDs de requisito (antes, os desta spec não entravam na conta, porque o arquivo passava do limite).
- **Quarta rodada: decisões de 2026-11-01 e conferência das condições.**
  - **DEC-2:** os cinco handlers de mensagem e anexo chamam o gravador de atividade depois do `SaveChangesAsync`, e não há transação explícita no código nem no `IApplicationDbContext` (base da CT-21).
  - **DEC-5:** a tabela do histórico vem do `DbSet` `TicketHistories`, sem `ToTable`. O `TicketHistory` não é `IAuditable`, e a auditoria grava o nome curto da classe (`AuditLoggingInterceptor.cs:58`). Nenhuma rota usa o `ProcessSlaBreaches`. O `LoggingDecorator` registra o nome do tipo (`LoggingDecorator.cs:17,47`). Detalhe na 7.5.1.
  - **CT-18:** só existem dois handlers de evento em `src` (`TicketAttachmentDeletedDomainEventHandler` e o vazio `UserRegisteredDomainEventHandler`). O padrão 7 da `domain-modeling` diz que o método criador do agregado dispara o evento do filho.
  - **Inventário e rastreabilidade:** o inventário das decisões (seção 5) passou a contar as decididas. A rastreabilidade (seção 9) foi gerada de novo depois das mudanças.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, só com saída no console, rodou depois da cópia: score 100%, CONFORME, sem falha crítica; a SDD-03 continua com 46 specs completas e nenhuma incompleta. O `spec.md` tem cerca de 451 KB, abaixo do limite de 500.000 bytes.
- **Segunda resposta de 2026-11-01: conferências feitas só por leitura (`git show` e `git diff` da `main` `3305fb5`).**
  - **Índice parcial dos convites (DEC-22): não existe.**
    - A `UserInvitationConfiguration.cs:18,21` tem só o índice único de `TokenHash` e o índice comum de (`UserId`, `CreatedAtUtc`).
    - A migration `20261027025440_Add_User_Invitations` (`:42-53`) e o snapshot (`:1235-1240`) têm os mesmos dois; nenhuma outra migration cita `user_invitations`.
    - Um banco real não foi consultado, porque está fora da autorização.
    - O reenvio já revoga todo convite não usado e não revogado, inclusive vencido, antes de inserir (`UserInvitations.cs:44,67-81`), que é o predicado do índice.
  - **Login e desativação (CT-26):**
    - o contador e o bloqueio ficam na credencial (`UserCredential.cs:27,32`) e são gravados pelo verificador (`UserCredentialsVerifier.cs:56-84`);
    - a renovação recusa conta inativa (`RefreshTokenCommandHandler.cs:65`) e compara o carimbo (`:52`);
    - a desativação revoga convites e sessões sem trocar o carimbo (`DeactivateUserCommandHandler.cs:60-71`);
    - a reativação só muda a situação (`ActivateUserCommandHandler.cs:44-49`).
  - **Auditoria do contador (CT-28):** a credencial é `IAuditable`. O interceptor lê o rastreador do EF e, da credencial, deixa de fora só os valores de `PasswordHash` e `SecurityStamp` (`AuditLoggingInterceptor.cs:19-24,45-79`).
  - **Janela da atividade (CT-21 e CT-27):**
    - o `PostMessage` carrega o chamado sem rastreio (`PostTicketMessageCommandHandler.cs:27-28`);
    - conclui a primeira resposta por um cronômetro rastreado à parte (`:58-64`);
    - chama o gravador fora do `try`, depois do commit (`:88`), e o gravador grava por SQL com `GREATEST` (`TicketActivityWriter.cs`).
  - **DEC-29:** as specs 036, 037 e 043 foram lidas na working tree (contexto, requisitos, decisões, plano e tarefas). A dependência real está na 7.15.4.
  - **024:** `git diff origin/main...feat/SPEC-024-own-tickets-current-sla-cycle`, no commit `d2f703f`, sem mudar a branch. São 6 arquivos: 2 de produção, 2 de teste, a spec 024 e o `SYSTEM_SPECIFICATION.md`. A spec 024 está `Verificada` nela, e o PR #61 (aberto, não mergeado) aponta para o mesmo `d2f703f` (`gh pr view 61`, só leitura). O commit local seguinte, `c198fda`, só preenche o `pr:` da spec 024 (7.17.1).
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, só com saída no console, rodou depois da cópia: score 100%, CONFORME, sem falha crítica. A SDD-03 continua com 46 specs completas e nenhuma incompleta, e a SDD-04 encontrou 318 IDs. O `spec.md` tem cerca de 495 KB: fica abaixo do limite de 500.000 bytes, mas com margem de uns 5 KB. A próxima rodada precisa mover conteúdo para os anexos, com autorização.
- **Terceira resposta de 2026-11-01: conferências feitas só por leitura (`git show` da `main` `3305fb5`).**
  - **CT-26:**
    - o login lê o usuário e a credencial em duas consultas (`UserCredentialsVerifier.cs:32-34,44-45`);
    - a renovação lê usuário e carimbo numa consulta só (`RefreshTokenCommandHandler.cs:38-49`), remove a sessão com carimbo diferente (`:52-61`) e recusa conta inativa sem remover (`:65-68`);
    - o access token leva `sub`, `email` e `name` (`TokenProvider.cs:26-28`) e vale `Jwt:ExpirationInMinutes` (`:34`; 60 no `appsettings.Development.json`). A autenticação não confere carimbo a cada requisição: não há `OnTokenValidated` nem claim de carimbo;
    - a desativação remove as sessões que existem no momento (`UserSessions.cs:61-73`) e não muda o carimbo.
  - **CT-27:**
    - as chamadas do gravador nos cinco handlers (linhas na própria CT-27);
    - a interface recebe a entidade (`ITicketActivityWriter.cs:15,21`);
    - as calculadoras do `Ticket` (`Ticket.cs:200,207`).
  - **CT-28:**
    - o interceptor de auditoria age no `SaveChanges` (`AuditLoggingInterceptor.cs:26-33`) e lê o rastreador (`:45`);
    - as rotas de autenticação têm limite de 10 requisições por minuto por IP (`appsettings.json`, seção `RateLimiting`; `RateLimitingExtensions.cs`);
    - o único teste de auditoria da credencial é o da ativação (`AuditLogsTests.cs:128-152`).
  - **DEC-29:** o grafo do plano foi conferido por script fora do repositório: 41 nós, 73 arestas, sem ciclo e sem aresta contra a ordem da DEC-18 (7.15.4).
  - **PR #61 (fato do fim da rodada):** `gh pr view 61` mostra o merge em 2026-11-02T00:13:42Z pela conta `rafaelps15` (`74a8a5f`, head `c198fda`), e a `origin/main` está em `74a8a5f` (7.17.1).
  - **Anexo A:** a seção 2.15.2, o índice dos 189 achados, foi transferida para o início do anexo A, por autorização do responsável, sem mudança de conteúdo: 218 linhas e 31.044 bytes, em 6 grupos por área. No `spec.md` ficaram a 2.15, a 2.15.1 (validação e contagens) e um ponteiro com o título 2.15.2. A conferência foi feita por script fora do repositório, comparando o conjunto gerado sem e com a transferência:
    - o bloco é igual linha a linha no destino;
    - as linhas de tabela com ID são as mesmas: 1.523, com 842 IDs distintos;
    - as menções de ID são as mesmas: 12.929;
    - a rastreabilidade lê o índice no anexo A e continua sem órfãos.
    Sem a transferência, o `spec.md` teria cerca de 520 KB, acima do limite; com ela, fica com cerca de 490 KB, e o anexo A com cerca de 326 KB.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, só com saída no console, rodou depois da cópia: score 100%, CONFORME, sem falha crítica. A SDD-03 continua com 46 specs completas e nenhuma incompleta, e a SDD-04 encontrou 319 IDs. O `spec.md` tem cerca de 490 KB, e o anexo A, cerca de 326 KB.
- **Quarta resposta de 2026-11-01:** conferências só por leitura, nos objetos da `origin/main` (`74a8a5f`). Os fatos da CT-26 estão no anexo C (2.10.9), os da DEC-22 no ciclo de vida (2.8.4), e o limite da auditoria está em `scripts/auditoria_sdd.py:148-151` (arquivo com mais de 500.000 bytes é lido como vazio). A working tree da sessão 035 ficou fora do escopo, sem leitura nem alteração, e as observações sobre ela saíram da 049. Auditoria SDD: `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs.
- **Décima resposta de 2026-11-04 (rodada 3, primeira parte):** conferências só por leitura nos objetos da `origin/main` (`1eef2aa`), depois dos merges da SPEC-051 (PR #64) e da SPEC-050 (PR #65):
  - **DEC-34:** `Infrastructure/DependencyInjection.cs:188` registra a autorização sem `FallbackPolicy`; `Web.Api/Program.cs:50` mapeia o `health` sem exigir autorização; as quatro rotas anônimas de propósito têm `.AllowAnonymous()` (`Web.Api/Endpoints/Users/ActivateAccount.cs:31`, `ChangeRequiredPassword.cs:28`, `Login.cs:28` e `Refresh.cs:28`).
  - **DEC-36 e CT-31:** `Infrastructure/Authentication/JwtOptions.cs:27-28` aceita de 1 a 1.440 min, e o refresh token vale de 1 a 365 dias, padrão 7 (`:33-34`). O `appsettings.json:26` traz 0, e o `appsettings.Development.json:28` e o `IntegrationTestWebAppFactory.cs:45` usam 60. Nenhum arquivo do repositório, inclusive os `docker-compose*.yml` e o `.github/workflows/build.yml`, define o valor de produção; o `SYSTEM_SPECIFICATION.md:611` documenta a faixa de 1 a 1.440. A sessão segue o refresh token: se o valor de produção passar de 60, ele precisa baixar antes da implantação, e o access token passa a ser renovado com mais frequência.
  - **DEC-46:** `Infrastructure/Audit/AuditLoggingInterceptor.cs:21-23` exclui só `PasswordHash`, `SecurityStamp` e `TokenHash`.
  - **DEC-68:** o back tem só `IntegrationTests/Swagger/SwaggerTests.cs`; o front não foi reconferido, porque é outro repositório.
  - **CT-30:** a saída das etapas (7.15), o AC-16, o AC-221, a T-215 e a T-400 pediam a suíte inteira local; a SPEC-051 roda local só os testes afetados e deixa a suíte completa para o CI.
  - **Requisitos:** as marcas `[SUPOSIÇÃO — DEC-n]` saíram do REQ-201, do REQ-212, do REQ-309 e do REQ-511, com o texto alinhado às condições: no REQ-201, os status de rota inexistente e de método não suportado ficam como esperados até o teste reproduzi-los; no REQ-212, a falha explícita na subida; no REQ-511, o repositório Angular e a ausência de geração de tipos. Restam 26 marcas: 23 de 21 decisões pendentes desta spec e 3 de decisões de outras specs (DEC-9 e DEC-10 da 043; DEC-1 e DEC-3 da 047).
  - **Edição:** por script, com cópia de segurança antes e cada trecho conferido como único. Nenhum ID perdeu ocorrência; entraram no `spec.md` a CT-30, a CT-31 e a SPEC-051. A B.10 foi atualizada com a regra da 9.4 (20 decididas e 22 pendentes).
  - **Tamanho:** o `spec.md` passou de 494.454 para 497.562 bytes. com margem de 2.438 até o limite de 500.000 (`scripts/auditoria_sdd.py:148-151`). Para isso, o texto desta rodada no `spec.md` ficou curto, e o detalhe ficou neste anexo e no anexo E (E.2).
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, só com saída no console: score 100%, CONFORME, sem falha crítica; a SDD-03 e a SDD-04 passaram.
- **Décima primeira resposta de 2026-11-04:** branch, movimentação e CT-26.
  - **Contagem:** conferida por script na coluna "Situação" das cinco tabelas da seção 5: 70 linhas, uma por decisão (DEC-1 a DEC-71, sem a 49 e sem repetição), com 20 decididas, 22 pendentes, 1 a confirmar (DEC-13), 26 aprovadas com a spec e 1 sem objeto (DEC-48). A divergência apontada pelo responsável (43 itens) estava no resumo do agente, que omitiu as 26 aprovadas com a spec e a sem objeto; a spec não precisou de correção. A DEC-30 e a DEC-39, aprovadas com a spec, citam na própria linha uma dependência pendente (a DEC-9 da 043 e a DEC-14).
  - **Branch:** o `git switch` do agente foi barrado pelo portão SDD, porque a troca reescreveria `src/` e `tests/`; o responsável atualizou a `main` (`git pull --ff-only`, de `ef1c981` para `e5d8a38`, 45 commits, os 5 últimos do Dependabot: MailKit 4.18.2 e OpenTelemetry 1.20.1) e criou a branch `refactor/SPEC-049-tickest-architecture-reassessment` com `--no-track`, a partir da `origin/main`. Estado conferido só na pasta da 049: sem upstream, 0 commits atrás e 0 à frente da `origin/main`, a pasta da 049 fora do git.
  - **Fato desatualizado, sem edição:** o anexo A (linha 505) cita o OpenTelemetry 1.16.0; desde `e5d8a38`, os pacotes do OpenTelemetry estão na 1.20.1. O texto do achado não foi alterado.
  - **Movimentação:** por script, com cópia de segurança antes; cada bloco foi copiado sem mudança e conferido como igual no destino: a seção 10 (12.276 bytes) na B.9.1, a 5.6 (11.599 bytes) na E.2.1 e o histórico da CT-26 (5.820 bytes) na 2.10.9 do anexo C. Nos seis arquivos juntos, nenhum identificador perdeu ocorrência.
  - **Tamanho:** o `spec.md` passou de 497.562 para 472.635 bytes, com margem de 27.365 até o limite de 500.000.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, depois da movimentação: score 100.0%, CONFORME, 0 falha(s) crítica(s); saída 0.
- **Décima segunda resposta de 2026-11-04 (rodada 3, segunda parte):** conferências só por leitura na branch `refactor/SPEC-049-tickest-architecture-reassessment`, igual à `origin/main` (`e5d8a38`):
  - **DEC-32:** a junção de permissões efetivas é montada em `Infrastructure/Authorization/PermissionProvider.cs:15-19` (com cache), `Application/Tickets/TicketAgentLookup.cs:19-21`, `Application/Tickets/TicketAssigneeEligibility.cs:17-19` e `Application/Authorization/PrivilegeEscalationGuard.cs:46-48`; a DEC-11 da 043 (uma fonte só para quem atende) continua pendente lá.
  - **DEC-67:** o `TicketScopeFilter` só tem `SectorIds` (`Application/Tickets/TicketScopeFilter.cs:14-23`); o front não foi reconferido.
  - **DEC-42:** nem o `ReopenTicketCommandHandler` nem o `TicketSlaCycleService` conferem o setor; o teste da 022 está em `IntegrationTests/Departments/DepartmentsTests.cs:604`.
  - **DEC-13:** o `TicketSlaTimer` tem `StartedAtUtc` e `WarningOnUtc`, `DueOnUtc`, `CompletedOnUtc`, `StoppedOnUtc`, `BreachedOnUtc` e `NextActionOnUtc` (`Domain/Tickets/TicketSlaTimer.cs:30-44`).
  - **Edição:** por script, com cópia de segurança antes e cada trecho conferido como único; a tabela de contagem da seção 5 foi recalculada da coluna "Situação" (25 decididas, 18 pendentes, 0 a confirmar, 26 aprovadas com a spec e 1 sem objeto, total 70); nenhum identificador perdeu ocorrência no conjunto dos seis arquivos, e cada linha de tabela alterada manteve o número de colunas.
  - **Tamanho:** o `spec.md` passou de 472.635 para 477.186 bytes, com margem de 22.814.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, depois da edição: score 100.0%, CONFORME, 0 falha(s) crítica(s); saída 0.
- **Décima terceira resposta de 2026-11-04 (conferência complementar, só leitura, e ajustes autorizados):** leitura dos arquivos da branch `refactor/SPEC-049-tickest-architecture-reassessment`, igual à `origin/main` (`e5d8a38`), sem comandos do git; no GitHub, só consultas de leitura pelo `gh`.
  - **CT-19:** `Infrastructure/Authorization/PermissionProvider.cs` filtra `user.IsActive` e usa cache em `GetForUserIdAsync`; `Application/Authorization/PrivilegeEscalationGuard.cs` (`EnsureCallerCanManageUserAsync`) lê as permissões do alvo direto das funções, sem o filtro de ativo, porque o cache fica vazio para usuário inativo. A reativação está coberta por `Activate_Should_ReturnForbidden_WhenTargetHasAdministrativePermissionsCallerLacks` (`IntegrationTests/Users/UsersTests.cs:1735`); a troca de setor só tem `ChangeSector_Should_ReturnForbidden_WhenCallerLacksUpdatePermission` (`:2003`), que não cobre alvo privilegiado inativo. A DEC-32 (b) decidiu a fonte, mas não a forma, e o REQ-203 ainda oferecia a consulta própria; por isso a CT-19 foi reaberta.
  - **DEC-11 e CT-13:** a situação da DEC-11, a CT-13, a orientação no REQ-4, o AC-7 e a T-9 preservavam a orientação; cinco textos divergiam e foram corrigidos: a frase do REQ-4 sem "global", o título da T-9, o item 2 da 7.7.1, a linha 11 da tabela de padrões (F-08) e a situação da DEC-11, que não dizia que a via da CT-13 não é a (a) nem a (b). Os dois testes de integração da CT-13, citados por linha da base de 2026-11-01 (`TicketsTests.cs:5006-5080`), são, pelo conteúdo, `PostMessage_Should_CompleteFirstResponse_WhenMonitorBreachesItBeforeTheHandlerSaves` (`:5340`) e `Cancel_Should_StopTimersAndKeepBreach_WhenMonitorBreachesThemBeforeTheHandlerSaves` (`:5381`); a identificação pela base não foi feita, porque o git ficou fora desta conferência. O teste de corrida da exclusão durante a gravação (AC-7) ainda não existe; `SlaMonitor_Should_IgnoreDeletedTicket` (`:5659`) cobre só chamado já excluído.
  - **Marcas de suposição:** 22 no `spec.md`, uma em cada um de 22 requisitos, ligadas a 17 das 18 decisões pendentes (a DEC-35 e a DEC-59 em dois requisitos cada; a DEC-14 sem marca) e, em três marcas, a quatro decisões externas: a DEC-9 da 043 (REQ-202, só no trecho do `TicketAccessGuard`), a DEC-10 da 043 (REQ-301) e a DEC-1 e a DEC-3 da 047 (REQ-512). Na 043, que está `Aprovada`, a DEC-9 e a DEC-10 aparecem como pendentes de aprovação; na 047, `Em descoberta`, a DEC-1 e a DEC-3 estão em aberto.
  - **Consumidores da API (CT-15):**
    - **comprovado, externo:** o front Angular em `C:\dev\tickest-pristine-front`, com remoto `github.com/rafaelps15/tickest-pristine-ui`. Nove serviços de dados e 40 arquivos citam rotas. Ele não chama `GET users/by-email`, `GET users/attendants`, `GET sectors`, `GET sectors/{id}`, `GET audit-logs` nem `PUT users/{id}/sector`; chama `GET tickets/sla-summary` (`src/app/tickets/application/ticket-summary.store.ts:104`, `ticket.data.service.ts:191-193`), enquanto o monitor já usa o `stats/overview` (`ticket-monitor.store.ts:79-84`); e chama `DELETE tickets/{id}` (`ticket.data.service.ts:52`). A leitura foi da pasta de trabalho, que pode ter alterações sem commit. O `environment.prod.ts` aponta para `http://localhost:5195`, o que não prova a ausência de ambiente.
    - **comprovados, internos:** os testes de integração chamam `users/by-email` (4 vezes), `users/attendants` (7), `tickets/sla-summary` (5), `sectors/{id}` em todos os verbos (19) e `audit-logs` (3); o Swagger existe só em Development.
    - **conferidos e não consumidores:** a API não faz chamada HTTP de saída (no código, só a instrumentação do OpenTelemetry); o monitor de SLA roda no processo; o `docker-compose.yml` tem `web-api`, `postgres`, `seq` e `mailpit`; o CI não chama a API; não há `.http` nem coleções no repositório; a autenticação é só JWT de usuário (`AddJwtBearer`); o `Cors:AllowedOrigins` do `appsettings.json` está vazio, e o Development aceita loopback. Em `C:\dev`, o `clean-architecture` e o `prefeitura-barra-guarita` só definem uma rota própria `users/refresh-token`; o `tickest-antigo`, o `tickest-pristine-antigo` e o `ticket-pristine` do GitHub são backends antigos; o `tickest-pristine-front-023` só tem `node_modules`; o `user-api`, o `tel`, o `tickest-pristine-sdd` e a `documentação` não citam a API. No GitHub, além deste backend, só o `tickest-pristine-ui` se liga ao Tickest.
    - **possíveis, não comprovados:** outros clientes de navegador configurados no CORS de produção; automações, scripts, BI ou integrações com conta de usuário comum; monitor de disponibilidade ou balanceador no `/health`; uso manual pelo Swagger ou pelo Postman; versão antiga do front ainda publicada.
    - **desconhecidos, sem resposta negativa:** os seis itens registrados na CT-15.
  - **Edição:** por script, com cópia de segurança antes e cada trecho conferido como único; nenhum identificador perdeu ocorrência no conjunto dos seis arquivos, e cada linha de tabela alterada manteve o número de colunas.
  - **Tamanho:** o `spec.md` passou de 477.186 para 482.086 bytes, com margem de 17.914.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, depois da edição: score 100.0%, CONFORME, 0 falha(s) crítica(s); saída 0.
- **Décima quarta resposta de 2026-11-04 (modelo de aprovação por etapa):** só ajustes de texto na 049, sem código, skills nem `CLAUDE.md`.
  - **Marcas:** as 22 marcas `[SUPOSIÇÃO — ...]` dos requisitos viraram `[DELEGADO — decisão; etapa]`, sem mudar o texto dos requisitos, dos critérios nem das tarefas: G3-b (REQ-110, REQ-112, REQ-210, REQ-211; REQ-411 antes da primeira migration nova), G3-c (REQ-303, REQ-304, REQ-310), G3-d (REQ-400, REQ-410, REQ-502, REQ-506; REQ-513 na T-516), G4 (REQ-215, REQ-404), G5 (REQ-412), G6 (REQ-504, REQ-507; REQ-512 antes da G6, pela 047), G7 (REQ-418, só o registro) e T-16, na G1 (REQ-202 e REQ-301, pela 043).
  - **Recomendações:** as 26 "aprovadas com a spec" passaram a "avaliar no portão da etapa": G1 (DEC-12, DEC-33, DEC-37, DEC-41, DEC-50, DEC-55 a DEC-58, DEC-60 e DEC-71), G3-a (DEC-6 a DEC-10, DEC-15 e DEC-16), G3-b (DEC-24, DEC-25, DEC-27, DEC-28, DEC-30 e DEC-39), G3-c (DEC-40) e G4 (DEC-45). Nenhuma é exigida na G0: o portão dela não cita decisão, e as tarefas T-1, T-13, T-100, T-301 e T-400 só citam a DEC-17 e a DEC-29, já decididas.
  - **Portões:** a DEC-69 saiu dos portões da G2 e da G3-b e ficou na G3-d (T-516); a DEC-59 entrou na G3-b, antes da primeira migration nova, e na G7 ficou só o registro.
  - **Conferências:** cópia de segurança antes; cada trecho conferido como único; nenhum identificador perdeu ocorrência no conjunto dos seis arquivos; as linhas de tabela alteradas mantiveram o número de colunas; nenhuma marca `[SUPOSIÇÃO — ...]` ficou fora do histórico.
  - **Tamanho:** o `spec.md` passou de 482.086 para 488.295 bytes, com margem de 11.705.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, depois da edição: score 100.0%, CONFORME, 0 falha(s) crítica(s); saída 0.
- **Linha de base da G0 (2026-11-04), no `e5d8a38`, igual à `main`:**
  - **Build (T-1 e T-400, AC-16):** `dotnet build TickestPristine.slnx`: 0 avisos e 0 erros.
  - **Testes locais:** `TickestPristine.Application.UnitTests`, 905 aprovados e 0 falhas; `TickestPristine.ArchitectureTests`, 60 aprovados e 0 falhas.
  - **Suíte completa (CT-30):** o workflow `Build` do CI passou no push do `e5d8a38` (2026-11-04).
  - **Contagens (T-400, como a 2.12.4):** unitários, 92 arquivos `*Tests.cs`, 719 métodos (650 `[Fact]` e 69 `[Theory]`), 255 `[InlineData]` e 905 casos; integração, 30 arquivos, 500 métodos (476 `[Fact]` e 24 `[Theory]`), 70 `[InlineData]` e 2 `[MemberData]`; arquitetura, 7 arquivos, 34 métodos (28 `[Fact]` e 6 `[Theory]`), 32 `[InlineData]` e 60 casos. Na base da 045 (`bc2395c`), eram 707 métodos e 892 casos unitários e 462 métodos de integração em 27 arquivos. Os testes de evidência da SPEC-050 usam um atributo próprio e não entram nas contagens.
  - **Usuários (T-100):** 187 métodos unitários, como previsto, e 128 de integração, igual à 2.12.4 (a T-100 cita 135). O `sub` é gravado como `JwtRegisteredClaimNames.Sub` (`Infrastructure/Authentication/TokenClaimTypes.cs:10`) e lido como `ClaimTypes.NameIdentifier` (`ClaimsPrincipalExtensions.cs:12`, `RateLimitingExtensions.cs:64`), sem `MapInboundClaims` explícito: vale o mapeamento padrão.
  - **Organização (T-301, AC-307):** build sem avisos e testes verdes; nenhum `new Department {` nos testes; o `DepartmentsTests` tem dois records de listagem, o `DepartmentListItemDto` (com `ResponsibleUserId` e `ActiveTicketCount`) e o `DepartmentSummaryDto`, usado no teste de exclusão para ler `GET departments` (`DepartmentsTests.cs:314-317`). O AC-307 pede um só; a decisão é do responsável.

### B.9.1 Seção 10 do `spec.md` (movida em 2026-11-04)

> Movido da seção 10 do `spec.md` em 2026-11-04, por autorização do responsável, sem mudança de conteúdo. No `spec.md` ficou a situação atual, com referência a esta seção.

Evidências da investigação; as da implementação entram na `spec-verify`.

- **Arquivos lidos diretamente:** listados na 2.2; trechos citados com `arquivo:linha` nas seções 2.3 a 2.7.
- **Conferências feitas no código sobre afirmações dos subagentes:**
  - o `UPDATE` do gravador muda a versão da linha: `SYSTEM_SPECIFICATION.md:601` (045) contra `ITicketActivityWriter.cs:5-9`;
  - o `Down` de `Store_Ticket_Enums_As_Names` cobre só as ações 1 a 10 (`:69-80`);
  - `TicketHistoryRecordedDomainEvent` nunca é disparado: busca por `.Raise(` e pelo nome em `src`;
  - só dois handlers de evento existem em `src`;
  - a lista de tipos está repetida no inspetor (`AttachmentContentInspector.cs:53,75-116`);
  - a chave do `LocalFileStorage` é um `Guid` com a extensão (`LocalFileStorage.cs:20`);
  - as rotas consumidas pelo front-end (2.5, 7.8).
- **Contagens:** arquivos, handlers e rotas por `git ls-tree` na branch da 045; métodos de teste por busca de `[Fact]` e `[Theory]` na 045.
- **Segunda rodada: conferências no código** (em `bc2395c`, salvo indicação):
  - corrida da organização (OR-01): `CreateSectorCommandHandler.cs:15-35` lê o departamento sem rastreio e confere só `IsActive`; `DeactivateDepartmentCommandHandler.cs:29-50` lista os setores ativos daquele instante;
  - último administrador (US-02, AZ-04): `AdministratorGuard.cs:44-55` conta "outro administrador ativo" só por `user.IsActive`, sem olhar a credencial, e incrementa a `Version` sombra pelo `context.Entry`;
  - contador de login (US-03, DB-08): a `UserCredentialConfiguration` não tem token; os tokens existem só em 5 configurações e 7 propriedades (GL-03);
  - negar por padrão (AZ-01): `Infrastructure/DependencyInjection.cs:190` registra só a `DefaultPolicy`;
  - validade do token (SE-01): `JwtOptions.cs:27-28`, `[Range(1, 1440)]`; limite global em `appsettings.json:36-40` (100 por 60 s);
  - FE-01: no front (`e977f64`), `ticket-status.ts:41` leva Resolvido para Fechado pelo `PUT`, sem chamar `close`; no back, o `UpdateTicketCommandHandler` recusa chamado Resolvido como não ativo;
  - FE-03: no front, `users.store.ts:145,371,417` faz uma requisição por usuário (`mergeMap`);
  - eventos (PL-06): `PublishDomainEventsInterceptor.cs:14-41` coleta no `SavedChangesAsync`, pelo `ChangeTracker.Entries<Entity>()`;
  - erros do banco (PL-02): `GlobalExceptionHandler.cs:20-48` só reconhece `DbUpdateException { InnerException: PostgresException }`;
  - auditoria (AU-01): `AuditLoggingInterceptor.cs:26-51` monta as entradas a partir do `ChangeTracker.Entries<IAuditable>()`, no `SavingChangesAsync`;
  - ordem dos decorators: `Application/DependencyInjection.cs` aplica Validation → Logging → handler (o último `Decorate` é o mais externo), como diz o `CLAUDE.md`; o template `clean-architecture` usa a ordem inversa;
  - referência `modular-monolith-with-ddd`: o `#if !DEBUG` do `HasPermissionAuthorizationHandler` aprova toda permissão fora de DEBUG. Por isso a referência não serve de modelo nesse ponto (2.14);
  - rotas: 61 classes `IEndpoint` e 61 chamadas `Map*`; com o `health`, são as 62 rotas da matriz (2.10.3);
  - acoplamento entre pastas (GL-11): `git grep -l "using TickestPristine.Application.<pasta>;"` por pasta.
- **Segunda rodada: contagens refeitas.** As contagens das áreas foram refeitas a partir das próprias tabelas: gravidades, destinos do inventário, situação das DECs e faixas de REQ, AC, tarefas e Q. Elas batem com a 2.8.1, a 7.16 e a 7.18.2.
- **Segunda rodada: estado das branches** (pelo `gh`):
  - PR #60 (022) mergeado às 20:09 UTC (`05e4f0f`);
  - PR #52 (023) mergeado às 20:58 UTC (`e2eb2f6`);
  - PR #59 (045) aberto;
  - entre a `main` atual e a 045, o único arquivo alterado pelos dois lados é o `SYSTEM_SPECIFICATION.md`.
- **Correções feitas na integração:**
  - OR-14 marcado como resolvido; DEC-48 sem objeto;
  - REQ-306, T-301 e OA-0 atualizados para os merges;
  - DEC-26 de volta a pendente (CT-02);
  - números da 2.8 recontados.
- **Integridade da spec:** todas as tabelas têm o mesmo número de colunas em cada linha (conferência automática). Nenhum número de REQ, AC, DEC, tarefa ou Q é definido duas vezes; a lista da 7.18.2 repete de propósito as DECs pendentes. A coluna REQ da tabela de critérios segue o modelo da `spec-new` (`REQ-n`).
- **Rodadas de revisão (2026-11-01):** a reconferência dos achados, a reorganização em anexos, as conferências de cada resposta do responsável e as auditorias SDD de cada rodada estão no [anexo B](anexo-b-inventario.md) (B.9), movidas desta seção em 2026-11-01. A conferência da rodada mais recente vem a seguir.
- **Quinta resposta de 2026-11-01:**
  - **Transferência autorizada:** três blocos, sem mudança de conteúdo:
    - as evidências das rodadas de revisão (seção 10, 10.703 bytes) foram para o anexo B (B.9);
    - os pedidos e as citações das respostas (seção 1, 6.176 bytes) foram para o anexo E (E.2);
    - a ordem das operações da CT-26 (2.8.4, 3.999 bytes) foi para o anexo C (2.10.9).
    Pela medida da proposta, com as quebras de linha finais, são 20.882 bytes. No `spec.md` ficaram três ponteiros. O registro da quinta resposta foi escrito direto na E.2, como texto novo. A conferência, por script fora do repositório, comparou o conjunto gerado sem e com a transferência:
    - os três blocos são iguais linha a linha no destino;
    - as linhas de tabela com ID são as mesmas: 1.524, com 843 IDs distintos;
    - as menções de ID são as mesmas, mais as duas do ponteiro da 2.8.4 (CT-26);
    - a rastreabilidade continua sem órfãos.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs. O `spec.md` ficou abaixo de 500.000 bytes.
- **Sexta resposta de 2026-11-01:**
  - **CT-26:** fatos do cache conferidos por leitura na `origin/main` (`DependencyInjection.cs:52-67`, `Directory.Packages.props:20`, `appsettings*.json` sem tempo limite de comando) e na documentação oficial do HybridCache e do Npgsql; a análise está no anexo C (2.10.9).
  - **Grafo:** 44 nós e 78 arestas, sem ciclo e sem aresta contra a DEC-18 (7.15.4).
  - **Portão SDD:** um comando que só gravava um script na pasta de rascunho foi barrado, como falso positivo, porque o texto tinha a palavra `src`. O portão não foi contornado: o script foi criado pela ferramenta de escrita, fora de `src/` e de `tests/`.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs. O `spec.md` continua abaixo de 500.000 bytes.
- **Sétima resposta de 2026-11-01:**
  - **CT-26:** código-fonte lido pelo GitHub nas versões do repositório: `dotnet/extensions` no commit `25f07afa` (HybridCache 10.11.0) e `dotnet/aspnetcore` na tag `v10.0.14` (`RedisCache.cs`), com a documentação XML de `Microsoft.Extensions.Caching.Abstractions` 10.0.14.
    - Na `origin/main`: `Directory.Packages.props:20` e `:28`, `DependencyInjection.cs:55-64`, `PermissionProvider.cs:38-42` e `appsettings.json:20`; o `docker-compose.yml` não tem Redis, e não há `EnableRetryOnFailure` nem `CommandTimeout` em `src/`.
    - A análise está no anexo C (2.10.9). Nenhum cenário foi executado.
  - **Inventário:** conferido por script na coluna "Situação" das cinco tabelas da seção 5, sem presumir identificadores. São 70 decisões, sem repetição, e o número 49 não foi usado.
    - **Decididas (16):** DEC-1, DEC-2, DEC-3, DEC-4, DEC-5, DEC-17, DEC-18, DEC-19, DEC-20, DEC-21, DEC-22, DEC-29, DEC-31, DEC-61, DEC-64, DEC-70.
    - **Pendentes (26):** DEC-11, DEC-14, DEC-23, DEC-26, DEC-32, DEC-34, DEC-35, DEC-36, DEC-38, DEC-42, DEC-43, DEC-44, DEC-46, DEC-47, DEC-51, DEC-52, DEC-53, DEC-54, DEC-59, DEC-62, DEC-63, DEC-65, DEC-66, DEC-67, DEC-68, DEC-69.
    - **A confirmar (1):** DEC-13.
    - **Aprovadas com a spec (26):** DEC-6, DEC-7, DEC-8, DEC-9, DEC-10, DEC-12, DEC-15, DEC-16, DEC-24, DEC-25, DEC-27, DEC-28, DEC-30, DEC-33, DEC-37, DEC-39, DEC-40, DEC-41, DEC-45, DEC-50, DEC-55, DEC-56, DEC-57, DEC-58, DEC-60, DEC-71. São recomendações que passam a valer com a aprovação formal da spec, que ainda não foi dada.
    - **Sem objeto (1):** DEC-48.
  - **Correção de texto:** um ponto duplo na conciliação da CT-26 ("validações..") foi corrigido.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs. O `spec.md` continua abaixo de 500.000 bytes.
- **Oitava resposta de 2026-11-01:**
  - **CT-26:** a decisão pela (3b) está registrada no anexo C (2.10.9), com os requisitos, as consequências para o desenho e as formas de controlar o relógio. A proposta da tarefa de testes (partes, cenários, arquivos, dependências e critérios) está no mesmo lugar. Nenhum arquivo de teste foi criado, e nada foi executado.
  - **Fatos lidos para a proposta:**
    - na `origin/main`: `IntegrationTestWebAppFactory.cs`, `IntegrationTestCollection.cs`, `Abstractions/EfCommandCounter.cs`, `Tickets/SlaMonitorBackgroundServiceTests.cs:176`, `DeactivateUserCommandHandler.cs:76`, `Directory.Packages.props:52` e o passo de testes do CI (`.github/workflows/build.yml:51`);
    - fora do repositório: `ConfigureDbContext` no EF Core 10.0.14 e `HybridCacheServiceExtensions.cs:24-29` e `:120` no commit `25f07afa`.
  - **Inventário:** a relação integral (identificador, situação e tema) foi gerada por script da seção 5 e do anexo E (E.1) e está no anexo B (B.10). São 16 decididas, 26 pendentes, 1 a confirmar, 26 aprovadas com a spec e 1 sem objeto, num total de 70.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs. O `spec.md` continua abaixo de 500.000 bytes.
- **Nona resposta de 2026-11-01:**
  - **SPEC-050:** a worktree `C:/dev/tickest-pristine-spec050` e a branch `test/SPEC-050-ct26-stamp-cache-validation` foram criadas da `origin/main` (`2804b07`), sem trocar a branch da sessão. Só o `spec.md` foi criado, `Em descoberta`, e a auditoria SDD da worktree deu 100%, CONFORME.
  - **HybridCache 10.11.0:** o construtor, as opções e o caminho sem função de leitura foram conferidos no commit `25f07afa`. O resumo está no anexo C (2.10.9), e o detalhe, na 1.1 da 050.
  - **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .` depois da cópia: 100%, CONFORME, sem falha crítica; SDD-03 com 46 specs completas e nenhuma incompleta; SDD-04 com 319 IDs. O `spec.md` continua abaixo de 500.000 bytes.
- **Décima resposta de 2026-11-04:** evidências no anexo B (B.9).
- **Build e testes:** não executados, porque nenhum arquivo de código, teste ou configuração mudou. A última execução registrada é a verificação da 045, em 2026-10-31 (build com 0 avisos e 1.466 testes aprovados). **Auditoria SDD:** `python -I scripts/auditoria_sdd.py .`, só com saída no console, rodou no fim desta rodada.
  - **Resultado:** score 94,6%, NÃO CONFORME, por uma falha crítica: a SDD-03, nesta spec.
  - **Causa:** o tamanho do arquivo. O script trata como vazio todo arquivo acima de 500.000 bytes (`scripts/auditoria_sdd.py:148-151`), e esta spec passa de 720.000 bytes. Lida como vazia, ela não mostra plano nem tarefas (`:270-275`).
  - **Demais verificações:** todas passaram. Com a versão anterior desta spec (cerca de 123 KB), a auditoria dava 100%.
  - **Correção:** depende de uma escolha do responsável, que nesta rodada não foi feita: anexos na mesma pasta, limite do script ou texto condensado. **Resolvido na consolidação final:** o responsável pediu para reorganizar e condensar sem perder conteúdo (seção 1); o resultado e a auditoria nova estão no item "Terceira rodada" desta seção.
- **Arquivos alterados por esta investigação:** só `specs/049-reavaliacao-arquitetural-dominio-chamados/spec.md` (novo). Os textos de trabalho das áreas ficaram na pasta temporária da sessão, fora do repositório.

## B.10 Relação integral das decisões por situação

> Gerada por script, sem presumir identificadores: a situação vem da coluna "Situação" do inventário (seção 5 do `spec.md`), com a regra da 9.4, e o tema vem da coluna "Pergunta" do anexo E (E.1). São 70 decisões, uma por linha; o número 49 não foi usado. "Aprovada com a spec" é uma recomendação que só passa a valer com a aprovação formal da spec, que ainda não foi dada. As pendentes são avaliadas uma a uma, respeitando os bloqueios e as fases de cada uma (oitava resposta de 2026-11-01).

> Atualizada em 2026-11-04 (décima quarta resposta): pelo modelo por etapa, nenhuma recomendação é aprovada junto com a 049; as 26 que eram "aprovadas com a spec" passam a "a avaliar no portão da etapa", com a etapa de cada uma.

> Atualizada em 2026-11-04 (décima resposta), com a mesma regra: a DEC-34, a DEC-36, a DEC-46 e a DEC-68 passaram de pendentes a decididas. Na décima segunda resposta, a DEC-11, a DEC-32, a DEC-42 e a DEC-67 passaram de pendentes a decididas, e a DEC-13, de a confirmar a decidida.

> Atualizada em 2026-11-04 (portão da G1): a DEC-37, a DEC-50, a DEC-55 a DEC-58, a DEC-60 e a DEC-71 passaram de recomendações a decididas, conforme a coluna "Situação" do inventário.

> Atualizada em 2026-11-04 (T-16): a DEC-12, a DEC-33 e a DEC-41 passaram a decididas, por delegação.

> Atualizada em 2026-11-05 (revisão da G1): a DEC-59 passou a decidida, antes da primeira migration nova do programa.

### Decididas (37)

| Nº | ID | Situação | Tema |
|---|---|---|---|
| 1 | DEC-1 | Decidida: (b) | Fronteira entre o chamado e os cronômetros de SLA |
| 2 | DEC-2 | Decidida: (b) | Última atividade e "aguardando" |
| 3 | DEC-3 | Decidida: (b) | Limite de anexos por chamado sob concorrência |
| 4 | DEC-4 | Decidida: (a) | Ordem de execução |
| 5 | DEC-5 | Decidida: o pacote | Pastas por agregado e nomes |
| 6 | DEC-11 | Decidida (CT-13, 2026-11-04): `UPDATE` condicional só na fila do monitor | Corrida entre exclusão e monitoramento |
| 7 | DEC-12 | Decidida: (b), por delegação | Contrato de SQL dos chamados para a 043 parte 2 |
| 8 | DEC-13 | Decidida: (a) | Sufixo das datas do cronômetro (`*_on_utc` × `*_at_utc`) |
| 9 | DEC-17 | Decidida: (a) | Forma de execução do plano global |
| 10 | DEC-18 | Decidida: (a) | Ordem global entre as áreas, a 043, as specs pendentes e a skill de agregados |
| 11 | DEC-19 | Decidida: (b) | Política de concorrência da 043p2 |
| 12 | DEC-20 | Decidida: (b) | Fronteira dos agregados de usuários e identidade |
| 13 | DEC-21 | Decidida: (b) | O que é "conta ativa" nas regras que pedem conta utilizável |
| 14 | DEC-22 | Decidida (CT-20, alternativa 1, em 2026-11-01): (b) sem o login, com o índice parcial de (c) | Serialização das operações da conta sob concorrência (contagem de senhas e emissão de convites) |
| 15 | DEC-29 | Decidida: (a), com a sequência da 7.15.4 | Ordem da área de usuários |
| 16 | DEC-31 | Decidida: (b) | Fronteiras de funções, vínculos e permissões, e ordem com a 036 |
| 17 | DEC-32 | Decidida: (b) | Fonte das permissões efetivas nas decisões que gravam estado |
| 18 | DEC-33 | Decidida: (b), por delegação | Concorrência do último administrador na 043 parte 2 |
| 19 | DEC-34 | Decidida: (b) | Negar por padrão |
| 20 | DEC-36 | Decidida: (b) | Validade máxima do access token |
| 21 | DEC-37 | Decidida: (a) | Endurecimento sem mudança para o cliente legítimo |
| 22 | DEC-41 | Decidida: (b), por delegação | Proteção das regras entre organização e chamados sob concorrência |
| 23 | DEC-42 | Decidida: (b) | Chamado em setor desativado |
| 24 | DEC-46 | Decidida: (b) | Dados sensíveis na auditoria |
| 25 | DEC-50 | Decidida: (b) | Política de log dos decorators |
| 26 | DEC-55 | Decidida: (b) | Banco em memória dos testes unitários até a 043p3 |
| 27 | DEC-56 | Decidida: o conjunto do REQ-416 | Regras de arquitetura novas |
| 28 | DEC-57 | Decidida: (b) | Observabilidade |
| 29 | DEC-58 | Decidida: (b) | Ordem do rate limiter e corpo do 429 |
| 30 | DEC-59 | Decidida: sim, restrita (2026-11-05) | Autorização para alterar o `CLAUDE.md` |
| 31 | DEC-60 | Decidida: (a) | Como o front fecha um chamado? |
| 32 | DEC-61 | Decidida: (b) | Como evitar a regravação de estado antigo no `PUT tickets/{id}`? |
| 33 | DEC-64 | Decidida: (b) | Operações que o front compõe com várias chamadas (lote, contagem por pessoa, cadastro mais funções) |
| 34 | DEC-67 | Decidida: (b) | Filtro por departamento |
| 35 | DEC-68 | Decidida: (b) | Como verificar o contrato entre os dois repositórios? |
| 36 | DEC-70 | Decidida (CT-22, alternativa 3, em 2026-11-01): consulta de carga nomeada por operação, com regra de arquitetura | Convenção de entidade interna persistida pela raiz |
| 37 | DEC-71 | Decidida: (a) | Módulos |

### Pendentes (17)

| Nº | ID | Situação | Tema |
|---|---|---|---|
| 1 | DEC-14 | Pendente | Autorização para o `CLAUDE.md` |
| 2 | DEC-23 | Pendente | Senha atual errada na troca autenticada (`PUT users/me/password`) |
| 3 | DEC-26 | Pendente | Eventos de sessão e eventos sem consumidor |
| 4 | DEC-35 | Pendente | Brechas de autorização entre operações parecidas |
| 5 | DEC-38 | Pendente | Resposta e contagem do bloqueio por tentativas |
| 6 | DEC-43 | Pendente | `Update` de departamento e de setor (desvio do `CLAUDE.md`) |
| 7 | DEC-44 | Pendente | Espaço no início ou no fim do nome |
| 8 | DEC-47 | Pendente | Natureza e lugar do log de auditoria técnico |
| 9 | DEC-51 | Pendente | Contrato de erro |
| 10 | DEC-52 | Pendente | Despacho de eventos nos commands em SQL (043p2) |
| 11 | DEC-53 | Pendente | Migrations em produção |
| 12 | DEC-54 | Pendente | Integridade das colunas de enum e padrões residuais |
| 13 | DEC-62 | Pendente | Que mudanças do contrato de erro o front precisa, e onde entram? |
| 14 | DEC-63 | Pendente | O que acontece com a sessão de quem troca a própria senha? |
| 15 | DEC-65 | Pendente | Nome do autor na conversa e no anexo |
| 16 | DEC-66 | Pendente | De onde vem a hora do servidor nas listas com SLA? |
| 17 | DEC-69 | Pendente | Rotas sem consumidor no front |

### A confirmar (0)

Nenhuma desde 2026-11-04: a DEC-13 foi confirmada pela (a) e passou às decididas.

### Recomendações a avaliar no portão da etapa (15)

| Nº | ID | Situação | Tema |
|---|---|---|---|
| 1 | DEC-6 | Recomendação; avaliar na G3-a | Quem cria o anexo e a forma dos resultados |
| 2 | DEC-7 | Recomendação; avaliar na G3-a | Fonte única da política de anexos |
| 3 | DEC-8 | Recomendação; avaliar na G3-a | Pré-conferências do handler |
| 4 | DEC-9 | Recomendação; avaliar na G3-a | `PUT tickets/{id}` |
| 5 | DEC-10 | Recomendação; avaliar na G3-a | Código morto |
| 6 | DEC-15 | Recomendação; avaliar na G3-a | Objetos de valor |
| 7 | DEC-16 | Recomendação; avaliar na G3-a | Convenção de enumerações |
| 8 | DEC-24 | Recomendação; avaliar na G3-b | Onde fica o envio do e-mail de convite |
| 9 | DEC-25 | Recomendação; avaliar na G3-b | Trabalho técnico de identidade (hash de token, token do convite, código `usr_`) |
| 10 | DEC-27 | Recomendação; avaliar na G3-b | Nomes e pasta da sessão |
| 11 | DEC-28 | Recomendação; avaliar na G3-b | TLS do SMTP |
| 12 | DEC-30 | Recomendação; avaliar na G3-b | Destino dos guards estáticos e forma das conferências repetidas |
| 13 | DEC-39 | Recomendação; avaliar na G3-b | Nomes e pastas das peças de autorização |
| 14 | DEC-40 | Recomendação; avaliar na G3-c | Fronteira entre departamento e setor |
| 15 | DEC-45 | Recomendação; avaliar na G4 | Gravação da auditoria depois da 043p2 |

### Sem objeto (1)

| Nº | ID | Situação | Tema |
|---|---|---|---|
| 1 | DEC-48 | Sem objeto | Branches 022 e 023 |

**Total:** 37 (decididas) + 17 (pendentes) + 0 (a confirmar) + 15 (recomendações) + 1 (sem objeto) = 70.

## B.11 Verificação da fase 1 da G3-a (2026-11-06)

> Evidências da `spec-verify` da fase 1 (T-2, T-3 e a pasta da DEC-72), escritas aqui pelo limite de tamanho do `spec.md`, que tem o ponteiro na seção 10.

- **Escopo:**
  - Domain com `TicketMessages/`, `TicketAttachments/` e `TicketHistories/` (18 arquivos movidos) e o `TicketHistoryRecordedDomainEvent` apagado (DEC-10); as configurações espelhadas na Infrastructure;
  - `TicketHistory` → `TicketHistoryEntry`, `TicketReassignedDomainEvent` → `TicketAssignedDomainEvent`, `GetState` → `StateAt`, `GetConsumed` → `ConsumedBusinessTime` e `IsLastMessageFromAgentAsync` → `IsLastValidMessageFromAgentAsync`;
  - a `TicketAttachmentPolicy` (limites, tipos com as extensões, `GetMediaType`, `Accepts` e `ExtensionsOf`), lida pela entidade, pelo inspetor, pelo `GetPolicy` e pelo limite do corpo do `Upload`, que continua em 11 MB;
  - a pasta `Application/TicketAttachments/GetContent` no lugar de `Download` (DEC-72);
  - nenhuma rota, tabela, `DbSet` ou migration mudou.
- **Build:** `dotnet build TickestPristine.slnx`: 0 avisos, 0 erros, também depois dos dois ajustes da revisão.
- **Testes:**
  - unitários 756 de 756 e arquitetura 142 de 142, do projeto inteiro, porque os namespaces mudaram em vários agregados;
  - integração inteira: 886 aprovados, 0 falhas e 11 não executados (as evidências da CT-26), de 897, em 8 min 34 s (`tests/artifacts/resultados/integracao-049-g3a-fase1.trx`), com o `Model_Should_HaveNoPendingChanges` (AC-15) e os testes da política (AC-11);
  - depois da limpeza dos `using`: `TicketAttachments`, `TicketHistories`, `PaginationExtensionsTests` e `DomainEventsDispatcherTests`, 70 de 70; depois do `Accepts`: `Storage` e `TicketAttachments`, 68 de 68;
  - testes de `scripts` 14 de 14.
- **Buscas (AC-12 e AC-14, parte da fase 1):** nenhum `TicketHistory`, `TicketHistoryConfiguration`, `TicketReassignedDomainEvent`, `GetState`, `GetConsumed`, `IsLastMessageFromAgentAsync` nem `TicketHistoryRecordedDomainEvent` em `src/` e `tests/`, fora das migrations; os tipos movidos nos namespaces novos; a lista de tipos aceitos só na política. `ProcessSlaBreaches` e `ProcessDue` ficam para a T-9 (DEC-1).
- **Auditoria:** `python scripts/auditoria_sdd.py .`: 100,0%, CONFORME, código de saída 0, de novo depois da última mudança de `src/`.
- **Revisão (`ca-review`):** nenhum achado bloqueante. Dois ajustes na própria fase: o `using` de `Domain.Tickets` que sobrou em 10 arquivos (o IDE0005 está desligado, e o build não acusava) e o `Accepts` no lugar de `ExtensionsOf(...) is null` no inspetor. O teste dos membros do cronômetro só mudou a ordem da lista, pelos nomes novos.
- **Documentação:** `SYSTEM_SPECIFICATION.md`, nos 12 trechos que citavam os nomes e os caminhos trocados (entidades, eventos, histórico, auditoria, envio de anexos e reabertura). O `CLAUDE.md` ainda cita `TicketHistory` em três trechos; mudar depende de autorização (DEC-14).
- **Pendências:** o PR da fase 1, pelo responsável; a fase 2 (T-4 a T-6 e T-304); a skill de desenho de agregados antes da fase 3.
- **Complemento (DEC-73, opção 2A), 2026-11-06:** `TicketHistoryDescriptions` → `TicketHistoryEntryDescription` (classe, arquivo e teste), sem mudar o texto exibido; build sem avisos; unitários 756 de 756; arquitetura 142 de 142; `TicketHistories` e os testes de histórico do `TicketsTests`, 30 de 30; `SYSTEM_SPECIFICATION.md` com o nome novo. As migrations ficam com o nome antigo nos comentários, porque registram o que foi aplicado.
- **Entrega:** PR #78 (https://github.com/rafaelps15/tickest-pristine/pull/78), aberto em 2026-11-06 da branch `refactor/SPEC-049-tickest-architecture-reassessment` para a `main`, com o commit `2816596`. O merge é do responsável.

## B.12 Fase 2 da G3-a: T-4 e T-304 (2026-11-06)

**Projeção por evento (DEC-74).**

- Saem o `ITicketActivityWriter` e o `TicketActivityWriter`; entram o `Application/Abstractions/Data/ITicketActivityProjector.cs` e o `Infrastructure/Tickets/TicketActivityProjector.cs`. O projetor abre uma transação e trava a linha do chamado com `FOR NO KEY UPDATE`, a trava do próprio `UPDATE`, que não bloqueia a gravação de mensagens e anexos (as chaves estrangeiras pedem só `FOR KEY SHARE`). Só depois lê o chamado, o maior instante do histórico e o autor da última mensagem válida, aplica `Ticket.IsAgentReply` e `Ticket.WaitingOnAfter` e grava as duas colunas, só quando alguma muda.
- Cinco handlers `ProjectActivityOn{Evento}DomainEventHandler`, um por evento, em `TicketMessages/TicketMessagePosted`, `TicketMessageEdited` e `TicketMessageDeleted` e em `TicketAttachments/TicketAttachmentUploaded` e `TicketAttachmentDeleted`. A falha da projeção fica no log do despacho, como a do expurgo do arquivo, e não derruba a resposta.
- Os handlers de publicar, editar e excluir mensagem e de enviar e excluir anexo não gravam mais a projeção; os de editar e excluir mensagem e o de excluir anexo também deixam de carregar o chamado.
- `Ticket.PostMessage(authorUserId, content, utcNow)` devolve `TicketMessagePosting(Message, History)` e não muda o chamado. O parâmetro `authorIsAgent` sai, porque nada o usa na fase 2; volta na fase 3, com a primeira resposta (DEC-1).
- Na reabertura com motivo, o "aguardando" passado ao `Reopen` é o `IsAgentReply` de quem reabriu, com o `IAgentDirectory` (REQ-5); sem motivo, vale a última mensagem válida (`TicketAgentLookup`).
- O `TicketAgentLookup` fica só com o `IsLastValidMessageFromAgentAsync` (atualização e reabertura) e passa a usar o `Ticket.IsAgentReply`; quem atende é do `IAgentDirectory` (`AgentDirectory`), usado pela atribuição, pela publicação, pela reabertura e pelo projetor (DEC-73, 5A).

**Auditoria (REQ-16 e REQ-312).** O `AuditLoggingInterceptor` descarta, na falha e no cancelamento do save, os registros que ele incluiu (o `Post` não desanexa mais nada), usa um só instante por save, audita também o `SavingChanges` síncrono e lê a chave única `Id`, sem o caminho de chave composta. Sai o comentário falso de `Web.Api/Endpoints/AuditLogs/GetHistory.cs`.

**Testes.**

| # | Teste | Projeto | Cobre |
|---|---|---|---|
| 1 | `ProjectActivityOn*DomainEventHandlerTests` (5 classes, 1 teste cada) | unitários | cada handler chama o projetor com o id do chamado (AC-8) |
| 2 | `PostTicketMessageCommandHandlerTests` › `Handle_Should_RaiseMessagePostedWithoutChangingTicket_WhenAgentAnswers` | unitários | AC-8 |
| 3 | `EditTicketMessageCommandHandlerTests` › `Handle_Should_RaiseMessageEditedWithoutChangingTicket_WhenCallerIsAuthor` e `Handle_Should_NotRecordHistory_WhenCallerIsNotAuthor` | unitários | AC-8 |
| 4 | `DeleteTicketMessageCommandHandlerTests` › `Handle_Should_RaiseMessageDeletedWithoutChangingTicket_WhenCallerIsAuthor` | unitários | AC-8 |
| 5 | `DeleteTicketAttachmentCommandHandlerTests` › `Handle_Should_RecordRemovalWithoutChangingTicket_WhenAttachmentIsDeleted` | unitários | AC-8 |
| 6 | `TicketTests` › `WaitingOnAfter_*` (3 casos), `IsAgentReply_*` (3) e `PostMessage_Should_CreateMessageAndHistoryWithoutChangingTicket_WhenCalled` | unitários | regras puras do `Ticket` (AC-8) |
| 7 | `Users/AgentDirectoryTests` (os 2 de quem atende, vindos do `TicketAgentLookupTests`) | unitários | DEC-73 (5A) |
| 8 | `TicketActivityTests` › `WaitingOn_Should_FollowLastValidMessage_When_EarlierMessageWritesLast` | integração | F-23, com gatilho temporário que segura o primeiro `UPDATE` de chamados antes de ele travar a linha (AC-8) |
| 9 | `AuditLogsTests` › `SaveChanges_Should_DiscardOwnAuditEntries_When_SaveFails` | integração | AC-17 |
| 10 | `AuditLogsTests` › `SaveChanges_Should_UseSingleTimestamp_WhenSavingSeveralEntities`, `SaveChanges_Should_Audit_WhenSavingSynchronously` e `AssignRoles_Should_AuditRemovedUserRoleWithOldValues` | integração | AC-313 |

Saem os testes do comportamento retirado: `RecordAttachmentActivity_*` e `RecordMessageActivity_*` do `TicketTests`, os de gravador do `Post`, do `Edit`, do `Delete` de mensagem e do `Delete` de anexo e o `Handle_Should_WaitOnAgent_WhenRequesterAnswers`, cuja regra passou à projeção, coberta pelos `WaitingOn_*` de integração.

**Execuções (2026-11-06).** Build sem avisos e sem erros; unitários 759 de 759; arquitetura 142 de 142; a integração roda na verificação da fase.

## B.13 Fase 2 da G3-a: T-5 (2026-11-06)

**Anexo criado pelo chamado (DEC-6, DEC-73 1A e 6A, DEC-75).**

- `Ticket.AddAttachment(uploadedByUserId, fileName, contentType, fileSizeBytes, storageKey, contentMatches, currentCount, currentTotalBytes, utcNow)` recusa, nesta ordem e sem mudar nada: o conteúdo que não confere (`ContentMismatch`), o arquivo fora da política (`EnsureAccepted`: tipo, extensão e tamanho) e o chamado no limite. Cria o anexo pelo `TicketAttachment.Create` `internal`, sem conferências, e devolve `TicketAttachmentUpload(Attachment, History)`; o chamado não muda.
- Saem o `RecordUpload` e o `Ticket` como parâmetro do `Create`; o `MarkPurged` vira `bool RecordFilePurge(DateTime)`. O handler da purga grava a purga só depois de o arquivo sair do armazenamento.
- Envio (DEC-75): inspeção do conteúdo, `IFileStorage.CreateKey`, `AddAttachment`, `SaveAsync(content, storageKey)` e o save; na recusa nada é gravado, e o arquivo só é apagado quando o save falha. O `LocalFileStorage.SaveAsync` abre o arquivo com `CreateNew` fora do bloco de limpeza: uma chave já usada falha sem apagar o arquivo dela.

**Política dos anexos, reorganizada a pedido do responsável.** A `TicketAttachmentPolicy` ficou com os parâmetros da política (os três limites e a lista `AllowedTypes`, agora do record `AllowedAttachmentType`, no lugar da tupla com arrays) e duas perguntas: `GetAllowedType(contentType)`, o tipo aceito correspondente ou nulo (substitui `Accepts`, `ExtensionsOf` e `GetMediaType`), e `EnsureAccepted(fileName, contentType, fileSizeBytes)`, o nome da DEC-73, que segue a convenção `Ensure*` com `Result` do próprio `Ticket`. O inspetor de conteúdo usa o `GetAllowedType`.

**Testes.**

| # | Teste | Projeto | Cobre |
|---|---|---|---|
| 1 | `TicketAttachmentTests` › `AddAttachment_*` (9 métodos, 15 casos) | unitários | regras, ordem dos erros, limite, sucesso com histórico e evento, chamado intacto (AC-9) |
| 2 | `TicketAttachmentTests` › `RecordFilePurge_*` (3) | unitários | AC-10 |
| 3 | `UploadTicketAttachmentCommandHandlerTests` | unitários | o fluxo do envio; os testes de limite, de extensão e de tamanho passam a esperar nenhuma gravação (DEC-75); o de extensão vira `..._ReturnExtensionMismatchAndStoreNothing_...`, e o de limite mantém o nome, citado pelo AC-6 da spec 011 |
| 4 | `GetTicketAttachmentPolicyQueryHandlerTests` | unitários | todo tipo informado passa no `EnsureAccepted` |
| 5 | `IntegrationTests/Storage/LocalFileStorageTests` (7) | integração | F-42: chave com a extensão e sem gravar nada, extensão insegura descartada, gravação legível pela chave, chave já usada sem apagar o arquivo dela, cópia interrompida sem arquivo, exclusão repetida, caminho fora da pasta recusado |

**Execuções (2026-11-06).** Build sem avisos e sem erros; unitários 763 de 763; arquitetura 142 de 142; a integração roda na verificação da fase.

## B.14 Fase 2 da G3-a: T-6 e a revisão do interceptor (2026-11-06)

**Conferências em fonte única (DEC-8, DEC-9).**

- O `Ticket` expõe `EnsureCanChangeStatusTo(TicketStatus)` (o antigo `EnsureActiveTransition`, privado), usado pelo `StartProgress`, pelo `Resolve`, pelo `Cancel` e pelo `PUT`; o `EnsureCanReopen(bool)` fica público com o parâmetro de hoje (CT-17).
- `Close`: sai a pré-conferência idêntica à do `Ticket.Close`.
- `Reopen`: sai a conferência de transição, inalcançável depois da de "ativo"; a recusa do chamado ativo antes do calendário fica, por precedência, e as demais conferências são do `EnsureCanReopen`, que a reabertura faz antes de calcular o ciclo.
- `PUT tickets/{id}`: `ResolveIntent` monta a intenção (status, cancelamento, descrição); `HasPermissionToUpdateAsync`, `HasPermissionToEditOwnAsync`, `HasPermissionToManageAsync` e `HasPermissionToCancelAnyAsync` conferem as permissões; `ChangeStatusAsync` confere a transição pelo `EnsureCanChangeStatusTo` e faz a troca. O braço `_ =>` vira `UnreachableException` (REQ-10). A ordem dos erros e o contrato não mudam.

**`AuditLoggingInterceptor` (questionário de 2026-11-06).** Reescrito no desenho de trilha por coluna escolhido: `SavingChanges` e `SavingChangesAsync` chamam o `AddAuditEntries`, que descarta os registros pendentes de um save que falhou (REQ-16), lê o instante uma vez (REQ-312) e cria um registro por entidade auditável incluída, alterada ou excluída, com `SerializeOldValues`, `SerializeNewValues` e `GetModifiedColumns`; a chave é o `Id` (AU-10), e a exclusão de segredos (`IsAudited`, DEC-46) continua. Saem os quatro ganchos de falha e de cancelamento da primeira versão da T-304. Tabela, formato e rota de leitura não mudam; na G4, a DEC-45 continua valendo.

**Execuções (2026-11-06).** Build sem avisos e sem erros; unitários 763 de 763 (as asserções dos handlers de `Close`, `Update` e `Reopen` não mudaram); arquitetura 142 de 142.

## B.15 Verificação da fase 2 da G3-a (2026-11-06)

- **Escopo:** T-4, T-5, T-6 e T-304, com as DEC-74 e DEC-75 e os ajustes do REQ-7 e do REQ-16 pedidos pelo responsável no chat.
- **Build:** `dotnet build TickestPristine.slnx` sem avisos e sem erros, depois da última mudança de `src/` e `tests/`.
- **Testes, pelo critério de impacto:** a fase mudou `Application/Abstractions`, a DI e os interceptors, então rodaram os projetos inteiros.

| Projeto | Total | Aprovados | Falhas | Não executados |
|---|---|---|---|---|
| Unitários | 766 | 766 | 0 | 0 |
| Arquitetura | 142 | 142 | 0 | 0 |
| Integração | 909 | 898 | 0 | 11 (evidências da CT-26, que só rodam quando ligadas) |

- **Testes da rastreabilidade na integração (TRX):** corrida do F-23 1 de 1; `LastActivity_*` 4 de 4; `WaitingOn_*` 4 de 4; `AuditLogsTests` 9 de 9 (os 4 novos); `PostMessage_Should_RecordBothMessagesAndSingleCompletion_WhenTwoAgentsAnswerAtOnce` 1 de 1; `LocalFileStorageTests` 7 de 7; `Update_*` 15 de 15; `Cancel_*` 7 de 7; `TicketAttachmentsTests` 28 de 28; `AttachmentContentInspectorTests` 30 de 30; `GetTicketAssigneesQueryHandlerTests` 4 de 4.
- **Correção durante a verificação:** a primeira rodada de integração reprovou os testes de "aguardando": o projetor lia o chamado por `FromSql` com `SELECT *`, que não traz o `xmin` do token de concorrência, e a falha ficava no log do despacho. A trava passou a ser um comando próprio (`SELECT 1 … FOR NO KEY UPDATE`), seguido da leitura do chamado pelo EF; a rodada foi interrompida, e a segunda é a desta tabela.
- **Auditoria:** `python scripts/auditoria_sdd.py .`: 100,0%, CONFORME, código de saída 0, de novo depois da última mudança de `src/`.
- **Revisão (`ca-review`):** nenhum achado bloqueante. Dois ajustes na própria fase: o `<summary>` do `TicketAttachmentPolicy.AllowedTypes`, que tinha saído com a reorganização, e os testes diretos do `EnsureCanChangeStatusTo`, agora público. Observações sem ação: o `AllowedAttachmentType` é um record de dados da política, e não um objeto de valor; os records de resultado seguem a DEC-6; o projetor grava as duas colunas de projeção por SQL, pela DEC-2 e pela DEC-74.
- **Documentação:** `SYSTEM_SPECIFICATION.md`, na RN-09, RN-31 e RN-34, na auditoria, nas gravações depois do commit, no fluxo de anexos, na atribuição e nas evidências E-07 e E-27.
- **Pendências:** o `CLAUDE.md` ainda diz que o `TicketAttachment` cria a entrada do envio, que agora é do `Ticket.AddAttachment`, e cita `TicketHistory` em três trechos; mudar depende de autorização (DEC-14). A DEC-76 fica para o portão da G3-b, e a DEC-47 continua pendente. Depois do PR da fase 2, a diretriz das entidades entra pela `spec-new`, com o anexo F.
- **Entrega:** PR #79 (https://github.com/rafaelps15/tickest-pristine/pull/79), aberto em 2026-11-06 da branch `refactor/SPEC-049-tickest-architecture-reassessment` para a `main`, com o commit `21144e1`. O merge é do responsável.

## B.16 T-7 da G3-a (2026-11-09)

- **Implementação:** `Ticket` passou a possuir internamente os cronômetros e a coordenar os ciclos; `TicketSlaCycleService` e `TicketReopening` foram removidos; o mapeamento e o `SampleDataSeeder` foram adaptados. Os testes que dependiam do serviço, dos setters ou dos cronômetros separados foram atualizados.
- **Teste do seeder (AC-31):** `SampleDataSeederTests.Migrate_ShouldCreateSampleTicketsWithCycleOneTimers_WhenSampleDataIsEnabled` usa uma fábrica isolada com `Seeding:SampleData` ligado e confere que os chamados de exemplo têm um cronômetro de resolução no ciclo 1.
- **Build:** `dotnet build TickestPristine.slnx`, sem avisos e sem erros, depois da última alteração.
- **Testes da tarefa:** `TickestPristine.Application.UnitTests`, filtro `FullyQualifiedName~Ticket`, 441 aprovados; `TickestPristine.ArchitectureTests`, 156 aprovados. O teste de integração do AC-31 fica para a verificação final da fase, conforme o critério de impacto do projeto.
- **T-8:** os handlers de abertura, atualização, reabertura, exclusão e publicação de mensagem usam a nova API do `Ticket`. A inspeção do diff dos testes de integração não encontrou mudanças nas asserções. Os 441 testes unitários do recorte passaram; a integração completa fica para a verificação da fase.
