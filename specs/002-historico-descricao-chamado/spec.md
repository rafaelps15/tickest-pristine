---
id: 002
titulo: Registrar no histórico a alteração da descrição do chamado
status: Aprovada        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos  # responsável pelo produto e pelo desenvolvimento
aprovada-em: 2026-10-22
branch: feat/spec-driven-development
---

# 002 — Registrar no histórico a alteração da descrição do chamado

## 1. Contexto
- **Pedido original:** "Spec 002 do UpdateTicket: rodar a spec-new para a falta de histórico ao editar só a descrição" (Rafael Santos, 2026-10-22, no chat, ao autorizar as correções da auditoria do ambiente).
- **Problema observado:** `PUT tickets/{id}` grava a nova descrição (`UpdateTicketCommandHandler.cs:55`), mas só acrescenta histórico quando o status muda (`UpdateTicketCommandHandler.cs:66-76`). Uma edição só da descrição não deixa registro em `ticket_histories`, o que contraria a regra permanente do `CLAUDE.md` ("Histórico de chamados: todo command que altera um chamado, uma mensagem ou um anexo grava o registro com `TicketHistoryEntries` no mesmo `SaveChangesAsync`").
- **Referências:**
  - `SYSTEM_SPECIFICATION.md`: 5.4 (fluxo "Tratar chamado"), C-18 (histórico), 8 (E-17, E-18).
  - Código: `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs`, `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs`, `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs` (modelo: `StatusChanged`, `:42-54`), `src/TickestPristine.Application/TicketHistories/TicketHistoryDescriptions.cs`, `src/TickestPristine.Infrastructure/Tickets/TicketHistoryConfiguration.cs:15-19` (ação até 30 caracteres; `oldValue` e `newValue` até 500), `src/TickestPristine.Infrastructure/Tickets/TicketConfiguration.cs:18` (descrição até 500).
  - Testes: `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs`, `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs:139,905-975`.
  - Spec relacionada: `specs/001-remover-responsavel/spec.md` reserva a ação `Unassigned = 11`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Alterar a descrição de um chamado grava um registro `DescriptionChanged` no histórico, com a descrição anterior em `oldValue` e a nova em `newValue`, no mesmo `SaveChangesAsync`. | `CLAUDE.md` ("Histórico de chamados"); DEC-1 |
| REQ-2 | Quando status e descrição mudam no mesmo `PUT`, o histórico segue a DEC-2. | DEC-2 |
| REQ-3 | Um `PUT` com a mesma descrição atual segue a DEC-3. | DEC-3 |
| REQ-4 | O histórico mostra a alteração ao usuário em pt-BR, conforme a DEC-5. | DEC-5; `CLAUDE.md` ("Textos em pt-BR") |
| REQ-5 | O comportamento atual da mudança de status e das recusas (chamado inexistente, sem permissão, inativo, transição inválida) não muda. | `SYSTEM_SPECIFICATION.md` 5.4; testes existentes |

## 3. Critérios de aceitação
Os ACs refletem as decisões DEC-1 a DEC-5 (todas com a opção (a)). "A" e "B" são marcadores de duas descrições válidas e diferentes (de 10 a 500 caracteres, `TicketValidationRules.cs:10-12`), por exemplo "Impressora do 2º andar não liga" e "Impressora do 2º andar não liga depois da troca do toner".

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado ativo com a descrição A e o dono com `tickets:update-own` | o dono envia `PUT tickets/{id}` com a descrição B e o mesmo status | resposta `204`; o histórico ganha um registro `DescriptionChanged` com `oldValue` A, `newValue` B e o dono em `changedByUserId`; nenhum registro `StatusChanged` |
| AC-2 | REQ-2 | um chamado Aberto com a descrição A e um chamador com `tickets:manage` | o chamador envia `PUT tickets/{id}` com a descrição B e o status Em andamento | resposta `204`; o histórico ganha exatamente dois registros novos: `StatusChanged` (Aberto → Em andamento) e `DescriptionChanged` (`oldValue` A, `newValue` B) |
| AC-3 | REQ-3, REQ-5 | um chamado Aberto com a descrição A | um chamador com `tickets:manage` envia `PUT tickets/{id}` com a descrição A e o status Em andamento | resposta `204`; o histórico ganha só o registro `StatusChanged` |
| AC-4 | REQ-3 | um chamado ativo com a descrição A | o dono envia `PUT tickets/{id}` com a descrição A e o mesmo status | resposta `204`; nenhum registro novo no histórico |
| AC-5 | REQ-4 | um chamado cuja descrição foi alterada (AC-1) | alguém com acesso consulta `GET tickets/{id}/history` | o registro aparece com a ação `12` (`DescriptionChanged`) e a descrição "Descrição alterada" |
| AC-6 | REQ-5 | um chamado inativo, ou um chamador sem permissão | o chamador envia `PUT tickets/{id}` com a descrição B | as respostas atuais (`400 Tickets.NotActive`, `403`) se mantêm; nenhum registro novo no histórico |
| AC-7 | REQ-5 | um id de chamado inexistente, ou um chamado Aberto com a descrição A | um chamador com `tickets:manage` envia `PUT tickets/{id}` com a descrição B e, no segundo caso, o status Fechado | as respostas atuais se mantêm (`404 Tickets.NotFound`; `400 Tickets.InvalidStatusTransition`); nenhum registro novo no histórico |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como registrar a alteração da descrição? | (a) ação nova `DescriptionChanged`, com a descrição anterior em `oldValue` e a nova em `newValue` (cabem: a descrição tem até 500 caracteres, como as duas colunas); (b) ação nova sem valores (a auditoria já guarda os valores); (c) não gravar histórico e registrar no `CLAUDE.md` que a regra não vale para a descrição | (a) ação `DescriptionChanged` com os valores antigo e novo | Rafael Santos, 2026-10-22 (chat: "DEC-1: ação `DescriptionChanged`, contendo os valores antigo e novo") |
| DEC-2 | Status e descrição mudam no mesmo `PUT` | (a) dois registros, `StatusChanged` e `DescriptionChanged`; (b) um registro só | (a) dois registros | Rafael Santos, 2026-10-22 (chat: "dois registros quando status e descrição mudarem na mesma operação") |
| DEC-3 | `PUT` com a descrição igual à atual | (a) nenhum registro (comparação exata, ordinal); (b) registrar mesmo assim | (a) nenhum registro, comparação exata | Rafael Santos, 2026-10-22 (chat: "nenhum registro quando a descrição não mudar") |
| DEC-4 | Número da ação nova no enum | (a) `DescriptionChanged = 12`, deixando o 11 para o `Unassigned` da spec 001; (b) o próximo número livre na hora da implementação | (a) `DescriptionChanged = 12` | Rafael Santos, 2026-10-22 (chat: "número 12") |
| DEC-5 | Texto exibido no histórico | (a) "Descrição alterada"; (b) um texto com os valores antigo e novo | (a) "Descrição alterada" | Rafael Santos, 2026-10-22 (chat: "texto `Descrição alterada`") |

## 5. Fora do escopo
- O `TicketUpdatedDomainEvent` disparado mesmo quando nada muda (`UpdateTicketCommandHandler.cs:64`).
- Editar o título do chamado (o `PUT tickets/{id}` só recebe descrição e status).
- A descrição obrigatória mesmo quando só o status muda (`SYSTEM_SPECIFICATION.md` 5.4, item 10).

## 6. Plano técnico
- **Arquivos a alterar** (caminhos do `CLAUDE.md`, "Mapa do repositório"; referência viva: o registro `StatusChanged` no mesmo handler):
  - `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs` (`DescriptionChanged = 12`, DEC-4);
  - `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs` (fábrica `DescriptionChanged(ticketId, changedByUserId, oldDescription, newDescription, occurredAtUtc)`) e `TicketHistoryDescriptions.cs` ("Descrição alterada", DEC-5);
  - `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs` (guarda a descrição anterior antes de atribuir a nova; grava `DescriptionChanged` só quando `string.Equals(anterior, nova, StringComparison.Ordinal)` for falso, DEC-3; mantém o `StatusChanged` independente, DEC-2);
  - testes em `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs`, `tests/TickestPristine.Application.UnitTests/TicketHistories/TicketHistoryEntriesTests.cs` e `TicketHistoryDescriptionsTests.cs`, e em `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` ou `TicketHistories/TicketHistoriesTests.cs`.
- **Contratos:** a rota e o corpo de `PUT tickets/{id}` não mudam; o histórico ganha a ação `12` (`DescriptionChanged`) na resposta de `GET tickets/{id}/history`.
- **Testes por regra:** DEC-1 → AC-1 (unitário do handler confere ação, `oldValue`, `newValue` e `changedByUserId`; unitário da fábrica em `TicketHistoryEntriesTests`); DEC-2 → AC-2 (unitário e integração contam exatamente dois registros novos); DEC-3 → AC-3 e AC-4 (unitário confere ausência de `DescriptionChanged` com descrição igual); DEC-4 → AC-5 (integração confere `Action == 12`); DEC-5 → AC-5 (`InlineData(TicketHistoryAction.DescriptionChanged, "Descrição alterada")` em `TicketHistoryDescriptionsTests` e integração na leitura do histórico).
- **Mudança no banco:** não. A ação é gravada como texto de até 30 caracteres, e "DescriptionChanged" cabe.
- **Riscos:**
  - R-1: o `Down` da migration `20261022023422_Store_Ticket_Enums_As_Names` não conhece a ação nova (`CASE` sem `ELSE`, coluna obrigatória); a reversão dessa migration falharia por NOT NULL com registros `DescriptionChanged` gravados. Mesmo risco do R-1 da spec 001; só afeta reversão.
  - R-2: um cliente que trate o número da ação e não conheça o novo valor pode quebrar ao exibir o histórico; o front-end não está neste repositório.
  - R-3: o número 12 pressupõe o 11 reservado ao `Unassigned` da spec 001 (DEC-4); se esta spec for implementada antes da 001, o enum fica com um intervalo (11 livre) até a 001 ser implementada.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Acréscimo no Domain: ação de histórico `DescriptionChanged` | AC-1, AC-2, AC-5 | add-feature | [ ] |
| T-2 | Fábrica `DescriptionChanged` em `TicketHistoryEntries` e texto em `TicketHistoryDescriptions` | AC-1, AC-5 | add-feature | [ ] |
| T-3 | `UpdateTicketCommandHandler`: gravar o registro quando a descrição mudar, conforme as DECs 1 a 3 | AC-1, AC-2, AC-3, AC-4, AC-6, AC-7 | add-feature | [ ] |
| T-4 | Testes unitários do handler, da fábrica e do texto; testes de integração por HTTP | AC-1 a AC-7 | add-tests | [ ] |

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
- **Documentação atualizada:** a atualizar em `SYSTEM_SPECIFICATION.md`: 2.7 (C-18), 5.4 (persistência: `DescriptionChanged`), 8 (evidência nova).
- **CI:**
- **Entrega:** <commit ou PR, e a autorização>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-22 | Rascunho | Spec criada pela `spec-new` a partir do pedido; 5 decisões em aberto (DEC-1 a DEC-5), com recomendação; os ACs assumem as recomendações. Aguardando decisões e aprovação. |
| 2026-10-22 | Rascunho | Ajustes da revisão independente: descrições dos ACs como marcadores de textos válidos (mínimo de 10 caracteres); AC-7 (chamado inexistente e transição inválida, já citados no REQ-5); REQ-1 compatível com a opção (c) da DEC-1; AC-3 ligado também ao REQ-3. Aguardando decisões e aprovação. |
| 2026-10-22 | Rascunho | DEC-1 a DEC-5 decididas por Rafael Santos no chat, todas com a opção (a). Na mensagem, a numeração das decisões diferia da spec (o chat chamou de DEC-2 o número 12, de DEC-3 os dois registros e de DEC-4 o "nenhum registro"); o registro segue o conteúdo de cada escolha, que é inequívoco. REQ-1, AC-2 e AC-5, plano e "Testes por regra" ajustados às decisões. Checklist de completude atendido. Aguardando aprovação explícita da spec. |
| 2026-10-22 | Aprovada | Aprovada explicitamente por Rafael Santos no chat ("Aprovo formalmente a Spec 002 e as decisões DEC-1 a DEC-5 anteriormente definidas. [...] Esta aprovação não autoriza commits nem a implementação automática."). Implementação só com `/spec-implement 002`. |
