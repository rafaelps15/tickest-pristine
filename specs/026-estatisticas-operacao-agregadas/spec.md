---
id: 026
titulo: Estatísticas da operação agregadas numa chamada, com os filtros da listagem
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-026-tickets-operation-overview                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 026 — Estatísticas da operação agregadas numa chamada, com os filtros da listagem

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "sim! pode criar na pasta do back" (item 2 do relatório do Início operacional).
- **Por que:** o Início mostra Volume, SLA e Central de atenção filtrados por setor, prioridade e responsável. Sem um endpoint agregado, o front faz 11 chamadas `GET tickets?pageSize=1` a cada troca de filtro, só para ler o `totalCount` de cada recorte.
- **Referências:** spec 025 (período e vários setores); `TicketSlaFilters` (regra de cada situação de SLA); `SYSTEM_SPECIFICATION.md` 4.3.
- **Fatos do repositório:**
  - `GET tickets/stats/status` não aceita filtro (`Endpoints/Tickets/GetStatusCounts.cs:13`); `GET tickets/sla-summary` só aceita `sectorId` e não conta cumpridos nem perdidos (`Tickets/GetSlaSummary/TicketSlaSummaryResponse.cs:10-14`).
  - A regra das seis situações de SLA está em `Tickets/TicketSlaFilters.cs:20-30` (no prazo, em risco e atrasado só para ativos não cancelados; cumprido e perdido pela primeira resolução contra o prazo; sem SLA para cancelado nunca resolvido).
  - O volume já é uma consulta SQL crua (`Tickets/GetVolume/GetTicketVolumeQueryHandler.cs:19-49`), e a stack prevê Dapper para consultas de relatório (`CLAUDE.md`, "Stack").
  - As estatísticas exigem `tickets:manage` e não têm escopo por setor ou usuário.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `GET tickets/stats/overview` devolve, numa só resposta: volume (total, aberto, em andamento, resolvido, fechado, cancelado e backlog = aberto + em andamento), SLA (no prazo, em risco, atrasado, cumprido, perdido e sem SLA) e atenção (atrasados, críticos e sem responsável), mais a hora do servidor. | pedido; DEC-3 |
| REQ-2 | Aceita os mesmos filtros de `GET tickets` que recortam chamados: setor (repetível), prioridade, responsável, sem responsável, solicitante e os períodos da spec 025. Não aceita `status`, `slaStatus`, `search`, `sort` nem paginação. | pedido; DEC-1 |
| REQ-3 | Cada contador é igual ao `totalCount` de `GET tickets` com o mesmo recorte e o filtro correspondente (por exemplo, "atrasado" = `slaStatus=Breached`). | DEC-2 |
| REQ-4 | "Críticos" são os chamados de prioridade Urgente ainda ativos (aberto ou em andamento); "sem responsável" são os ativos sem responsável. | decisão do Início operacional (front, 2026-10-28) |
| REQ-5 | Exige `tickets:manage`, como as outras estatísticas. | fato do repositório |

## 3. Critérios de aceitação
Dados comuns: um atendente com `tickets:manage`; chamados em todos os status, prioridades e situações de SLA, em dois setores, com e sem responsável.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | os chamados acima | chama sem filtros | 200 com os três blocos e a hora do servidor; o backlog é a soma de aberto e em andamento |
| AC-2 | REQ-3 | os chamados acima | para cada contador, compara com o `totalCount` de `GET tickets` com o filtro equivalente, sem recorte e com recorte de setor e prioridade | todos iguais |
| AC-3 | REQ-2 | chamados em dois setores e em períodos diferentes | chama com dois `sectorId` e com `createdFrom`/`createdTo` | os contadores consideram só os chamados dos dois setores abertos no período |
| AC-4 | REQ-4 | um urgente ativo, um urgente resolvido e um ativo sem responsável | chama sem filtros | críticos = 1; sem responsável conta só os ativos |
| AC-5 | REQ-2 | os chamados acima | chama com `status=1` e sem ele | as duas respostas são iguais: a rota não lê `status` (o recorte por status é o próprio bloco de volume) |
| AC-6 | REQ-5 | um usuário sem `tickets:manage` | chama a rota | 403; sem token, 401 |
| AC-7 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quais filtros o agregado aceita? | (a) os de `GET tickets` + período + vários setores; (b) também `departmentId` | (a): o front monta o departamento mandando os setores dele, sem conceito novo no back | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Como contar tudo numa chamada? | (a) um SQL com contagens condicionais (`COUNT(*) FILTER`), como o volume; (b) várias consultas EF | (a): uma ida ao banco; as regras de SLA espelham `TicketSlaFilters`, e o AC-2 garante que não divergem | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | Dependência | — | depois do merge da 025 (período e vários setores); a 029 acrescenta "aguardando" a este endpoint depois | Rafael Santos, 2026-10-28, no chat |

## 5. Fora do escopo
- Tempos médios (spec 027) e séries por período (spec 028).
- "Aguardando solicitante/atendente" (spec 029, que estende este endpoint).
- Escopo por setor para atendentes (decisão de produto à parte).
- Remover `GET tickets/stats/status` e `GET tickets/sla-summary`, que continuam para quem já os usa.

## 6. Plano técnico
- **Pré-requisito:** PR da spec 025 mergeado na `main`.
- **Arquivos a criar ou alterar:**
  - Application:
    - `Tickets/GetOverview/GetTicketOverviewQuery.cs`, `GetTicketOverviewQueryHandler.cs` e `TicketOverviewResponse.cs` (novos), com os três blocos e a hora do servidor.
    - O handler monta uma consulta com contagens condicionais, com os mesmos filtros de recorte da listagem (reaproveitando a conversão de período da 025) e as condições de SLA de `TicketSlaFilters`, num só comando.
    - Os filtros de recorte comuns à listagem e às estatísticas ficam num tipo único, reaproveitado pelas specs 027 e 028.
  - Web.Api:
    - `Endpoints/Tickets/GetOverview.cs` (novo): `GET tickets/stats/overview`, com `tickets:manage`.
  - Testes:
    - integração: um teste por contador comparando com `GET tickets` (AC-2) e os casos de filtro, permissão e regras.
- **Contratos:** rota nova `GET tickets/stats/overview`; resposta com `serverNowUtc` e os blocos `volume`, `sla` e `attention` com os contadores do REQ-1; 200 · 400 (período inválido) · 401 · 403.
- **Mudança no banco:** não. Se a consulta ficar lenta com muitos chamados, um índice por setor e status entra numa spec à parte, com medição.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 e a seção de estatísticas.
- **Análise:**
  - **Consistência:** as condições de SLA são escritas duas vezes (EF em `TicketSlaFilters` e SQL aqui); o AC-2 é o teste que impede que divirjam.
  - **Desempenho:** uma consulta com contagens condicionais sobre os chamados do recorte, no lugar de 11 consultas paginadas.
  - **Segurança:** mesma permissão das estatísticas; sem dados de chamado individual na resposta.
- **Riscos:**
  - R-1: duas implementações da regra de SLA (mitigado pelo AC-2).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Tipo único dos filtros de recorte (listagem e estatísticas) | AC-3 | `add-feature` | [x] |
| T-2 | Consulta, resposta e rota do agregado | AC-1, AC-3 a AC-6 | `add-feature` | [ ] |
| T-3 | Testes de paridade com `GET tickets` e demais casos | AC-1 a AC-6 | `add-tests` | [ ] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [ ] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [ ] |

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
| 2026-10-28 | Em descoberta | Perguntas respondidas no chat (DEC-1 a DEC-3). Aguardando aprovação. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação depois do merge da 025. |
| 2026-10-28 | Em implementação | `/spec-implement 026` na branch `feat/SPEC-026-tickets-operation-overview`, com a `origin/main` mesclada depois do merge da 025 (PR #43) e da 030 (PR #44). Rafael Santos pediu a implementação completa, com o agente respondendo por ele ("atue como senior e só pare quando finalizar"). Tabelas de rastreabilidade e de evidências criadas. |
