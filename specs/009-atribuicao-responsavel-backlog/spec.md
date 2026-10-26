---
id: 009
titulo: Consulta de atendentes e atribuição de responsável pelo Administrador
status: Em implementação        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-24
branch: feat/SPEC-009-attendant-lookup-assignment
backlog:
---

# 009 — Consulta de atendentes e atribuição de responsável pelo Administrador

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-24: "AGORA COLOQUE AS ESPCS DENTRO DO PROJETO E PRONTAS PARA O DESENVOLVIMENTO", sobre a "SPEC-01 — Atribuição e Reatribuição de Responsáveis em Tickets do Backlog" refinada no mesmo dia.
- **Fonte:** `specs/_fontes/atribuicao-responsavel.md` (identificadores RF-xx, RNF-xx, L-xx e D-xx citados aqui são dela).
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.2 (roles e permissões), 2.5 (estados do chamado), 5.10 (atribuir e remover responsável), 9.5 (Q-05); spec 001 (remoção de responsável).
- **Código:** `Application/Tickets/Assign/AssignTicketCommandHandler.cs` (hoje exige `tickets:manage` do executor e do responsável), `Web.Api/Endpoints/Tickets/Assign.cs` (`HasPermission(TicketsManage)`), `Application/TicketHistories/TicketHistoryEntries.cs` (`Reassigned` com `oldValue` nulo na primeira atribuição), `Domain/Tickets/TicketHistoryAction.cs`, `Application/Users/GetAll/GetUsersQueryHandler.cs` (busca atual com `Contains`, que diferencia maiúsculas nos nomes), `Application/Permissions/PermissionCatalog.cs`, `Infrastructure/Roles/RolePermissionConfiguration.cs` (permissões padrão por role via `HasData`; vínculo novo exige `INSERT ... ON CONFLICT DO NOTHING` na migration), `Domain/Users/User.cs` e `Domain/Sectors/Sector.cs`, `Application/Authorization/PrivilegeEscalationGuard.cs`, `Application/Abstractions/Pagination/*`.
- **Fatos do repositório que afetam a fonte:**
  - Não existe estado "Backlog": a fonte o define como chamado ativo (Aberto ou Em andamento), que é exatamente o que a atribuição aceita hoje.
  - `User` não tem setor nem departamento. Sem o vínculo novo (REQ-1), `DepartmentName`, `SectorName` e `SectorId` do DTO não têm de onde vir.
  - A role se chama `Atendente` (`RoleNames.Agent`). Hoje é elegível qualquer usuário ativo com `tickets:manage`, o que inclui o Administrador.
  - Hoje o Atendente pode atribuir chamados (tem `tickets:manage`). A fonte restringe ao Administrador.
  - As regras de negócio decidem por permissão, não por nome de role: fora do seed, `RoleNames` só aparece no `AdministratorGuard` e na role padrão do cadastro. As permissões das roles Atendente e Colaborador são editáveis pela API.
  - A policy `HasPermission` responde 403 sem corpo de negócio; a mensagem "Você não tem permissão para buscar usuários." exige a checagem no handler (DEC-6).
  - Não há front neste repositório: o modal (RF-05 da fonte) fica no repositório Angular.
  - `FirstName`, `LastName`, `Email` e os nomes de setor e departamento são `citext`: `Contains` já compara sem diferenciar maiúsculas, como faz o `GetUsersQueryHandler`.
  - A paginação por offset (`PaginationExtensions.ToPagedListAsync`) nunca devolve 400: ajusta página e tamanho, com padrão 20 e máximo 100 para todo o sistema.
  - O rate limit global já limita cada usuário a 100 requisições por 60 s em todas as rotas.
  - O `AssignTicketCommandHandler` confere o `tickets:manage` do responsável pelo `IPermissionProvider`, que guarda as permissões em cache por até 5 minutos; uma consulta de elegíveis lida direto do banco poderia discordar dele nesse intervalo.
  - A descrição do histórico é montada por linha, a partir da ação e dos valores gravados (`TicketHistoryDescriptions.For`), sem acesso ao banco; os ids de usuário gravados em `oldValue` e `newValue` são texto.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `User` ganha `SectorId` opcional (FK para `sectors`, índice). `PUT users/{id}/sector` com `{ sectorId }` (nulo remove) exige `users:update`, passa pelo `PrivilegeEscalationGuard` e recusa setor inexistente ou desativado com 404 `Sectors.NotFound`. Desativar um setor não apaga o vínculo. | RF-01; DEC-2 |
| REQ-2 | `GET users/attendants` exige usuário ativo e confere `users:read` no handler: sem ela, 403 em Problem Details com código `Users.ReadForbidden` e mensagem "Você não tem permissão para buscar usuários.". Nunca 200 com lista vazia por falta de permissão; `tickets:manage` não substitui `users:read`. | RF-02.1, RF-02.7; DEC-6 |
| REQ-3 | A consulta devolve só atendentes elegíveis: usuário ativo com a permissão efetiva `tickets:manage` (vinda de qualquer role), que é a regra atual de quem atende a fila. A consulta e a atribuição (REQ-9) usam a mesma regra, lida do banco no momento da operação, sem cache. | RF-02.2; DEC-3; DEC-10 |
| REQ-4 | Cada item traz `id`, `fullName` (nome + " " + sobrenome), `email`, `sectorId`, `sectorName` e `departmentName`; sem setor, os três últimos vêm nulos. Nenhum outro dado do usuário é exposto. | RF-02.3 |
| REQ-5 | `searchTerm` opcional: depois do `Trim`, de 2 a 100 caracteres (senão 400). O termo é dividido em palavras (no máximo 5; as demais são ignoradas), e cada palavra precisa estar contida, sem diferenciar maiúsculas, em pelo menos um destes campos: nome, sobrenome, e-mail, setor ou departamento. Assim "ana souza" encontra Ana Souza. `%`, `_` e `\` são tratados como texto. | RF-02.4; DEC-7 |
| REQ-6 | Paginação no padrão do sistema (`ToPagedListAsync`): `pageSize` padrão 20; valores ausentes ou inválidos são ajustados, sem 400, e o máximo é o global de 100. | RF-02.5; DEC-8 |
| REQ-7 | `sectorId` opcional filtra pelo setor; `prioritySectorId` opcional só ordena (desse setor primeiro); depois nome, sobrenome e `id`, a mesma ordem estável da listagem de usuários. Os filtros se combinam com E. Resultado vazio por filtro = 200 com lista vazia e total 0. | RF-02.6, RF-02.8 |
| REQ-8 | `PUT tickets/{id}/assignee` continua exigindo `tickets:manage`. Atribuir o chamado a si mesmo basta com ela; atribuir a **outra pessoa** exige também a nova permissão `tickets:assign`, administrativa, concedida pela migration só à role Administrador; sem ela, 403 `Tickets.CannotAssignToOthers` ("Você só pode atribuir o chamado a você mesmo."). `tickets:assign` não concede `users:read`, e vice-versa. | RF-03.1; DEC-4 |
| REQ-9 | O responsável precisa ser atendente elegível (REQ-3); senão, 400 `Tickets.AssigneeNotAttendant`. As demais validações e a ordem atual se mantêm: chamado inexistente 404, chamado fora do Backlog 400 `Tickets.NotActive`, responsável inexistente ou inativo 404. | RF-03.2; DEC-1, DEC-3 |
| REQ-10 | A atribuição sem responsável anterior grava a ação de histórico `Assigned`; com anterior diferente, `Reassigned`. As duas guardam anterior (`oldValue`), novo (`newValue`), executor e data e hora UTC, no mesmo `SaveChangesAsync` da troca. | RF-03.5, RF-04.1, RF-04.2 |
| REQ-11 | A descrição das entradas `Assigned`, `Reassigned` e `Unassigned` no `GET tickets/{id}/history` mostra os nomes dos responsáveis (ex.: "Responsável alterado de Ana Souza para Beto Lima"), inclusive de usuários desativados. Os nomes vêm na própria consulta da página do histórico, sem consulta à parte. Id que não corresponda a usuário aparece como "usuário removido". | RF-04.3; DEC-11 |
| REQ-12 | Comportamentos atuais mantidos: atribuir ao responsável atual = 204 sem gravar; concorrência = 409 (`xmin`); a atribuição não se restringe ao setor do chamado. | RF-03.3, RF-03.4, RF-03.6; DEC-5 |
| REQ-13 | `GET users/attendants` fica coberto pelo rate limit global (100 requisições por 60 s por usuário), sem política própria. | RNF-03; DEC-9 |

## 3. Critérios de aceitação
Dados comuns: Ana (Atendente, ativa, setor TI do depto. Tecnologia), Beto (Atendente, ativo, TI), Caio (Atendente, ativo, Financeiro), Bruno (Atendente, inativo), Carla (Colaborador, ativa), Davi (Administrador, ativo), Eva (Atendente, ativa, sem setor).

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | Administrador; setor TI ativo e setor X desativado | `PUT users/{Ana}/sector` com TI; depois com X; depois com `null` | 204 e `sector_id` = TI; 404 `Sectors.NotFound` e o vínculo continua TI; 204 e `sector_id` nulo |
| AC-2 | REQ-1 | Ana vinculada a TI | o setor TI é desativado | `sector_id` de Ana continua TI |
| AC-3 | REQ-1 | Atendente (sem `users:update`) | `PUT users/{Ana}/sector` | 403 |
| AC-4 | REQ-2 | Atendente com `tickets:manage` e sem `users:read` | `GET users/attendants` | 403 com `code` `Users.ReadForbidden` e `detail` "Você não tem permissão para buscar usuários." |
| AC-5 | REQ-3, REQ-4 | os usuários comuns | Administrador chama `GET users/attendants` sem filtros | 200 com Ana, Beto, Caio, Davi e Eva, nessa ordem (por nome); sem Bruno (inativo) e Carla (sem `tickets:manage`); Eva com `sectorId`, `sectorName` e `departmentName` nulos; nenhum campo além dos seis do REQ-4 |
| AC-6 | REQ-3 | a role Atendente sem `tickets:manage` (removida pela API de roles) | `GET users/attendants` | 200 só com Davi; depois, a role Colaborador recebe `tickets:manage` e Carla passa a aparecer |
| AC-7 | REQ-5 | — | `searchTerm` = "a" (1 caractere) e com 101 caracteres | 400 de validação nas duas |
| AC-8 | REQ-5 | Ana Souza e Beto Lima | `searchTerm` = "ANA"; = "tecnolog"; = "ana souza"; = "souza lima"; = "%%" | Ana; Ana e Beto (pelo departamento); Ana; lista vazia; lista vazia |
| AC-9 | REQ-6 | 120 atendentes elegíveis | sem `pageSize`; `pageSize` = 50; `pageSize` = 500; `page` = 0 | 20 itens; 50 itens; 100 itens; primeira página. Em todos, 200 e `totalCount` 120 |
| AC-10 | REQ-7 | os usuários comuns | `prioritySectorId` = Financeiro | ordem Caio, Ana, Beto, Davi, Eva |
| AC-11 | REQ-7 | os usuários comuns | `searchTerm` = "caio" e `sectorId` = TI | 200, lista vazia, total 0 |
| AC-12 | REQ-8 | Beto (Atendente, sem `tickets:assign`); chamado Aberto sem responsável | Beto atribui a Ana; depois Beto atribui a si mesmo | 403 `Tickets.CannotAssignToOthers` e o responsável não muda; depois 204 e Beto é o responsável |
| AC-13 | REQ-8 | banco migrado | lê as permissões das roles | `tickets:assign` existe, é administrativa, está na role Administrador e não está nas roles Atendente e Colaborador; a migration roda de novo sem erro se o vínculo já existir |
| AC-14 | REQ-9 | chamado Aberto | Davi (Administrador) atribui Carla; Bruno; um id inexistente; ele mesmo | 400 `Tickets.AssigneeNotAttendant`; 404 `Users.NotFound`; 404 `Users.NotFound`; 204 |
| AC-15 | REQ-9 | chamado Resolvido | Administrador atribui Ana | 400 `Tickets.NotActive`, nada gravado |
| AC-16 | REQ-10 | chamado Aberto sem responsável | Administrador atribui Ana; depois Beto | 204 e 204; histórico com `Assigned` (`oldValue` nulo, `newValue` Ana, executor Davi) e depois `Reassigned` (`oldValue` Ana, `newValue` Beto) |
| AC-17 | REQ-11 | o histórico do AC-16, com Ana desativada depois, e uma remoção de responsável | `GET tickets/{id}/history` | as descrições citam Ana e Beto pelo nome completo; o teste de integração confirma que só a consulta da página lê nomes de usuário (contador de comandos do EF) |
| AC-18 | REQ-12 | chamado com responsável Ana | Administrador atribui Ana de novo; e, em outro chamado, duas atribuições simultâneas | 204 sem entrada nova; uma 204 e uma 409, com uma só entrada nova |
| AC-19 | REQ-3, REQ-9 | Beto na lista de elegíveis e com as permissões já em cache (uma chamada autenticada antes) | o Administrador tira `tickets:manage` da role Atendente e, no mesmo minuto, consulta a lista e atribui um chamado a Beto | Beto não aparece na lista, e a atribuição recebe 400 `Tickets.AssigneeNotAttendant` |
| AC-21 | REQ-13 | — | revisão do endpoint `Endpoints/Users/GetAttendants.cs` e teste de integração com 101 chamadas do mesmo usuário em 60 s | o endpoint não chama `RequireRateLimiting`; a 101ª chamada recebe 429 do limite global |
| AC-20 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | "Backlog" inclui chamados Em andamento? | (a) sim, todo chamado ativo (regra atual); (b) só Aberto | (a): reatribuir um chamado em andamento (férias, sobrecarga) é o caso mais comum de reatribuição | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-2 | Usuário atende mais de um setor? | (a) um setor por usuário (`users.sector_id`); (b) N:N usuário × setor | (a): o setor só ordena a lista (DEC-5); passar para N:N depois é uma migration simples, sem quebrar contrato se o DTO mantiver o setor principal | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-3 | Quem pode ser responsável? | (a) só a role Atendente com `tickets:manage` (o Administrador deixa de ser elegível); (b) manter qualquer `tickets:manage` | (b): o Tickest autoriza por permissão em todas as regras de negócio, e as permissões das roles são editáveis; testar o nome da role criaria uma segunda regra que diverge da primeira (ex.: role Atendente sem `tickets:manage`). Desvia da fonte, que pedia a role "Attendant" | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-4 | O Atendente deixa de atribuir chamados (inclusive a si mesmo)? | (a) sim, só `tickets:assign`; (b) o Atendente pode atribuir só a si mesmo, sem `tickets:assign` | (b): o Administrador distribui os chamados (pedido da fonte) e o Atendente continua "puxando" chamados da fila para si, que é o fluxo normal de helpdesk e o comportamento atual | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-5 | Restringir a atribuição ao setor do chamado? | (a) não, o setor só ordena; (b) sim | (a): com setores ainda não cadastrados nos usuários (R-4), restringir travaria a atribuição | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-6 | Como devolver o 403 com mensagem de negócio? | (a) o endpoint exige só usuário ativo, e o handler confere `users:read` e devolve `Error.Forbidden` (`Users.ReadForbidden`), como os guards atuais; (b) personalizar o 403 da policy para todas as rotas | (a): mesmo padrão dos guards existentes e alcance limitado a esta rota | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("encontre as melhores soluções") |
| DEC-7 | Como buscar por nome completo? | (a) dividir o termo em palavras e exigir cada uma em algum campo, com `Contains` sobre colunas `citext`; (b) concatenar nome e sobrenome e usar `ILike` com escape manual | (a): reaproveita a comparação sem maiúsculas que o `citext` já dá, não depende de escape manual (o EF parametriza o `Contains`) e encontra "ana souza" mesmo com a ordem trocada ou e-mail na busca | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("Na spec 009 atue como arquiteto .net e encontre a melhor solução") |
| DEC-8 | Limite da página | (a) o padrão do sistema (20, máximo 100, ajuste sem erro); (b) máximo 50 com 400, como a fonte | (a): todas as listagens se comportam igual, e o front não precisa tratar um 400 que nenhuma outra rota dá | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("Na spec 009 atue como arquiteto .net e encontre a melhor solução") |
| DEC-9 | Rate limit próprio da consulta | (a) só o global; (b) política de 60 por minuto | (a): o global já é por usuário e mais restrito que o uso real do autocomplete com espera de 300 ms; uma política a mais é configuração sem ganho | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("Na spec 009 atue como arquiteto .net e encontre a melhor solução") |
| DEC-10 | Onde mora a regra de elegibilidade | (a) uma expressão única na Application (`TicketAssigneeEligibility`), traduzida em `EXISTS` pelo EF, usada pela consulta e pela atribuição, sempre no banco; (b) consulta no banco e atribuição pelo `IPermissionProvider` em cache | (a): a lista e a atribuição nunca discordam, e a regra fica num lugar só | Rafael Santos, 2026-10-24, no chat, delegando a escolha ("Na spec 009 atue como arquiteto .net e encontre a melhor solução") |
| DEC-11 | Como mostrar nomes no histórico | (a) depois de buscar a página, juntar os ids de usuário das linhas de responsável e buscá-los numa consulta; (b) gravar os nomes no histórico; (c) subconsultas no `.Select` da própria página, só nas linhas de responsável | (c), no lugar de (a): o handler fica no padrão das skills (filtro, `AfterCursor`, `.Select` direto no DTO e `ToCursorPagedListAsync`), sem passo em memória depois da página; o histórico continua guardando só fatos (ids) e mostra o nome atual da pessoa. Custo aceito: a comparação do id com o texto gravado não usa índice, numa página de no máximo 100 linhas | Rafael Santos, 2026-10-24, no chat, delegando a escolha (a); trocada por (c) por Rafael Santos em 2026-10-26, no chat ("GetTicketHistoryQueryHandler está fora do padrão pré estabelecido nas skills", escolhendo a opção "Subconsulta no Select") |

## 5. Fora do escopo
- Front Angular (modal `AssignResponsibleModalComponent`, RF-05 da fonte): repositório separado. Este repositório entrega os contratos da seção 6.
- Remoção de responsável (spec 001, permanece com `tickets:manage`).
- Responsável indicado na abertura do chamado (Q-05).
- Notificação ao responsável (não há envio de e-mail).
- Log de aviso por 403 (RNF-01 da fonte, parte de log): o pipeline atual já registra a resposta; não há log novo.
- Meta de desempenho com 5.000 usuários (RNF-05 da fonte): sem teste de carga nesta spec; o índice em `users.sector_id` entra, e a ordenação usa o índice existente de nome, sobrenome e id.
- Rate limit próprio da consulta (RNF-03 da fonte): coberto pelo global (DEC-9).

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Domain: `Users/User.cs` (`SectorId`); `Users/UserSectorChangedDomainEvent.cs`; `Tickets/TicketHistoryAction.cs` (`Assigned = 13`); `Tickets/TicketErrors.cs` (`AssigneeNotAttendant`, substituindo `AssigneeCannotManageTickets`; `CannotAssignToOthers`); `Users/UserErrors.cs` (`ReadForbidden`).
  - Application:
    - `Permissions/PermissionCodes.cs` e `PermissionCatalog.cs`: `tickets:assign` ("Atribuir chamados", administrativa).
    - `Users/ChangeSector/*` (command, validator, handler com `PrivilegeEscalationGuard`).
    - `Tickets/TicketAssigneeEligibility.cs`: filtro `WhereEligibleAssignee` em `IQueryable<User>`, no formato do `TicketSlaFilters`, que recebe o `IApplicationDbContext` — `u.IsActive && context.UserRoles.Any(ur => ur.UserId == u.Id && context.RolePermissions.Any(rp => rp.RoleName == ur.RoleName && rp.PermissionCode == PermissionCodes.Tickets.Manage))` —, que o EF traduz em `EXISTS` aninhados, apoiados nos índices únicos de `user_roles` e `role_permissions`.
    - `Users/GetAttendants/*` (`GetBacklogAttendantsQuery`, handler e `AttendantResponse`): o tamanho do termo é conferido no próprio handler (400 `Users.InvalidSearchTerm`), porque query não tem validator no padrão das skills, e os limites ficam em `Users/UserValidationRules.cs` (`SearchTermMinLength`, `SearchTermMaxLength`, `SearchTermMaxWords`); checagem de `users:read` pelo `IPermissionProvider` (a permissão de quem consulta pode vir do cache); `WhereEligibleAssignee`; junção à esquerda com `Sectors` e `Departments`; um `Where` por palavra do termo com `Contains` nos cinco campos; `OrderBy(x => x.SectorId == prioritySectorId ? 0 : 1)` seguido de nome, sobrenome e `Id`; projeção no DTO antes do `ToPagedListAsync`.
    - `Tickets/Assign/AssignTicketCommandHandler.cs`: elegibilidade do responsável por `context.Users.Where(...).WhereEligibleAssignee(context).AnyAsync(...)` no lugar do `IPermissionProvider`; `tickets:assign` do executor (cache aceitável) exigida quando o responsável não é ele; ação `Assigned`/`Reassigned`.
    - `TicketHistories/TicketHistoryEntries.cs` (`Assigned`); `TicketHistoryDescriptions.For` ganha os nomes do responsável anterior e do novo; no `GetTicketHistoryQueryHandler`, o `.Select` da página passa a eles subconsultas em `Users`, só nas linhas `Assigned`, `Reassigned` e `Unassigned` (DEC-11).
  - Infrastructure: `Users/UserConfiguration.cs` (FK opcional, `OnDelete(Restrict)`, índice); `Roles/RolePermissionConfiguration.cs` (vínculo Administrador × `tickets:assign`); migration `Add_User_Sector_And_Tickets_Assign_Permission`, com `INSERT ... ON CONFLICT DO NOTHING` para a permissão e o vínculo.
  - Web.Api: `Endpoints/Users/GetAttendants.cs` (`GET users/attendants`), `Endpoints/Users/ChangeSector.cs` (`PUT users/{id}/sector`, `users:update`), `Endpoints/Tickets/Assign.cs` (continua `HasPermission(tickets:manage)`), `Endpoints/Tickets/Permissions.cs`.
  - Testes: unitários em `tests/TickestPristine.Application.UnitTests/Users/` e `Tickets/`; integração em `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` e `Tickets/TicketsTests.cs`; ajuste dos testes `Assign_*` que usam Atendente como executor.
- **Contratos:**
  - `GET users/attendants?searchTerm&sectorId&prioritySectorId&page&pageSize` → 200 `PagedResponse<AttendantResponse>`; 400 (`Users.InvalidSearchTerm`); 401; 403 `Users.ReadForbidden`; 429.
  - `PUT users/{id}/sector` `{ sectorId }` → 204; 400; 401; 403; 404 (`Users.NotFound`, `Sectors.NotFound`); 409.
  - `PUT tickets/{id}/assignee` → continua com `tickets:manage`; atribuir a outra pessoa exige `tickets:assign` (403 `Tickets.CannotAssignToOthers`); erro `Tickets.AssigneeNotAttendant` substitui `Tickets.AssigneeCannotManageTickets`.
  - Evento novo `UserSectorChangedDomainEvent(UserId, OldSectorId, NewSectorId)`.
- **Mudança no banco:** sim. Coluna `users.sector_id` (nula, FK, índice); permissão `tickets:assign` e o vínculo com a role Administrador. A ação `Assigned` é texto e não exige migration.
- **Riscos:**
  - R-1: o Atendente perde a atribuição a outras pessoas (DEC-4); o front mostra a ele só a ação "Assumir chamado".
  - R-2: o Administrador aparece na lista de elegíveis, porque tem `tickets:manage` (DEC-3).
  - R-3: chamados antigos têm `Reassigned` também na primeira atribuição; a ação `Assigned` só vale daqui em diante.
  - R-4: até alguém cadastrar os setores dos usuários, todos aparecem "Sem setor" e a priorização não tem efeito.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `User.SectorId`, configuração, evento; permissão `tickets:assign` no catálogo e na role Administrador; migration com `ON CONFLICT` | AC-1, AC-2, AC-13 | add-entity | [x] |
| T-2 | Slice `ChangeSector` e endpoint `PUT users/{id}/sector` | AC-1, AC-2, AC-3 | add-feature | [x] |
| T-3 | `TicketAssigneeEligibility`; slice `GetAttendants` (permissão no handler, elegibilidade, busca por palavras, ordenação, paginação) e endpoint | AC-4 a AC-11, AC-19, AC-21 | add-feature | [x] |
| T-4 | Atribuição: elegibilidade pela expressão compartilhada, `tickets:assign` para atribuir a outra pessoa, `Assigned`/`Reassigned`; descrições do histórico com nomes em lote | AC-12, AC-14 a AC-19 | add-feature | [x] |
| T-5 | Testes unitários e de integração dos ACs; ajuste dos testes `Assign_*` em que o Atendente atribui a outra pessoa | AC-1 a AC-19 | add-tests | [x] |
| T-6 | Build e testes da solução | AC-20 | add-tests | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `ChangeSector_Should_SetRejectDeactivatedAndRemoveSector_WhenCallerIsAdmin`; `ChangeUserSectorCommandHandlerTests` › `Handle_Should_SetSectorAndRaiseDomainEvent_WhenSectorIsActive`, `Handle_Should_ReturnSectorNotFoundAndKeepSector_WhenSectorIsDeactivated`, `Handle_Should_RemoveSector_WhenSectorIdIsNull` | Pela API, o administrador grava TI (204), tenta o setor desativado (404 `Sectors.NotFound`, o banco continua com TI) e remove o setor (204, `sector_id` nulo). | |
| AC-2 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `ChangeSector_Should_KeepUserSector_WhenSectorIsDeactivated` | Desativa o setor TI pela API e confere no banco que o `sector_id` de Ana continua TI. | |
| AC-3 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `ChangeSector_Should_ReturnForbidden_WhenCallerLacksUpdatePermission` | Um usuário com a role Atendente chama a rota e recebe 403, e o setor não muda. | |
| AC-4 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_ReturnReadForbidden_WhenCallerManagesTicketsButLacksReadPermission`; `GetBacklogAttendantsQueryHandlerTests` › `Handle_Should_ReturnReadForbidden_WhenCallerLacksReadPermission` | Um Atendente (com `tickets:manage`, sem `users:read`) recebe 403 com `title` `Users.ReadForbidden` e o `detail` exato. | |
| AC-5 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_ReturnOnlyEligibleUsersByNameWithSixFields`; `GetBacklogAttendantsQueryHandlerTests` › `Handle_Should_ReturnOnlyActiveUsersWithManagePermissionOrderedByName` | Com os usuários comuns (isolados por um marcador no e-mail; Davi com uma role de teste no papel de Administrador), a lista vem como Ana, Beto, Caio, Davi e Eva, sem Bruno e Carla, com Eva sem setor e só os seis campos no JSON. | |
| AC-6 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_FollowManagePermissionOfEachRole` | Roles de teste no papel de Atendente e Colaborador (as fixas são compartilhadas entre os testes): sem `tickets:manage` na primeira, só Davi aparece; com `tickets:manage` na segunda, Carla passa a aparecer. | |
| AC-7 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_ReturnBadRequest_WhenSearchTermLengthIsOutOfRange`; `GetBacklogAttendantsQueryHandlerTests` › `Handle_Should_ReturnInvalidSearchTerm_WhenTrimmedTermIsOutOfRange`, `Handle_Should_AcceptSearchTerm_WhenTermIsAbsentOrTrimmedTermHasValidLength` | Pela API, termos com 1 e com 101 caracteres recebem 400; no handler, o termo com 1 caractere, só com espaços ou com 101 caracteres é recusado, e o ausente, o vazio e o de 100 caracteres depois do `Trim` são aceitos. | |
| AC-8 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_MatchEveryWordWithoutCaseInAnyField` | Contra o PostgreSQL: "ANA" acha Ana, "tecnolog" acha Ana e Beto pelo departamento, "ana souza" acha Ana, e "souza lima" e "%%" voltam vazios (cada busca leva também o marcador do cenário). | |
| AC-9 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_AdjustPageSizeWithoutError_WhenPagingIsMissingOrOutOfRange` | Com 120 atendentes: sem `pageSize` vêm 20, com 50 vêm 50, com 500 vêm 100 e com `page=0` vem a página 1; todas com 200 e total 120. | |
| AC-10 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_PutPrioritySectorFirst`; `GetBacklogAttendantsQueryHandlerTests` › `Handle_Should_PutPrioritySectorFirst_WhenPrioritySectorIdIsGiven` | Com `prioritySectorId` = Financeiro, a ordem é Caio, Ana, Beto, Davi e Eva. | |
| AC-11 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_ReturnEmptyPage_WhenFiltersMatchNobody`; `GetBacklogAttendantsQueryHandlerTests` › `Handle_Should_FilterBySectorAndEveryWord_WhenFiltersAreCombined` | "caio" com `sectorId` = TI devolve 200, lista vazia e total 0. | |
| AC-12 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_LetAgentAssignOnlyToSelf_WhenAgentLacksAssignPermission`; `AssignTicketCommandHandlerTests` › `Handle_Should_ReturnCannotAssignToOthers_WhenCallerLacksAssignPermission`, `Handle_Should_AssignToCaller_WhenCallerIsAttendantWithoutAssignPermission` | Beto, com a role Atendente real, recebe 403 `Tickets.CannotAssignToOthers` ao atribuir a Ana (o responsável continua nulo) e 204 ao assumir o chamado. | |
| AC-13 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Migration_Should_GrantAdministrativeAssignPermissionOnlyToAdministrator_AndRunAgain` | Roda de novo o SQL da migration sem erro e confere que `tickets:assign` é administrativa, aparece uma única vez no Administrador e não está no Atendente nem no Colaborador. | |
| AC-14 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_ValidateAssigneeInOrder_WhenAdministratorAssigns`; `AssignTicketCommandHandlerTests` › `Handle_Should_ReturnAssigneeNotAttendant_WhenAssigneeLacksManagePermission`, `Handle_Should_ReturnUserNotFound_*` | O administrador recebe 400 `Tickets.AssigneeNotAttendant` para Carla, 404 `Users.NotFound` para Bruno (desativado) e para um id inexistente, e 204 ao atribuir a si mesmo. | |
| AC-15 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_ReturnNotActiveAndRecordNothing_WhenTicketIsResolved` | Num chamado Resolvido, recebe 400 `Tickets.NotActive`, sem responsável e sem nenhuma entrada nova no histórico. | |
| AC-16 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_RecordAssignedThenReassigned_AndShowNamesInHistoryInPageQuery`; `AssignTicketCommandHandlerTests` › `Handle_Should_RecordAssigned_WhenTicketHasNoAssignee` | Duas atribuições (204 e 204) gravam `Assigned` (anterior nulo, novo Ana, executor o administrador, data UTC) e depois `Reassigned` (Ana → Beto). | |
| AC-17 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_RecordAssignedThenReassigned_AndShowNamesInHistoryInPageQuery`; `GetTicketHistoryQueryHandlerTests` › `Handle_Should_DescribeAssigneesByName_IncludingDeactivatedAndRemovedUsers` | Com Ana desativada e o responsável removido, o `GET tickets/{id}/history` cita Ana e Beto pelo nome completo, e o `EfCommandCounter` mostra que um único comando lê nomes de usuário e que esse comando é a consulta da página. | |
| AC-18 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Assign_Should_RecordNothing_WhenAssigneeIsAlreadyTheCurrentOne`, `Assign_Should_ReturnNoContentAndConflict_WhenTwoAssignmentsRunAtOnce` | Reatribuir a Ana dá 204 sem entrada nova; duas atribuições simultâneas (um gatilho segura a primeira) dão 204 e 409, com uma só entrada `Assigned`. | |
| AC-19 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendantsAndAssign_Should_IgnoreCachedPermissions_WhenRoleLosesManagePermission`; `AssignTicketCommandHandlerTests` › `Handle_Should_ReturnAssigneeNotAttendant_WhenCachedPermissionsStillSayAssigneeManagesTickets` | Beto faz uma chamada autenticada antes (permissões em cache); a role dele perde `tickets:manage` e, logo depois, ele some da lista e a atribuição recebe 400 `Tickets.AssigneeNotAttendant`. | |
| AC-20 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | Build com 0 avisos; 50 testes de arquitetura, 581 unitários e 328 de integração aprovados (execução de 2026-10-26, a refazer na `spec-verify`). | |
| AC-21 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAttendants_Should_HaveNoRateLimitPolicyOfItsOwn`, `GetAttendants_Should_ReturnTooManyRequests_WhenGlobalLimitIsExceeded` | O endpoint não tem metadado de rate limit próprio; com o limite global padrão de 100, a 101ª chamada do mesmo usuário recebe 429. | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar pela `spec-verify`: `SYSTEM_SPECIFICATION.md` 2.2 (permissão nova e elegibilidade), 2.4 (`User` × `Sector`), 2.6 (RN-22), 4.3.1 (rotas), 5.10 (atribuir).
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-24 | Rascunho | Criada a partir de `specs/_fontes/atribuicao-responsavel.md`, a pedido de Rafael Santos no chat. DEC-1 a DEC-6 em aberto, com recomendação. |
| 2026-10-24 | Rascunho | DEC-1 a DEC-6 decididas: Rafael Santos delegou a escolha no chat ("encontre as melhores soluções"). DEC-3 e DEC-4 ficaram diferentes da recomendação inicial e da fonte (elegibilidade por permissão; Atendente assume para si, só o Administrador atribui a outros); REQ-3, REQ-8, AC-5, AC-6, AC-10, AC-12, AC-14, riscos e plano ajustados. Checklist de completude atendido; aguarda aprovação explícita. |
| 2026-10-24 | Rascunho | Revisão de arquitetura pedida por Rafael Santos no chat ("Na spec 009 atue como arquiteto .net e encontre a melhor solução"): DEC-7 a DEC-11 decididas; REQ-3, REQ-5, REQ-6, REQ-7, REQ-11, REQ-13, AC-8, AC-9, AC-17, AC-19, plano e tarefas ajustados. Regra de elegibilidade única e sem cache; busca por palavras sobre `citext`; paginação e rate limit nos padrões do sistema; nomes do histórico em lote. Aguarda aprovação explícita. |
| 2026-10-24 | Rascunho | AC-21 acrescentado para cobrir o REQ-13, que ficou sem critério depois da revisão de arquitetura (checklist de completude). |
| 2026-10-24 | Aprovada | Aprovação explícita de Rafael Santos no chat ("aprovada!"), em resposta ao pedido de aprovação da spec 009. Checklist de completude conferido: todo REQ com AC, nenhuma DEC em aberto, toda tarefa ligada a ACs. |
| 2026-10-26 | Em implementação | `/spec-implement 009` na branch `feat/SPEC-009-attendant-lookup-assignment`; Rafael Santos pediu a execução contínua de todas as tarefas, e os commits ficam para depois do diff e do ok. |
| 2026-10-26 | Em implementação | T-1 a T-6 implementadas, com o build e todos os testes aprovados; os commits aguardam o ok de Rafael Santos. Pendente de decisão dele: a recusa do `PrivilegeEscalationGuard` na troca de setor usa a mensagem atual de `Users.CannotManagePrivilegedUser`, que fala em desativar e reativar. |
| 2026-10-26 | Em implementação | Revisão pedida por Rafael Santos no chat ("GetTicketHistoryQueryHandler está fora do padrão pré estabelecido nas skills"; "GetUserIds não compactua com as minhas skills tb!"): DEC-11 trocada de (a) para (c), com a escolha "Subconsulta no Select"; REQ-11, AC-17 e o plano técnico ajustados, sem mudar o objetivo. Na mesma revisão, com a escolha "Alinhar ao padrão", a consulta de atendentes deixou de ter validator (o termo é conferido no handler, com 400 `Users.InvalidSearchTerm`) e o DTO passou a se chamar `AttendantResponse`. |
| 2026-10-26 | Em implementação | Revisão pedida por Rafael Santos no chat ("AttendantSearch não segue minhas skills !"; sobre o `TicketAssigneeEligibility`, "ele segue minhas skilss ?"): com as escolhas "Sim, apague e mova" e "Filtro como o TicketSlaFilters", o `AttendantSearch` foi removido (os limites do termo foram para o `UserValidationRules`, e a checagem e a divisão em palavras, para o handler), e a regra de elegibilidade virou o filtro `WhereEligibleAssignee`, mantida num lugar só (DEC-10 sem mudança). |
