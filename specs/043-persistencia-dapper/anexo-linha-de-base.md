# 043 — Anexo: linha de base das consultas (T-1)

> Parte da [spec 043](spec.md). Este anexo não tem estado próprio; o estado, as decisões e as tarefas ficam no `spec.md`.

## Linha de base

- **Código:** branch `refactor/SPEC-043-dapper-persistence`, criada da `origin/main` em `251da97` (merge do PR #71, com a G1 da spec 049).
- **Build:** `dotnet build TickestPristine.slnx` com 0 avisos e 0 erros (2026-11-05).
- **Testes:** unitários 940 de 940 e arquitetura 134 de 134 (2026-11-05); integração verde no CI do PR #71 sobre o mesmo código (674 testes, 663 aprovados, 0 falhas e 11 evidências sob demanda).
- **Testes de query handler hoje:** 29 classes `*QueryHandlerTests` em `tests/TickestPristine.Application.UnitTests`, com 158 testes (`[Fact]` e `[Theory]`), todas com o banco em memória; a T-6 as leva para a integração, com as mesmas asserções (REQ-7).

## Inventário das 29 consultas

Gerado do código por script: o acesso ao banco, a paginação, as peças de autorização e de recorte usadas no handler, as tabelas com exclusão lógica lidas por ele (`tickets`, `ticket_messages`, `ticket_attachments`) e a classe de teste unitário de hoje.

| # | Slice | Handler | Acesso hoje | Paginação | Autorização e peças | Tabelas com exclusão lógica | Ignora filtro global | Testes unitários |
|---|---|---|---|---|---|---|---|---|
| 1 | `AuditLogs/GetHistory` | `GetAuditHistoryQueryHandler` | EF | cursor | — | — | — | AuditLogs/GetAuditHistoryQueryHandlerTests.cs |
| 2 | `Departments/GetAll` | `GetDepartmentsQueryHandler` | EF | — | — | Tickets | — | Departments/GetDepartmentsQueryHandlerTests.cs |
| 3 | `Departments/GetById` | `GetDepartmentByIdQueryHandler` | EF | — | — | — | — | Departments/GetDepartmentByIdQueryHandlerTests.cs |
| 4 | `Permissions/GetAll` | `GetPermissionsQueryHandler` | sem banco | — | — | — | — | Permissions/GetPermissionsQueryHandlerTests.cs |
| 5 | `Roles/GetAll` | `GetRolesQueryHandler` | EF | — | — | — | — | Roles/GetRolesQueryHandlerTests.cs |
| 6 | `Sectors/GetAll` | `GetSectorsQueryHandler` | EF | — | — | — | — | Sectors/GetSectorsQueryHandlerTests.cs |
| 7 | `Sectors/GetById` | `GetSectorByIdQueryHandler` | EF | — | — | — | — | Sectors/GetSectorByIdQueryHandlerTests.cs |
| 8 | `TicketAttachments/Download` | `GetTicketAttachmentContentQueryHandler` | EF | — | guard de acesso ao chamado, permissões efetivas, usuário logado | Tickets, TicketAttachments | — | TicketAttachments/GetTicketAttachmentContentQueryHandlerTests.cs |
| 9 | `TicketAttachments/GetAll` | `GetTicketAttachmentsQueryHandler` | EF | cursor | guard de acesso ao chamado, permissões efetivas, usuário logado | TicketAttachments | — | TicketAttachments/GetTicketAttachmentsQueryHandlerTests.cs |
| 10 | `TicketAttachments/GetPolicy` | `GetTicketAttachmentPolicyQueryHandler` | sem banco | — | — | — | — | TicketAttachments/GetTicketAttachmentPolicyQueryHandlerTests.cs |
| 11 | `TicketHistories/GetAll` | `GetTicketHistoryQueryHandler` | EF | cursor | guard de acesso ao chamado, permissões efetivas, usuário logado | — | — | TicketHistories/GetTicketHistoryQueryHandlerTests.cs |
| 12 | `TicketMessages/GetAll` | `GetTicketMessagesQueryHandler` | EF | cursor | guard de acesso ao chamado, permissões efetivas, usuário logado | TicketMessages | — | TicketMessages/GetTicketMessagesQueryHandlerTests.cs |
| 13 | `Tickets/GetAll` | `GetTicketsQueryHandler` | EF | offset | — | Tickets | — | Tickets/GetTicketsQueryHandlerTests.cs |
| 14 | `Tickets/GetAssignees` | `GetTicketAssigneesQueryHandler` | EF | offset | elegibilidade de atendente | — | — | Tickets/GetTicketAssigneesQueryHandlerTests.cs |
| 15 | `Tickets/GetById` | `GetTicketByIdQueryHandler` | EF | — | guard de acesso ao chamado, permissões efetivas, usuário logado | Tickets | — | Tickets/GetTicketByIdQueryHandlerTests.cs |
| 16 | `Tickets/GetOverview` | `GetTicketOverviewQueryHandler` | Dapper | — | filtros de SLA, recorte do chamado | Tickets | — | Tickets/GetTicketOverviewQueryHandlerTests.cs |
| 17 | `Tickets/GetOwn` | `GetOwnTicketsQueryHandler` | EF | offset | permissões efetivas, usuário logado | Tickets | — | Tickets/GetOwnTicketsQueryHandlerTests.cs |
| 18 | `Tickets/GetOwnSummary` | `GetOwnTicketSummaryQueryHandler` | EF | — | usuário logado | Tickets | — | Tickets/GetOwnTicketSummaryQueryHandlerTests.cs |
| 19 | `Tickets/GetSla` | `GetTicketSlaQueryHandler` | EF | — | guard de acesso ao chamado, permissões efetivas, usuário logado | Tickets | — | Tickets/GetTicketSlaQueryHandlerTests.cs |
| 20 | `Tickets/GetSlaSummary` | `GetTicketSlaSummaryQueryHandler` | EF | — | — | Tickets | — | Tickets/GetTicketSlaSummaryQueryHandlerTests.cs |
| 21 | `Tickets/GetStatusCounts` | `GetTicketStatusCountsQueryHandler` | EF | — | — | Tickets | — | Tickets/GetTicketStatusCountsQueryHandlerTests.cs |
| 22 | `Tickets/GetTimes` | `GetTicketTimesQueryHandler` | EF | — | — | Tickets | — | Tickets/GetTicketTimesQueryHandlerTests.cs |
| 23 | `Tickets/GetVolume` | `GetTicketVolumeQueryHandler` | Dapper | — | permissões efetivas, usuário logado, recorte do chamado | Tickets | — | Tickets/GetTicketVolumeQueryHandlerTests.cs |
| 24 | `Users/GetAll` | `GetUsersQueryHandler` | EF | offset | — | — | — | Users/GetUsersQueryHandlerTests.cs |
| 25 | `Users/GetAttendants` | `GetBacklogAttendantsQueryHandler` | EF | offset | permissões efetivas, usuário logado, elegibilidade de atendente | — | — | Users/GetBacklogAttendantsQueryHandlerTests.cs |
| 26 | `Users/GetByEmail` | `GetUserByEmailQueryHandler` | EF | — | — | — | — | Users/GetUserByEmailQueryHandlerTests.cs |
| 27 | `Users/GetById` | `GetUserByIdQueryHandler` | EF | — | permissões efetivas, usuário logado | — | — | Users/GetUserByIdQueryHandlerTests.cs |
| 28 | `Users/GetCurrent` | `GetCurrentUserQueryHandler` | EF | — | permissões efetivas, usuário logado | — | — | Users/GetCurrentUserQueryHandlerTests.cs |
| 29 | `Users/GetStatusCounts` | `GetUserStatusCountsQueryHandler` | EF | — | — | — | — | Users/GetUserStatusCountsQueryHandlerTests.cs |

Total: 29; EF: 25; Dapper: 2; EF + Dapper: 0; sem banco: 2

## Observações

- **Já em Dapper:** `Tickets/GetOverview` e `Tickets/GetVolume` leem pelo `IDbConnectionFactory`, mas ainda usam o `TicketScopeFilter` e, no `GetOverview`, o `TicketSlaFilters`; a reescrita os leva aos templates e tira o `TicketSlaFilters` (REQ-10).
- **Sem banco:** `Permissions/GetAll` lê o `PermissionCatalog`, e `TicketAttachments/GetPolicy` lê as constantes de `TicketAttachment`. Os dois não leem o banco e só seguem a estrutura do slice (REQ-1); a fonte deles não muda, porque mudar para a tabela trocaria a fonte de verdade (REQ-8).
- **Conferência por recurso:** seis consultas usam o `TicketAccessGuard` (`Download`, `TicketAttachments/GetAll`, `TicketHistories/GetAll`, `TicketMessages/GetAll`, `Tickets/GetById` e `Tickets/GetSla`); pela DEC-9 (b), a regra passa a uma peça injetada da Application, com SQL e 404 antes de 403 (REQ-5).
- **Exclusão lógica:** nas consultas que leem `tickets`, `ticket_messages` e `ticket_attachments`, o filtro global do EF deixa de valer no Dapper; o `deleted_at_utc IS NULL` passa a ser escrito em cada SQL (REQ-2). O histórico (`ticket_histories`) não tem exclusão lógica, mas a conferência de acesso lê o chamado, com o filtro.
- **Comparação com o EF:** o SQL que o EF gera hoje é reproduzível a partir das consultas LINQ desta linha de base, na `origin/main` (`251da97`). A comparação de comportamento fica com os testes de cada consulta, levados para a integração com as asserções de hoje (REQ-7, AC-6); o SQL do EF é consultado na reescrita só onde a tradução tem detalhe (ordenação de desempate, comparação em `citext`, fuso e paginação).
