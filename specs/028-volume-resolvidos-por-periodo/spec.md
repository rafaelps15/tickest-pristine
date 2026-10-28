---
id: 028
titulo: Resolvidos por período no volume de chamados, ao lado dos criados, com filtros
status: Aprovada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
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
| T-1 | Resolvidos por período, filtros e período no volume | AC-1 a AC-6 | `add-feature` | [ ] |
| T-2 | Testes de integração | AC-1 a AC-6 | `add-tests` | [ ] |
| T-3 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [ ] |
| T-4 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [ ] |

## 8. Rastreabilidade
Não se aplica até a aprovação.

## 9. Evidências de verificação
Não se aplica até a implementação.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-28 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end; número reservado já na criação, porque outra sessão cria specs em paralelo. |
| 2026-10-28 | Em descoberta | Perguntas respondidas no chat (DEC-1 a DEC-4). Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação depois do merge da 025 e da 026. |
