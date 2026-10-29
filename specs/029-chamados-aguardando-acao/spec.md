---
id: 029
titulo: Chamados aguardando ação do solicitante ou de quem atende
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28
branch: feat/SPEC-029-tickets-awaiting-action                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 029 — Chamados aguardando ação do solicitante ou de quem atende

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end, em 2026-10-28: "sim! pode criar na pasta do back" (item 7 do relatório do Início operacional).
- **Por que:** a Central de atenção do Início deveria mostrar os chamados "aguardando ação". Hoje não existe status nem campo que diga se o chamado espera o solicitante ou quem atende.
- **Referências:** spec 026 (agregado, que esta estende); spec 015 (última atividade, mesmo padrão de coluna mantida a cada ação).
- **Fatos do repositório:**
  - Não há status de espera nem pausa do SLA (`Domain/Tickets/TicketStatus.cs`; `Tickets/TicketSlaClock.cs:9`).
  - As mensagens guardam `AuthorUserId` e `CreatedAtUtc` (`TicketMessages/*`); o solicitante é o `CreatedByUserId` do chamado; excluir mensagem é lógico.
  - A primeira resposta de SLA já usa a regra "mensagem de quem não é o solicitante e tem `tickets:manage`" (`TicketMessages/Post/PostTicketMessageCommandHandler.cs:68-107`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada chamado ativo (aberto ou em andamento) tem uma situação de espera: **aguardando o solicitante** quando a última mensagem válida é de quem atende; **aguardando quem atende** quando a última é do solicitante ou quando ainda não há mensagem de quem atende. Chamados inativos não têm situação de espera. | pedido; DEC-1 |
| REQ-2 | "Quem atende" segue a mesma regra da primeira resposta: autor que não é o solicitante e tem `tickets:manage` no momento da mensagem. | DEC-1; fato do repositório |
| REQ-3 | A situação fica guardada no chamado e é atualizada ao publicar ou excluir mensagem e ao mudar o status; os chamados existentes são preenchidos na migration. | DEC-2 |
| REQ-4 | `GET tickets` traz a situação em cada item e aceita filtrá-la. | pedido |
| REQ-5 | O agregado da 026 ganha os contadores "aguardando o solicitante" e "aguardando quem atende", com os mesmos filtros de recorte. | DEC-3 |
| REQ-6 | Nada muda no status nem no SLA: a espera é só informativa. | DEC-1 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado novo, sem mensagens | consulta | aguardando quem atende |
| AC-2 | REQ-1, REQ-2, REQ-3 | o mesmo chamado | um atendente responde; depois o solicitante responde | aguardando o solicitante; depois aguardando quem atende |
| AC-3 | REQ-3 | a última mensagem é do atendente | ela é excluída | volta a valer a mensagem anterior |
| AC-4 | REQ-1, REQ-3 | um chamado aguardando | é resolvido; depois reaberto | sem situação de espera enquanto inativo; ao reabrir, recalculada pela última mensagem |
| AC-5 | REQ-3 | chamados existentes com e sem mensagens | aplica a migration | cada ativo com a situação certa |
| AC-6 | REQ-4 | chamados nas duas situações | lista com o filtro de cada situação | só os da situação pedida, com o campo em cada item |
| AC-7 | REQ-5 | a mesma base | chama o agregado | os dois contadores iguais ao `totalCount` da listagem filtrada |
| AC-8 | REQ-6 | um chamado aguardando o solicitante | o prazo passa | o SLA corre como hoje |
| AC-9 | todos | a mudança aplicada | `dotnet build` e `dotnet test` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como saber quem é aguardado? | (a) pela última mensagem, sem mudar status nem SLA; (b) status novo que pode pausar o SLA | (a) | Rafael Santos, 2026-10-28, no chat |
| DEC-2 | Como guardar? | (a) coluna no chamado, mantida a cada ação; (b) calcular na consulta | (a): filtrar e contar fica barato, como a última atividade da 015 | Rafael Santos, 2026-10-28, no chat |
| DEC-3 | Entra no agregado? | (a) sim, depois da 026; (b) só na listagem | (a) | Rafael Santos, 2026-10-28, no chat |
| DEC-4 | A mensagem não guarda a permissão do autor no momento em que foi escrita. Ao excluir mensagem, trocar o status e preencher os chamados existentes, como decidir se o autor da última mensagem válida atende? | (a) permissão atual: uma regra só, recalculada no banco depois do commit (mensagem nova ou alterada, chamado criado ou com status alterado e migration), com o critério do `PermissionProvider` (usuário ativo com `tickets:manage` em alguma role); (b) gravar na mensagem se o autor atendia (coluna nova, fora do plano, volta para `Em descoberta`); (c) só pelo solicitante, ignorando a permissão (contradiz o REQ-2) | (a): sem corrida entre mensagens simultâneas e dentro do plano. A regra fica num interceptor da Infrastructure que atualiza o chamado depois do commit, sem checagem de versão, como a última atividade (spec 015, DEC-11); os handlers não mudam. Ressalva aceita, como no R-1: um recálculo depois de o autor perder ou ganhar `tickets:manage` usa a permissão de hoje | Rafael Santos, 2026-10-28, no chat (escolheu "Permissão atual (Recommended)") |

## 5. Fora do escopo
- Status "aguardando" com pausa do SLA (DEC-1).
- Avisos ou lembretes para quem está sendo aguardado.
- Mostrar a situação em "Meus chamados" (pode virar spec do front depois).

## 6. Plano técnico
- **Pré-requisito:** PR da spec 026 mergeado.
- **Arquivos a criar ou alterar:**
  - Domain: `Tickets/Ticket.cs` (situação de espera, opcional) e o enum da situação (novo).
  - Application: a publicação e a exclusão de mensagem e a troca de status recalculam a situação; `Tickets/GetAll/*` (campo e filtro); `Tickets/GetOverview/*` (dois contadores).
  - Infrastructure: configuração da coluna, índice para o filtro e migration com o preenchimento.
  - Testes: unitários do recálculo e de integração dos ACs.
- **Contratos:** cada item de `GET tickets` ganha a situação de espera (nula quando inativo) e a rota aceita filtrá-la; o agregado ganha os dois contadores. Mudança aditiva.
- **Mudança no banco:** sim, uma coluna no chamado e um índice.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` (regra nova, 4.3 e estatísticas).
- **Análise:**
  - **Concorrência:** publicar mensagem passa a atualizar a linha do chamado; usar o mesmo cuidado da DEC-11 da spec 015 (atualização fora da checagem de versão quando o chamado não muda) para não reabrir conflitos entre mensagens simultâneas.
- **Riscos:**
  - R-1: a regra de "quem atende" depende da permissão no momento da mensagem; um usuário que perca `tickets:manage` depois não muda as situações já gravadas (comportamento aceitável, como a primeira resposta).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Situação de espera no chamado, recálculo nas ações e migration | AC-1 a AC-5, AC-8 | `add-entity` | [x] |
| T-2 | Campo e filtro na listagem; contadores no agregado | AC-6, AC-7 | `add-feature` | [x] |
| T-3 | Testes unitários e de integração | AC-1 a AC-8 | `add-tests` | [x] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-9 | — | [x] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-9 | `ca-review` | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `WaitingOn_Should_FollowTheLastMessage_WhenAgentAndRequesterAnswer` (abertura) | Logo depois de aberto pela API, sem mensagens, o chamado aguarda quem atende. | Aprovado |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `WaitingOn_Should_FollowTheLastMessage_WhenAgentAndRequesterAnswer` | Um Atendente responde e o chamado passa a aguardar o solicitante; o solicitante responde e volta a aguardar quem atende. | Aprovado |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `WaitingOn_Should_FallBackToPreviousMessage_WhenLastMessageIsDeleted` | Com a mensagem do solicitante e depois a do Atendente (aguardando o solicitante), excluir a do Atendente faz valer a do solicitante: volta a aguardar quem atende. | Aprovado |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `WaitingOn_Should_BeClearedWhileInactive_AndRecalculatedOnReopen` | Respondido pelo Atendente e resolvido, fica sem situação; reaberto sem motivo pelo solicitante, a situação é recalculada pela última mensagem (aguardando o solicitante). Uma mutação que deixava de recalcular na troca de status foi detectada por 4 casos. | Aprovado |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Migration_Should_FillWaitingOnOfExistingTickets` | Com um chamado sem mensagens, um respondido pelo Atendente e um resolvido, a migration desce (apagando a coluna) e sobe: aguardando quem atende, aguardando o solicitante e sem situação. | Aprovado |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAll_Should_ReturnOnlyTicketsWaitingOnTheGivenParty_WithTheFieldInEachItem` (Requester e Agent); `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnOnlyTicketsWaitingOnTheGivenParty_WhenWaitingOnIsGiven` | No mesmo setor, o filtro `waitingOn` traz só o chamado da situação pedida, com o campo `waitingOn` no item; o resolvido vem com o campo nulo. | Aprovado |
| AC-7 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOverview_Should_MatchWaitingCountsWithListing` (sem recorte e com o setor) | `waitingOnRequesterCount` e `waitingOnAgentCount` do agregado são iguais ao `totalCount` da listagem com `waitingOn=Requester` e `waitingOn=Agent`, sem recorte e com o setor do cenário (1 e 1). | Aprovado |
| AC-8 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `WaitingOn_Should_NotPauseSla_WhenTicketWaitsOnRequesterPastDueDate` | Um chamado aguardando o solicitante com o prazo vencido aparece em `slaStatus=Breached` e continua Aberto: a espera não pausa o SLA nem muda o status. | Aprovado |
| AC-9 | | | |

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
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("Pode aprovar as specs", confirmando "As seis (025 a 030)"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação depois do merge da 026. |
| 2026-10-28 | Em implementação | `/spec-implement 029` na branch `feat/SPEC-029-tickets-awaiting-action`, com a `origin/main` mesclada depois do merge da 026 (PR #45) e das 025, 027, 028 e 030. Rafael Santos pediu a implementação completa seguindo as skills ("desenvolva seguindo minhas skills modelo como senior e so pare quando finalizar"). DEC-4 respondida por Rafael Santos no chat: permissão atual, recalculada no banco depois do commit. Tabelas de rastreabilidade e de evidências criadas. |
| 2026-10-28 | Em implementação | T-1: dois testes de migration anteriores quebraram com a coluna nova, porque gravam ou leem chamados pelo EF com o banco num esquema antigo. `TicketActivityTests.Migration_Should_BackfillLastActivityFromHistory_WhenApplied` (spec 015) não voltava à última migration no fim, ao contrário dos demais, e deixava o banco compartilhado sem as colunas posteriores: ganhou o `finally` que volta à última migration. `TicketsTests.Migration_Should_TurnDescriptionIntoCitextKeepingExistingText` (spec 019) passou a gravar o chamado antes de descer a migration; o teste continua provando que o texto é mantido na troca de tipo. Só testes mudaram. |
