---
id: 024
titulo: "Meus Chamados" com o prazo e a situação do ciclo de SLA vigente
status: Em descoberta        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch: feat/SPEC-024-own-tickets-current-sla-cycle                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 024 — "Meus Chamados" com o prazo e a situação do ciclo de SLA vigente

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end (`tickest-pristine-front`), em 2026-10-28, depois do critique de "Meus Chamados" (26/40), escolhendo "Spec de back + texto já" para o P1 "Prazo e SLA ficam congelados depois de reabrir".
- **O problema, como aparece na tela:** um chamado resolvido e depois reaberto volta para a lista como "Aberto" com a etiqueta "Cumprido" (ou "Resolvido com atraso") e a data do prazo antigo, enquanto o diálogo de reabrir e a seção SLA do detalhe mostram um novo ciclo de prazo correndo. O front já parou de prometer o novo prazo na própria lista (commit `7a4ec67` do front); esta spec faz a lista mostrar o ciclo vigente.
- **Referências:**
  - Spec 015 (listagem de "Meus chamados", `Entregue`): criou `dueAtUtc` e `slaStatus` no item, com a mesma regra do `GET tickets`.
  - Spec 005 (plano e cronômetros de SLA) e spec 006 (monitoramento): reabrir abre um novo ciclo de resolução; os cronômetros ficam em `ticket_sla_timers`, um por objetivo e por ciclo.
  - Spec 019 (busca também na descrição em "Meus chamados", `Aprovada`): mexe no mesmo `GetOwnTicketsQueryHandler` (DEC-4).
  - `SYSTEM_SPECIFICATION.md` 4.3 (`GET tickets/mine`) e a regra do `TicketSlaStatus` ("Met e Missed ficam congelados na primeira resolução, mesmo que o chamado seja reaberto").
- **Fatos do repositório:**
  - `GET tickets/mine` calcula `SlaStatus` com `TicketSla.GetStatus(status, DueAtUtc, SlaWarningAtUtc, ResolvedAtUtc, now)` sobre as colunas do próprio chamado (`Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`). `ReopenTicketCommandHandler` não mexe em `DueAtUtc` nem em `ResolvedAtUtc`, então depois de reabrir a situação continua `Met`/`Missed` e o prazo continua o da primeira resolução.
  - `TicketSlaTimer` (`Domain/Tickets/TicketSlaTimer.cs`) tem `Metric` (`Resolution` = 2), `Cycle`, `DueOnUtc`, `WarningOnUtc`, `CompletedOnUtc`, `StoppedOnUtc` e `BreachedOnUtc`. O ciclo vigente da resolução é o de maior `Cycle`.
  - O estado de um cronômetro é calculado na leitura por `TicketSlaClock.GetState(timer, nowUtc)` (`Application/Tickets/TicketSlaClock.cs:122`), em `SlaObjectiveState` (`Running`, `AtRisk`, `Breached`, `Met`, `Missed`, `Stopped`, `NotApplicable`). É a mesma regra que o `GET tickets/{id}/sla` usa para marcar o ciclo atual (`IsCurrentCycle`).
  - Chamados do plano legado (`TicketSlaPlan` legado) podem não ter cronômetro de resolução.
  - O Monitor (`GET tickets`) filtra e resume por SLA usando as colunas do chamado (`WhereSlaStatus`, spec 006); ele fica fora desta spec (DEC-3).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada item de `GET tickets/mine` passa a trazer `currentDueAtUtc` e `currentSlaStatus`: o prazo e a situação do ciclo de resolução vigente (o cronômetro de `Resolution` de maior ciclo do chamado). Os campos atuais `dueAtUtc` e `slaStatus` continuam iguais, com o mesmo significado. | pedido; DEC-1; DEC-2 |
| REQ-2 | `currentSlaStatus` usa o enum `TicketSlaStatus` (o mesmo de `slaStatus`), traduzido do estado do cronômetro calculado por `TicketSlaClock.GetState`: `Running` → `OnTime`, `AtRisk` → `AtRisk`, `Breached` → `Breached`, `Met` → `Met`, `Missed` → `Missed`, `Stopped` → `NotApplicable`, `NotApplicable` → `NotApplicable`. | DEC-6 |
| REQ-3 | Num chamado reaberto, os dois campos refletem o ciclo novo: prazo do novo ciclo e situação calculada sobre ele (`OnTime`, `AtRisk` ou `Breached` enquanto corre). | pedido; DEC-1 |
| REQ-4 | Sem cronômetro de resolução (plano legado), `currentDueAtUtc` e `currentSlaStatus` repetem `dueAtUtc` e `slaStatus`; nunca vêm nulos. | DEC-7 |
| REQ-5 | Filtros, busca, ordenação, paginação e autorização de `GET tickets/mine` não mudam; `GET tickets`, `GET tickets/{id}` e `GET tickets/{id}/sla` não mudam. | DEC-3 |

## 3. Critérios de aceitação
Dados comuns: Maria (solicitante, com `tickets:view-own`); o relógio controlado pelo `IDateTimeProvider` dos testes; um plano de SLA não legado.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado de Maria aberto, nunca resolvido | Maria chama `GET tickets/mine` | `currentDueAtUtc` = `dueAtUtc` e `currentSlaStatus` = `slaStatus` (um único ciclo) |
| AC-2 | REQ-1, REQ-3 | um chamado de Maria resolvido dentro do prazo e depois reaberto | Maria chama `GET tickets/mine` | `slaStatus` = `Met` e `dueAtUtc` = prazo antigo (como hoje); `currentSlaStatus` = `OnTime` e `currentDueAtUtc` = `DueOnUtc` do cronômetro de resolução do ciclo 2 |
| AC-3 | REQ-3 | o chamado reaberto do AC-2 | o relógio passa do aviso e depois do prazo do ciclo 2, e Maria consulta a cada passo | `currentSlaStatus` = `AtRisk` e depois `Breached`, iguais ao estado do ciclo atual em `GET tickets/{id}/sla` |
| AC-4 | REQ-2 | um chamado de Maria cancelado (cronômetro parado) | Maria chama `GET tickets/mine` | `currentSlaStatus` = `NotApplicable` |
| AC-5 | REQ-2 | um chamado de Maria resolvido depois do prazo | Maria chama `GET tickets/mine` | `currentSlaStatus` = `Missed` |
| AC-6 | REQ-4 | um chamado de Maria do plano legado, sem cronômetro de resolução | Maria chama `GET tickets/mine` | `currentDueAtUtc` = `dueAtUtc` e `currentSlaStatus` = `slaStatus` |
| AC-7 | REQ-5 | os testes existentes de `GET tickets/mine` (spec 015) e de `GET tickets`/`GET tickets/{id}/sla` | rodam depois da mudança | todos passam sem alteração |
| AC-8 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Num chamado reaberto, o que a lista mostra em Prazo? | (a) o ciclo vigente; (b) o ciclo vigente com uma marca "reaberto" | (a): o mesmo que a seção SLA do detalhe mostra como ciclo atual | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Como entregar | (a) campos novos `currentDueAtUtc` e `currentSlaStatus`, aditivos; (b) mudar o significado de `dueAtUtc`/`slaStatus` | (a): nada muda de significado; o front troca a coluna | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | O Monitor (`GET tickets`) entra junto? | (a) só "Meus chamados"; (b) os dois | (a): o Monitor filtra e resume pelas colunas do chamado (spec 006); mudar lá vira outra spec | Rafael Santos, 2026-10-28, no chat |
| DEC-4 | Base da branch | (a) depois do merge da 019; (b) independente, da `main` | (a): a 019 mexe no mesmo `GetOwnTicketsQueryHandler`; a 024 pode ser aprovada antes | Rafael Santos, 2026-10-28, no chat |
| DEC-5 | Como calcular o ciclo vigente | (a) subconsulta nos cronômetros, no mesmo `SELECT`, com o estado pela regra do `GET tickets/{id}/sla`; (b) colunas novas no chamado, gravadas a cada novo ciclo | (a): sem coluna nova nem migration; no máximo 100 linhas por página | Rafael Santos, 2026-10-28, no chat |
| DEC-6 | Enum de `currentSlaStatus` | (a) o mesmo `TicketSlaStatus`, com a tradução do REQ-2; (b) `SlaObjectiveState` | (a): o front reaproveita rótulos e cores | Rafael Santos, 2026-10-28, no chat |
| DEC-7 | Chamado sem cronômetro (plano legado) | (a) repete `dueAtUtc`/`slaStatus`; (b) vem nulo | (a): a coluna nunca fica vazia | Rafael Santos, 2026-10-28, no chat |

## 5. Fora do escopo
- O ciclo vigente no Monitor (`GET tickets`), nos filtros e no resumo de SLA (DEC-3).
- Mudar a regra de congelamento de `Met`/`Missed` em `slaStatus` ou qualquer coluna do chamado.
- O ciclo de primeira resposta (só a resolução entra).
- A troca da coluna "Prazo" no front (repositório `tickest-pristine-front`).

## 6. Plano técnico
- **Pré-requisito:** PR da spec 019 mergeado na `main` (DEC-4).
- **Arquivos a criar ou alterar:**
  - Application:
    - `Tickets/GetOwn/OwnTicketResponse.cs`: `CurrentDueAtUtc` (`DateTime`) e `CurrentSlaStatus` (`TicketSlaStatus`).
    - `Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`: no `.Select`, uma subconsulta em `TicketSlaTimers` pelo chamado e por `Metric = Resolution`, com o maior `Cycle`, trazendo as datas do cronômetro (DEC-5). O estado é calculado depois da consulta, sobre as no máximo 100 linhas da página, com `TicketSlaClock.GetState` e a tradução do REQ-2; sem cronômetro, os dois campos repetem `DueAtUtc`/`SlaStatus` (DEC-7).
    - Uma tradução única de `SlaObjectiveState` para `TicketSlaStatus` (no próprio slice ou em `TicketSlaClock`, conforme o padrão já usado), para não haver duas regras.
  - Testes:
    - unitários: `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` (estender) e o teste da tradução de estados;
    - integração: `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` (casos `GetOwn_*` de reabertura, cancelamento e legado).
- **Contratos:** `GET tickets/mine` (itens) ganha `currentDueAtUtc` e `currentSlaStatus`. Mudança só aditiva: nenhum campo sai nem muda de significado. Nenhuma rota, erro ou evento novo.
- **Mudança no banco:** não.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 (campos de `GET tickets/mine`) e a nota sobre o congelamento de `Met`/`Missed`, dizendo que a lista do solicitante também mostra o ciclo vigente.
- **Análise:**
  - **Concorrência:** só leitura; chamado e cronômetro saem do mesmo comando SQL, então o item é consistente mesmo com uma reabertura acontecendo ao mesmo tempo.
  - **Quebra de contrato:** nenhuma; dois campos novos.
  - **Desempenho:** uma subconsulta por linha em `ticket_sla_timers`, pelo índice único `(ticket_id, metric, cycle)` (`TicketSlaTimerConfiguration.cs:23`), sobre no máximo 100 linhas por página. O estado é calculado em memória sobre a página, sem afetar o `Count`.
  - **Segurança:** os campos novos seguem a autorização da rota; o usuário continua vindo do token.
- **Árvore de arquivos:**

  ```text
  src/TickestPristine.Application/Tickets/GetOwn/
    OwnTicketResponse.cs                 [MODIFIED]  + CurrentDueAtUtc, CurrentSlaStatus
    GetOwnTicketsQueryHandler.cs         [MODIFIED]  subconsulta do cronômetro e estado do ciclo vigente
  tests/TickestPristine.Application.UnitTests/Tickets/
    GetOwnTicketsQueryHandlerTests.cs    [MODIFIED]
  tests/TickestPristine.IntegrationTests/Tickets/
    TicketsTests.cs                      [MODIFIED]  GetOwn_* do ciclo vigente
  ```

- **Testes previstos** (nomes no padrão dos existentes; podem ser ajustados na implementação, e a rastreabilidade registra os reais):

  | AC | Unitários | Integração (`TicketsTests`) |
  |---|---|---|
  | AC-1 | `GetOwnTicketsQueryHandlerTests` › `Handle_Should_ReturnSameCurrentCycle_WhenNeverReopened` | `GetOwn_Should_ReturnCurrentCycleEqualToTicket_WhenNeverReopened` |
  | AC-2, AC-3 | `Handle_Should_ReturnCurrentResolutionCycle_WhenReopened` | `GetOwn_Should_ReturnNewCycleDueAndStatus_AfterReopen` |
  | AC-4, AC-5 | teste da tradução de `SlaObjectiveState` para `TicketSlaStatus` | `GetOwn_Should_ReturnNotApplicable_WhenCanceled` |
  | AC-6 | `Handle_Should_FallBackToTicketSla_WhenNoResolutionTimer` | — |
  | AC-7 | suíte existente | suíte existente |
  | AC-8 | build e suíte inteira | build e suíte inteira |
- **Riscos:**
  - R-1: a lista do solicitante e o Monitor passam a mostrar prazos diferentes para o mesmo chamado reaberto (o Monitor continua nas colunas do chamado, DEC-3). Fica registrado para a spec do Monitor.
  - R-2: o front precisa trocar a coluna para os campos novos; até lá, nada muda na tela (mudança aditiva).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Campos `CurrentDueAtUtc`/`CurrentSlaStatus` na resposta, subconsulta do cronômetro de resolução vigente, tradução do estado e o caso legado | AC-1 a AC-6 | `add-feature` | [ ] |
| T-2 | Testes unitários e de integração da seção 6 | AC-1 a AC-7 | `add-tests` | [ ] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` (4.3 e a nota do congelamento de `Met`/`Missed`) | AC-8 | — | [ ] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-8 | `ca-review` | [ ] |

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
- **Entrega:** <PR #n (o mesmo do campo `pr:`); a spec fica `Entregue` quando o responsável mergear o PR na `main`>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end (P1 do critique de "Meus Chamados"). As specs 017 a 022 já existiam, então esta ficou com a 023. |
| 2026-10-28 | Em descoberta | Renumerada para 024: outra sessão tinha criado a `023-editar-departamento-sem-mexer-no-responsavel` dez minutos antes (21:59 contra 22:09). Nenhuma regra muda. |
| 2026-10-28 | Em descoberta | Duas rodadas de perguntas respondidas no chat (DEC-1 a DEC-7). Aguardando aprovação. |
