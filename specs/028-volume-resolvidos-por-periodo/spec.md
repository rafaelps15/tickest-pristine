---
id: 028
titulo: Resolvidos por período no volume de chamados, ao lado dos criados, com filtros
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-028-tickets-volume-resolved                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 028 — Resolvidos por período no volume de chamados, ao lado dos criados, com filtros

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "sim! pode criar na pasta do back" (item 6 do relatório do Início operacional).
- **Por que:** o gráfico do Início deveria comparar criados e resolvidos por dia, semana ou mês, seguindo os filtros. Hoje o volume conta só os criados e não aceita filtros.
- **Referências:** specs 025 (período) e 026 (filtros de recorte); spec 002 (histórico do chamado).
- **Fatos do repositório:**
  - `GET tickets/stats/volume` aceita só `groupBy` (dia, semana ou mês) e devolve 30, 12 ou 12 pontos com `PeriodStart` e `Count` dos **criados** (`Tickets/GetVolume/GetTicketVolumeQueryHandler.cs:19-49`), em `America/Sao_Paulo`, semana começando na segunda.
  - O `ResolvedAtUtc` guarda só a primeira resolução; cada resolução, inclusive depois de reabrir, fica no histórico como `StatusChanged` para Resolvido, com `OccurredAtUtc` (`TicketHistoryAction`; `Tickets/Update/UpdateTicketCommandHandler.cs`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada ponto do volume passa a trazer também a quantidade de resoluções no período: toda mudança de status para Resolvido, inclusive depois de reabrir. O campo dos criados continua igual. | pedido; DEC-1 |
| REQ-2 | O volume aceita os filtros de recorte da 026 (setores, prioridade, responsável, sem responsável e solicitante). | DEC-2 |
| REQ-3 | Com o período da 025 (`createdFrom`/`createdTo` para o intervalo do gráfico), os pontos vão do início ao fim do período, na granularidade escolhida; sem período, as janelas de hoje (30 dias, 12 semanas, 12 meses). | DEC-3; DEC-4 |
| REQ-4 | Um período que gere mais de 366 pontos responde 400 com erro de validação claro. | DEC-3 |
| REQ-5 | Períodos sem chamados criados nem resolvidos vêm com zero nos dois campos; a ordem continua do mais antigo para o mais recente. | fato do repositório |
| REQ-6 | Exige `tickets:manage`, como hoje. | fato do repositório |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado resolvido em 02/11, reaberto e resolvido de novo em 05/11 | chama com `groupBy=Day` | resolvidos = 1 em 02/11 e 1 em 05/11 |
| AC-2 | REQ-1, REQ-5 | a mesma base | compara os criados com a resposta de antes da mudança | os criados são iguais; dias vazios trazem zero nos dois |
| AC-3 | REQ-2 | chamados em dois setores | chama com um `sectorId` | criados e resolvidos só daquele setor |
| AC-4 | REQ-3 | — | chama com `groupBy=Week&createdFrom=2026-09-01&createdTo=2026-10-31` | pontos semanais cobrindo de 01/09 a 31/10; sem período, as 12 semanas de hoje |
| AC-5 | REQ-4 | — | chama com `groupBy=Day` e um período de dois anos | 400 com erro de validação |
| AC-6 | REQ-6 | um usuário sem `tickets:manage` | chama a rota | 403 |
| AC-7 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | O que conta como resolvido no período? | (a) cada resolução, pelo histórico; (b) só a primeira resolução | (a): é a vazão real da equipe | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | O volume passa a aceitar filtros? | (a) sim, os da 026; (b) não | (a): o gráfico passa a seguir a barra de filtros do Início | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | Com período, como fica o gráfico? | (a) o período define o intervalo, limite de 366 pontos; (b) janelas fixas | (a) | Rafael Santos, 2026-10-28, no chat |
| DEC-4 | Qual data decide a entrada no período? | (a) a data do evento (criação para criados, resolução para resolvidos); (b) a abertura | (a) | Rafael Santos, 2026-10-28, no chat |
| DEC-5 | Onde recusar o período grande demais (REQ-4), já que o decorator de validação só envolve commands? | (a) no handler, com `TicketErrors.VolumeTooManyPoints` (`Error.Problem`, 400), como a DEC-6 da spec 025; (b) estender a validação às queries | (a): mesmo precedente da 025 e do `UserErrors.InvalidSearchTerm`; o AC-5 não muda | Delegado por Rafael Santos ao agente, 2026-10-28, no chat ("desenvolva tudo a nivel senior [...] Atue como senior no desenvolvimento") |
| DEC-6 | E com só uma ponta do período? | (a) sem `createdTo`, o fim é hoje em São Paulo; sem `createdFrom`, o início é a janela padrão (30 dias, 12 semanas, 12 meses) terminando no fim; início depois do fim responde 400 `Tickets.InvalidPeriod`; (b) exigir as duas pontas | (a): o gráfico sempre tem um intervalo, a regra sem período continua a mesma e o período invertido segue a 025 | Delegado por Rafael Santos ao agente, 2026-10-28, no chat ("desenvolva tudo a nivel senior [...] Atue como senior no desenvolvimento") |

## 5. Fora do escopo
- Fechados e cancelados por período.
- Mudar a semana (continua começando na segunda) ou o fuso (continua `America/Sao_Paulo`).

## 6. Plano técnico
- **Pré-requisito:** PRs das specs 025 e 026 mergeados (período e tipo único de filtros).
- **Arquivos a criar ou alterar:**
  - Application: `Tickets/GetVolume/GetTicketVolumeQuery.cs` (filtros e período), `GetTicketVolumeQueryHandler.cs` (segunda contagem sobre o histórico de resoluções, mesmos períodos e mesmo fuso; intervalo pelo período quando informado) e `TicketVolumeResponse.cs` (o campo dos resolvidos); validador do limite de pontos.
  - Web.Api: `Endpoints/Tickets/GetVolume.cs` (lê os filtros e o período).
  - Testes: integração dos ACs; o teste atual do volume continua passando para os criados.
- **Contratos:** `GET tickets/stats/volume` ganha os filtros de recorte e o período, e cada ponto ganha a quantidade de resolvidos. Mudança aditiva; 400 novo só para período grande demais.
- **Mudança no banco:** não. A contagem de resolvidos lê o histórico; o índice existente do histórico por chamado e data atende, e um índice por ação e data entra numa spec à parte se a medição pedir.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` (estatísticas e 4.3).
- **Riscos:**
  - R-1: chamados excluídos (exclusão lógica) somem dos criados pelo filtro global; o histórico de resoluções deles também precisa ser filtrado pelo chamado, para os dois campos baterem.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Resolvidos por período, filtros e período no volume | AC-1 a AC-6 | `add-feature` | [x] |
| T-2 | Testes de integração | AC-1 a AC-6 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [x] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_CountEveryResolution_WhenTicketIsResolvedReopenedAndResolvedAgain` | Chamado aberto em 01/09, resolvido em 02/09 e de novo em 05/09 (duas mudanças para Resolvido no histórico): com `groupBy=Day`, resolvidos = 1 em 02/09 e 1 em 05/09. | Aprovado |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › os 9 `GetVolume_*` que já existiam (criados por dia, semana e mês, virada do dia e da semana em Brasília, excluído fora); `GetVolume_Should_CountEveryResolution_WhenTicketIsResolvedReopenedAndResolvedAgain` | Os testes anteriores dos criados passam sem alteração no comportamento (só o DTO ganhou o campo novo); os dias vazios trazem zero nos dois campos (sete pontos de 01/09 a 07/09, com a abertura e as resoluções nos seus dias). | Aprovado |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_CountOnlyTicketsOfTheSector_WhenSectorIsGiven`, `GetVolume_Should_NotCountResolutionsOfDeletedTicket` | Com um `sectorId`, criados e resolvidos só daquele setor (2 e 2, não 3); o chamado excluído sai dos dois campos (R-1). Uma mutação que tirava o recorte da contagem de resolvidos foi detectada pelos dois testes. | Aprovado |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_CoverWholePeriodWeekByWeek_WhenPeriodIsGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/TicketVolumePeriodsTests.cs` › `Build_Should_CoverWholePeriodInWeeks_WhenPeriodIsGiven`, `Build_Should_ReturnDefaultWindowEndingToday_WhenNoPeriodIsGiven`, `Build_Should_UseTodayAsEnd_WhenOnlyStartIsGiven`, `Build_Should_UseDefaultWindowEndingAtEnd_WhenOnlyEndIsGiven` | `groupBy=Week&createdFrom=2026-09-01&createdTo=2026-10-31` traz 9 pontos semanais, das segundas-feiras 31/08 a 26/10, de 7 em 7 dias; sem período, as 12 semanas de hoje, terminando numa segunda-feira. Os unitários conferem as janelas padrão e o período com uma ponta só (DEC-6). | Aprovado |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_ReturnBadRequest_WhenPeriodGeneratesTooManyPoints`; `tests/TickestPristine.Application.UnitTests/Tickets/TicketVolumePeriodsTests.cs` › `Build_Should_AcceptExactlyTheMaximumOfPoints_AndRefuseOneMore`, `Build_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd`; `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketVolumeQueryHandlerTests.cs` › `Handle_Should_ReturnVolumeTooManyPoints_WhenPeriodGeneratesMoreThanTheMaximum`, `Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd` | Dois anos agrupados por dia respondem 400 com `Tickets.VolumeTooManyPoints` e o limite de 366 na mensagem (DEC-5); o unitário aceita exatamente 366 dias e recusa 367, e recusa período invertido com `Tickets.InvalidPeriod`; o handler devolve os dois erros sem abrir conexão com o banco (teste acrescentado depois da revisão de convenções). | Aprovado |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_ReturnForbidden_WhenCallerLacksManagePermission`, `GetVolume_Should_ReturnUnauthorized_WhenNoTokenProvided` (já existiam) | Sem `tickets:manage`, 403; sem token, 401: a permissão da rota não mudou. | Aprovado |
| AC-7 | `dotnet build TickestPristine.slnx --no-incremental` e `dotnet test TickestPristine.slnx` | 0 avisos e 0 erros; 50 testes de arquitetura, 752 unitários e 455 de integração aprovados, nenhuma falha; depois, os 2 unitários do handler acrescentados pela revisão: 754 unitários aprovados. | Aprovado |

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
| 2026-10-28 | Em descoberta | Perguntas respondidas no chat (DEC-1 a DEC-4). Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação depois do merge da 025 e da 026. |
| 2026-10-28 | Em implementação | `/spec-implement 028` na branch `feat/SPEC-028-tickets-volume-resolved`, com a `origin/main` mesclada depois do merge da 025 (PR #43), da 026 (PR #45) e da 027 (PR #47). Rafael Santos pediu a implementação completa seguindo as skills, com o agente respondendo por ele ("desenvolva tudo a nivel senior e siga minha skill modelo de codigo como exemplo padrão. Atue como senior no desenvolvimento"). DEC-5 (limite de pontos no handler) e DEC-6 (período com uma ponta só) registradas como delegadas. Tabelas de rastreabilidade e de evidências criadas. |
