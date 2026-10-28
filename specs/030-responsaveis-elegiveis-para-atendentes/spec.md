---
id: 030
titulo: Lista de responsáveis elegíveis para quem atende a fila, sem `users:read`
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-030-ticket-assignees-for-agents                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 030 — Lista de responsáveis elegíveis para quem atende a fila, sem `users:read`

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "sim! pode criar na pasta do back" (item 8 do relatório do Início operacional).
- **Por que:** o filtro de responsável do Início e do Monitor ficou só em "atribuídos a mim" e "sem responsável", porque a lista de atendentes exige `users:read`, que o papel Atendente não tem.
- **Referências:** spec 009 (consulta de atendentes e atribuição); `SYSTEM_SPECIFICATION.md` RN-28.
- **Fatos do repositório:**
  - `GET users/attendants` (`Web.Api/Endpoints/Users/GetAttendants.cs`) exige `users:read` no handler (`Users/GetAttendants/GetBacklogAttendantsQueryHandler.cs:36-44`) e devolve id, nome, e-mail, setor e departamento.
  - O Atendente tem `tickets:create`, `view-own`, `update-own`, `reopen-own` e `manage`, sem `users:read` (`Infrastructure/Roles/RolePermissionConfiguration.cs:38-42`).
  - A elegibilidade de responsável é uma regra única: usuário ativo com `tickets:manage` efetivo (`Tickets/TicketAssigneeEligibility.cs:14-20`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET tickets/assignees` lista os responsáveis elegíveis pela mesma regra da atribuição, com id, nome completo e setor (id e nome). Não expõe e-mail nem outros dados de usuário. | pedido; DEC-1 |
| REQ-2 | Exige `tickets:manage` e não exige `users:read`. | DEC-1 |
| REQ-3 | Aceita busca opcional por nome (sem diferenciar maiúsculas) e setor opcional; ordena por nome; paginação no padrão do sistema. | DEC-1 |
| REQ-4 | `GET users/attendants` não muda. | DEC-1 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-2 | um Atendente (sem `users:read`); dois atendentes ativos, um inativo e um colaborador sem `tickets:manage` | o Atendente chama `GET tickets/assignees` | 200 só com os dois atendentes ativos, cada um com id, nome e setor, sem e-mail |
| AC-2 | REQ-3 | a mesma base | chama com uma busca por parte do nome e com um setor | só os que batem, em ordem de nome |
| AC-3 | REQ-2 | um colaborador sem `tickets:manage` | chama a rota | 403; sem token, 401 |
| AC-4 | REQ-4 | os testes de `GET users/attendants` | rodam depois da mudança | passam sem alteração |
| AC-5 | REQ-1 | um usuário listado | é atribuído pela rota de atribuição | a atribuição aceita (a lista e a atribuição usam a mesma regra) |
| AC-6 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como liberar a lista para quem atende? | (a) rota nova com `tickets:manage`, só id, nome e setor; (b) aceitar `tickets:manage` em `users/attendants` | (a): não expõe e-mail nem dados de administração de usuários a quem só atende a fila | Rafael Santos, 2026-10-28, no chat |

## 5. Fora do escopo
- Mudar quem pode atribuir (spec 009).
- O filtro de responsável arbitrário no front (o Início e o Monitor passam a usar esta rota numa mudança do front).

## 6. Plano técnico
- **Pré-requisito:** nenhum; branch a partir da `origin/main`.
- **Arquivos a criar ou alterar:**
  - Application: `Tickets/GetAssignees/*` (novo), reaproveitando `TicketAssigneeEligibility` para o filtro.
  - Web.Api: `Endpoints/Tickets/GetAssignees.cs` (novo), com `tickets:manage`.
  - Testes: integração dos ACs.
- **Contratos:** rota nova `GET tickets/assignees`, com busca, setor e paginação; itens com id, nome completo, id e nome do setor. 200 · 401 · 403.
- **Mudança no banco:** não.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 e RN-28.
- **Análise:**
  - **Segurança:** a rota devolve menos que `users/attendants` e só para quem já vê todos os chamados (`tickets:manage`).
- **Riscos:** nenhum relevante.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Consulta e rota dos responsáveis elegíveis | AC-1 a AC-3, AC-5 | `add-feature` | [x] |
| T-2 | Testes de integração | AC-1 a AC-5 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-6 | — | [ ] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-6 | `ca-review` | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAssignees_Should_ReturnOnlyEligibleAssigneesWithoutEmail_WhenCallerIsAgentWithoutUsersRead`; `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketAssigneesQueryHandlerTests.cs` › `Handle_Should_ReturnOnlyActiveUsersWithManagePermission_WhenSomeUsersAreNotEligible` | Um Atendente recém-criado (sem `users:read`) recebe 200 só com Ana e Beto (atendentes ativos), por nome; o atendente inativo e o colaborador ficam fora. Cada item tem id, nome completo e setor (id e nome), e o corpo não contém "email". O unitário confere a regra (ativo com `tickets:manage`) e o setor nulo de quem não tem setor. Uma mutação que tirava o filtro de elegibilidade foi detectada por 1 caso unitário e 2 de integração. | Aprovado |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAssignees_Should_ReturnMatchingAssignees_WhenSearchAndSectorAreGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketAssigneesQueryHandlerTests.cs` › `Handle_Should_ReturnMatchingAssigneesOrderedByName_WhenSearchAndSectorAreGiven`, `Handle_Should_RequireEveryWordInNameOrSurname_WhenSearchHasSeveralWords` | Contra o PostgreSQL, a busca `an` + sobrenome em maiúsculas com o setor Helpdesk traz só Ana (sem diferenciar maiúsculas), e o setor Infra traz só Beto. O unitário confere a ordem por nome (Ana antes de Mariana) e que cada palavra precisa estar no nome ou no sobrenome. | Aprovado |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAssignees_Should_ReturnForbidden_WhenCallerLacksManagePermission`, `GetAssignees_Should_ReturnUnauthorized_WhenNoTokenProvided` | Colaborador sem `tickets:manage` recebe 403; sem token, 401. | Aprovado |
| AC-4 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › os 12 testes `GetAttendants_*`; `tests/TickestPristine.Application.UnitTests/Users/GetBacklogAttendantsQueryHandlerTests.cs` | Passam sem alteração: `GET users/attendants` e o handler dele não foram tocados. | Aprovado |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAssignees_Should_ListAssigneeThatAssignAccepts` | O Atendente consulta a lista e escolhe Ana; a atribuição por `PUT tickets/{id}/assignee` responde 204 e grava Ana como responsável. Quem atribui é o administrador, porque atribuir a outra pessoa exige `tickets:assign` (spec 009, fora do escopo); o critério é que a lista e a atribuição usam a mesma regra de elegibilidade (`TicketAssigneeEligibility`). | Aprovado |
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
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end; número reservado já na criação, porque outra sessão cria specs em paralelo. |
| 2026-10-28 | Em descoberta | Pergunta respondida no chat (DEC-1). Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Pode começar já. |
| 2026-10-28 | Em implementação | `/spec-implement 030` na branch `feat/SPEC-030-ticket-assignees-for-agents`, a partir da `origin/main`. Rafael Santos pediu a implementação completa seguindo as skills como modelo de código, com o agente respondendo por ele ("atue como senior e só pare quando finalizar [...] Mantenha exatamente como no modelo"). Tabelas de rastreabilidade e de evidências criadas. |
