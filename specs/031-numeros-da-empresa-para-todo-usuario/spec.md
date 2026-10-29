---
id: 031
titulo: Totais por status e volume da empresa toda para qualquer usuário logado
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: fix/SPEC-031-company-stats-for-every-user                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 031 — Totais por status e volume da empresa toda para qualquer usuário logado

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28, ao escolher "Spec no back" para o achado da auditoria do Início: quem não atende a fila (o Solicitante) vê erro nos números da empresa.
- **Por que:** a decisão de produto é que todo papel vê os mesmos números da empresa no Início (totais por status e o gráfico de chamados criados). O front segue essa decisão, mas as duas rotas respondem 403 a quem não tem `tickets:manage`, e o Início do Solicitante mostra os cards e o gráfico em erro.
- **Referências:** `SYSTEM_SPECIFICATION.md`, tabela de permissões (linha de `GET tickets/sla-summary`, `GET tickets/stats/status`, `GET tickets/stats/volume`); spec 028 (volume com filtros de recorte); spec 026 (agregado da operação, que continua só para `tickets:manage`).
- **Fatos do repositório:**
  - As rotas nasceram abertas a qualquer usuário autenticado (`RequireAuthorization()`, commit `5e158ee`, 2026-10-08, "contagem por status e volume de chamados para o dashboard").
  - Passaram a exigir `tickets:manage` no commit `54c68b1` (2026-10-21, "código alinhado às skills e ao CLAUDE.md"), um refactor de alinhamento, sem pedido de mudança de permissão registrado (`Web.Api/Endpoints/Tickets/GetStatusCounts.cs:24`, `GetVolume.cs:25`).
  - As duas rotas devolvem só contagens agregadas da empresa: totais por status (`Tickets/GetStatusCounts/TicketStatusCountsResponse.cs`) e pontos `periodStart`/`count` (`Tickets/GetVolume/*`). Nenhum dado de pessoa.
  - Já existe permissão verificada no handler em outras rotas (ex.: `GET tickets/{id}`: "participante ou `tickets:manage`", tabela de permissões do `SYSTEM_SPECIFICATION.md`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET tickets/stats/status` atende qualquer usuário autenticado e ativo, sem exigir permissão específica. A resposta não muda. | pedido; DEC-1 |
| REQ-2 | `GET tickets/stats/volume` sem nenhum filtro de recorte atende qualquer usuário autenticado e ativo. Com o período da spec 025 (`createdFrom`/`createdTo`) e sem filtro de recorte, também. | DEC-1; DEC-2; DEC-3 |
| REQ-3 | `GET tickets/stats/volume` com qualquer filtro de recorte da spec 028 (setores, prioridade, responsável, sem responsável, solicitante) exige `tickets:manage`; sem ela, 403. | DEC-2 |
| REQ-4 | `GET tickets/sla-summary` e o agregado da spec 026 (`GET tickets/stats/overview`) continuam exigindo `tickets:manage`. | DEC-1 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um Solicitante ativo (sem `tickets:manage`) e chamados em vários status | chama `GET tickets/stats/status` | 200 com os mesmos totais que um atendente recebe |
| AC-2 | REQ-1, REQ-2 | — | chama as duas rotas sem token | 401 |
| AC-3 | REQ-2 | um Solicitante ativo e chamados criados em dias diferentes | chama `GET tickets/stats/volume?groupBy=Day` | 200 com os mesmos pontos que um atendente recebe |
| AC-4 | REQ-2 | um Solicitante ativo, com a spec 025 já mergeada | chama o volume só com `createdFrom`/`createdTo` | 200 com os pontos do período |
| AC-5 | REQ-3 | um Solicitante ativo, com a spec 028 já mergeada | chama o volume com `sectorId` (ou outro filtro de recorte) | 403; o mesmo pedido de um atendente responde 200 |
| AC-6 | REQ-4 | um Solicitante ativo | chama `GET tickets/sla-summary` (e o agregado da 026, se já mergeada) | 403, como hoje |
| AC-7 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quem vê os totais por status e o volume da empresa toda? | (a) qualquer usuário logado, só números agregados; (b) só `tickets:manage` (o front esconde os blocos) | (a): volta ao comportamento de 2026-10-08 e à decisão de produto de que todo papel vê os mesmos números; a restrição de 2026-10-21 veio de um refactor | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Como fica o volume com os filtros de recorte da spec 028? | (a) mesma rota: sem filtro de recorte, qualquer logado; com filtro, `tickets:manage`; (b) rota nova só para a empresa, aberta a todos; (c) tudo aberto, com filtros | (a): o front não muda de rota e ninguém sem a permissão recorta por setor ou pessoa | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | O período conta como filtro de recorte? | — | Não: a spec 028 separa os filtros de recorte (REQ-2 dela) do período (REQ-3 dela); o período da empresa toda não expõe setor nem pessoa | consequência da DEC-2 e da definição da spec 028, registrada em 2026-10-28 |

## 5. Fora do escopo
- `GET tickets/sla-summary` e o agregado da spec 026: continuam com `tickets:manage` (REQ-4).
- Mudar o conteúdo das respostas.
- Os filtros de recorte do volume em si (spec 028).

## 6. Plano técnico
- **Pré-requisito:** nenhum; branch a partir da `origin/main`. Pode ser mergeada antes ou depois das specs 025 e 028.
- **Arquivos a criar ou alterar:**
  - Web.Api: `Endpoints/Tickets/GetStatusCounts.cs` e `GetVolume.cs` (autorização por usuário autenticado no lugar de `tickets:manage`).
  - Application: no handler do volume, a verificação de `tickets:manage` quando houver filtro de recorte — só a partir da spec 028; se a 028 for mergeada depois, a regra entra junto com os filtros dela.
  - Testes: integração dos ACs.
- **Contratos:** as duas rotas passam a aceitar qualquer usuário autenticado e ativo; o volume com filtro de recorte passa a responder 403 sem `tickets:manage`. Nenhum payload muda.
- **Mudança no banco:** não.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md`, tabela de permissões (a linha de `sla-summary`, `stats/status` e `stats/volume` se divide).
- **Riscos:**
  - R-1: a spec 028 (aprovada) diz no REQ-6 que o volume exige `tickets:manage`, como hoje. Esta spec refina essa regra para "com filtro de recorte". Quem implementar a 028 depois desta segue a DEC-2 daqui; quem implementar a 028 antes deixa a regra de recorte para esta spec.
  - R-2: um usuário desativado com token ainda válido (GAP-14) continuaria vendo os números agregados até o token expirar — o mesmo comportamento das outras rotas abertas a usuário ativo.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Autorização das duas rotas por usuário autenticado; `tickets:manage` só com filtro de recorte no volume | AC-1 a AC-6 | `add-feature` | [x] |
| T-2 | Testes de integração | AC-1 a AC-6 | `add-tests` | [x] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [x] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetStatusCounts_Should_ReturnSameTotalsAsAgent_WhenCallerLacksManagePermission` | Com um chamado aberto e um resolvido, um Solicitante (Colaborador, sem `tickets:manage`) recebe 200 em `GET tickets/stats/status`, com os mesmos totais que um Atendente recebe logo depois (pelo menos 1 aberto e 1 resolvido). | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetStatusCounts_Should_ReturnUnauthorized_WhenNoTokenProvided`, `GetVolume_Should_ReturnUnauthorized_WhenNoTokenProvided` | Sem token, as duas rotas respondem 401. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_ReturnSameCompanyPointsAsAgent_WhenCallerLacksManagePermission` | Com chamados abertos ontem e três dias atrás, o Solicitante recebe 200 em `?groupBy=Day` com os mesmos pontos do Atendente, e os dois dias contados. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_ReturnPointsOfThePeriod_WhenCallerLacksManagePermissionAndGivesOnlyThePeriod` | Só com `createdFrom`/`createdTo` (10 a 16/02/2025), o Solicitante recebe 200 com um ponto por dia do período, os mesmos do Atendente, e os dois dias com chamados contados. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetVolume_Should_ReturnForbiddenToRequesterAndOkToAgent_WhenSlicingFilterIsGiven` (setor, setor com período, prioridade, solicitante, responsável e só sem responsável); `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketVolumeQueryHandlerTests.cs` › `Handle_Should_ReturnStatsScopeForbidden_WhenSlicingFilterIsGivenWithoutManagePermission` (cada filtro) | Com cada filtro de recorte, o Solicitante recebe 403 `Tickets.StatsScopeForbidden` e o Atendente, 200; no unitário, o handler confere `tickets:manage` de quem chama e não abre a conexão. Uma mutação que tirava "só sem responsável" do recorte foi detectada por 1 caso unitário e 1 de integração. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetSlaSummary_Should_ReturnForbidden_WhenCallerLacksManagePermission`, `GetOverview_Should_ReturnForbidden_WhenCallerLacksManagePermission` | Um usuário recém-cadastrado (Colaborador, sem `tickets:manage`) continua recebendo 403 em `GET tickets/sla-summary` e no agregado da 026. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-7 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` (suíte inteira) | Build com 0 avisos e 0 erros; 1.282 testes aprovados e 0 com falha (50 de arquitetura, 761 unitários, 471 de integração), com o `deps.json` dos testes conferido antes e depois da execução. | Aprovado na execução da `spec-verify` de 2026-10-28 |

## 9. Evidências de verificação
- **Build:** 2026-10-28, `dotnet build TickestPristine.slnx --no-incremental`: 0 avisos, 0 erros.
- **Testes:** 2026-10-28, `dotnet test TickestPristine.slnx` com relatório TRX por teste: 50 de arquitetura, 761 unitários e 471 de integração aprovados, 0 com falha. No TRX, aparecem como `Passed` os casos da rastreabilidade: os 5 de `GetTicketVolumeQueryHandlerTests.Handle_Should_ReturnStatsScopeForbidden_*`, os 6 de `GetVolume_Should_ReturnForbiddenToRequesterAndOkToAgent_*`, `GetStatusCounts_Should_ReturnSameTotalsAsAgent_*`, `GetVolume_Should_ReturnSameCompanyPointsAsAgent_*`, `GetVolume_Should_ReturnPointsOfThePeriod_*`, os dois `*_Should_ReturnUnauthorized_WhenNoTokenProvided`, `GetSlaSummary_Should_ReturnForbidden_*` e `GetOverview_Should_ReturnForbidden_*`. O `TickestPristine.Web.Api.deps.json` da pasta de testes foi conferido antes e depois da execução. Auditoria `python scripts/auditoria_sdd.py .`: score 100,0%, CONFORME, 0 falhas críticas, código de saída 0.
- **Revisão:** `ca-review` sobre as mudanças da spec: nenhum achado bloqueante nem violação de convenção. A autorização segue a forma 2 do `CLAUDE.md` (rota com usuário ativo e `tickets:manage` conferida no handler, como em `GET users/attendants`), e o erro `Tickets.StatsScopeForbidden` está em `TicketErrors`, em pt-BR. O único ajuste, de formatação (comentário de `GetTicketVolumeQuery`), foi feito na T-4, antes desta verificação. Observação sem defeito: o 400 de período inválido vem antes do 403 de recorte, a mesma ordem do handler de atendentes.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-35), 2.7 (C-13), 4.3.1 (a linha de `sla-summary`, `stats/status` e `stats/volume` dividida em três) e 5.7 (gatilhos do painel), no commit `7ec9f52` (T-3); conferidas nesta verificação, sem outra seção afetada.
- **CI:** roda no PR para a `main`.
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do achado da auditoria do Início no front-end (Solicitante vê erro nos números da empresa). |
| 2026-10-28 | Em descoberta | Perguntas respondidas no chat (DEC-1, DEC-2); DEC-3 derivada da definição da spec 028. Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat ("sim"). |
| 2026-10-28 | Em implementação | `/spec-implement 031` na branch `fix/SPEC-031-company-stats-for-every-user`, criada a partir da `origin/main` depois do merge da 029 (PR #49); as specs 025 e 028 já estão mergeadas, então a regra de recorte entra nesta spec (R-1). Rafael Santos pediu a implementação completa seguindo as skills de código. |
| 2026-10-28 | Em implementação | T-1: as duas rotas passam a `RequireAuthorization()` (usuário ativo); o handler do volume confere `tickets:manage` só quando `TicketScopeFilter.HasSlicingFilter` (setores, prioridade, solicitante, responsável ou só sem responsável; os períodos não contam, DEC-3) e responde 403 `Tickets.StatsScopeForbidden`, depois da validação do período, como o handler de atendentes. Os dois testes de integração que conferiam o 403 antigo (`GetStatusCounts_Should_ReturnForbidden_*` e `GetVolume_Should_ReturnForbidden_*`) contradiziam a DEC-1 e foram trocados, no mesmo commit, pelos testes do AC-1 e do AC-3. |
| 2026-10-28 | Em implementação | T-4: revisão de convenções sem achados bloqueantes (autorização na forma 2 do `CLAUDE.md`: rota com usuário ativo e permissão conferida no handler; erro em `TicketErrors` com texto em pt-BR). Único ajuste: quebra de linha no comentário de `GetTicketVolumeQuery`. Build com 0 avisos e suíte inteira verde (1.282 testes). Todas as tarefas concluídas; próximo passo: `/spec-verify 031`. |
| 2026-10-28 | Verificada | `spec-verify`: build sem avisos, 1.282 testes aprovados, auditoria 100% (saída 0), AC-1 a AC-7 cobertos por testes que passaram nesta execução, `ca-review` sem achados bloqueantes. |
