---
id: 001
titulo: Remover o responsável de um chamado
status: Aprovada        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos  # responsável pelo produto e pelo desenvolvimento
aprovada-em: 2026-10-22
branch: feat/spec-driven-development
---

# 001 — Remover o responsável de um chamado

## 1. Contexto
- **Pedido original:** "permitir remover o responsável de um chamado"
- **Referências:**
  - `SYSTEM_SPECIFICATION.md`: C-10 (atribuição; lacuna "não é possível remover o responsável"), RN-22 (regra da atribuição), 5.10 (fluxo "Atribuir", item 10), C-18 (histórico), 4.3.1 (rotas), Q-04 e Q-05.
  - Código: `src/TickestPristine.Application/Tickets/Assign/AssignTicketCommandHandler.cs` (referência mais próxima), `src/TickestPristine.Web.Api/Endpoints/Tickets/Assign.cs`, `src/TickestPristine.Application/Authorization/TicketAccessGuard.cs:35` (o responsável é participante do chamado), `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs`, `src/TickestPristine.Application/TicketHistories/TicketHistoryDescriptions.cs`, `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | A API permite deixar um chamado sem responsável (`AssignedToUserId` nulo). | pedido |
| REQ-2 | Só pode remover o responsável quem a DEC-1 definir. | DEC-1 |
| REQ-3 | A remoção grava um registro no histórico do chamado, no mesmo `SaveChangesAsync`, e dispara um evento de domínio. | `CLAUDE.md` ("Histórico de chamados", "Eventos de domínio") |
| REQ-4 | O comportamento para chamado que já está sem responsável segue a DEC-2. | DEC-2 |
| REQ-5 | O comportamento para chamado inativo (Resolvido, Fechado, Cancelado) segue a DEC-3, e essa regra vem antes da regra de chamado sem responsável (DEC-6). | DEC-3, DEC-6 |
| REQ-6 | O efeito da remoção no status do chamado segue a DEC-4. | DEC-4 |
| REQ-7 | O histórico mostra a remoção ao usuário em pt-BR, conforme a DEC-5. | DEC-5; `CLAUDE.md` ("Textos em pt-BR") |

## 3. Critérios de aceitação

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-3 | um chamado ativo com responsável e um chamador com `tickets:manage` | o chamador envia `DELETE tickets/{id}/assignee` | resposta `204`; `GET tickets/{id}` devolve `assignedToUserId` nulo; o histórico ganha um registro da remoção com o id do responsável anterior em `oldValue` e o chamador em `changedByUserId`; o evento de domínio da remoção é disparado |
| AC-2 | REQ-2 | um chamado ativo com responsável e um chamador **sem** `tickets:manage` | o chamador envia `DELETE tickets/{id}/assignee` | resposta `403`; o responsável continua o mesmo |
| AC-3 | REQ-1 | um id de chamado inexistente ou excluído | um chamador com `tickets:manage` envia `DELETE tickets/{id}/assignee` | resposta `404` com o erro `Tickets.NotFound` |
| AC-4 | REQ-5 | um chamado Resolvido, Fechado ou Cancelado, com responsável | um chamador com `tickets:manage` envia `DELETE tickets/{id}/assignee` | resposta `400` com o erro `Tickets.NotActive`; nada é gravado |
| AC-5 | REQ-4 | um chamado ativo sem responsável | um chamador com `tickets:manage` envia `DELETE tickets/{id}/assignee` | resposta `204`; nenhum registro de histórico e nenhum evento |
| AC-6 | REQ-6 | um chamado Em andamento com responsável | um chamador com `tickets:manage` envia `DELETE tickets/{id}/assignee` | resposta `204`; o status continua Em andamento |
| AC-7 | REQ-7 | um chamado cujo responsável foi removido | alguém com acesso consulta `GET tickets/{id}/history` | o registro da remoção aparece com a descrição "Responsável removido" |
| AC-8 | REQ-5 | um chamado Resolvido, Fechado ou Cancelado **sem** responsável | um chamador com `tickets:manage` envia `DELETE tickets/{id}/assignee` | resposta `400` com o erro `Tickets.NotActive` (não `204`): a regra de chamado inativo é avaliada antes da regra de chamado sem responsável (DEC-6); nada é gravado |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Quem pode remover o responsável? | (a) só quem tem `tickets:manage`, como na atribuição (RN-22); (b) também o próprio responsável, mesmo sem `tickets:manage` ("devolver à fila") | (a) só quem tem `tickets:manage` | Rafael Santos, 2026-10-22 (chat) |
| DEC-2 | Remover de um chamado que já está sem responsável | (a) `204` sem gravar nada, como "atribuir a quem já é o responsável" (RN-22); (b) `409` com um erro novo `Tickets.NotAssigned` | (a) `204` sem gravar nada | Rafael Santos, 2026-10-22 (chat) |
| DEC-3 | Chamados inativos (Resolvido, Fechado, Cancelado) | (a) recusar com `400 Tickets.NotActive`, como a atribuição; (b) permitir | (a) recusar com `400 Tickets.NotActive` | Rafael Santos, 2026-10-22 (chat) |
| DEC-4 | Chamado Em andamento que fica sem responsável | (a) o status não muda (a situação fica registrada como lacuna); (b) volta para Aberto; (c) a remoção é recusada enquanto o chamado estiver Em andamento | (a) o status não muda | Rafael Santos, 2026-10-22 (chat) |
| DEC-5 | Como registrar a remoção no histórico | (a) ação nova `Unassigned`, com a descrição "Responsável removido" (ver o risco R-1); (b) reaproveitar `Reassigned` com `newValue` nulo e a descrição "Responsável alterado" | (a) ação nova `Unassigned` | Rafael Santos, 2026-10-22 (chat) |
| DEC-6 | Chamado inativo **e** sem responsável: qual regra vem primeiro? | (a) a de chamado inativo → `400 Tickets.NotActive`; (b) a de chamado sem responsável → `204` | (a) `400 Tickets.NotActive`: o handler confere a inatividade antes de conferir o responsável | Rafael Santos, 2026-10-22 (chat: "defina qual regra vem primeiro quando o chamado está inativo e sem responsável (quero 400)") |

## 5. Fora do escopo
- Notificar o responsável removido: o sistema não tem mecanismo de notificação (`SYSTEM_SPECIFICATION.md`, C-21).
- Mudar a regra do responsável indicado na abertura do chamado (Q-05).
- Permitir que o responsável sem `tickets:manage` trate o chamado (Q-04), além do que a DEC-1 decidir sobre a remoção.

## 6. Plano técnico
- **Arquivos a criar ou alterar** (caminhos do `CLAUDE.md`, "Mapa do repositório"; referência viva: `Tickets/Assign`):
  - criar `src/TickestPristine.Application/Tickets/Unassign/` com `UnassignTicketCommand.cs`, `UnassignTicketCommandHandler.cs` e `UnassignTicketCommandValidator.cs`;
  - criar `src/TickestPristine.Web.Api/Endpoints/Tickets/Unassign.cs`;
  - criar `src/TickestPristine.Domain/Tickets/TicketUnassignedDomainEvent.cs` (o `TicketReassignedDomainEvent` tem `AssignedToUserId` não anulável, e torná-lo anulável mudaria o contrato do evento existente);
  - alterar `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs` (`Unassigned = 11`), `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs` (fábrica `Unassigned`) e `src/TickestPristine.Application/TicketHistories/TicketHistoryDescriptions.cs` ("Responsável removido") (DEC-5);
  - testes em `tests/TickestPristine.Application.UnitTests/Tickets/` (`UnassignTicketCommandHandlerTests.cs`, ampliar `TicketValidatorsTests.cs`), em `tests/TickestPristine.Application.UnitTests/TicketHistories/TicketHistoryDescriptionsTests.cs` e em `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs`.
- **Contratos:**
  - rota `DELETE tickets/{ticketId:guid}/assignee`, sem corpo, resposta `204`; par do `PUT tickets/{ticketId:guid}/assignee` existente; `.HasPermission(Permissions.TicketsManage)` (DEC-1);
  - erros: só os existentes, `Tickets.NotFound` (404) e `Tickets.NotActive` (400) (DEC-2, DEC-3);
  - evento: `TicketUnassignedDomainEvent(TicketId, PreviousAssignedToUserId, ChangedByUserId)`.
- **Mudança no banco:** não. A ação do histórico é gravada como texto de até 30 caracteres (`Infrastructure/Tickets/TicketHistoryConfiguration.cs:15`), e "Unassigned" cabe.
- **Riscos:**
  - R-1 (assumido com a escolha da opção (a) da DEC-5, cuja descrição apresentada no chat citava este risco): o `Down` da migration `20261022023422_Store_Ticket_Enums_As_Names` converte as ações de texto para número com um `CASE` sem `ELSE`; um registro `Unassigned` viraria `NULL` numa reversão dessa migration. Só afeta reversão de migration.
  - R-2 (aceito por Rafael Santos, 2026-10-22, no chat: "Registre também que aceito o R-2"): quem deixa de ser responsável perde o acesso de participante ao chamado (`TicketAccessGuard.cs:35`), a menos que tenha `tickets:manage` ou seja o solicitante.
- **Verificação prévia (DEC-4):** nenhuma regra existente assume que um chamado Em andamento tenha responsável. `Tickets/Update/UpdateTicketCommandHandler.cs` muda o status para Em andamento sem conferir `AssignedToUserId`, e `TicketStatusTransitions` e `TicketSla` não usam o responsável; o teste de integração `TicketsTests.Update_Should_ReturnNoContent_WhenOwnerUpdatesOwnTicket` já leva um chamado sem responsável a Em andamento. Só os dados de exemplo (`Infrastructure/Database/Seeding/SampleDataSeeder.cs:196`) sempre atribuem um responsável aos chamados que passaram por Em andamento; é convenção do seed, não regra. Um front-end que assuma o contrário não está neste repositório e não foi verificado.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Acréscimos no Domain: evento `TicketUnassignedDomainEvent` e ação de histórico `Unassigned = 11` | AC-1, AC-7 | add-feature | [ ] |
| T-2 | Registro do histórico: fábrica `Unassigned` em `TicketHistoryEntries` e descrição "Responsável removido" em `TicketHistoryDescriptions` | AC-1, AC-7 | add-feature | [ ] |
| T-3 | Slice `Tickets/Unassign`: command, validator e handler (busca; inatividade antes de responsável, DEC-6; remoção; evento; histórico) | AC-1, AC-3, AC-4, AC-5, AC-6, AC-8 | add-feature | [ ] |
| T-4 | Endpoint `DELETE tickets/{ticketId:guid}/assignee` com `.HasPermission(Permissions.TicketsManage)` | AC-1, AC-2 | add-feature | [ ] |
| T-5 | Testes unitários do handler, do validator e da descrição do histórico; testes de integração por HTTP | AC-1 a AC-8 | add-tests | [ ] |

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
- **Documentação atualizada:** a atualizar em `SYSTEM_SPECIFICATION.md`: 2.3 (casos de uso de Tickets), 2.6 (nova RN-23), 2.7 (C-10 e C-18), 4.3.1 (rota nova), 5.10 (fluxo "Remover responsável"), 7 (lacuna: chamado Em andamento pode ficar sem responsável, conforme a DEC-4), 8 (evidência nova).
- **CI:**
- **Entrega:** <commit ou PR, e a autorização>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-22 | Rascunho | Spec criada pela `spec-new` a partir do pedido; 5 decisões em aberto (DEC-1 a DEC-5). |
| 2026-10-22 | Rascunho | DEC-1 a DEC-5 decididas por Rafael Santos no chat, todas com a opção (a); critérios e plano ajustados; checklist de completude atendido; aguardando aprovação. |
| 2026-10-22 | Rascunho | A pedido do responsável antes da aprovação: DEC-6 (inativo vem antes de sem responsável → 400) e AC-8 acrescentados; R-2 aceito; verificado que nenhuma regra assume Em andamento com responsável. Aguardando aprovação. |
| 2026-10-22 | Rascunho | Revisão antes da aprovação: DEC-6 movida para depois da DEC-5; AC-6 com ator e resposta explícitos; AC-8 cita a ordem da DEC-6; fontes literais da DEC-6 e do aceite do R-2; R-1 descrito como assumido com a DEC-5; lacuna da DEC-4 incluída na documentação a atualizar. Nenhum item duplicado. Aguardando aprovação. |
| 2026-10-22 | Rascunho | Rafael Santos confirmou no chat a identidade e o papel de responsável pelo produto e pelo desenvolvimento; registrado no campo `responsavel`. Aguardando aprovação. |
| 2026-10-22 | Aprovada | Aprovada explicitamente por Rafael Santos no chat ("Aprovo a Spec 001 ... na versão final revisada"). Implementação só com `/spec-implement 001`. |
