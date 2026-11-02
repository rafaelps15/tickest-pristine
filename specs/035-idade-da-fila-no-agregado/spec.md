---
id: 035
titulo: Idade da fila (abertos e em andamento) por faixa no agregado de estatísticas
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-035-tickets-backlog-aging                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 035 — Idade da fila (abertos e em andamento) por faixa no agregado de estatísticas

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "Spec 035: bloco de contagens de idade da fila no agregado de estatísticas, sem carregar listas paginadas de tickets para calcular totais." E a definição: "A idade será calculada desde a abertura do ticket até o momento da consulta, em dias corridos, considerando apenas tickets abertos e em andamento. As faixas serão 0–1, 2–3, 4–7 e mais de 7 dias. Documente o fuso horário, os limites exatos das faixas, o tratamento de tickets criados no mesmo dia e a fonte dos dados. Não confunda idade desde a abertura com tempo desde a última atividade nem com tempo útil de SLA." Autorizado só como rascunho.
- **Por que:** o gráfico "Idade da fila" do Início precisa mostrar chamados esquecidos sem contar a partir de listas.
- **Referências:** spec 026 (agregado `GET tickets/stats/overview`); spec 029 (que também estende o agregado); `SYSTEM_SPECIFICATION.md`, seção de estatísticas.
- **Fatos do repositório:**
  - O agregado é um único SQL com contagens condicionais (`Tickets/GetOverview/GetTicketOverviewQueryHandler.cs:32-46`); a condição de ativo é `t.status IN ('Open', 'InProgress')` (constante `Active`, linha 27).
  - A abertura do chamado é `tickets.created_at_utc`; as datas locais das estatísticas são de `America/Sao_Paulo` (`TicketScopeFilter`, specs 025 e 028).
  - O tempo útil de SLA usa o calendário de expediente (`Abstractions/Sla/ISlaCalendarProvider.cs`) — **não** é usado aqui.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET tickets/stats/overview` ganha o bloco `aging` com quatro contagens de chamados **ativos** (aberto + em andamento) do recorte: até 1 dia, 2 a 3 dias, 4 a 7 dias e mais de 7 dias. | pedido |
| REQ-2 | A idade de um chamado é a diferença, em **dias de calendário de `America/Sao_Paulo`**, entre a data local da consulta e a data local da abertura (`created_at_utc` convertida pro fuso) — a mesma régua de datas locais das specs 025 (REQ-1, DEC-2) e 028, com a conversão de `SlaCalendars/LocalDatePeriod.cs`. **Não** é a contagem de blocos de 24 horas. Chamado aberto hoje tem idade 0, qualquer que seja a hora. Exemplos na seção 3. | pedido; DEC-1; DEC-3 |
| REQ-3 | Limites exatos (inclusivos): `upToOneDay` = idade 0 ou 1; `twoToThreeDays` = 2 ou 3; `fourToSevenDays` = 4 a 7; `overSevenDays` = 8 ou mais. | pedido; DEC-2 |
| REQ-4 | Não é tempo desde a última atividade nem tempo útil: reabertura, troca de status, mensagens e expediente não mudam a idade. Um chamado reaberto conta pela data da **abertura original**. | pedido; DEC-1 |
| REQ-5 | As contagens saem do mesmo comando SQL do agregado (contagens condicionais a mais), com os mesmos filtros de recorte; nenhuma lista é carregada. | pedido |
| REQ-6 | A soma das quatro faixas é igual a `volume.backlogCount` com o mesmo recorte. | consistência |
| REQ-7 | A mudança é aditiva: os campos atuais do agregado não mudam; a permissão continua `tickets:manage`. | spec 026 |

## 3. Critérios de aceitação
Dados comuns: um atendente com `tickets:manage`; o relógio do teste fixado; chamados ativos abertos hoje, ontem, há 2, 3, 4, 7, 8 e 30 dias (datas de São Paulo), um reaberto aberto há 10 dias, e chamados resolvidos, fechados e cancelados antigos.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-3 | os chamados acima | chama o agregado sem filtros | `aging` = até 1 dia: 2 (hoje e ontem); 2–3: 2; 4–7: 2; mais de 7: 3 (8, 30 e o reaberto de 10) |
| AC-2 | REQ-2 | um chamado criado às 23h30 de São Paulo (02h30 UTC do dia seguinte) | consulta às 00h30 de São Paulo do dia seguinte | a idade é 1 (dias de calendário locais), não 0 (horas corridas) |
| AC-3 | REQ-2 | um chamado criado às 00h05 de hoje em São Paulo | consulta às 23h55 de hoje | a idade é 0 (mesmo dia) |
| AC-4 | REQ-4 | o chamado reaberto | compara antes e depois da reabertura | ele está na faixa da data de abertura original nas duas leituras |
| AC-5 | REQ-6 | os chamados acima, com e sem recorte de setor e prioridade | soma as faixas | igual a `volume.backlogCount` do mesmo recorte |
| AC-6 | REQ-5 | os chamados acima | chama o agregado contando os comandos enviados ao banco | continua um comando de contagem por requisição |
| AC-7 | REQ-7 | um usuário sem `tickets:manage` | chama o agregado | 403; sem token, 401; os campos anteriores do JSON continuam iguais pra quem tem a permissão |
| AC-8 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

**Exemplos da régua (REQ-2 e REQ-3), com horários de São Paulo — todos viram casos de teste no AC-1 a AC-3:**

| Abertura | Consulta | Horas decorridas | Idade (dias de calendário) | Faixa |
|---|---|---|---|---|
| 28/10 00h05 | 28/10 23h55 | 23h50 | 0 | 0–1 |
| 27/10 23h30 | 28/10 00h30 | 1h | 1 | 0–1 |
| 27/10 00h05 | 28/10 23h55 | 47h50 | 1 | 0–1 |
| 26/10 23h59 | 28/10 00h01 | 24h02 | 2 | 2–3 |
| 25/10 10h00 | 28/10 09h00 | 71h | 3 | 2–3 |
| 24/10 10h00 | 28/10 10h00 | 96h | 4 | 4–7 |
| 21/10 08h00 | 28/10 18h00 | 178h | 7 | 4–7 |
| 20/10 23h00 | 28/10 08h00 | 177h | 8 | mais de 7 |

A 3ª linha mostra a diferença para os blocos de 24 horas: quase 48 horas ainda são 1 dia de calendário. A 4ª mostra o inverso: pouco mais de 24 horas já são 2 dias, porque o chamado atravessou duas meias-noites.

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | O que é a idade | (a) desde a abertura até a consulta, em dias corridos, só abertos e em andamento; (b) desde a última atividade; (c) tempo útil de SLA | (a) | Rafael Santos, 2026-10-28, no chat do front-end |
| DEC-2 | Faixas | 0–1, 2–3, 4–7, mais de 7 | essas quatro, com os limites inclusivos do REQ-3 | Rafael Santos, 2026-10-28, no chat do front-end |
| DEC-3 | "Dias corridos": dias de calendário locais ou blocos de 24h | (a) diferença de datas locais de São Paulo (um chamado de 23h30 tem 1 dia à 00h30); (b) horas corridas ÷ 24 (o mesmo chamado tem 0 dias até completar 24h) | (a) dias corridos pela data do calendário de `America/Sao_Paulo`, a mesma convenção das specs 025 e 028; blocos de 24 horas não são usados (exemplos na seção 3) | Rafael Santos, 2026-10-28, no chat do front-end ("Defina dias corridos pela data do calendário, seguindo a mesma convenção das specs 025 e 028. Não calcular dias corridos por blocos completos de 24 horas") |
| DEC-4 | Onde fica o bloco | (a) no agregado (`GET tickets/stats/overview`); (b) rota própria | (a), pedido do responsável ("bloco de contagens de idade da fila no agregado") | Rafael Santos, 2026-10-28, no chat do front-end |

## 5. Fora do escopo
- Tempo desde a última atividade e tempo útil de SLA (outras medidas; não misturar).
- Idade dos resolvidos, fechados ou cancelados.
- Lista dos chamados de cada faixa (o drill-down usa `GET tickets` com o período de abertura da spec 025).
- Implementar a mudança e o gráfico do front (só depois da aprovação).

## 6. Plano técnico
- **Pré-requisito:** spec 026 na `main` (já está). Se a spec 029 entrar antes, as duas acrescentam blocos ao mesmo SQL; sem conflito de contrato.
- **Arquivos a criar ou alterar:**
  - Application: `Tickets/GetOverview/TicketOverviewResponse.cs` (bloco `TicketOverviewAgingResponse` com `UpToOneDayCount`, `TwoToThreeDaysCount`, `FourToSevenDaysCount`, `OverSevenDaysCount`) e `GetTicketOverviewQueryHandler.cs` (quatro `COUNT(*) FILTER` sobre `Active`, com a idade em dias = data local de hoje − data local de abertura, convertendo `created_at_utc` e `@NowUtc` para `America/Sao_Paulo`).
  - Testes: integração por AC, com relógio fixo (`IDateTimeProvider`) e datas em torno da meia-noite de São Paulo.
- **Contratos:** `GET tickets/stats/overview` ganha `aging: { upToOneDayCount, twoToThreeDaysCount, fourToSevenDaysCount, overSevenDaysCount }`; nada mais muda.
- **Mudança no banco:** não.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md`, a entrada "[Spec 026] Estatísticas da operação" (o bloco novo e a definição de idade).
- **Análise:**
  - **Consistência:** AC-5 amarra a soma das faixas ao backlog.
  - **Desempenho:** quatro contagens condicionais a mais no comando que já existe.
  - **Segurança:** a mesma permissão do agregado; só contagens.
- **Riscos:**
  - R-1: a fronteira de dia depende do fuso; os AC-2 e AC-3 e a tabela de exemplos testam a meia-noite de São Paulo. A conta usa a conversão de `SlaCalendars/LocalDatePeriod.cs` das specs 025 e 028, não uma conversão nova.
  - R-2: o front hoje lê o agregado com a interface da 026; o campo novo é opcional até a flag correspondente ser ligada.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Bloco `aging` na resposta e as contagens no SQL do agregado | AC-1 a AC-4, AC-6, AC-7 | `add-feature` | [x] |
| T-2 | Testes de faixas, meia-noite de São Paulo (com os 8 exemplos da seção 3), reabertura e paridade com o backlog | AC-1 a AC-6 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-8 | — | [x] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-8 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `IntegrationTests/Tickets/TicketsTests.cs` › `GetOverview_Should_CountActiveTicketsByAgeBucket_WhenTheyWereOpenedOnSeveralDays` | Com o relógio parado em 28/10 12h de São Paulo, chamados ativos abertos hoje, ontem e há 2, 3, 4, 7, 8 e 30 dias, um reaberto (pelo `TicketSlaCycleService`) aberto há 10 dias e um resolvido, um fechado e um cancelado antigos, num setor novo: o agregado do setor devolve `aging` = 2, 2, 2 e 3. O filtro pelo setor do cenário isola os dados, porque o banco dos testes é compartilhado (como nos demais testes do agregado); a leitura sem recorte fica no AC-5 | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-2 | `TicketsTests.cs` › `GetOverview_Should_AgeByLocalCalendarDays_AroundSaoPauloMidnight` (os 8 exemplos da seção 3) | O chamado aberto às 23h30 e consultado às 00h30 do dia seguinte cai em "até 1 dia". A API devolve só as faixas, não a idade; a contagem por dias de calendário, e não por blocos de 24 horas, aparece nas fronteiras das faixas: 26/10 23h59 → 28/10 00h01 (24h02) cai em "2 a 3 dias" e 20/10 23h → 28/10 08h (177h) cai em "mais de 7", onde os blocos de 24 horas dariam "até 1 dia" e "4 a 7" | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-3 | `TicketsTests.cs` › `GetOverview_Should_AgeByLocalCalendarDays_AroundSaoPauloMidnight` (o exemplo 28/10 00h05 → 28/10 23h55) | Aberto e consultado no mesmo dia local, quase 24 horas depois: "até 1 dia" | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-4 | `TicketsTests.cs` › `GetOverview_Should_KeepAgeOfOriginalOpening_WhenTicketIsReopened` | Com o relógio parado, o chamado aberto há 10 dias está em "mais de 7" antes; resolvido e reaberto pela API, continua em "mais de 7", e não volta para "até 1 dia" | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-5 | `TicketsTests.cs` › `GetOverview_Should_SumAgingBucketsToBacklog_ForEveryScope` (sem recorte, com o setor e com o setor e a prioridade Urgente) | A soma das quatro faixas é igual a `volume.backlogCount` do mesmo recorte, inclusive sem recorte, com os chamados dos outros testes | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-6 | `TicketsTests.cs` › `GetOverview_Should_ReadTicketsInASingleCommand_WhenAgingIsIncluded` | O `NpgsqlCommandCounter` conta, durante a requisição, os comandos enviados ao PostgreSQL que leem `public.tickets` (também os do Dapper): um só, e a resposta traz a idade da fila | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-7 | `TicketsTests.cs` › `GetOverview_Should_ReturnForbidden_WhenCallerLacksManagePermission`, `GetOverview_Should_ReturnUnauthorized_WhenNoTokenProvided` e os demais `GetOverview_*` que já existiam | 403 sem `tickets:manage` e 401 sem token; os testes dos campos anteriores (paridade com a listagem, recorte por setores e período, atenção e aguardando) passam sem alteração; o `OverviewDto` dos testes só ganhou o bloco `aging` | Aprovado em 2026-11-01 (execução da verificação, das 21:48 às 21:55) |
| AC-8 | `dotnet build TickestPristine.slnx --no-incremental` e `dotnet test TickestPristine.slnx --no-build --logger trx` | Build com 0 avisos e 0 erros e a suíte inteira aprovada | Aprovado em 2026-11-01 (build às 21:48 com 0 avisos e 0 erros; suíte inteira das 21:48 às 21:55 com 1.509 aprovados e 0 falhas) |

## 9. Evidências de verificação
- **Build:** 2026-11-01, 21:48, `dotnet build TickestPristine.slnx --no-incremental`: 0 avisos, 0 erros.
- **Testes:** 2026-11-01, das 21:48 às 21:55, `dotnet test TickestPristine.slnx --no-build --logger trx`: 1.509 aprovados, 0 com falha (60 de arquitetura, 905 unitários, 544 de integração); os 14 casos de teste novos e os 12 do agregado que já existiam conferidos no relatório por teste (todos aprovados). Auditoria `python scripts/auditoria_sdd.py .` às 21:48: score 100%, CONFORME, código de saída 0.
- **Revisão:** `ca-review` na T-4 e de novo na verificação, sobre o mesmo código: nenhum achado bloqueante nem violação de convenção. A regra usa o `LocalDatePeriod` (R-1), os limites são inclusivos no início de cada dia local, e a conversão pelo fuso IANA continua certa se o horário de verão voltar. A infraestrutura de teste nova segue os padrões existentes: o `TestDateTimeProvider` como o `CapturingEmailSender` e o `NpgsqlCommandCounter` como o `EfCommandCounter`. Observação: a API devolve só as faixas, então o "idade 1, não 0" do AC-2 e o "idade 0" do AC-3 não aparecem sozinhos; a regra de dias de calendário fica comprovada nas fronteiras das faixas (exemplos 4 e 8 da seção 3).
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`: na T-3, a entrada "[Spec 026] Estatísticas da operação" (o bloco `aging` e a definição de idade) e a rota `GET tickets/stats/overview` (4.3.1); na verificação, a tabela 6.1.
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end. Número conferido: 034 reservado para o agrupamento por prioridade/setor/responsável, criado na mesma sessão. |
| 2026-10-28 | Em descoberta | DEC-1, DEC-2 e DEC-4 registradas com as palavras do responsável; DEC-3 (dias de calendário × 24h) em aberto, com recomendação e `[SUPOSIÇÃO]` nos AC-2 e AC-3. Autorizada só como rascunho — não pode ser aprovada nem implementada sem nova autorização. |
| 2026-10-28 | Em descoberta | DEC-3 respondida por Rafael Santos no chat do front-end: dias corridos pela data do calendário de São Paulo, como nas specs 025 e 028, sem blocos de 24 horas. REQ-2 refeito com a referência à conversão existente; tabela de 8 exemplos acrescentada à seção 3; `[SUPOSIÇÃO]` removido. Nenhuma pergunta aberta; continua sem aprovação. |
| 2026-10-28 | Em descoberta | Correções de texto autorizadas por Rafael Santos na revisão final: fonte do REQ-4 passa a DEC-1 (o que a idade mede); R-1 cita as specs 025 e 028, as que usam datas locais. Nenhuma regra mudou; continua sem aprovação. |
| 2026-10-28 | Aprovada | Aprovada explicitamente por Rafael Santos no chat do front-end ("Aprovada"), depois da revisão final das versões completas: nenhuma pergunta aberta, nenhum `[SUPOSIÇÃO]` pendente, auditoria SDD 100%. Implementação só com `/spec-implement`, numa branch própria. |
| 2026-11-01 | Em implementação | `/spec-implement 035` iniciado por Rafael Santos na branch `feat/SPEC-035-tickets-backlog-aging`, criada da `origin/main` em `74a8a5f` (com as specs 022, 023, 045 e 024), com a instrução de implementar a spec inteira com autonomia técnica, sem commits nem outras operações de git, e apresentar um relatório final antes de qualquer commit. |
| 2026-11-01 | Em implementação | T-1 a T-4 concluídas na árvore de trabalho, sem commits, como pedido. O agregado ganhou o bloco `aging`, com quatro contagens no mesmo SQL. Os limites das faixas são inícios de dias locais de São Paulo, convertidos pelo `LocalDatePeriod` (R-1), e as contagens são comparações com `created_at_utc`. Para o relógio fixo do plano, os testes de integração ganharam o `TestDateTimeProvider`, registrado na fábrica no lugar do relógio da API (o real, a não ser enquanto um teste o para) e exposto pela `BaseIntegrationTest`; para contar os comandos do AC-6, inclusive os do Dapper, o `NpgsqlCommandCounter`, no molde do `EfCommandCounter`. Entraram 14 casos de teste de integração (os 8 exemplos da seção 3 entre eles), e o `SYSTEM_SPECIFICATION.md` foi atualizado na entrada da spec 026 e na rota do agregado. `ca-review` sem bloqueantes nem violações. Build com 0 avisos; suíte inteira com 1.509 testes aprovados (60 de arquitetura, 905 unitários e 544 de integração), das 21:31 às 21:38. Próximo passo: `spec-verify`. |
| 2026-11-01 | Verificada | `spec-verify`: build com 0 avisos; suíte inteira verde (1.509 testes: 60 de arquitetura, 905 unitários e 544 de integração), com os 14 casos de teste novos e os 12 do agregado que já existiam aprovados no relatório por teste; auditoria SDD 100% (saída 0). AC-1 a AC-8 cobertos por testes lidos e aprovados nesta execução; o AC-2 e o AC-3 comprovados pelas faixas, que é o que a API expõe. `ca-review` sem bloqueantes nem violações. Documentação de estado: a entrada da spec 026 e a rota do agregado (T-3) e a tabela 6.1. Próximo passo: commit autorizado por Rafael Santos e PR. |
