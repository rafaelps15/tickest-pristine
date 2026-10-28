---
id: 027
titulo: Tempos médios de primeira resposta e de resolução
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-027-tickets-average-times                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 027 — Tempos médios de primeira resposta e de resolução

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "sim! pode criar na pasta do back" (item 5 do relatório do Início operacional).
- **Por que:** o Início deveria mostrar o tempo médio de primeira resposta e de resolução, com "dados insuficientes" quando a amostra for pequena. Não há endpoint para isso, e calcular no front exigiria buscar o SLA de cada chamado.
- **Referências:** specs 025 (período) e 026 (filtros de recorte); spec 005 (cronômetros de SLA).
- **Fatos do repositório:**
  - Cada objetivo de SLA tem um cronômetro por ciclo (`Domain/Tickets/TicketSlaTimer.cs:12-24`: `Metric`, `Cycle`, `StartedAtUtc`, `CompletedOnUtc`, `StoppedOnUtc`, `CalendarId`, `TargetMinutes`).
  - A primeira resposta é concluída pela primeira mensagem de quem não é o solicitante e tem `tickets:manage` (`TicketMessages/Post/PostTicketMessageCommandHandler.cs:68-107`), **e também ao resolver** (`Tickets/Update/UpdateTicketCommandHandler.cs:175-178`). O cronômetro não guarda qual das duas concluiu.
  - Os minutos úteis seguem o calendário do plano (`SlaCalendars/DefaultSlaCalendar.cs`; `BusinessCalendar.AddBusinessTime`), e o `GET tickets/{id}/sla` já calcula os minutos consumidos por ciclo.
  - Não existe nenhum endpoint de média (`Average`/`Avg` não aparece no código).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | O cronômetro de primeira resposta passa a guardar a causa da conclusão: mensagem de quem atende ou resolução. Cada caminho que conclui grava a sua causa. | DEC-1 |
| REQ-2 | Os cronômetros já concluídos ganham a causa na migration: "mensagem" quando existe mensagem de quem não é o solicitante no instante da conclusão, "resolução" nos demais. | DEC-1 |
| REQ-3 | `GET tickets/stats/times` devolve, para primeira resposta e para resolução: média e mediana em minutos úteis e o tamanho da amostra. | pedido; DEC-2 |
| REQ-4 | A primeira resposta conta só os cronômetros concluídos por mensagem. A resolução conta o ciclo 1 (abertura → primeira resolução). Cronômetros parados (cancelamento) ficam fora. | DEC-1; DEC-3 |
| REQ-5 | Com menos de 5 chamados na amostra, média e mediana vêm nulas; a amostra vem sempre. | DEC-2 |
| REQ-6 | Aceita os filtros de recorte da 026 e os períodos da 025; o período considera a data da conclusão (primeira resposta ou primeira resolução no período). | DEC-4 |
| REQ-7 | Exige `tickets:manage`. | padrão das estatísticas |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado respondido por mensagem e outro resolvido sem resposta | consulta os cronômetros | causa "mensagem" no primeiro e "resolução" no segundo |
| AC-2 | REQ-2 | cronômetros concluídos antes da migration, nos dois casos | aplica a migration | cada um com a causa certa |
| AC-3 | REQ-3, REQ-4 | 6 chamados respondidos por mensagem em 30, 60, 60, 90, 120 e 600 minutos úteis, e 1 resolvido sem resposta | chama `GET tickets/stats/times` | primeira resposta: amostra 6, média 160, mediana 75; o resolvido sem resposta não entra |
| AC-4 | REQ-4 | um chamado resolvido, reaberto e resolvido de novo | chama a rota | a resolução usa só o ciclo 1 |
| AC-5 | REQ-5 | 4 chamados resolvidos | chama a rota | resolução com amostra 4 e média e mediana nulas |
| AC-6 | REQ-6 | conclusões em meses diferentes | chama com `resolvedFrom`/`resolvedTo` de um mês e com um setor | entram só as conclusões daquele mês e setor |
| AC-7 | REQ-7 | um usuário sem `tickets:manage` | chama a rota | 403; sem token, 401 |
| AC-8 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | O que conta como primeira resposta, e como saber? | (a) só a mensagem de quem atende, com a causa gravada no cronômetro; (b) o cronômetro como está; (c) deduzir na consulta | (a): resolver sem responder distorceria a média; a causa gravada deixa a regra explícita e a consulta barata | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Medida e amostra mínima | (a) média + mediana, mínimo 5; (b) só média, sem mínimo | (a): a mediana resiste a casos extremos; abaixo de 5 o front mostra "dados insuficientes" | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | Trecho do tempo de resolução | (a) abertura → primeira resolução (ciclo 1); (b) todos os ciclos | (a): reaberturas aparecem na vazão da 028 | Rafael Santos, 2026-10-28, no chat |
| DEC-4 | Qual data decide se o chamado entra no período? | (a) a data da conclusão; (b) a data de abertura | (a): mede o desempenho daquele período | Rafael Santos, 2026-10-28, no chat |

## 5. Fora do escopo
- Mudar a regra que conclui a primeira resposta ao resolver (só passa a ser identificada).
- Tempos por atendente ou por setor numa lista (só o recorte filtrado).
- Percentis além da mediana.

## 6. Plano técnico
- **Pré-requisito:** PRs das specs 025 e 026 mergeados.
- **Arquivos a criar ou alterar:**
  - Domain: `Tickets/TicketSlaTimer.cs` (causa da conclusão, opcional) e o enum da causa (novo).
  - Application: a conclusão por mensagem (`TicketMessages/Post/PostTicketMessageCommandHandler.cs`) e por resolução (`Tickets/Update/UpdateTicketCommandHandler.cs`) gravam a causa; `Tickets/GetTimes/*` (novo) com consulta, resposta e o cálculo dos minutos úteis por cronômetro, pela mesma regra do `GET tickets/{id}/sla`.
  - Infrastructure: configuração da coluna e migration com o preenchimento do REQ-2.
  - Web.Api: `Endpoints/Tickets/GetTimes.cs` (novo).
  - Testes: unitários do cálculo (média, mediana, amostra mínima) e de integração dos ACs.
- **Contratos:** rota nova `GET tickets/stats/times`; resposta com os blocos `firstResponse` e `resolution`, cada um com média, mediana (minutos úteis, nulos abaixo da amostra mínima) e amostra, e o valor da amostra mínima. 200 · 400 · 401 · 403.
- **Mudança no banco:** sim, uma coluna no cronômetro, com preenchimento dos existentes.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` (cronômetros de SLA, estatísticas e 4.3).
- **Análise:**
  - **Desempenho:** o tempo útil de cada cronômetro é calculado em memória sobre os cronômetros do recorte; se o volume crescer muito, os minutos consumidos viram coluna numa spec futura.
  - **Consistência:** o cálculo de minutos úteis é o mesmo do `GET tickets/{id}/sla`, para a média bater com o painel do detalhe.
- **Riscos:**
  - R-1: a migration deduz a causa dos cronômetros antigos pela mensagem no mesmo instante; casos muito antigos podem ficar como "resolução" quando foram resposta. Aceitável: só afeta a média histórica.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Causa da conclusão no cronômetro, gravada nos dois caminhos, e migration | AC-1, AC-2 | `add-entity` | [x] |
| T-2 | Consulta, cálculo e rota dos tempos médios | AC-3 a AC-7 | `add-feature` | [x] |
| T-3 | Testes unitários e de integração | AC-1 a AC-7 | `add-tests` | [ ] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-8 | — | [ ] |
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
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end; número reservado já na criação, porque outra sessão cria specs em paralelo. |
| 2026-10-28 | Em descoberta | Perguntas respondidas no chat (DEC-1 a DEC-4). Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação depois do merge da 025 e da 026. |
| 2026-10-28 | Em implementação | `/spec-implement 027` na branch `feat/SPEC-027-tickets-average-times`, com a `origin/main` mesclada depois do merge da 025 (PR #43) e da 026 (PR #45). Rafael Santos pediu a implementação completa seguindo o `CLAUDE.md` e as skills, com o agente respondendo por ele ("atue como senior e só pare quando finalizar e siga o claude.md e as skills de modelo de codigo ok"). Tabelas de rastreabilidade e de evidências criadas. |
