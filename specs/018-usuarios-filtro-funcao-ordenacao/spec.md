---
id: 018
titulo: Filtro por função (role) e ordenação por nome ou cadastro em GET users
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-018-users-role-filter-sorting                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 018 — Filtro por função (role) e ordenação por nome ou cadastro em GET users

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27 (item 2 de 5): "Filtro e ordenação na listagem de usuários (`GET /users`). Novo parâmetro opcional para filtrar pelo nome de uma função, com uma opção para listar só quem não tem nenhuma função. Novo parâmetro opcional de ordenação por nome ou por data de cadastro, crescente ou decrescente. Sem ele, a ordem continua a de hoje. Os dois se combinam com os filtros existentes (status, busca e período) e valem antes da paginação." A primeira versão sugeria `roleName` com valor reservado `none` ou flag `withoutRole=true`, e `sortBy`/`sortDirection` "ou equivalente".
- **Referências:** `SYSTEM_SPECIFICATION.md` 4.3.1 (`GET users` com `users:read`, linha 510), 2.6 RN-29 e RN-30 (campos da listagem), 2.7 C-05; specs 008 e 013 (campos de `UserSummaryResponse`).
- **Fatos do repositório:**
  - `Web.Api/Endpoints/Users/GetAll.cs` lê `status` (`UserStatusFilter?`), `search`, `createdFrom`, `createdTo` e a paginação (`PageRequest`), com `.HasPermission(users:read)`.
  - `Users/GetAll/GetUsersQueryHandler.cs:19-28` aplica os filtros num único `Where` sobre `IQueryable<User>`, ordena por `FirstName`, `LastName`, `Id` crescentes (linhas 29-31) e só então projeta e pagina com `ToPagedListAsync` (`Abstractions/Pagination/PaginationExtensions.cs`): `Count` e `Skip/Take` rodam no banco sobre a consulta já filtrada.
  - A função do usuário fica em `UserRole` (`Domain/Users/UserRole.cs`): `UserId` + `RoleName` (chave estrangeira para `Role.Name`), com índice único `(UserId, RoleName)` (`Infrastructure/Users/UserRoleConfiguration.cs:14`). Um usuário pode ter várias roles ou nenhuma (a resposta já traz a lista `roles`).
  - As roles do seed são "Administrador", "Atendente" e "Colaborador" (`Application/Roles/RoleNames.cs`); `GET roles` (`Endpoints/Roles/GetAll.cs`) devolve os nomes que o front usa no seletor.
  - Índices de `users`: `(FirstName, LastName, Id)` (`UserConfiguration.cs:26`), e-mail e código únicos, `SectorId`. Não há índice em `CreatedAtUtc`.
  - Precedentes de parâmetro: `GET tickets` usa `assignedToUserId` + `unassignedOnly` (o booleano vence quando os dois vêm, `Tickets/GetAll/GetTicketsQueryHandler.cs:17-18`) e um enum `sort` (`TicketSortOrder`: `Newest`, `DueDate`). Um enum inválido na query string responde 400 pela própria leitura do parâmetro.
  - Queries não passam pelo decorator de validação (`CLAUDE.md`, "Comportamentos transversais"): regras de combinação de parâmetros ficam no handler ou na leitura do endpoint.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET users` aceita `roleName` opcional: traz só os usuários que têm uma role com esse nome (comparação exata com o nome devolvido por `GET roles`, ignorando espaços nas pontas). Nome inexistente devolve lista vazia (200), não 404. Em branco equivale a sem filtro. | pedido; DEC-1; DEC-2 |
| REQ-2 | `GET users` aceita `withoutRole` opcional (booleano): com `true`, traz só os usuários sem nenhuma role. Com `withoutRole=true`, o `roleName` é ignorado, como `unassignedOnly` em `GET tickets`. | pedido; DEC-1 |
| REQ-3 | `GET users` aceita `sortBy` (`Name`, `CreatedAt`) e `sortDirection` (`Ascending`, `Descending`), opcionais. Sem `sortBy`, a ordem é a de hoje (nome, sobrenome e id, crescentes), e `sortDirection` sozinho inverte essa ordem. Com `sortBy=CreatedAt` e sem direção, a ordem é decrescente (mais recentes primeiro). Valor fora do enum responde 400. | pedido; DEC-3; DEC-4 |
| REQ-4 | Desempate estável: `Name` ordena por `FirstName`, `LastName` e `Id`; `CreatedAt`, por `CreatedAtUtc` e `Id`; a direção vale para todas as chaves. | DEC-4 |
| REQ-5 | Filtro por role e ordenação se combinam com `status`, `search`, `createdFrom` e `createdTo` e são aplicados no banco antes do `Count` e do `Skip/Take`: `totalCount` e as páginas refletem tudo. | pedido |
| REQ-6 | Sem os parâmetros novos, a resposta é idêntica à de hoje (contrato só aditivo). A permissão continua `users:read`. | pedido; fato do repositório |

## 3. Critérios de aceitação
Dados comuns: Ana (Atendente, cadastrada em 01/10), Bruno (Atendente e Administrador, 05/10), Carla (Colaboradora, 10/10, inativa), Davi (sem role, 15/10) e o administrador do seed.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | os dados comuns | `GET users?roleName=Atendente`; `?roleName=%20Atendente%20`; `?roleName=Inexistente`; `?roleName=%20` | Ana e Bruno; Ana e Bruno; 200 com lista vazia e `totalCount` = 0; todos |
| AC-2 | REQ-2 | os dados comuns | `GET users?withoutRole=true`; `?withoutRole=true&roleName=Atendente`; `?withoutRole=false` | só Davi; só Davi; todos |
| AC-3 | REQ-3, REQ-4 | os dados comuns | `?sortBy=Name`; `?sortBy=Name&sortDirection=Descending`; `?sortBy=CreatedAt`; `?sortBy=CreatedAt&sortDirection=Ascending`; `?sortDirection=Descending` | nome crescente; nome decrescente; Davi, Carla, Bruno, Ana (e o admin na posição da sua data); Ana, Bruno, Carla, Davi; nome decrescente |
| AC-4 | REQ-3 | — | `?sortBy=Email` e `?sortDirection=Up` | 400 nos dois |
| AC-5 | REQ-4 | dois usuários com o mesmo nome e sobrenome, e dois com o mesmo `CreatedAtUtc` | ordena por nome e por cadastro, nas duas direções, com `pageSize=1` | a ordem entre os empatados segue o `Id` e é a mesma em chamadas repetidas; nenhum usuário aparece em duas páginas nem some |
| AC-6 | REQ-5 | os dados comuns | `?roleName=Atendente&status=Active&search=an&createdFrom=2026-10-01&createdTo=2026-10-03&sortBy=CreatedAt&pageSize=1` | só Ana, `totalCount` = 1; com `roleName=Atendente&pageSize=1&page=2`, a página 2 traz o segundo atendente e `totalCount` = 2 |
| AC-7 | REQ-6 | os dados comuns | `GET users` sem os parâmetros novos; usuário sem `users:read`; sem token | mesma resposta e ordem de antes; 403; 401 |
| AC-8 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como pedir "sem nenhuma função" | (a) booleano `withoutRole`, separado do `roleName`; (b) valor reservado `roleName=none` | (a): um valor reservado colide com uma role que alguém crie com esse nome (as roles são cadastráveis), e o padrão já existe em `GET tickets` (`unassignedOnly`) | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-2 | Nome inexistente | (a) 200 com lista vazia; (b) 404 `Roles.NotFound` | (a): é filtro, não busca de recurso; não exige consulta extra | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-3 | Formato da ordenação | (a) `sortBy` + `sortDirection`, como no pedido; (b) um enum `sort` só (`Name`, `NameDescending`, `Newest`, `Oldest`), como `TicketSortOrder` em `GET tickets` | (a): é o que o front pediu e cobre as quatro combinações sem multiplicar valores; o desvio do padrão de `GET tickets` fica registrado | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-4 | Direção padrão quando só `sortBy` vem | (a) `Name` crescente e `CreatedAt` decrescente; (b) sempre crescente | (a): para cadastro, o caso comum é "mais recentes primeiro" | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-5 | Índice em `users.created_at_utc` | (a) não criar agora; (b) criar `(CreatedAtUtc, Id)` | (a): a tabela de usuários de uma empresa tem centenas a poucos milhares de linhas, e a ordenação em memória do PostgreSQL sobre isso é desprezível (YAGNI); reavaliar se o volume crescer | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |

## 5. Fora do escopo
- Filtro por várias roles ao mesmo tempo, por setor ou por departamento.
- Ordenação por e-mail, setor ou status.
- Filtro `status=PendingActivation` (ver a pergunta da spec 020).
- Qualquer mudança em `GET users/{id}`, `GET users/attendants` ou `GET roles`.

## 6. Plano técnico
- **Pré-requisito:** nenhum; branch a partir da `origin/main`.
- **Convenções:** handlers próprios `IQueryHandler` (sem MediatR, `CLAUDE.md`); `Result<T>`; identificadores em inglês, comentários em pt-BR.
- **Arquivos a criar ou alterar:**
  - Application:
    - `Users/GetAll/GetUsersQuery.cs`: ganha `RoleName` (`string?`), `WithoutRole` (`bool`), `SortBy` (`UserSortField?`) e `SortDirection` (`SortDirection?`).
    - `Users/GetAll/UserSortField.cs` (novo): enum `Name = 0`, `CreatedAt = 1`.
    - `Abstractions/Pagination/SortDirection.cs` (novo): enum `Ascending = 0`, `Descending = 1` (genérico, junto da paginação, para outra listagem reaproveitar).
    - `Users/GetAll/GetUsersQueryHandler.cs`:
      - normaliza `roleName` como já faz com `search` (`Trim`, branco vira nulo);
      - acrescenta ao `Where` existente, ainda sobre `IQueryable<User>`:
        - com `WithoutRole`: `!context.UserRoles.Any(ur => ur.UserId == u.Id)`;
        - senão, com `roleName`: `context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleName == roleName)`;
        - o EF traduz os dois para `NOT EXISTS`/`EXISTS` correlacionados, atendidos pelo índice único `(user_id, role_name)`;
      - troca o `OrderBy` fixo por uma escolha entre quatro ordenações, todas montadas no `IQueryable` antes do `.Select` e do `ToPagedListAsync`:
        - `Name` crescente (padrão de hoje): `OrderBy(FirstName).ThenBy(LastName).ThenBy(Id)`;
        - `Name` decrescente: `OrderByDescending(FirstName).ThenByDescending(LastName).ThenByDescending(Id)`;
        - `CreatedAt` crescente: `OrderBy(CreatedAtUtc).ThenBy(Id)`;
        - `CreatedAt` decrescente: `OrderByDescending(CreatedAtUtc).ThenByDescending(Id)`;
      - a projeção e o `ToPagedListAsync` não mudam: o `COUNT(*)` e o `OFFSET/LIMIT` continuam rodando no banco sobre a consulta filtrada e ordenada.
  - Web.Api:
    - `Endpoints/Users/GetAll.cs`: lê `roleName`, `withoutRole` (`bool?`), `sortBy` (`UserSortField?`) e `sortDirection` (`SortDirection?`) da query string e repassa (`withoutRole ?? false`). Enum inválido dá 400 pela leitura do parâmetro, como `status`.
  - Testes:
    - unitários: `tests/TickestPristine.Application.UnitTests/Users/GetUsersQueryHandlerTests.cs` (estender);
    - integração: `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` (casos `GetAll_*`), contra o PostgreSQL, para garantir a tradução para SQL.
- **Contratos:**

  | Rota | Autorização (sem mudança) | Entrada nova (query string, todas opcionais) | Resposta | Status |
  |---|---|---|---|---|
  | `GET users` | `users:read` | `roleName`, `withoutRole`, `sortBy` (`Name`, `CreatedAt`), `sortDirection` (`Ascending`, `Descending`) | `PagedResponse<UserSummaryResponse>` sem mudança | 200 · 400 (enum inválido) · 401 · 403 |

  Exemplo de requisição:

  ```http
  GET /users?status=Active&roleName=Atendente&search=ana&sortBy=CreatedAt&sortDirection=Descending&page=1&pageSize=20
  ```

  Exemplo de resposta:

  ```json
  {
    "items": [
      {
        "id": "6d3c0f7e-2b1a-4c55-9f0e-1a2b3c4d5e6f",
        "email": "ana.souza@empresa.com.br",
        "firstName": "Ana",
        "lastName": "Souza",
        "isActive": true,
        "createdAtUtc": "2026-10-01T13:20:00Z",
        "deactivatedAtUtc": null,
        "sectorId": "0b7e…",
        "sectorName": "Helpdesk",
        "departmentName": "TI",
        "sectorIsActive": true,
        "pendingActivation": false,
        "invitationExpiresAtUtc": null,
        "roles": [ { "name": "Atendente" } ]
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
  ```

  O envelope é o `PagedResponse<T>` atual (`Abstractions/Pagination/PagedResponse.cs`), sem mudança.
- **Mudança no banco:** não.
- **Front-end (`tickest-pristine-front`, tela de usuários):**
  - seletor "Função" com as opções vindas de `GET roles` mais "Sem função"; "Sem função" envia `withoutRole=true` e não envia `roleName`; "Todas" não envia nenhum dos dois;
  - seletor "Ordenar por" com "Nome (A–Z)", "Nome (Z–A)", "Cadastro mais recente" e "Cadastro mais antigo", traduzidos para `sortBy` + `sortDirection`;
  - ao trocar filtro ou ordenação, voltar para a página 1 e manter os filtros de status, busca e período na mesma requisição;
  - guardar filtro e ordenação na URL (query string) para o voltar do navegador e o compartilhamento manterem a visão.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3.1 (parâmetros de `GET users`) e 2.7 C-05 (nota da listagem).
- **Riscos:**
  - R-1: `NOT EXISTS` sobre `user_roles` para cada usuário; com o índice `(user_id, role_name)`, é uma busca por índice por linha, barata no volume esperado.
  - R-2: sem índice em `created_at_utc` (DEC-5); aceitável no volume atual.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Filtro por role e "sem role" na query, no handler e no endpoint | AC-1, AC-2, AC-6, AC-7 | `add-feature` | [x] |
| T-2 | Ordenação por nome ou cadastro, com direção e desempate, no handler e no endpoint | AC-3, AC-4, AC-5, AC-6 | `add-feature` | [ ] |
| T-3 | Testes unitários e de integração | AC-1 a AC-7 | `add-tests` | [ ] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` (4.3.1 e C-05) | AC-8 | — | [ ] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-8 | `ca-review` | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |
| AC-8 | | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:**
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do item 2 do pedido de Rafael Santos no chat. |
| 2026-10-27 | Em descoberta | Fatos levantados no código; DEC-1 a DEC-5 com recomendação, aguardando confirmação. |
| 2026-10-27 | Em descoberta | Rafael Santos delegou as decisões ao agente ("responda vc como senior .net"); DECs fechadas com as recomendações, sem `[SUPOSIÇÃO]` pendente. Aguardando aprovação explícita. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat ("aprovo 017 a 021"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. |
| 2026-10-28 | Em implementação | `/spec-implement 018` na branch `feat/SPEC-018-users-role-filter-sorting`, a partir da `origin/main`. Rafael Santos pediu a implementação completa, com o agente respondendo por ele ("implemente tudo e so pare quando finalizar. atue como senior"). |
