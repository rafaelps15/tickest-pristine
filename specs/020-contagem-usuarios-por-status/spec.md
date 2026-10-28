---
id: 020
titulo: Contagem de usuários por status (ativos, inativos e com convite pendente) numa só chamada
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-020-users-status-counts                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 020 — Contagem de usuários por status numa só chamada

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27 (item 4 de 5, marcado como opcional): "Contagem de usuários por status. Um endpoint que devolva, numa chamada só, quantos usuários estão ativos, inativos e com convite pendente, com a mesma permissão de leitura de usuários. Ele substituiria as duas consultas de um item que o card Resumo faz hoje." A primeira versão sugeria `GET /users/summary` ou `GET /users/count-by-status`, com `Active`, `Inactive` e `PendingInvitation`.
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.6 RN-30 (conta pendente de ativação), 4.3.1 (rotas de usuários).
- **Fatos do repositório:**
  - "Ativo" e "inativo" são `User.IsActive` (`true`/`false`). "Convite pendente" não tem coluna própria: é usuário ativo **sem** `UserCredential` (RN-30; `Users/GetAll/GetUsersQueryHandler.cs`, campo `PendingActivation = u.IsActive && !context.UserCredentials.Any(c => c.UserId == u.Id)`).
  - Hoje o filtro `GET users?status=Active` traz também os pendentes (eles são `IsActive = true`). O card Resumo do front faz `GET users?status=Active&pageSize=1` e `?status=Inactive&pageSize=1` e lê o `totalCount` de cada um.
  - Desativar uma conta pendente revoga o convite (RN-30): o usuário fica inativo e continua sem credencial.
  - Precedente de contagem: `GET tickets/stats/status` (`Endpoints/Tickets/GetStatusCounts.cs`, `Tickets/GetStatusCounts/TicketStatusCountsResponse.cs`), com `TotalCount` e um `…Count` por status.
  - A rota `users/{userId:guid}` tem restrição de tipo, então `users/stats/status` não conflita com ela.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET users/stats/status` devolve `totalCount`, `activeCount`, `inactiveCount` e `pendingActivationCount` numa única resposta. | pedido; DEC-1 |
| REQ-2 | As três categorias são disjuntas e somam o total: `pendingActivationCount` = ativos sem credencial; `activeCount` = ativos com credencial; `inactiveCount` = todos os inativos (com ou sem credencial). | DEC-2 |
| REQ-3 | A rota exige `users:read`, a mesma de `GET users`. | pedido |
| REQ-4 | A contagem roda no banco num único comando SQL, sem carregar usuários na memória. | pedido (otimização); DEC-3 |

## 3. Critérios de aceitação
Dados: o administrador do seed (ativo, com senha), Ana (ativa, com senha), Bruno (cadastrado, pendente), Carla (desativada depois de ativar a conta) e Davi (cadastrado e desativado antes de ativar).

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-2 | os dados | `GET users/stats/status` | 200 com `totalCount` = 5, `activeCount` = 2, `pendingActivationCount` = 1, `inactiveCount` = 2 |
| AC-2 | REQ-2 | Bruno pendente | Bruno ativa a conta (`POST users/activation`) e a rota é chamada de novo | `pendingActivationCount` cai 1 e `activeCount` sobe 1; `totalCount` igual |
| AC-3 | REQ-2 | Ana ativa | Ana é desativada e a rota é chamada de novo | `activeCount` cai 1 e `inactiveCount` sobe 1 |
| AC-4 | REQ-3 | — | usuário sem `users:read`; sem token | 403; 401 |
| AC-5 | REQ-4 | os dados | teste de integração com o log de comandos do EF (ou contagem de comandos) | a rota executa um único comando SQL de leitura |
| AC-6 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Rota e nomes | (a) `GET users/stats/status` com `…Count`, como `GET tickets/stats/status`; (b) `GET users/summary`; (c) `GET users/count-by-status` | (a): mesmo padrão já usado para chamados, e "summary" já nomeia outra coisa (`tickets/mine/summary`, contagem por prioridade e status) | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-2 | O pendente entra em "ativos"? | (a) não: três categorias disjuntas que somam o total; (b) sim, como o filtro `status=Active` faz hoje | (a): o card mostra três números lado a lado, e (b) contaria o pendente duas vezes. Consequência: `activeCount` deixa de bater com o `totalCount` de `GET users?status=Active` (que inclui pendentes); ver DEC-4 | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-3 | Como contar | (a) LINQ: projetar `IsActive` e "tem credencial" por usuário e agrupar pelos dois (`GROUP BY` com no máximo 4 linhas), somando na memória; (b) três `CountAsync` (três idas ao banco); (c) SQL com Dapper, como os relatórios | (a): um comando só, sem SQL à mão; Dapper fica para relatórios (`CLAUDE.md`) | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-4 | O filtro de `GET users` deve ganhar `status=PendingActivation` e `status=Active` passar a excluir pendentes, para o clique no card abrir a lista coerente? | (a) sim, numa spec própria (muda a semântica de `Active`, que o front já usa); (b) não agora | (b): mudar o significado de `status=Active` quebraria o que o front já usa; se o card ficar clicável, abre-se uma spec própria | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |

## 5. Fora do escopo
- Contagem por role, setor ou departamento.
- Filtros (status, busca, período) aplicados à contagem: a rota conta todos os usuários.
- Mudar a semântica de `GET users?status=Active` (DEC-4).
- Cache da contagem (`CLAUDE.md`: nenhuma query usa cache sem decisão explícita).

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Application (slice novo `Users/GetStatusCounts/`):
    - `GetUserStatusCountsQuery.cs`: `IQuery<UserStatusCountsResponse>`, sem parâmetros.
    - `UserStatusCountsResponse.cs`: `TotalCount`, `ActiveCount`, `InactiveCount`, `PendingActivationCount`, com comentário em pt-BR dizendo que as três somam o total.
    - `GetUserStatusCountsQueryHandler.cs`: `context.Users.Select(u => new { u.IsActive, HasCredential = context.UserCredentials.Any(c => c.UserId == u.Id) }).GroupBy(x => new { x.IsActive, x.HasCredential }).Select(g => new { g.Key.IsActive, g.Key.HasCredential, Count = g.Count() }).ToListAsync(...)`; o resultado (até 4 linhas) vira a resposta: ativo com credencial → `ActiveCount`; ativo sem credencial → `PendingActivationCount`; inativo → `InactiveCount`; soma → `TotalCount`.
  - Web.Api:
    - `Endpoints/Users/GetStatusCounts.cs`: `GET users/stats/status`, `.WithTags(Tags.Users)`, `.HasPermission(Permissions.UsersRead)`, `result.Match(Results.Ok, CustomResults.Problem)`.
  - Testes: `tests/TickestPristine.Application.UnitTests/Users/GetUserStatusCountsQueryHandlerTests.cs` (novo) e `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` (`GetStatusCounts_*`).
- **Contratos:**

  | Rota | Autorização | Entrada | Resposta | Status |
  |---|---|---|---|---|
  | `GET users/stats/status` (nova) | `users:read` | — | `UserStatusCountsResponse` | 200 · 401 · 403 |

  Exemplo de resposta:

  ```json
  {
    "totalCount": 42,
    "activeCount": 35,
    "inactiveCount": 4,
    "pendingActivationCount": 3
  }
  ```
- **Mudança no banco:** não. O agrupamento usa o índice de `user_credentials` por `user_id` (credencial única por usuário, RN-30).
- **Front-end (`tickest-pristine-front`, card "Resumo" da tela de usuários):**
  - trocar as duas chamadas `GET users?status=…&pageSize=1` por uma `GET users/stats/status`;
  - mostrar três números: "Ativos" (`activeCount`), "Convite pendente" (`pendingActivationCount`) e "Inativos" (`inactiveCount`), e o total (`totalCount`) se o layout tiver espaço;
  - recarregar o card depois de cadastrar, desativar, reativar ou reenviar convite;
  - atenção à DEC-2: se o card for clicável e abrir `GET users?status=Active`, a lista incluirá os pendentes e o total não baterá com `activeCount` (DEC-4).
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3.1 (rota nova) e 2.7 C-05.
- **Riscos:** R-1 — se o EF não traduzir o `Any` dentro da chave do `GroupBy`, a implementação troca por um `LEFT JOIN` com `user_credentials` (mesmo resultado, um comando); a T-1 confere o SQL gerado.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Slice `Users/GetStatusCounts` (query, resposta, handler) e endpoint | AC-1 a AC-5 | `add-feature` | [x] |
| T-2 | Testes unitários e de integração | AC-1 a AC-5 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-6 | — | [x] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-6 | `ca-review` | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.Application.UnitTests/Users/GetUserStatusCountsQueryHandlerTests.cs` › `Handle_Should_CountActivePendingAndInactiveSeparately_WhenUsersHaveEverySituation`, `Handle_Should_ReturnZeros_WhenThereAreNoUsers`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetStatusCounts_Should_CountActivePendingAndInactiveSeparately` | O unitário monta exatamente os dados do AC (administrador e Ana com senha, Bruno pendente, Carla inativa com senha, Davi inativo sem senha) e confere `totalCount` 5, `activeCount` 2, `pendingActivationCount` 1 e `inactiveCount` 2. A integração, contra o PostgreSQL e pelas rotas reais (cadastro, ativação e desativação), confere 200 e a variação a partir da contagem anterior (+4, +1, +1, +2) e que as três somam o total: o banco é compartilhado pelos testes, então o total absoluto não é fixo. Uma mutação que contava os pendentes como ativos foi detectada por 1 caso unitário e 2 de integração. | Aprovado |
| AC-2 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetStatusCounts_Should_MovePendingUserToActive_WhenAccountIsActivated` | Bruno cadastrado sem senha ativa a conta por `POST users/activation` com o token do e-mail: `pendingActivationCount` cai 1, `activeCount` sobe 1, `inactiveCount` e `totalCount` iguais. | Aprovado |
| AC-3 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetStatusCounts_Should_MoveActiveUserToInactive_WhenUserIsDeactivated` | Ana ativa com senha é desativada por `PUT users/{id}/deactivate`: `activeCount` cai 1, `inactiveCount` sobe 1, `pendingActivationCount` e `totalCount` iguais. | Aprovado |
| AC-4 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetStatusCounts_Should_ReturnForbidden_WhenCallerLacksUsersReadPermission`, `GetStatusCounts_Should_ReturnUnauthorized_WhenNotAuthenticated` | Colaborador sem `users:read` recebe 403; sem token, 401. | Aprovado |
| AC-5 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetStatusCounts_Should_CountInSingleSqlCommand` | Com o `EfCommandCounter` ativo durante a requisição, um único comando lê `user_credentials`, e ele tem `GROUP BY`: o EF traduziu o `Any` dentro da chave do agrupamento (R-1 não se concretizou). | Aprovado |
| AC-6 | | | |

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
| 2026-10-27 | Rascunho | Criada a partir do item 4 (opcional) do pedido de Rafael Santos no chat. |
| 2026-10-27 | Em descoberta | Fatos levantados no código; DEC-1 a DEC-3 com recomendação e DEC-4 em aberto. |
| 2026-10-27 | Em descoberta | Rafael Santos delegou as decisões ao agente ("responda vc como senior .net"); DECs fechadas com as recomendações, sem `[SUPOSIÇÃO]` pendente. Aguardando aprovação explícita. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat ("aprovo 017 a 021"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. |
| 2026-10-28 | Em implementação | `/spec-implement 020` na branch `feat/SPEC-020-users-status-counts`, a partir da `origin/main`. Rafael Santos pediu a implementação completa, com o agente respondendo por ele ("atue como senior e só pare quando finalizar"). |
