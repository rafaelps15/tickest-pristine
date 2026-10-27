---
id: 013
titulo: Setor do usuário nas respostas de consulta de usuários
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-26
branch: feat/SPEC-013-user-sector-in-responses                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr: "#28"                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 013 — Setor do usuário nas respostas de consulta de usuários

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-26, escolhendo a opção "3" ("Spec nova no backend (`spec-new`) para o setor do usuário aparecer nas respostas de `GET users` e `GET users/{id}`, antes da tela de setor"), depois de o front da spec 009 ficar sem como exibir o setor atual do usuário.
- **Referências:**
  - Spec 009 (REQ-1, DEC-2): `User.SectorId` opcional e `PUT users/{id}/sector`; ainda não mergeada na `main` (branch `feat/SPEC-009-attendant-lookup-assignment`, sem PR aberto em 2026-10-26).
  - `SYSTEM_SPECIFICATION.md` 2.4 (`User` × `Sector`), 2.6 (RN-29), 4.3.1 (rotas de usuários), C-05.
- **Fatos do repositório:**
  - `GET users` projeta `UserSummaryResponse` (`Application/Users/GetAll/UserSummaryResponse.cs`: id, e-mail, nome, sobrenome, ativo, datas e roles), sem setor (`GetUsersQueryHandler.cs`).
  - `GET users/{id}` projeta `GetById/UserResponse` (id, e-mail, nome e sobrenome) e aceita o próprio usuário ou quem tem `users:read` (`GetUserByIdQueryHandler.cs`, `IsSelfOrHasPermissionAsync`).
  - `GET users/by-email` e `GET users/me` também têm DTOs próprios sem setor (`GetByEmail/UserResponse.cs`, `GetCurrent/CurrentUserResponse.cs`).
  - Os DTOs de leitura já existentes usam campos planos para setor (`TicketSummaryResponse`: `SectorId`, `SectorName`; `AttendantResponse` da spec 009: `SectorId`, `SectorName`, `DepartmentName`).
  - O setor desativado continua vinculado ao usuário (spec 009, AC-2); a fonte da spec 009 pedia que ele aparecesse "marcado como inativo" (`specs/_fontes/atribuicao-responsavel.md`, RF-01.4).
  - No front, a lista de usuários (`users/feature-user-list`) usa `GET users` e o modelo `UserSummary` (`users/entities/user-summary.ts`), sem setor.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada item de `GET users` traz `sectorId`, `sectorName`, `departmentName` e `sectorIsActive` do setor vinculado ao usuário; sem setor, os quatro vêm nulos. Filtros, ordenação e paginação da listagem não mudam. | pedido; DEC-1; DEC-2; DEC-3 |
| REQ-2 | `GET users/{id}` traz os mesmos quatro campos, com a mesma regra para usuário sem setor. A autorização não muda: o próprio usuário ou quem tem `users:read`. | pedido; DEC-1; DEC-3 |
| REQ-3 | Um setor desativado continua aparecendo nas duas respostas, com `sectorIsActive` = `false` e os nomes do setor e do departamento. | spec 009 (AC-2); fonte da spec 009 (RF-01.4); DEC-3 |
| REQ-4 | As demais respostas de usuário não mudam: `GET users/me` e `GET users/by-email` continuam sem os campos de setor. | DEC-1 |

## 3. Critérios de aceitação
Dados comuns: setor TI ativo, no departamento Tecnologia; Ana (ativa, com setor TI); Eva (ativa, sem setor); o administrador do seed.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | Ana e Eva | o administrador chama `GET users` com um `search` que encontra só as duas | 200; o item de Ana traz `sectorId` = TI, `sectorName` = "TI", `departmentName` = "Tecnologia" e `sectorIsActive` = `true`; o item de Eva traz os quatro campos nulos; a ordem e o `totalCount` são os de antes da mudança |
| AC-2 | REQ-2 | Ana com setor TI; um Atendente sem `users:read` | o administrador chama `GET users/{Ana}`; Ana chama `GET users/{Ana}`; o Atendente chama `GET users/{Ana}` | 200 com os quatro campos do setor TI nas duas primeiras; 403 na terceira, como hoje |
| AC-3 | REQ-3 | Ana com setor TI | o setor TI é desativado e o administrador chama `GET users` e `GET users/{Ana}` | nas duas, `sectorId` = TI, `sectorName` = "TI", `departmentName` = "Tecnologia" e `sectorIsActive` = `false` |
| AC-4 | REQ-1, REQ-2 | Eva sem setor | o administrador grava o setor TI em Eva por `PUT users/{Eva}/sector`, consulta as duas rotas, remove o setor (`null`) e consulta de novo | depois da gravação, as duas rotas trazem o setor TI; depois da remoção, os quatro campos vêm nulos |
| AC-5 | REQ-4 | Ana com setor TI | Ana chama `GET users/me`; o administrador chama `GET users/by-email` com o e-mail de Ana | as duas respostas não têm nenhuma das propriedades `sectorId`, `sectorName`, `departmentName` e `sectorIsActive` |
| AC-6 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quais respostas ganham o setor? | (a) só `GET users` e `GET users/{id}`; (b) também `GET users/me`; (c) as quatro, com `GET users/by-email` | (a): resolve a tela de setor sem mexer no login nem no cabeçalho do front | Rafael Santos, 2026-10-26, no chat ("1a") |
| DEC-2 | Filtro por setor em `GET users`? | (a) não, fora desta spec; (b) sim, junto com esta mudança | (a): o pedido é mostrar e editar o setor atual; o filtro pode virar outra spec | Rafael Santos, 2026-10-26, no chat ("2a") |
| DEC-3 | Campos e formato | (a) campos planos `sectorId`, `sectorName`, `departmentName`, `sectorIsActive`, nulos sem setor; (b) objeto `sector` aninhado ou nulo; (c) só `sectorId` | (a): segue `AttendantResponse` e `TicketSummaryResponse`; `sectorIsActive` marca o setor desativado | Rafael Santos, 2026-10-26, no chat ("3a") |
| DEC-4 | Como buscar setor e departamento | (a) subconsultas no `.Select` do DTO, como o `GetTicketsQueryHandler`; (b) junções à esquerda, como a consulta de atendentes | (a): é o `.Select` direto no DTO do padrão das skills; a listagem é paginada (no máximo 100 por página) | Rafael Santos, 2026-10-26, no chat ("4a") |
| DEC-5 | Base da branch | (a) esperar o merge do PR da spec 009 e criar a branch a partir da `origin/main`; (b) criar agora a partir da branch da 009 | (a): segue a regra do `CLAUDE.md` (branch a partir da `main` atualizada, só com a spec dela); a spec pode ser aprovada antes, e a implementação começa depois do merge | Rafael Santos, 2026-10-26, no chat ("5a") |

## 5. Fora do escopo
- Setor em `GET users/me` e em `GET users/by-email` (DEC-1).
- Filtro e ordenação por setor em `GET users` (DEC-2).
- A tela de setor do usuário e o ajuste do modelo `UserSummary` no front Angular (repositório `tickest-pristine-front`).
- Qualquer mudança em `PUT users/{id}/sector`, na consulta de atendentes ou na atribuição (spec 009).

## 6. Plano técnico
- **Pré-requisito:** PR da spec 009 mergeado na `main` (DEC-5).
- **Arquivos a criar ou alterar:**
  - Application:
    - `Users/GetAll/UserSummaryResponse.cs`: `SectorId` (`Guid?`), `SectorName`, `DepartmentName` (`string?`) e `SectorIsActive` (`bool?`).
    - `Users/GetAll/GetUsersQueryHandler.cs`: no `.Select` do DTO, os quatro campos por subconsultas em `Sectors` e `Departments`, pelo `u.SectorId` (DEC-4), como o `SectorName` do `GetTicketsQueryHandler`.
    - `Users/GetById/UserResponse.cs` e `Users/GetById/GetUserByIdQueryHandler.cs`: os mesmos quatro campos, da mesma forma.
  - Testes:
    - unitários: `tests/TickestPristine.Application.UnitTests/Users/GetUsersQueryHandlerTests.cs` e `GetUserByIdQueryHandlerTests.cs` (estender os existentes);
    - integração: `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs`.
- **Contratos:** `GET users` (itens) e `GET users/{id}` ganham `sectorId`, `sectorName`, `departmentName` e `sectorIsActive` (nulos sem setor). Mudança só aditiva: nenhum campo sai nem muda de tipo. Nenhuma rota, erro ou evento novo.
- **Mudança no banco:** não (a coluna `users.sector_id` vem da spec 009).
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-29, leitura do setor), 2.7 (C-05) e 4.4, se citar os DTOs de usuário.
- **Análise:**
  - **Concorrência:** não se aplica. As duas rotas só leem, e cada uma lê usuário, setor e departamento num único comando SQL (as subconsultas fazem parte dele), então a resposta é consistente mesmo com um `PUT users/{id}/sector` acontecendo ao mesmo tempo: vale o estado de antes ou o de depois, nunca uma mistura.
  - **Quebra de contrato:** nenhuma. Os quatro campos são novos, nenhum sai e nenhum muda de tipo. A API não configura opções de JSON (nenhum `JsonSerializerOptions` nem `ConfigureHttpJsonOptions` em `src/`), então os campos nulos são enviados como `null`, e não omitidos: o cliente sempre recebe as quatro propriedades. O front ignora campos que o modelo `UserSummary` ainda não declara.
  - **Desempenho:** até 4 subconsultas escalares por linha, todas pela chave primária de `sectors` (`id`) e de `departments` (`id`), sobre no máximo 100 linhas por página (`PaginationExtensions.MaxPageSize`). A contagem do `ToPagedListAsync` não muda, porque o filtro e o `Count` não usam os campos novos.
  - **Segurança:** os campos novos seguem a autorização que já existe em cada rota; nenhum dado de outro usuário fica visível a quem já não o via.
- **Rotas afetadas:**

  | Rota | Autorização (sem mudança) | Resposta | Status |
  |---|---|---|---|
  | `GET users` | `users:read` | `PagedResponse<UserSummaryResponse>`, cada item com `sectorId`, `sectorName`, `departmentName` e `sectorIsActive` | 200 · 401 · 403 |
  | `GET users/{id}` | usuário ativo, e no handler: o próprio usuário ou `users:read` | `UserResponse` com os mesmos quatro campos | 200 · 401 · 403 `Users.Unauthorized` · 404 `Users.NotFound` |

- **Árvore de arquivos:**

  ```text
  src/TickestPristine.Application/Users/
    GetAll/
      UserSummaryResponse.cs             [MODIFIED]  + SectorId, SectorName, DepartmentName, SectorIsActive
      GetUsersQueryHandler.cs            [MODIFIED]  subconsultas no .Select
    GetById/
      UserResponse.cs                    [MODIFIED]  + os mesmos quatro campos
      GetUserByIdQueryHandler.cs         [MODIFIED]  subconsultas no .Select
  tests/TickestPristine.Application.UnitTests/Users/
    GetUsersQueryHandlerTests.cs         [MODIFIED]
    GetUserByIdQueryHandlerTests.cs      [MODIFIED]
  tests/TickestPristine.IntegrationTests/Users/
    UsersTests.cs                        [MODIFIED]
  ```

  Sem arquivo novo, sem endpoint novo, sem configuração do EF e sem migration.

- **Testes previstos** (nomes no padrão dos testes existentes; podem ser ajustados na implementação, e a rastreabilidade registra os reais):

  | AC | Unitários | Integração (`UsersTests`) |
  |---|---|---|
  | AC-1 | `GetUsersQueryHandlerTests` › `Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector`, `Handle_Should_ReturnNullSector_WhenUserHasNoSector` | `GetAll_Should_ReturnSectorFields_ForUsersWithAndWithoutSector` |
  | AC-2 | `GetUserByIdQueryHandlerTests` › `Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector` | `GetById_Should_ReturnSectorFields_WhenCallerIsAdminOrSelf`; o 403 já é coberto por `GetById_Should_ReturnForbidden_WhenQueryingAnotherUserWithoutReadPermission` |
  | AC-3 | `GetUsersQueryHandlerTests` e `GetUserByIdQueryHandlerTests` › `Handle_Should_ReturnInactiveSector_WhenSectorIsDeactivated` | `GetAllAndGetById_Should_ReturnInactiveSector_WhenSectorIsDeactivated` |
  | AC-4 | — | `GetAllAndGetById_Should_FollowSectorChanges` |
  | AC-5 | — | `GetCurrentAndGetByEmail_Should_NotExposeSectorFields` |
  | AC-6 | build e suíte inteira | build e suíte inteira |
- **Riscos:**
  - R-1: duas subconsultas a mais por linha na listagem; com no máximo 100 linhas por página e as chaves primárias de `sectors` e `departments`, o custo é baixo.
  - R-2: o front ignora os campos novos até o modelo `UserSummary` ser atualizado no repositório dele; como a mudança é aditiva, nada quebra.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `GET users`: os quatro campos no `UserSummaryResponse` e no `.Select` do `GetUsersQueryHandler` | AC-1, AC-3, AC-4 | add-feature | [x] |
| T-2 | `GET users/{id}`: os quatro campos no `UserResponse` e no `.Select` do `GetUserByIdQueryHandler` | AC-2, AC-3, AC-4 | add-feature | [x] |
| T-3 | Testes unitários e de integração dos ACs, incluindo a ausência dos campos em `GET users/me` e `GET users/by-email` | AC-1 a AC-5 | add-tests | [x] |
| T-4 | Build e testes da solução | AC-6 | add-tests | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAll_Should_ReturnSectorFields_ForUsersWithAndWithoutSector`; `GetUsersQueryHandlerTests` › `Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector`, `Handle_Should_ReturnNullSector_WhenUserHasNoSector` | Pela API, `GET users?search=` com o marcador do cenário devolve Ana e Eva nessa ordem, total 2; Ana com setor TI, departamento e `sectorIsActive` = `true`, Eva com os quatro campos nulos.  | Aprovado em 2026-10-26 |
| AC-2 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetById_Should_ReturnSectorFields_WhenCallerIsAdminOrSelf`; `GetUserByIdQueryHandlerTests` › `Handle_Should_ReturnSectorAndDepartment_WhenUserHasSector`, `Handle_Should_ReturnNullSector_WhenUserHasNoSector` | O administrador e a própria Ana recebem os quatro campos do setor TI em `GET users/{id}`; um Atendente (sem `users:read`) recebe 403.  | Aprovado em 2026-10-26 |
| AC-3 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAllAndGetById_Should_ReturnInactiveSector_WhenSectorIsDeactivated`; `GetUsersQueryHandlerTests` e `GetUserByIdQueryHandlerTests` › `Handle_Should_ReturnInactiveSector_WhenSectorIsDeactivated` | Depois de `DELETE sectors/{TI}` (204), as duas rotas continuam trazendo o setor TI e o departamento, com `sectorIsActive` = `false`.  | Aprovado em 2026-10-26 |
| AC-4 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAllAndGetById_Should_FollowSectorChanges` | Grava o setor TI em Eva por `PUT users/{id}/sector` e as duas rotas passam a mostrá-lo; remove com `null` e as duas voltam aos quatro campos nulos.  | Aprovado em 2026-10-26 |
| AC-5 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetCurrentAndGetByEmail_Should_NotExposeSectorFields` | Com Ana vinculada ao setor TI, os JSONs de `GET users/me` e de `GET users/by-email` não têm nenhuma das propriedades `sectorId`, `sectorName`, `departmentName` e `sectorIsActive`.  | Aprovado em 2026-10-26 |
| AC-6 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | Build com 0 avisos; 970 testes aprovados (50 de arquitetura, 587 unitários, 333 de integração) na execução de 2026-10-26, a refazer na `spec-verify`.  | Aprovado em 2026-10-26 |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx` em 2026-10-26: 0 avisos, 0 erros.
- **Testes:** `dotnet test TickestPristine.slnx --no-build --logger trx` em 2026-10-26: 970 aprovados, 0 com falha (ArchitectureTests 50/50, Application.UnitTests 587/587, IntegrationTests 333/333). Os 11 testes da rastreabilidade (5 de integração e 6 unitários, com 3 nomes repetidos nas duas classes de handler) aparecem como aprovados no relatório TRX dessa execução. Auditoria `python scripts/auditoria_sdd.py .`: 100% (`CONFORME`), código de saída 0.
- **Revisão:** `ca-review` sobre `origin/main...HEAD` (os 9 arquivos do plano técnico): nenhum achado bloqueante e nenhuma violação de convenção (DTOs de cada slice, `.Select` direto no DTO, handlers `internal sealed`, textos em pt-BR, testes no padrão Arrange/Act/Assert). Observação não bloqueante: o `GetBacklogAttendantsQueryHandlerTests` (spec 009) tem um `SeedSectorAsync` privado próprio, que agora convive com o auxiliar do `BaseHandlerTest`; unificar fica para uma mudança à parte. Fora do escopo e sem revisão: `specs/012-dominio-rico-estilo-milan/` e `specs/_fontes/contratos-central-atendimento.md`, não versionados.
- **Limitações da verificação:** no caso sem setor (Eva), o DTO do teste não distingue um campo `null` de um campo ausente no JSON; a presença é coberta pelo item com setor, do mesmo tipo de resposta, e a API não omite nulos (sem opções de JSON configuradas). Os cenários de integração usam um marcador único no e-mail e no nome do departamento, porque o banco dos testes é compartilhado.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-29: leitura do setor em `GET users` e `GET users/{id}`) e 2.7 (C-05). A seção 4.4 não cita os campos dos DTOs de usuário e não muda. Achado fora do escopo: a 4.4 ainda descreve `TicketHistoryAction` como 1–12, sem o `Assigned = 13` da spec 009.
- **CI:**
- **Entrega:** PR #28 (https://github.com/rafaelps15/tickest-pristine/pull/28); a spec fica `Entregue` quando o responsável mergear o PR na `main`.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-26 | Rascunho | Criada a pedido de Rafael Santos no chat (opção "3"), como continuação do RF-01 da spec 009 para o front exibir e editar o setor atual do usuário. |
| 2026-10-26 | Em descoberta | Descoberta iniciada: fatos do repositório levantados; primeira rodada de perguntas enviada. |
| 2026-10-26 | Em descoberta | Primeira rodada respondida por Rafael Santos no chat ("1a 2a 3a 4a 5a"): DEC-1 a DEC-5 registradas; requisitos, critérios, plano e tarefas escritos. Checklist de completude atendido; aguarda aprovação explícita. |
| 2026-10-26 | Aprovada | Aprovação explícita de Rafael Santos no chat ("sim"), em resposta ao pedido de aprovação da spec 013. Checklist de completude conferido: todo REQ com AC, nenhuma DEC em aberto, nenhum `[SUPOSIÇÃO]`, toda tarefa ligada a ACs. A implementação começa depois do merge do PR da spec 009 (DEC-5). |
| 2026-10-26 | Aprovada | Detalhamento do plano técnico a pedido de Rafael Santos no chat (escolha "Detalhar a spec 013"): análise de concorrência, contrato, desempenho e segurança; tabela de rotas; árvore de arquivos; testes previstos por AC. Sem mudança de REQ, AC, DEC ou escopo, então a aprovação continua valendo. |
| 2026-10-26 | Em implementação | `/spec-implement 013` na branch `feat/SPEC-013-user-sector-in-responses`, já com o merge do PR #27 (spec 009) na base, como pede a DEC-5. |
| 2026-10-26 | Em implementação | T-1 a T-4 implementadas (T-1 commitada em `1564131`), com o build e todos os testes aprovados. Os cenários de integração usam um marcador único no e-mail e no nome do departamento ("Tecnologia-<marcador>"), porque o banco dos testes é compartilhado e o nome do departamento é único entre os ativos. Os commits de T-2 a T-4 aguardam o ok de Rafael Santos. |
| 2026-10-26 | Verificada | `spec-verify`: build com 0 avisos; 970 testes aprovados e 0 com falha na execução de 2026-10-26; auditoria 100% com código de saída 0; os 6 ACs conferidos teste a teste (Dado, Quando e Então), todos aprovados nessa execução; `ca-review` sem achado bloqueante; `SYSTEM_SPECIFICATION.md` atualizado (2.6 e 2.7). Pendências não bloqueantes: o CI, que roda no PR, e a 4.4 desatualizada pela spec 009. |
| 2026-10-26 | Verificada | PR #28 aberto para a `main` a pedido de Rafael Santos no chat ("ok, com PR"); campo `pr:` e linha "Entrega" preenchidos. |
