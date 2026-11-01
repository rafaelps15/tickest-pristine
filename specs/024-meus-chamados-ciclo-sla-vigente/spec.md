---
id: 024
titulo: "Meus Chamados" com o prazo e a situação do ciclo de SLA vigente
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
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
- **Atualização de 2026-11-01, antes da implementação:** as specs 041 (PR #57) e 045 (PR #59) mudaram o código citado nos fatos acima.
  - `TicketSla` e `TicketSlaClock` não existem mais. A listagem calcula `SlaStatus` com a regra estática do chamado `Ticket.GetSlaStatus(status, dueAtUtc, slaWarningAtUtc, resolvedAtUtc, nowUtc)` (`Ticket.cs:403`, usada em `GetOwnTicketsQueryHandler.cs:69`).
  - O estado de um cronômetro é o método `TicketSlaTimer.GetState(utcNow)` (`TicketSlaTimer.cs:167`), que o `GET tickets/{id}/sla` chama depois de carregar os cronômetros (`GetTicketSlaQueryHandler.cs:120`). `SlaObjectiveState` e `TicketSlaStatus` ficam no Domain.
  - A reabertura cria o novo ciclo só nos cronômetros, com `Cycle + 1` (`TicketSlaCycleService.cs:143`); o ciclo corrente é o cronômetro de resolução de maior número, aberto ou não (`TicketSlaCycleService.cs:195`). O prazo do chamado só é gravado na abertura (`Ticket.cs:82-83`) e a data de resolução fica com a primeira (`Ticket.cs:251`): o problema desta spec continua existindo.
  - O índice único `(ticket_id, metric, cycle)` continua cobrindo a subconsulta (`TicketSlaTimerConfiguration.cs:28`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada item de `GET tickets/mine` passa a trazer `currentDueAtUtc` e `currentSlaStatus`: o prazo e a situação do ciclo de resolução vigente (o cronômetro de `Resolution` de maior ciclo do chamado). Os campos atuais `dueAtUtc` e `slaStatus` continuam iguais, com o mesmo significado. | pedido; DEC-1; DEC-2 |
| REQ-2 | `currentSlaStatus` usa o enum `TicketSlaStatus` (o mesmo de `slaStatus`), traduzido do estado do cronômetro pela regra do `TicketSlaTimer.GetState` (até a spec 041, `TicketSlaClock.GetState`): `Running` → `OnTime`, `AtRisk` → `AtRisk`, `Breached` → `Breached`, `Met` → `Met`, `Missed` → `Missed`, `Stopped` → `NotApplicable`, `NotApplicable` → `NotApplicable`. | DEC-6; DEC-8 |
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
| DEC-8 | Como a listagem calcula a situação do ciclo vigente, depois que a spec 041 tirou o `TicketSlaClock` e passou a regra para o método `TicketSlaTimer.GetState`, que o EF Core não traduz | (a) regra estática sobre as colunas no `TicketSlaTimer`, chamada na projeção; (b) segunda consulta com os cronômetros vigentes da página e `GetState` em memória; (c) a mesma regra do `TicketSlaTimer.GetState`, já com a tradução do REQ-2, escrita como expressão traduzível na própria consulta | (c): só esta consulta usa a regra, então ela fica escrita na consulta, que continua uma só (DEC-5) e calcula a situação no SQL. O `TicketSlaTimer.GetState` continua sendo a regra dos cronômetros carregados, e os dois expressam a mesma regra: os testes da entidade cobrem o método, e o teste de integração cobre a consulta traduzida. Se outra consulta passar a precisar da regra (o Monitor, DEC-3), ela vira um `Expression` no `TicketSlaTimer`. Sem mudança no Domain. | Rafael Santos, 2026-11-01, delegando a escolha no chat ("veja qual deles se encaixa melhor para esse caso e faça identico ok") |
| DEC-9 | O que fazer com a branch `feat/SPEC-024-own-tickets-current-sla-cycle`, que só tem o commit da descoberta, de uma `main` antiga | (a) reaproveitar a branch, trazendo a `main` e gravando a spec revisada; (b) recriar a branch a partir da `origin/main`, excluindo a antiga | (a): sem excluir branch nem forçar push; a `main` entra sem conflito | Rafael Santos, 2026-11-01 (idem) |

## 5. Fora do escopo
- O ciclo vigente no Monitor (`GET tickets`), nos filtros e no resumo de SLA (DEC-3).
- Mudar a regra de congelamento de `Met`/`Missed` em `slaStatus` ou qualquer coluna do chamado.
- O ciclo de primeira resposta (só a resolução entra).
- A troca da coluna "Prazo" no front (repositório `tickest-pristine-front`).

## 6. Plano técnico
- **Pré-requisito:** PR da spec 019 mergeado na `main` (DEC-4). Cumprido, assim como as specs 041 e 045 (PRs #57 e #59), que mudaram o SLA (DEC-8).
- **Arquivos a criar ou alterar:**
  - Application:
    - `Tickets/GetOwn/OwnTicketResponse.cs`: `CurrentDueAtUtc` (`DateTime`) e `CurrentSlaStatus` (`TicketSlaStatus`).
    - `Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`: no `.Select`, uma subconsulta em `TicketSlaTimers` pelo chamado e por `Metric = Resolution`, com o maior `Cycle` (DEC-5), trazendo o prazo e a situação já como `TicketSlaStatus`: a regra do `TicketSlaTimer.GetState` com a tradução do REQ-2, escrita como expressão traduzível na própria consulta, que o SQL calcula (DEC-8). Sem cronômetro, os dois campos repetem `DueAtUtc`/`SlaStatus` (DEC-7).
    - A tradução de `SlaObjectiveState` para `TicketSlaStatus` fica nessa mesma expressão, a única que a usa (DEC-8); não há segunda regra fora dela e do `TicketSlaTimer.GetState`.
  - Testes:
    - unitários: `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` (estender, incluídos os casos da tradução: cronômetro parado e resolvido depois do prazo); a regra do `TicketSlaTimer.GetState` já tem os testes da entidade;
    - integração: `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` (casos `GetOwn_*` de reabertura, cancelamento e legado).
- **Contratos:** `GET tickets/mine` (itens) ganha `currentDueAtUtc` e `currentSlaStatus`. Mudança só aditiva: nenhum campo sai nem muda de significado. Nenhuma rota, erro ou evento novo.
- **Mudança no banco:** não.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 (campos de `GET tickets/mine`) e a nota sobre o congelamento de `Met`/`Missed`, dizendo que a lista do solicitante também mostra o ciclo vigente.
- **Análise:**
  - **Concorrência:** só leitura; chamado e cronômetro saem do mesmo comando SQL, então o item é consistente mesmo com uma reabertura acontecendo ao mesmo tempo.
  - **Quebra de contrato:** nenhuma; dois campos novos.
  - **Desempenho:** duas subconsultas escalares por linha em `ticket_sla_timers` (o prazo e a situação do ciclo vigente), pelo índice único `(ticket_id, metric, cycle)` (`TicketSlaTimerConfiguration.cs:28`; era a linha 23 quando o plano foi escrito), sobre no máximo 100 linhas por página. A situação sai na própria consulta (DEC-8), sem afetar o `Count`.
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
  | AC-4, AC-5 | `Handle_Should_ReturnNotApplicable_WhenResolutionTimerIsStopped` e `Handle_Should_ReturnMissed_WhenResolvedAfterDue` | `GetOwn_Should_ReturnNotApplicable_WhenCanceled` |
  | AC-6 | `Handle_Should_FallBackToTicketSla_WhenNoResolutionTimer` | — |
  | AC-7 | suíte existente | suíte existente |
  | AC-8 | build e suíte inteira | build e suíte inteira |
- **Riscos:**
  - R-1: a lista do solicitante e o Monitor passam a mostrar prazos diferentes para o mesmo chamado reaberto (o Monitor continua nas colunas do chamado, DEC-3). Fica registrado para a spec do Monitor.
  - R-2: o front precisa trocar a coluna para os campos novos; até lá, nada muda na tela (mudança aditiva).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Campos `CurrentDueAtUtc`/`CurrentSlaStatus` na resposta, subconsulta do cronômetro de resolução vigente, tradução do estado e o caso legado | AC-1 a AC-6 | `add-feature` | [x] |
| T-2 | Testes unitários e de integração da seção 6 | AC-1 a AC-7 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` (4.3 e a nota do congelamento de `Met`/`Missed`) | AC-8 | — | [x] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-8 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnSameCurrentCycle_WhenNeverReopened`; `IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_ReturnCurrentCycleEqualToTicket_WhenNeverReopened` | Chamado aberto e nunca resolvido, com os cronômetros do ciclo 1: `currentDueAtUtc` igual a `dueAtUtc` e `currentSlaStatus` igual a `slaStatus` (`OnTime`), no handler e pela API | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-2 | `GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnCurrentResolutionCycle_WhenReopened`; `TicketsTests.cs` › `GetOwn_Should_ReturnNewCycleDueAndStatus_AfterReopen` | Resolvido dentro do prazo e reaberto: `slaStatus` `Met` e `dueAtUtc` do ciclo 1; `currentDueAtUtc` igual ao `DueOnUtc` do cronômetro de resolução do ciclo 2 (pela API, o do ciclo vigente em `GET tickets/{id}/sla`) e `currentSlaStatus` `OnTime` | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-3 | `GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_FollowStateOfCurrentCycle_WhenTimePassesAfterReopen` (dois casos); `TicketsTests.cs` › `GetOwn_Should_FollowStateOfCurrentCycleInSla_WhenWarningOrDuePasses` (dois casos) | No alerta e depois no prazo do ciclo 2, `currentSlaStatus` `AtRisk` e depois `Breached`, iguais ao estado do ciclo vigente dado pelo `TicketSlaTimer.GetState` (handler) e mostrado pelo `GET tickets/{id}/sla` (API, com os cronômetros levados para o passado), e o `slaStatus` continua `Met` | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-4 | `GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnNotApplicable_WhenResolutionTimerIsStopped` e `Handle_Should_ReturnNotApplicable_WhenStoppedCycleIsPastItsDue`; `TicketsTests.cs` › `GetOwn_Should_ReturnNotApplicable_WhenCanceled` | Cronômetro de resolução vigente parado pelo cancelamento: `currentSlaStatus` `NotApplicable`; no handler, o ciclo parado é o 2, com o `slaStatus` ainda `Met`, e continua `NotApplicable` consultado depois do prazo dele (parado vale antes de violado, como o `TicketSlaTimer.GetState` dá `Stopped` no mesmo instante); pela API, o `GET tickets/{id}/sla` mostra o ciclo vigente `Stopped` | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-5 | `GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnMissed_WhenResolvedAfterDue` e, no limite do prazo, `Handle_Should_ReturnMet_WhenCurrentCycleIsResolvedExactlyAtItsDue`; `TicketsTests.cs` › `GetOwn_Should_ReturnMissed_WhenCurrentCycleWasResolvedAfterItsDue` | O ciclo 2 resolvido depois do prazo dele: `currentSlaStatus` `Missed`, com o `slaStatus` ainda `Met` (a primeira resolução foi no prazo). No limite, o ciclo resolvido exatamente no prazo dá `Met`, mesmo consultado depois dele e com o `slaStatus` `Missed`, como o `TicketSlaTimer.GetState` (a regra `<=` do REQ-2). Pela API, com o ciclo 1 concluído depois do próprio prazo (cronômetros levados para o passado), o SQL dá `Missed`, igual ao estado do ciclo vigente em `GET tickets/{id}/sla`, e o `slaStatus` continua `Met` | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-6 | `GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_FallBackToTicketSla_WhenNoResolutionTimer`; `TicketsTests.cs` › `GetOwn_Should_RepeatTicketDueAndStatus_WhenThereIsNoResolutionTimer` | Chamado do plano legado, sem cronômetro (gravado direto no banco, na integração): `currentDueAtUtc` e `currentSlaStatus` repetem `dueAtUtc` e `slaStatus` (`AtRisk`), no handler e pela API, onde a subconsulta não acha cronômetro | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-7 | Os testes existentes de `GET tickets/mine` (`GetOwnTicketsQueryHandlerTests.cs` e `TicketsTests.cs` › `GetOwn_*`), de `GET tickets` (`GetTicketsQueryHandlerTests.cs` e `TicketsTests.cs` › `GetAll_*`) e de `GET tickets/{id}/sla` (`GetTicketSlaQueryHandlerTests.cs` e `TicketsTests.cs` › `GetSla_*`) | Nenhum desses testes mudou; só o `OwnTicketItemDto` dos testes de integração ganhou os dois campos novos, para os testes da spec lê-los | Aprovado em 2026-11-01 (execução da segunda verificação, das 19:57 às 20:04) |
| AC-8 | `dotnet build TickestPristine.slnx --no-incremental` e `dotnet test TickestPristine.slnx --no-build --logger trx` (relatório por teste) | Build com 0 avisos e 0 erros e a suíte inteira aprovada, com os testes da spec e os existentes | Aprovado em 2026-11-01 (build às 19:56 com 0 avisos e 0 erros; suíte inteira das 19:57 às 20:04 com 1.495 aprovados e 0 falhas) |

## 9. Evidências de verificação
- **Build:** 2026-11-01, 19:56, `dotnet build TickestPristine.slnx --no-incremental`: 0 avisos, 0 erros.
- **Testes:** 2026-11-01, das 19:57 às 20:04, `dotnet test TickestPristine.slnx --no-build --logger trx`: 1.495 aprovados, 0 com falha (60 de arquitetura, 905 unitários, 530 de integração); os 16 casos de teste da rastreabilidade conferidos um a um no relatório por teste (todos aprovados). Auditoria `python scripts/auditoria_sdd.py .` às 19:57: score 100%, CONFORME, código de saída 0. A primeira verificação (19:37 às 19:43: 1.492 aprovados, 0 falhas) foi refeita depois de fechar as lacunas de teste. O SQL gerado para `GET tickets/mine`, conferido no log da integração, calcula as duas situações numa única consulta, com as subconsultas do cronômetro de maior ciclo.
- **Revisão:**
  - **Revisões feitas:** a `ca-review` da T-4 e duas revisões independentes, que só leram o código, sem build nem testes: uma na primeira verificação e outra nesta, depois de fechar as lacunas. Nenhuma encontrou bloqueante ou violação de convenção.
  - **Conferido nas revisões independentes, linha a linha:**
    - a regra escrita na consulta é a do `TicketSlaTimer.GetState`, com a mesma ordem e os mesmos limites e com a tradução do REQ-2;
    - o ciclo vigente é o mesmo do `TicketSlaCycleService` e do `GET tickets/{id}/sla`;
    - o caso sem cronômetro repete exatamente a chamada do `SlaStatus` (DEC-7);
    - na segunda revisão, regra por regra, que algum teste falha se a regra da cadeia for removida, trocada de ordem com a vizinha ou tiver o limite trocado.
  - **Lacunas da primeira revisão:**
    - 1 a 3 fechadas com testes, a pedido de Rafael Santos: o `Met` no limite do prazo (`Handle_Should_ReturnMet_WhenCurrentCycleIsResolvedExactlyAtItsDue`), `Stopped` antes de `Breached` (`Handle_Should_ReturnNotApplicable_WhenStoppedCycleIsPastItsDue`) e o `Missed` pelo SQL (`GetOwn_Should_ReturnMissed_WhenCurrentCycleWasResolvedAfterItsDue`);
    - a 4, o teste de integração do cancelamento, que não separa o cronômetro parado do caso sem cronômetro, ficou como melhoria futura, por decisão de Rafael Santos.
  - **Lacunas não bloqueantes da segunda revisão, opcionais:**
    - o resultado `Met` e a precedência de `Stopped` sobre `Breached` são fixados só em memória; pelo SQL, só o caso falso do `Met` e o `Stopped` antes do prazo;
    - os limites exatos (`<=`/`>=`) também só em memória, porque pela API não dá para reproduzir o instante exato.
  - **Ajustes de texto feitos nas verificações:**
    - no `SYSTEM_SPECIFICATION.md`: o fato da reabertura (com a marca no texto antigo "visível só no bloco `sla`"), a RN-26 ("expressão LINQ que o EF traduz para SQL") e a RN-31 (os campos novos de `GET tickets/mine`);
    - nesta spec, a análise de desempenho (duas subconsultas, índice na linha 28).
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`:
  - na T-3: o fato da reabertura (seção 2.6), a RN-26, a C-13 e a rota `GET tickets/mine` (4.3.1);
  - na verificação: os ajustes de texto acima e a tabela 6.1.
- **CI:**
- **Entrega:** <PR #n (o mesmo do campo `pr:`); a spec fica `Entregue` quando o responsável mergear o PR na `main`>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end (P1 do critique de "Meus Chamados"). As specs 017 a 022 já existiam, então esta ficou com a 023. |
| 2026-10-28 | Em descoberta | Renumerada para 024: outra sessão tinha criado a `023-editar-departamento-sem-mexer-no-responsavel` dez minutos antes (21:59 contra 22:09). Nenhuma regra muda. |
| 2026-10-28 | Em descoberta | Duas rodadas de perguntas respondidas no chat (DEC-1 a DEC-7). Aguardando aprovação. |
| 2026-10-28 | Em descoberta | Conferido: o pré-requisito da DEC-4 está cumprido (spec 019 mergeada na `main` pelo PR #41) e o `TicketSlaClock.GetState` existe na `main` (`TicketSlaClock.cs:124`). Checklist de completude atendido, sem `[SUPOSIÇÃO]`; aguardando aprovação explícita. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat ("Aprovo a 023 e a 024."). |
| 2026-11-01 | Aprovada | Revisão antes da implementação, a pedido de Rafael Santos ("vamos para a 24 tem como ?"): o texto aprovado citava `TicketSla.GetStatus` e `TicketSlaClock.GetState`, que a spec 041 removeu, e a spec 045 mudou a reabertura. Descoberta refeita no código da `main` com as specs 041 e 045 (atualização no Contexto): as regras de negócio continuam valendo. DEC-8 (a situação do ciclo vigente pela regra do `TicketSlaTimer.GetState`, escrita como expressão traduzível na própria consulta) e DEC-9 (branch reaproveitada) decididas por Rafael Santos, que delegou a escolha ("veja qual deles se encaixa melhor para esse caso e faça identico ok"); REQ-2 e plano técnico ajustados para citar o código atual, com o mesmo comportamento. Sem REQ novo nem removido e sem arquivo fora do plano: o status não muda. |
| 2026-11-01 | Aprovada | Revisão aprovada por Rafael Santos no chat ("aprovo a revisão da 024"). |
| 2026-11-01 | Em implementação | `/spec-implement 024` iniciado por Rafael Santos na branch `feat/SPEC-024-own-tickets-current-sla-cycle`, já com a `main` das specs 022, 023 e 045 (merge `ca8113b`), com a instrução de implementar a spec inteira com autonomia técnica, sem commits nem outras operações de git, e apresentar um relatório final antes de qualquer commit. |
| 2026-11-01 | Em implementação | T-1 a T-4 concluídas na árvore de trabalho, sem commits, como pedido. `GET tickets/mine` ganhou `currentDueAtUtc` e `currentSlaStatus`, calculados no SQL por duas subconsultas escalares do cronômetro de resolução de maior ciclo, no mesmo SELECT (conferido no SQL gerado). A regra do `TicketSlaTimer.GetState` foi escrita como uma cadeia de `??`, uma regra por linha, porque o analisador recusa ternários aninhados e a árvore de expressão não aceita `switch`; sem cronômetro, valem o prazo e a situação do chamado. Entraram 7 testes unitários e 6 de integração, incluído um de integração do AC-6 além dos previstos, para o caminho sem cronômetro no SQL. O `SYSTEM_SPECIFICATION.md` foi atualizado no fato da reabertura, na RN-26, na C-13 e na rota `GET tickets/mine`. `ca-review` sem bloqueantes nem violações. Build com 0 avisos; suíte inteira com 1.492 testes aprovados (60 de arquitetura, 903 unitários e 529 de integração), das 19:28 às 19:35. A auditoria SDD local falha só no SDD-03, pela `specs/049-reavaliacao-arquitetural-dominio-chamados`, que está fora do git e é de outra sessão. Próximo passo: `spec-verify`. |
| 2026-11-01 | Verificada | `spec-verify`: build com 0 avisos; suíte inteira verde (1.492 testes: 60 de arquitetura, 903 unitários e 529 de integração), com os 13 casos de teste da rastreabilidade aprovados no relatório por teste; auditoria SDD 100% (saída 0; a spec 049 da outra sessão já estava completa). AC-1 a AC-8 cobertos por testes lidos e aprovados nesta execução. Revisão independente sem bloqueantes nem violações, com quatro lacunas de teste não bloqueantes registradas nas evidências. A verificação corrigiu textos da documentação (fato da reabertura e RN-26 no `SYSTEM_SPECIFICATION.md`, análise de desempenho nesta spec) e acrescentou a tabela 6.1. Próximo passo: decisão de Rafael Santos entre fechar as lacunas de teste antes do commit ou commitar como está. |
| 2026-11-01 | Em implementação | Rafael Santos decidiu fechar antes do commit as lacunas de teste 1 a 3 da revisão independente ("Fechar as lacunas. Retorne a SPEC-024 para Em implementação"): o `Met` do ciclo vigente, a precedência de `Stopped` sobre `Breached` e o `Missed` pelo SQL; a do teste de integração do cancelamento fica como melhoria futura. Só testes mudam, sem alterar a implementação funcional; a verificação anterior deixa de valer e a spec passa de novo pela `spec-verify`, com nova revisão independente. |
| 2026-11-01 | Em implementação | Lacunas 1 a 3 fechadas na T-2, só com testes; o código de produção ficou igual ao da implementação. Entraram três testes: `Handle_Should_ReturnMet_WhenCurrentCycleIsResolvedExactlyAtItsDue` (o `Met` no limite `<=` do prazo, consultado depois dele), `Handle_Should_ReturnNotApplicable_WhenStoppedCycleIsPastItsDue` (`Stopped` antes de `Breached`) e `GetOwn_Should_ReturnMissed_WhenCurrentCycleWasResolvedAfterItsDue` (`Missed` pelo SQL). Build com 0 avisos; os testes de `GET tickets/mine` aprovados (23 unitários e 23 de integração). |
| 2026-11-01 | Verificada | Segunda `spec-verify`, depois de fechar as lacunas 1 a 3. Build com 0 avisos; suíte inteira verde (1.495 testes: 60 de arquitetura, 905 unitários e 530 de integração), com os 16 casos da rastreabilidade aprovados no relatório por teste; auditoria SDD 100% (saída 0). AC-1 a AC-8 cobertos por testes lidos e aprovados nesta execução. A nova revisão independente não encontrou bloqueantes nem violações; as lacunas opcionais ficaram registradas nas evidências (`Met` e precedência de `Stopped` só em memória; o teste do cancelamento como melhoria futura). Documentação: a RN-31, a marca no fato da reabertura e a linha da tabela 6.1 com esta execução. Próximo passo: aprovação de Rafael Santos e commit autorizado por ele. |
